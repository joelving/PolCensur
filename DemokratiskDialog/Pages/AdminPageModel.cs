using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DemokratiskDialog.Pages
{
    public abstract class AdminPageModel : PageModel
    {
        protected bool IsAdmin()
            => User.Identity.IsAuthenticated && User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value == "peter_joelving";
    }
}
