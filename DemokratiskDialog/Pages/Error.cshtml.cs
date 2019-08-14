using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NodaTime;

namespace DemokratiskDialog.Pages
{
    public class ErrorModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IClock _clock;

        public ErrorModel(ApplicationDbContext context, IClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public string RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public async Task OnGetAsync()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            // Get the details of the exception that occurred
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionFeature != null)
            {
                // Get which route the exception occurred at
                string routeWhereExceptionOccurred = exceptionFeature.Path;

                // Get the exception that occurred
                Exception exceptionThatOccurred = exceptionFeature.Error;

                try
                {
                    await _context.LogException(RequestId, routeWhereExceptionOccurred, exceptionThatOccurred, _clock, HttpContext.RequestAborted);
                }
                catch (Exception)
                { }
            }
        }
    }
}