using Fundoo.Notes.Api.Contracts.Requests;
using Fundoo.Notes.Api.Contracts.Response;
using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Fundoo.Notes.Api.Controllers
{
    [ApiController]
    [Route("api/notes")]
    [Authorize]
    public class NotesController : ControllerBase
    {
        private readonly INotesService _notesService;

        public NotesController(INotesService notesService)
        {
            _notesService = notesService;
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveNotes(
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user token." });

            var notes = await _notesService.GetActiveAsync(
                userId,
                cancellationToken);

            var response = notes
                .Select(MapToResponse)
                .ToList();

            return Ok(new
            {
                isSuccess = true,
                message = "Active notes retrieved successfully.",
                data = response
            });
        }

        [HttpGet("archived")]
        public async Task<IActionResult> GetArchivedNotes(CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var notes = await _notesService.GetArchivedAsync(
                userId, cancellationToken);

            var response = notes.Select(MapToResponse).ToList();

            return Ok(new
            {
                isSuccess = true,
                message = "Archived notes retrieved successfully.",
                data = response
            });
        }

        [HttpGet("trash")]
        public async Task<IActionResult> GetTrashedNotes(CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var notes = await _notesService.GetTrashedAsync(
                userId, cancellationToken);

            var response = notes.Select(MapToResponse).ToList();

            return Ok(new
            {
                isSuccess = true,
                message = "Trashed notes retrieved successfully.",
                data = response
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetNoteById(Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var note = await _notesService.GetByIdAsync(
                id, userId, cancellationToken);

            if (note == null)
                return NotFound(new { message = "Note not found." });

            return Ok(new
            {
                isSuccess = true,
                message = "Note retrieved successfully.",
                data = MapToResponse(note)
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user ID in token."
                });
            }

            var note = new Note
            {
                UserId = userId,
                Title = request.Title.Trim(),
                Content = request.Content,
                Color = request.Color,
                IsPinned = request.IsPinned,
                IsArchived = false,
                IsTrashed = false
            };

            var createdNote = await _notesService.CreateAsync(
                note, cancellationToken);

            return CreatedAtAction(
                nameof(GetNoteById),
                new { id = createdNote.Id },
                new
                {
                    isSuccess = true,
                    message = "Note created successfully.",
                    data = MapToResponse(createdNote)
                });
        }


        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteRequest request,
           CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user ID in token."
                });
            }

            var note = new Note
            {
                Id = id,
                UserId = userId,
                Title = request.Title.Trim(),
                Content = request.Content,
                Color = request.Color,
                IsPinned = request.IsPinned
            };

            try
            {
                var updated = await _notesService.UpdateAsync(
                    note, cancellationToken);

                if (!updated)
                {
                    return NotFound(new
                    {
                        message = "Note not found or cannot be updated."
                    });
                }

                return Ok(new
                {
                    isSuccess = true,
                    message = "Note updated successfully."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }


        [HttpPut("{id:guid}/archive")]
        public Task<IActionResult> ArchiveNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeArchiveStatus(id, true, cancellationToken);
        }


        [HttpPut("{id:guid}/unarchive")]
        public Task<IActionResult> UnarchiveNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeArchiveStatus(id, false, cancellationToken);
        }

        private async Task<IActionResult> ChangeArchiveStatus(Guid id, bool isArchived,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var updated = await _notesService.SetArchivedAsync(
                id, userId, isArchived, cancellationToken);

            if (!updated)
                return NotFound(new
                {
                    message = "Note not found or cannot be archived."
                });

            return Ok(new
            {
                isSuccess = true,
                message = isArchived
                    ? "Note archived successfully."
                    : "Note unarchived successfully."
            });
        }


        [HttpPut("{id:guid}/trash")]
        public Task<IActionResult> TrashNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeTrashStatus(id, true, cancellationToken);
        }


        [HttpPut("{id:guid}/restore")]
        public Task<IActionResult> RestoreNote(
            Guid id,
            CancellationToken cancellationToken)
        {
            return ChangeTrashStatus(id, false, cancellationToken);
        }

        private async Task<IActionResult> ChangeTrashStatus(Guid id, bool isTrashed,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var updated = await _notesService.SetTrashedAsync(
                id, userId, isTrashed, cancellationToken);

            if (!updated)
                return NotFound(new
                {
                    message = "Note not found or cannot be moved."
                });

            return Ok(new
            {
                isSuccess = true,
                message = isTrashed
                    ? "Note moved to trash successfully."
                    : "Note restored successfully."
            });
        }


        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteNote(Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var deleted = await _notesService.DeleteAsync(
                id, userId, cancellationToken);

            if (!deleted)
                return NotFound(new
                {
                    message = "Note not found or must be moved to trash before deletion."
                });

            return Ok(new
            {
                isSuccess = true,
                message = "Note permanently deleted."
            });
        }

        private bool TryGetUserId(out Guid userId)
        {
            var userIdClaim = User.FindFirst("sub")
                              ?? User.FindFirst(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userIdClaim?.Value, out userId);
        }

        private static NoteResponse MapToResponse(Note note)
        {
            return new NoteResponse
            {
                Id = note.Id,
                UserId = note.UserId,
                Title = note.Title,
                Content = note.Content,
                Color = note.Color,
                IsPinned = note.IsPinned,
                IsArchived = note.IsArchived,
                IsTrashed = note.IsTrashed,
                CreatedAtUtc = note.CreatedAtUtc,
                UpdatedAtUtc = note.UpdatedAtUtc,

                Labels = note.NoteLabels
                    .Where(nl => nl.Label != null)
                    .Select(nl => new NoteLabelResponse
                    {
                        Id = nl.Label!.Id,
                        Name = nl.Label.Name
                    })
                    .ToList()
            };
        }
    }
}