using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemokratiskDialog.Pages
{
    public class ContinuousJobsModel : AdminPageModel
    {
        //private readonly TaskManager _taskManager;
        private readonly ContinuousCheckBlockedJobQueue _jobQueue;

        public ContinuousJobsModel(/*TaskManager taskManager, */IBackgroundQueue<ContinuousCheckBlockedJob> jobQueue)
        {
            //_taskManager = taskManager;
            _jobQueue = jobQueue as ContinuousCheckBlockedJobQueue;
        }

        public List<string> ActiveUsers { get; set; }
        public int QueuedCount { get; set; }
        public int AvailableCount { get; set; }

        public IActionResult OnGet()
        {
            if (!IsAdmin())
                return NotFound();

            ActiveUsers = _jobQueue.GetAllActiveUserIds();
            QueuedCount = _jobQueue.GetQueuedCount();
            AvailableCount = _jobQueue.GetAvailableCount();

            return Page();
        }

        public IActionResult OnPostIncreaseAvailable()
        {
            _jobQueue.AllowOne();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTerminateTaskAsync([FromForm]string userId)
        {
            await _jobQueue.EnqueueAsync(new ContinuousCheckBlockedJob
            {
                CheckingForUserId = userId,
                Terminate = true
            });

            return RedirectToPage();
        }
    }
}