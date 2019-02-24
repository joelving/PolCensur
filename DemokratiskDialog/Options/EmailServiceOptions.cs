using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemokratiskDialog.Options
{
    public class EmailServiceOptions
    {
        public string SendGridKey { get; set; }
        public string SenderAddress { get; set; }
        public string SenderName { get; set; }
    }
}
