using Fundoo.Notes.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notes.Application.Interfaces
{
    public interface INotesService
    {
        Task<Note?> GetByIdAsync(Guid noteId, Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<Note> CreateAsync(Note note, CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(Note note, CancellationToken cancellationToken = default);

        Task<bool> SetArchivedAsync(Guid noteId, Guid userId, bool isArchived,
            CancellationToken cancellationToken = default);

        Task<bool> SetTrashedAsync(Guid noteId, Guid userId, bool isTrashed,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid noteId, Guid userId,
            CancellationToken cancellationToken = default);
    }
}

