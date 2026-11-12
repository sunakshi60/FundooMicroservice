using System.ComponentModel.DataAnnotations;

namespace Fundoo.Notes.Api.Contracts.Requests
{
    public class UpdateNoteRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        [MaxLength(50)]
        public string? Color { get; set; }

        public bool IsPinned { get; set; }
    }
}
