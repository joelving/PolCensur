using DemokratiskDialog.Models;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class ContinuousCheckBlockedJobQueue : IBackgroundQueue<ContinuousCheckBlockedJob>
    {
        private ConcurrentQueue<ContinuousCheckBlockedJob> _workItems = new ConcurrentQueue<ContinuousCheckBlockedJob>();
        private SemaphoreSlim _queuedItems = new SemaphoreSlim(0);
        private SemaphoreSlim _maxQueueSize;
        private ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokenSources = new ConcurrentDictionary<string, CancellationTokenSource>();

        public ContinuousCheckBlockedJobQueue(int maxQueueSize)
        {
            _maxQueueSize = new SemaphoreSlim(maxQueueSize);
        }

        public async Task EnqueueAsync(ContinuousCheckBlockedJob job, CancellationToken cancellationToken = default)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            if (job.Terminate)
            {
                if (_cancellationTokenSources.TryRemove(job.CheckingForUserId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
            else if (_cancellationTokenSources.ContainsKey(job.CheckingForUserId))
            {
                // Don't start another job, if one is already running.
                return;
            }
            else
            {
                // This causes callers to wait until there's room in the queue.
                await _maxQueueSize.WaitAsync(cancellationToken);

                var cts = new CancellationTokenSource();
                job.CancellationToken = cts.Token;
                _cancellationTokenSources.TryAdd(job.CheckingForUserId, cts);

                _workItems.Enqueue(job);
                _queuedItems.Release();
            }
        }

        public async Task<(ContinuousCheckBlockedJob job, Action callback)> DequeueAsync(CancellationToken cancellationToken)
        {
            // This ensures we can never dequeue unless the semaphore has been increased by a corresponding release.
            await _queuedItems.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var job);

            return (job, () => _maxQueueSize.Release());
        }
    }
}
