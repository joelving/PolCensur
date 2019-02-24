using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemokratiskDialog.Models
{
    public class CheckBlockedJob
    {
        public string Email { get; set; }
        public string CheckingForUserId { get; set; }
        public string CheckingForScreenName { get; set; }
        public string AccessTokenSecret { get; set; }
    }
}
