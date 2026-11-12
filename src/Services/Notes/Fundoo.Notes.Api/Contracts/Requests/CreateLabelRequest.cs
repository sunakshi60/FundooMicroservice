using System.ComponentModel.DataAnnotations;

namespace Fundoo.Notes.Api.Contracts.Requests
{
    public class CreateLabelRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
