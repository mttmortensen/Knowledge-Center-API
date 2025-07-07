using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center.Models
{
    public class LogEntry
    {
        public int LogId { get; set; }
        public int NodeId { get; set; }
        public DateTime EntryDate { get; set; }
        public string Content { get; set; }
        public int TagId { get; set; }
        public string Tag { get; set; }
        public bool ContributesToProgress { get; set; }

    }
}
