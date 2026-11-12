using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notes.Domain.Entity
{
    public class Note
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }          // plain column - NO foreign key (User table is in another database)
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string? Color { get; set; }
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }
        public bool IsTrashed { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public List<NoteLabel> NoteLabels { get; set; } = new();

    }
}
