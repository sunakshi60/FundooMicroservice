
using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fundoo.Notes.Application.Services
{
    public class NotesService : INotesService
    {
        private readonly INotesRepository _notesRepository;
        private readonly IDistributedCache _cache;
        private readonly ILogger<NotesService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        private static readonly TimeSpan CacheDuration =
            TimeSpan.FromMinutes(5);

        public NotesService(
            INotesRepository notesRepository,
            IDistributedCache cache,
            ILogger<NotesService> logger)
        {
            _notesRepository = notesRepository;
            _cache = cache;
            _logger = logger;
        }

        // GET: Single note
        public async Task<Note?> GetByIdAsync(
            Guid noteId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            string key = $"notes:{userId}:note:{noteId}";

            var cachedNote = await GetFromCacheAsync<Note>(
                key, cancellationToken);

            if (cachedNote is not null)
                return cachedNote;

            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is not null)
            {
                await SetCacheAsync(key, note, cancellationToken);
            }

            return note;
        }

        // GET: All notes
        public async Task<IReadOnlyList<Note>> GetAllAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            string key = $"notes:{userId}:all";

            var cachedNotes =
                await GetFromCacheAsync<List<Note>>(key, cancellationToken);

            if (cachedNotes is not null)
                return cachedNotes;

            var notes = await _notesRepository.GetAllAsync(
                userId, cancellationToken);

            await SetCacheAsync(key, notes, cancellationToken);

            return notes;
        }

        // GET: Active notes
        public async Task<IReadOnlyList<Note>> GetActiveAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            string key = $"notes:{userId}:active";

            var cachedNotes =
                await GetFromCacheAsync<List<Note>>(key, cancellationToken);

            if (cachedNotes is not null)
                return cachedNotes;

            var notes = await _notesRepository.GetActiveAsync(
                userId, cancellationToken);

            await SetCacheAsync(key, notes, cancellationToken);

            return notes;
        }

        // GET: Archived notes
        public async Task<IReadOnlyList<Note>> GetArchivedAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            string key = $"notes:{userId}:archived";

            var cachedNotes =
                await GetFromCacheAsync<List<Note>>(key, cancellationToken);

            if (cachedNotes is not null)
                return cachedNotes;

            var notes = await _notesRepository.GetArchivedAsync(
                userId, cancellationToken);

            await SetCacheAsync(key, notes, cancellationToken);

            return notes;
        }

        // GET: Trashed notes
        public async Task<IReadOnlyList<Note>> GetTrashedAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            string key = $"notes:{userId}:trash";

            var cachedNotes =
                await GetFromCacheAsync<List<Note>>(key, cancellationToken);

            if (cachedNotes is not null)
                return cachedNotes;

            var notes = await _notesRepository.GetTrashedAsync(
                userId, cancellationToken);

            await SetCacheAsync(key, notes, cancellationToken);

            return notes;
        }

        // CREATE
        public async Task<Note> CreateAsync(
            Note note,
            CancellationToken cancellationToken = default)
        {
            ValidateTitle(note.Title);

            note.IsArchived = false;
            note.IsTrashed = false;

            var createdNote = await _notesRepository.CreateAsync(
                note, cancellationToken);

            await InvalidateCacheAsync(
                createdNote.UserId,
                createdNote.Id,
                cancellationToken);

            return createdNote;
        }

        // UPDATE
        public async Task<bool> UpdateAsync(
            Note note,
            CancellationToken cancellationToken = default)
        {
            ValidateTitle(note.Title);

            var existingNote = await _notesRepository.GetByIdAsync(
                note.Id, note.UserId, cancellationToken);

            if (existingNote is null)
                return false;

            if (existingNote.IsTrashed)
                return false;

            existingNote.Title = note.Title.Trim();
            existingNote.Content = note.Content;
            existingNote.Color = note.Color;
            existingNote.IsPinned = note.IsPinned;

            bool updated = await _notesRepository.UpdateAsync(
                existingNote, cancellationToken);

            if (updated)
            {
                await InvalidateCacheAsync(
                    note.UserId, note.Id, cancellationToken);
            }

            return updated;
        }

        // ARCHIVE / UNARCHIVE
        public async Task<bool> SetArchivedAsync(
            Guid noteId,
            Guid userId,
            bool isArchived,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is null || note.IsTrashed)
                return false;

            note.IsArchived = isArchived;

            bool updated = await _notesRepository.UpdateAsync(
                note, cancellationToken);

            if (updated)
            {
                await InvalidateCacheAsync(
                    userId, noteId, cancellationToken);
            }

            return updated;
        }

        // TRASH / RESTORE
        public async Task<bool> SetTrashedAsync(
            Guid noteId,
            Guid userId,
            bool isTrashed,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is null)
                return false;

            if (isTrashed)
            {
                note.IsTrashed = true;
            }
            else
            {
                note.IsTrashed = false;
                note.IsArchived = false;
            }

            bool updated = await _notesRepository.UpdateAsync(
                note, cancellationToken);

            if (updated)
            {
                await InvalidateCacheAsync(
                    userId, noteId, cancellationToken);
            }

            return updated;
        }

        // DELETE
        public async Task<bool> DeleteAsync(
            Guid noteId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(
                noteId, userId, cancellationToken);

            if (note is null || !note.IsTrashed)
                return false;

            bool deleted = await _notesRepository.DeleteAsync(
                noteId, userId, cancellationToken);

            if (deleted)
            {
                await InvalidateCacheAsync(
                    userId, noteId, cancellationToken);
            }

            return deleted;
        }

        // READ FROM CACHE
        private async Task<T?> GetFromCacheAsync<T>(
            string key,
            CancellationToken cancellationToken)
        {
            try
            {
                var cachedData = await _cache.GetStringAsync(
                    key, cancellationToken);

                if (string.IsNullOrEmpty(cachedData))
                    return default;

                return JsonSerializer.Deserialize<T>(
                    cachedData, JsonOptions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex, "Redis cache read failed for key {CacheKey}", key);

                return default;
            }
        }

        // WRITE TO CACHE
        private async Task SetCacheAsync<T>(
            string key,
            T data,
            CancellationToken cancellationToken)
        {
            try
            {
                var jsonData = JsonSerializer.Serialize(
                    data, JsonOptions);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration
                };

                await _cache.SetStringAsync(
                    key, jsonData, options, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex, "Redis cache write failed for key {CacheKey}", key);
            }
        }

        // INVALIDATE CACHE AFTER SUCCESSFUL WRITE
        private async Task InvalidateCacheAsync(
            Guid userId,
            Guid noteId,
            CancellationToken cancellationToken)
        {
            string[] keys =
            {
                $"notes:{userId}:all",
                $"notes:{userId}:active",
                $"notes:{userId}:archived",
                $"notes:{userId}:trash",
                $"notes:{userId}:note:{noteId}"
            };

            foreach (var key in keys)
            {
                try
                {
                    await _cache.RemoveAsync(key, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex, "Redis cache invalidation failed for key {CacheKey}", key);
                }
            }
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
