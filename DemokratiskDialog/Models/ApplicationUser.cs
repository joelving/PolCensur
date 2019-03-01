using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace DemokratiskDialog.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string ProfilePictureUrl { get; set; }
        public bool IsDummyUser { get; set; }
        public List<Block> Blocks { get; set; }
        public bool ShowProfileWithBlocks { get; set; }
    }
}
