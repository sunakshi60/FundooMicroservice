using Fundoo.Notes.Api.Contracts.Requests;
using Fundoo.Notes.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Fundoo.Notes.Api.Controllers
{
    [ApiController]
    [Route("api/labels")]
    [Authorize]
    public class LabelsController : ControllerBase
    {
        private readonly ILabelsService _labelsService;

        public LabelsController(ILabelsService labelsService)
        {
            _labelsService = labelsService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateLabel([FromBody] CreateLabelRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            try
            {
                var label = await _labelsService.CreateAsync(
                    userId, request.Name, cancellationToken);

                return CreatedAtAction(
                    nameof(GetLabels),
                    new
                    {
                        isSuccess = true,
                        message = "Label created successfully.",
                        data = label
                    });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetLabels(
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var labels = await _labelsService.GetAllAsync(
                userId, cancellationToken);

            return Ok(new
            {
                isSuccess = true,
                message = "Labels retrieved successfully.",
                data = labels
            });
        }


        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateLabel(Guid id,
            [FromBody] UpdateLabelRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            try
            {
                var updated = await _labelsService.UpdateAsync(
                    id, userId, request.Name, cancellationToken);

                if (!updated)
                    return NotFound(new { message = "Label not found." });

                return Ok(new
                {
                    isSuccess = true,
                    message = "Label updated successfully."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteLabel(Guid id,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { message = "Invalid user ID in token." });

            var deleted = await _labelsService.DeleteAsync(
                id, userId, cancellationToken);

            if (!deleted)
                return NotFound(new { message = "Label not found." });

            return Ok(new
            {
                isSuccess = true,
                message = "Label deleted successfully."
            });
        }

        [HttpPut("{noteId:guid}/attach/{labelId:guid}")]
        public async Task<IActionResult> AttachLabel(Guid noteId, Guid labelId,
        [FromServices] ILabelsService labelsService,
        CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user ID in token."
                });
            }

            var attached = await labelsService.AttachToNoteAsync(
                noteId, labelId, userId, cancellationToken);

            if (!attached)
            {
                return NotFound(new
                {
                    message = "Note or label not found, or they do not belong to you."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Label attached to note successfully."
            });
        }

        [HttpDelete("{noteId:guid}/delete/{labelId:guid}")]
        public async Task<IActionResult> DetachLabel(Guid noteId, Guid labelId,
            [FromServices] ILabelsService labelsService,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user ID in token."
                });
            }

            var detached = await labelsService.DetachFromNoteAsync(
                noteId, labelId, userId, cancellationToken);

            if (!detached)
            {
                return NotFound(new
                {
                    message = "Note-label association not found."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Label removed from note successfully."
            });
        }
        private bool TryGetUserId(out Guid userId)
        {
            var userIdClaim = User.FindFirst("sub")
                              ?? User.FindFirst(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userIdClaim?.Value, out userId);
        }
    }
}
