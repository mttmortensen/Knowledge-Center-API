using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Knowledge_Center_API.Models
{
    public class Domain
    {
        public int DomainId { get; set; }
        public string DomainName { get; set; }
        public string DomainDescription { get; set; }
        public string DomainStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsed { get; set; }
    }
}
