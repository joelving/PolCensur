using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public interface IBackgroundQueue<T>
    {
        Task EnqueueAsync(T job, CancellationToken cancellationToken);

        Task<(T job, Action callback)> DequeueAsync(CancellationToken cancellationToken);
    }
}
