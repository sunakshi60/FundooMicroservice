using System.ComponentModel.DataAnnotations;

namespace Fundoo.Notes.Api.Contracts.Requests
{
    public class AddCollaboratorRequest
    {
        [Required]
        public Guid UserId { get; set; }
    }
}
