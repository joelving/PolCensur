using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class TaskManager
    {
        private ConcurrentDictionary<Guid, WeakReference<Task>> tasks = new ConcurrentDictionary<Guid, WeakReference<Task>>();

        public bool TryAdd(Guid key, Task task) => tasks.TryAdd(key, new WeakReference<Task>(task));

        public TaskStatus? TryGetStatus(Guid key)
        {
            if (tasks.TryGetValue(key, out var wref) && wref.TryGetTarget(out var task))
                return task?.Status;

            return null;
        }

        public bool TryRemove(Guid key, out Task task)
        {
            task = null;
            return tasks.TryRemove(key, out var wref) && wref.TryGetTarget(out task);
        }
    }
}
