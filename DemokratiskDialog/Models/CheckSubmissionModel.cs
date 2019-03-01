using System.ComponentModel.DataAnnotations;

namespace DemokratiskDialog.Models
{
    public class CheckSubmissionModel
    {
        [EmailAddress]
        public string Email { get; set; }

        public bool Publicity { get; set; }
    }
}
