using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces
{
    public interface ILabelsRepository
    {
        Task<Label> CreateAsync(Label label, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Label>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(Guid labelId, Guid userId, string name, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid labelId, Guid userId, CancellationToken cancellationToken = default);

        Task<bool> AttachToNoteAsync(Guid noteId, Guid labelId, Guid userId, CancellationToken cancellationToken = default);

        Task<bool> DetachFromNoteAsync(Guid noteId, Guid labelId, Guid userId, CancellationToken cancellationToken = default);
    }
}
