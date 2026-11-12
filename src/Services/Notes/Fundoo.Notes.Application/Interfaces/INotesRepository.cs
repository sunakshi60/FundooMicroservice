using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces
{
    public interface INotesRepository
    {
        Task<Note?> GetByIdAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(Note note, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);
    }
}
