using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Notes.Domain.Entity
{
    public class NoteLabel
    {
        public Guid NoteId { get; set; }
        public Note Note { get; set; } = null!;
        public Guid LabelId { get; set; }
        public Label Label { get; set; } = null!;

    }
}
