using DemokratiskDialog.Models;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class CheckBlockedJobQueue : IBackgroundQueue<CheckBlockedJob>
    {
        private ConcurrentQueue<CheckBlockedJob> _workItems = new ConcurrentQueue<CheckBlockedJob>();
        private SemaphoreSlim _queuedItems = new SemaphoreSlim(0);
        private SemaphoreSlim _maxQueueSize;

        public CheckBlockedJobQueue(int maxQueueSize)
        {
            _maxQueueSize = new SemaphoreSlim(maxQueueSize);
        }

        public async Task EnqueueAsync(CheckBlockedJob job, CancellationToken cancellationToken)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            // This causes callers to wait until there's room in the queue.
            await _maxQueueSize.WaitAsync(cancellationToken);
            _workItems.Enqueue(job);
            _queuedItems.Release();
        }

        public async Task<(CheckBlockedJob job, Action callback)> DequeueAsync(CancellationToken cancellationToken)
        {
            // This ensures we can never dequeue unless the semaphore has been increased by a corresponding release.
            await _queuedItems.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var job);

            return (job, () => _maxQueueSize.Release());
        }
    }
}
