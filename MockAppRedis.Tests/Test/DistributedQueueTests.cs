using System.Numerics;
using NUnit.Framework;
using Serilog;

namespace MockAppRedis.Tests.Test;

[TestFixture]
public class DistributedQueueTests
{
    public const string QueueName = "TestQueue";

    [Test]
    [TestCase(5, 10)]
    [TestCase(10, 10)]
    [TestCase(50, 10)]

    [TestCase(50000, 10)]
    [TestCase(50000, 1, ExpectedResult = "Ratio between number of items and parallelism is to small")]
    [TestCase(50000, 100)]
    public void EnqueueAndProcessWorkItem(int numberOfItems, int initialNumberOfParallelism)
    {
        if (Log.Logger.GetType().FullName == "Serilog.Core.Pipeline.SilentLogger")
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
            Log.Logger.Debug("Logger is not configured. Either this is a unit test or you have to configure the logger");
        }
        const decimal ratio = (decimal) 1 / 40000;
        var ratioIs = (decimal) initialNumberOfParallelism / numberOfItems;
        Assert.GreaterOrEqual(ratioIs, ratio, "Ratio between number of items and parallelism is to small");

        var counter = 0;
        var sut = new DistributedQueue<TestDistributedWorkItem>(QueueName, (item, token) =>
            {
                item.ExpensiveWork();
                Interlocked.Increment(ref counter);
            }, Log.Logger , initialNumberOfParallelism,
            CancellationToken.None);

        for (var i = 0; i < numberOfItems; i++)
        {
            var workItem = new TestDistributedWorkItem(false);
            sut.TryEnqueueWorkItem(workItem);
        }

        Task.Run(() => sut.ScheduleWorkItems());

        #region Waiting for completion...

        var maxTimeItCanTake = numberOfItems * 10;
        var minDelay = Math.Max(maxTimeItCanTake / 1000, 10);

        // Check 1000th times if we are completed 
        // Checks if que is not to slow 
        for (var i = 0; i < 1000; i++)
        {
            if (counter == numberOfItems)
            {
                break;
            }

            Thread.Sleep(minDelay);
        }

        #endregion

        Assert.That(counter, Is.EqualTo(numberOfItems));
    }

    [TestCase(5, 1)]
    public void EnqueueAndProcessWorkItemWithPriority(int numberOfItems, int initialNumberOfParallelism)
    {
        if (Log.Logger.GetType().FullName == "Serilog.Core.Pipeline.SilentLogger")
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
            Log.Logger.Debug("Logger is not configured. Either this is a unit test or you have to configure the logger");
        }

        CancellationTokenSource source = new CancellationTokenSource();
        CancellationToken token = source.Token;
        var counter = 0;
        var sut = new DistributedQueue<TestDistributedWorkItem>(QueueName, (item, token) =>
            {
                item.ExpensiveWork();
                Interlocked.Increment(ref counter);
            }, Log.Logger , initialNumberOfParallelism,
            token);
        
        for (var i = 0; i < numberOfItems; i++)
        {
            var workItem = new TestDistributedWorkItem(false);
            sut.TryEnqueueWorkItem(workItem);
        }
        for (var i = 0; i < numberOfItems; i++)
        {
            var workItem = new TestDistributedWorkItem(true);
            sut.TryEnqueueWorkItem(workItem);
        }

        Task.Run(() => sut.ScheduleWorkItems());
        
        #region Waiting for completion...

        var maxTimeItCanTake = numberOfItems * 10;
        var minDelay = Math.Max(maxTimeItCanTake / 1000, 10);

        // Check 1000th times if we are completed 
        // Checks if que is not to slow 
        for (var i = 0; i < 1000; i++)
        {
            if (counter == numberOfItems)
            {
                source.Cancel();
                break;
            }

            Thread.Sleep(minDelay);
        }

        Assert.That(sut.GetPriorityWorkItemQueueCount == 0);
        Assert.That(sut.GetWorkItemQueueCount == numberOfItems);
        
        #endregion
    }
    
    
    
}
public class TestDistributedWorkItem : IDistributedWorkItem
{
    public TestDistributedWorkItem()
    {
    }

    public TestDistributedWorkItem(bool isPriority)
    {
        IsPriority = isPriority;
        HasPriority = isPriority;
    }

    public bool IsPriority { get; set; }
    public Guid Id { get; set; }
    public bool IsDone { get; set; }
    public bool HasPriority { get; }

    public void ExpensiveWork()
    {
        DoExpensiveWork();
    }
    
    
    private void DoExpensiveWork()
    {
        var random = new Random();

        for (var i = 0; i < random.Next(1000); i++)
        {
            var n = i * 55;
            BigInteger firstnumber = 0, secondnumber = 1, result = 0;
            
            for (var j = 2; j <= n; j++)
            {
                result = firstnumber + secondnumber;
                firstnumber = secondnumber;
                secondnumber = result;
            }
        }

        var list = new List<int>();
        for (var i = 0; i < 1000000; i++)
        {
            if (list.Contains(i))
            {
                list.Add(i);
            }

            list.Contains(i);
            list.Contains(i);
            list.Contains(i);
            list.Contains(i);
        }
    }
}