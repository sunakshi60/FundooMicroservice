namespace Fundoo.Notes.Api.Contracts.Response
{
    public class NoteResponse
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string? Color { get; set; }

        public bool IsPinned { get; set; }

        public bool IsArchived { get; set; }

        public bool IsTrashed { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public List<NoteLabelResponse> Labels { get; set; } = new();
    }
}
