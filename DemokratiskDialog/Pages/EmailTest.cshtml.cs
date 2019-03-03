using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemokratiskDialog.Pages
{
    public class EmailTestModel : PageModel
    {
        private readonly IHostingEnvironment _environment;
        private readonly EmailService _emailService;

        public EmailTestModel (IHostingEnvironment environment, EmailService emailService)
        {
            _environment = environment;
            _emailService = emailService;
        }

        public IActionResult OnGet()
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            var response = await _emailService.SendEmailAsync("peter@joelving.dk", "Test", "Please let this through.");

            return Page();
        }
    }
}