using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    //public class TaskManager
    //{
    //    private readonly ConcurrentDictionary<Guid, WeakReference<Task>> tasks = new ConcurrentDictionary<Guid, WeakReference<Task>>();

    //    public bool TryAdd(Guid key, Task task) => tasks.TryAdd(key, new WeakReference<Task>(task));

    //    public TaskStatus? TryGetStatus(Guid key)
    //    {
    //        if (tasks.TryGetValue(key, out var wref) && wref.TryGetTarget(out var task))
    //            return task?.Status;

    //        if (TryRemove(key, out task))
    //            return task?.Status;

    //        return null;
    //    }

    //    public bool TryRemove(Guid key, out Task task)
    //    {
    //        task = null;
    //        return tasks.TryRemove(key, out var wref) && wref.TryGetTarget(out task);
    //    }

    //    public List<(Guid, TaskStatus)> GetAllStatuses()
    //        => tasks.Keys
    //        .Select(key => (key, status: TryGetStatus(key)))
    //        .Where(t => !(t.status is null))
    //        .Select(t => (t.key, status: t.status.Value))
    //        .ToList();
    //}
}
