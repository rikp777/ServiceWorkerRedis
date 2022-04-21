using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

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
        var ratio = (decimal) 1 / 40000;
        var ratioIs = (decimal) initialNumberOfParallelism / numberOfItems;
        Assert.GreaterOrEqual(ratioIs, ratio, "Ratio between number of items and parallelism is to small");
            
        var counter = 0;
        var sut = new DistributedQueue<TestDistributedWorkItem>(QueueName, (item, token) =>
        {
            DoExpensiveWork();
            Interlocked.Increment(ref counter);
        }, NullLogger<DistributedQueue<TestDistributedWorkItem>>.Instance, initialNumberOfParallelism, CancellationToken.None);
            
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
        for (int i = 0; i < 1000; i++)
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

    private void DoExpensiveWork()
    {
        var random = new Random();
   
        for (var i = 0; i < random.Next(1000); i++)  
        {  
            Console.Write("{0} ", FibonacciSeries(i).ToString());  
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

    public class TestDistributedWorkItem : IDistributedWorkItem
    {
        public TestDistributedWorkItem() { }
        public TestDistributedWorkItem(bool isPriority)
        {
            IsPriority = isPriority;
        }

        public bool IsPriority { get; set; }
        public Guid Id { get; set; }
        public bool IsDone { get; set; }
        public bool HasPriority { get; }
        public Task Run()
        {
            throw new NotImplementedException();
        }
    }
        
    private BigInteger FibonacciSeries(int n)  
    {  
        BigInteger firstnumber = 0, secondnumber = 1, result = 0;  
   
        if (n == 0) return 0; //To return the first Fibonacci number   
        if (n == 1) return 1; //To return the second Fibonacci number   
   
   
        for (int i = 2; i <= n; i++)  
        {  
            result = firstnumber + secondnumber;  
            firstnumber = secondnumber;  
            secondnumber = result;  
        }  
   
        return result;  
    }  
}