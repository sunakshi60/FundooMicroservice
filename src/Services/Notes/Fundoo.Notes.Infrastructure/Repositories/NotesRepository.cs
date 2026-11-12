using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Fundoo.Notes.Infrastructure.Data;
using Npgsql;

namespace Fundoo.Notes.Application.Repositories
{
    public class NotesRepository : INotesRepository
    {
        private readonly NotesDbConnectionFactory _connectionFactory;

        public NotesRepository(NotesDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        // Get a single note belonging to the logged-in user.
        public async Task<Note?> GetByIdAsync(
            Guid noteId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string query = """
        SELECT id, user_id, title, content, color,
               is_pinned, is_archived, is_trashed,
               created_at_utc, updated_at_utc
        FROM notes
        WHERE id = @noteId
          AND user_id = @userId;
        """;

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("noteId", noteId);
            command.Parameters.AddWithValue("userId", userId);

            Note? note;

            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                note = MapNote(reader);
            }

            await LoadLabelsAsync(
                connection,
                new[] { note },
                userId,
                cancellationToken);

            return note;
        }

        // Get all notes belonging to the user.
        public async Task<IReadOnlyList<Note>> GetAllAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                SELECT id, user_id, title, content, color,
                       is_pinned, is_archived, is_trashed,
                       created_at_utc, updated_at_utc
                FROM notes
                WHERE user_id = @userId
                ORDER BY created_at_utc DESC;
                """;

            return await GetNotesAsync(query, userId, cancellationToken);
        }

        // Get active notes: neither archived nor trashed.
        public async Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                SELECT id, user_id, title, content, color,
                       is_pinned, is_archived, is_trashed,
                       created_at_utc, updated_at_utc
                FROM notes
                WHERE user_id = @userId
                  AND is_archived = FALSE
                  AND is_trashed = FALSE
                ORDER BY is_pinned DESC, created_at_utc DESC;
                """;

            return await GetNotesAsync(
                query, userId, cancellationToken);
        }

        // Get archived notes that are not in trash.
        public async Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                SELECT id, user_id, title, content, color,
                       is_pinned, is_archived, is_trashed,
                       created_at_utc, updated_at_utc
                FROM notes
                WHERE user_id = @userId
                  AND is_archived = TRUE
                  AND is_trashed = FALSE
                ORDER BY updated_at_utc DESC;
                """;

            return await GetNotesAsync(
                query, userId, cancellationToken);
        }

        // Get trashed notes.
        public async Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                SELECT id, user_id, title, content, color,
                       is_pinned, is_archived, is_trashed,
                       created_at_utc, updated_at_utc
                FROM notes
                WHERE user_id = @userId
                  AND is_trashed = TRUE
                ORDER BY updated_at_utc DESC;
                """;

            return await GetNotesAsync(query, userId, cancellationToken);
        }

        // Shared asynchronous query execution for note lists.
        private async Task<IReadOnlyList<Note>> GetNotesAsync(
            string query,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var notes = new List<Note>();

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("userId", userId);

            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    notes.Add(MapNote(reader));
                }
            }

            await LoadLabelsAsync(connection, notes, userId, cancellationToken);

            return notes;
        }

        // Create a new note.
        public async Task<Note> CreateAsync(Note note,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                INSERT INTO notes
                (
                    id, user_id, title, content, color,
                    is_pinned, is_archived, is_trashed,
                    created_at_utc, updated_at_utc
                )
                VALUES
                (
                    @id, @userId, @title, @content, @color,
                    @isPinned, @isArchived, @isTrashed,
                    @createdAtUtc, @updatedAtUtc
                );
                """;

            if (note.Id == Guid.Empty)
                note.Id = Guid.NewGuid();

            note.CreatedAtUtc = DateTime.UtcNow;
            note.UpdatedAtUtc = note.CreatedAtUtc;

            await using var connection = _connectionFactory.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);

            command.Parameters.AddWithValue("id", note.Id);
            command.Parameters.AddWithValue("userId", note.UserId);
            command.Parameters.AddWithValue("title", note.Title);
            command.Parameters.AddWithValue("content", note.Content);

            command.Parameters.AddWithValue("color", (object?)note.Color ?? DBNull.Value);

            command.Parameters.AddWithValue("isPinned", note.IsPinned);
            command.Parameters.AddWithValue("isArchived", note.IsArchived);
            command.Parameters.AddWithValue("isTrashed", note.IsTrashed);
            command.Parameters.AddWithValue("createdAtUtc", note.CreatedAtUtc);
            command.Parameters.AddWithValue("updatedAtUtc", note.UpdatedAtUtc);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return note;
        }

        // Update note fields.
        public async Task<bool> UpdateAsync(Note note,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                UPDATE notes
                SET title = @title,
                    content = @content,
                    color = @color,
                    is_pinned = @isPinned,
                    is_archived = @isArchived,
                    is_trashed = @isTrashed,
                    updated_at_utc = @updatedAtUtc
                WHERE id = @id
                  AND user_id = @userId;
                """;

            note.UpdatedAtUtc = DateTime.UtcNow;

            await using var connection = _connectionFactory.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);

            command.Parameters.AddWithValue("id", note.Id);
            command.Parameters.AddWithValue("userId", note.UserId);
            command.Parameters.AddWithValue("title", note.Title);
            command.Parameters.AddWithValue("content", note.Content);

            command.Parameters.AddWithValue("color", (object?)note.Color ?? DBNull.Value);

            command.Parameters.AddWithValue("isPinned", note.IsPinned);
            command.Parameters.AddWithValue("isArchived", note.IsArchived);
            command.Parameters.AddWithValue("isTrashed", note.IsTrashed);
            command.Parameters.AddWithValue("updatedAtUtc", note.UpdatedAtUtc);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            return rowsAffected > 0;
        }

        // Permanently delete only a note that is already trashed.
        public async Task<bool> DeleteAsync(Guid noteId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                DELETE FROM notes
                WHERE id = @noteId
                  AND user_id = @userId
                  AND is_trashed = TRUE;
                """;

            await using var connection = _connectionFactory.CreateConnection();

            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);

            command.Parameters.AddWithValue("noteId", noteId);
            command.Parameters.AddWithValue("userId", userId);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            return rowsAffected > 0;
        }
        private static async Task LoadLabelsAsync(
            NpgsqlConnection connection,
            IEnumerable<Note> notes,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var noteList = notes.ToList();

            if (noteList.Count == 0)
                return;

            var noteIds = noteList.Select(n => n.Id).ToArray();

            const string query = """
                SELECT
                    nl.note_id,
                    l.id AS label_id,
                    l.user_id AS label_user_id,
                    l.name AS label_name
                FROM note_labels nl
                INNER JOIN labels l ON l.id = nl.label_id
                INNER JOIN notes n ON n.id = nl.note_id
                WHERE nl.note_id = ANY(@noteIds)
                  AND n.user_id = @userId
                  AND l.user_id = @userId
                ORDER BY l.name;
                """;

            await using var command = new NpgsqlCommand(query, connection);

            command.Parameters.AddWithValue(
                "noteIds",
                NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid,
                noteIds);

            command.Parameters.AddWithValue("userId", userId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var notesById = noteList.ToDictionary(n => n.Id);

            while (await reader.ReadAsync(cancellationToken))
            {
                var noteId = reader.GetGuid(
                    reader.GetOrdinal("note_id"));

                var labelId = reader.GetGuid(
                    reader.GetOrdinal("label_id"));

                if (!notesById.TryGetValue(noteId, out var note))
                    continue;

                var label = new Label
                {
                    Id = labelId,
                    UserId = reader.GetGuid(
                        reader.GetOrdinal("label_user_id")),
                    Name = reader.GetString(
                        reader.GetOrdinal("label_name"))
                };

                note.NoteLabels.Add(new NoteLabel
                {
                    NoteId = noteId,
                    LabelId = labelId,
                    Label = label
                });
            }
        }


        // Convert a PostgreSQL row into a Note object.
        private static Note MapNote(NpgsqlDataReader reader)
        {
            return new Note
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),

                UserId = reader.GetGuid(reader.GetOrdinal("user_id")),

                Title = reader.GetString(reader.GetOrdinal("title")),

                Content = reader.GetString(reader.GetOrdinal("content")),

                Color = reader.IsDBNull(reader.GetOrdinal("color"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("color")),

                IsPinned = reader.GetBoolean(reader.GetOrdinal("is_pinned")),

                IsArchived = reader.GetBoolean(reader.GetOrdinal("is_archived")),

                IsTrashed = reader.GetBoolean(reader.GetOrdinal("is_trashed")),

                CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("created_at_utc")),

                UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("updated_at_utc"))
            };
        }
    }
}