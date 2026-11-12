using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Services
{
    public class NotesService : INotesService
    {
        private readonly INotesRepository _notesRepository;

        public NotesService(INotesRepository notesRepository)
        {
            _notesRepository = notesRepository;
        }

        public async Task<Note?> GetByIdAsync(Guid noteId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);
        }

        public async Task<IReadOnlyList<Note>> GetAllAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _notesRepository.GetAllAsync(
                userId, cancellationToken);
        }

        public async Task<IReadOnlyList<Note>> GetActiveAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _notesRepository.GetActiveAsync(
                userId, cancellationToken);
        }

        public async Task<IReadOnlyList<Note>> GetArchivedAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _notesRepository.GetArchivedAsync(
                userId, cancellationToken);
        }

        public async Task<IReadOnlyList<Note>> GetTrashedAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _notesRepository.GetTrashedAsync(
                userId, cancellationToken);
        }

        public async Task<Note> CreateAsync(Note note,
            CancellationToken cancellationToken = default)
        {
            ValidateTitle(note.Title);

            // A newly created note starts as active.
            note.IsArchived = false;
            note.IsTrashed = false;

            return await _notesRepository.CreateAsync(
                note, cancellationToken);
        }

        public async Task<bool> UpdateAsync(Note note,
            CancellationToken cancellationToken = default)
        {
            ValidateTitle(note.Title);

            // Verify that the note belongs to the caller.
            var existingNote = await _notesRepository.GetByIdAsync(
                note.Id, note.UserId, cancellationToken);

            if (existingNote is null)
                return false;

            // Do not allow normal editing of trashed notes.
            if (existingNote.IsTrashed)
                return false;

            // Update only editable fields.
            existingNote.Title = note.Title.Trim();
            existingNote.Content = note.Content;
            existingNote.Color = note.Color;
            existingNote.IsPinned = note.IsPinned;

            return await _notesRepository.UpdateAsync(
                existingNote, cancellationToken);
        }

        public async Task<bool> SetArchivedAsync(Guid noteId, Guid userId, bool isArchived,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is null)
                return false;

            // Trashed notes must be restored before archiving.
            if (note.IsTrashed)
                return false;

            note.IsArchived = isArchived;

            return await _notesRepository.UpdateAsync(
                note, cancellationToken);
        }

        public async Task<bool> SetTrashedAsync(Guid noteId, Guid userId, bool isTrashed,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is null)
                return false;

            if (isTrashed)
            {
                // Move the note to trash.
                note.IsTrashed = true;
            }
            else
            {
                // Restore the note to the active list.
                note.IsTrashed = false;
                note.IsArchived = false;
            }

            return await _notesRepository.UpdateAsync(
                note, cancellationToken);
        }

        public async Task<bool> DeleteAsync(Guid noteId, Guid userId,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            // Missing notes and non-trashed notes cannot be deleted.
            if (note is null || !note.IsTrashed)
                return false;

            return await _notesRepository.DeleteAsync(
                noteId, userId, cancellationToken);
        }

        private static void ValidateTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException(
                    "Note title is required.");
            }
        }
    }
}