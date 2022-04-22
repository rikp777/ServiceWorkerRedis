using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace MockAppRedis;

using Task = Task;

/// <summary>
/// Distribute work item interface 
/// </summary>
public interface IDistributedWorkItem
{
    Guid Id { get; set; }
    bool IsDone { get; set; }
    bool HasPriority { get; }

    void ExpensiveWork();
}

/// <summary>
/// Some work item
/// </summary>
public class WorkItem : IDistributedWorkItem
{
    public Guid Id { get; set; }
    public bool IsDone { get; set; }
    public bool HasPriority { get; set; }
    public void ExpensiveWork()
    {
        throw new NotImplementedException();
    }

    public WorkItem(bool isDone, bool hasPriority)
    {
        IsDone = isDone;
        HasPriority = hasPriority;
    }
}

//https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task?view=net-6.0
/// <summary>
/// Interaction with a distributed queue
/// </summary>
/// <typeparam name="TWorkItem">The type that implements the work item that is queued on the distributed queue</typeparam>
public class DistributedQueue<TWorkItem> where TWorkItem : IDistributedWorkItem
{
    /// <summary>
    /// Constructor DistributedQueue
    /// </summary>
    /// <param name="queueNamespace">The que namespace </param>
    /// <param name="handleWorkFunction">The function that needs to be handled in the worker</param>
    /// <param name="log">The logger instance</param>
    /// <param name="maxAmountOfParallelism">The max parallel workers that may exists</param>
    /// <param name="queueProcessingCancellationToken">The cancellation token for canceling que work</param>
    public DistributedQueue(
        string queueNamespace, 
        Action<TWorkItem, CancellationToken> handleWorkFunction, 
        ILogger log,
        int maxAmountOfParallelism,
        CancellationToken queueProcessingCancellationToken
        )
    {
        _queueNamespace = queueNamespace;
        _handleWorkFunction = handleWorkFunction;
        _log = log;
        _maxAmountOfParallelism = maxAmountOfParallelism;
        _queueProcessingCancellationToken = queueProcessingCancellationToken;
    }

    private readonly ILogger _log;
    private int _maxAmountOfParallelism;
    private readonly CancellationToken _queueProcessingCancellationToken;

    private const int DefaultRetryDequeueDelay = 100;

    /// <summary>
    /// Namespace for this specific <seealso cref="DistributedQueue{TWorkItem}"/> so multiple queues can exist without collisions
    /// </summary>
    /// <remarks>Can be used as prefix for pub/sub topics and a component name for metrics</remarks>
    private readonly string _queueNamespace;

    /// <summary>
    /// The function that is able to handle a work item inside a <seealso cref="Worker"/>
    /// </summary>
    private readonly Action<TWorkItem, CancellationToken> _handleWorkFunction;

    /// <summary>
    /// This is our temporary mock queue, should be replaced by a central queue where we can dequeue individual items
    /// </summary>
    private readonly ConcurrentQueue<TWorkItem> _workItemQueue = new();
    
    /// <summary>
    /// This is our temporary mock queue, should be replaced by a central queue where we can dequeue individual items
    /// </summary>
    private readonly ConcurrentQueue<TWorkItem> _priorityWorkItemQueue = new();

    /// <summary>
    /// Dequeues work items form the queue as long as the <seealso cref="_maxAmountOfParallelism"/> threshold is not reached
    /// </summary>
    public async Task ScheduleWorkItems()
    {
        _log.Information("Scheduler started");
        var tasks = new List<Task>(_maxAmountOfParallelism);
        
        while (!_queueProcessingCancellationToken.IsCancellationRequested)
        {
            
            if (tasks.Count < _maxAmountOfParallelism)
            {
                // There is space left in the task list, so we dequeue a work item to schedule
                if (!_priorityWorkItemQueue.TryDequeue(out var workItem) && !_workItemQueue.TryDequeue(out workItem))
                {
                    // no work item available, we wait a little bit and retry
                    await Task.Delay(DefaultRetryDequeueDelay, _queueProcessingCancellationToken);
                    continue;
                }

                // create the actual work item task and start it immediately
                var workItemTask = Task.Run(() =>
                {
                    _handleWorkFunction(workItem, _queueProcessingCancellationToken);
                    workItem.IsDone = true;
                    workItem.Id = Guid.NewGuid();
                }, _queueProcessingCancellationToken);
                _log.Information($"{_queueNamespace} Running work item {workItem.Id} | current running {tasks.Count} workers parallel");
                tasks.Add(workItemTask);
                continue;
            }

            // Task list is full
            // First try to create a spot in the list by cleaning the completed tasks
            var numberOfTasksCleared = tasks.RemoveAll(q => q.IsCompleted);
            if (numberOfTasksCleared > 0)
            {
                // Try the while loop again, we have a space for new tasks now
                continue;
            }
            
            // The task list is full, and all tasks are running
            var completedTaskIndex = Task.WaitAny(tasks.ToArray(), DefaultRetryDequeueDelay, _queueProcessingCancellationToken);
            if (completedTaskIndex == -1)
            {
                // -1 signals that the timeout has been reached, we retry the loop in that case
                continue;
            }
            
            tasks.RemoveAt(completedTaskIndex);
        }
    }
    
    /// <summary>
    /// Enqueue: to add item
    /// Enqueue a new work item of type <typeparamref name="TWorkItem"/> on the the distributed queue, to be picked up by the <seealso cref="DistributedQueue{TWorkItem}"/> later
    /// </summary>
    /// <param name="newWorkItem">The work item to enqueue on the distributed queue</param>
    /// <returns>True if the work item is successfully enqueued on the distributed queue</returns>
    public bool TryEnqueueWorkItem(TWorkItem newWorkItem)
    {
        // TODO: serialize the work item and make sure that the required files of IWorkItem are set
        // TODO: publish the work item to redis

        if (newWorkItem.HasPriority)    
        {
            _priorityWorkItemQueue.Enqueue(newWorkItem);
        }
        else
        {
            _workItemQueue.Enqueue(newWorkItem);
        }
        
        return true;
    }
    
    /// <summary>
    /// Set the maximum number of work items that can be processed in parallel
    /// </summary>
    /// <param name="maxAmountOfParallelism">Max number of parallel work items that can be consuming from the distributed queue</param>
    public void SetMaxAmountOfParallelism(int maxAmountOfParallelism)
    {
        _maxAmountOfParallelism = maxAmountOfParallelism;
    }
}


