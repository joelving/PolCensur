using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public interface IBackgroundQueue<T>
    {
        Task EnqueueAsync(T job, CancellationToken cancellationToken = default);

        Task<(Guid id, T job, Action callback)> DequeueAsync(CancellationToken cancellationToken = default);
    }
}
