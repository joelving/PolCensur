using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemokratiskDialog.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsDummyUser { get; set; }
        public List<Block> Blocks { get; set; }
    }
}
