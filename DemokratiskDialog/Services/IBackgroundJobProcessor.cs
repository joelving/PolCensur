using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public interface IBackgroundJobProcessor<T>
    {
        Task ProcessJob((T job, Action callback) job, CancellationToken cancellationToken);
    }
    
}
