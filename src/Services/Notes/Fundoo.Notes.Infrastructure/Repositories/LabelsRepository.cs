using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Infrastructure.Data;
using Fundoo.Notes.Domain.Entity;
using Npgsql;


namespace Fundoo.Notes.Infrastructure.Repositories
{
    public class LabelsRepository : ILabelsRepository
    {
        private readonly NotesDbConnectionFactory _connectionFactory;

        public LabelsRepository(NotesDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Label> CreateAsync(Label label,
            CancellationToken cancellationToken = default)
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                INSERT INTO labels (id, user_id, name)
                VALUES (@id, @userId, @name)
                RETURNING id, user_id, name;
                """;

            await using var command = new NpgsqlCommand(sql, connection);

            label.Id = Guid.NewGuid();

            command.Parameters.AddWithValue("id", label.Id);
            command.Parameters.AddWithValue("userId", label.UserId);
            command.Parameters.AddWithValue("name", label.Name);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            await reader.ReadAsync(cancellationToken);

            return new Label
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Name = reader.GetString(2)
            };
        }

        public async Task<IReadOnlyList<Label>> GetAllAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var labels = new List<Label>();

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                SELECT id, user_id, name
                FROM labels
                WHERE user_id = @userId
                ORDER BY name;
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("userId", userId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                labels.Add(new Label
                {
                    Id = reader.GetGuid(0),
                    UserId = reader.GetGuid(1),
                    Name = reader.GetString(2)
                });
            }

            return labels;
        }

        public async Task<bool> UpdateAsync(Guid labelId, Guid userId, string name,
            CancellationToken cancellationToken = default)
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                UPDATE labels
                SET name = @name
                WHERE id = @labelId
                  AND user_id = @userId;
                """;

            await using var command = new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("labelId", labelId);
            command.Parameters.AddWithValue("userId", userId);
            command.Parameters.AddWithValue("name", name);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

            return affectedRows > 0;
        }

        public async Task<bool> DeleteAsync(Guid labelId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                DELETE FROM labels
                WHERE id = @labelId
                  AND user_id = @userId;
                """;

            await using var command = new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("labelId", labelId);
            command.Parameters.AddWithValue("userId", userId);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

            return affectedRows > 0;
        }

        public async Task<bool> AttachToNoteAsync(Guid noteId, Guid labelId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                const string ownershipSql = """
                    SELECT EXISTS (
                        SELECT 1
                        FROM notes n
                        INNER JOIN labels l ON l.user_id = n.user_id
                        WHERE n.id = @noteId
                          AND n.user_id = @userId
                          AND l.id = @labelId
                          AND l.user_id = @userId
                    );
                    """;

                await using var ownershipCommand = new NpgsqlCommand(ownershipSql, connection, transaction);

                ownershipCommand.Parameters.AddWithValue("noteId", noteId);
                ownershipCommand.Parameters.AddWithValue("labelId", labelId);
                ownershipCommand.Parameters.AddWithValue("userId", userId);

                var ownsBoth = (bool)(await ownershipCommand.ExecuteScalarAsync(
                    cancellationToken) ?? false);

                if (!ownsBoth)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }

                const string insertSql = """
                    INSERT INTO note_labels (note_id, label_id)
                    VALUES (@noteId, @labelId)
                    ON CONFLICT (note_id, label_id) DO NOTHING;
                    """;

                await using var insertCommand =
                    new NpgsqlCommand(insertSql, connection, transaction);

                insertCommand.Parameters.AddWithValue("noteId", noteId);
                insertCommand.Parameters.AddWithValue("labelId", labelId);

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> DetachFromNoteAsync(Guid noteId, Guid labelId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                DELETE FROM note_labels nl
                USING notes n, labels l
                WHERE nl.note_id = n.id
                  AND nl.label_id = l.id
                  AND nl.note_id = @noteId
                  AND nl.label_id = @labelId
                  AND n.user_id = @userId
                  AND l.user_id = @userId;
                """;

            await using var command = new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("noteId", noteId);
            command.Parameters.AddWithValue("labelId", labelId);
            command.Parameters.AddWithValue("userId", userId);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

            return affectedRows > 0;
        }
    }
}
