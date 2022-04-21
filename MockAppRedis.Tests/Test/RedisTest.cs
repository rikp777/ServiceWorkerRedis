using Microsoft.Extensions.Logging.Abstractions;
using MockAppRedis.Redis;
using NUnit.Framework;

namespace MockAppRedis.Tests.Test;

public class TestDataEntity
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string DisplayName { get; set; }
    public string UserName { get; set; }
    public string ColorCode { get; set; }
    public string Email { get; set; }
    public bool Avatar { get; set; }

    public TestDataEntity(Guid id, string firstName, string lastName, string displayName, string userName, string colorCode, string email, bool avatar)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        UserName = userName;
        ColorCode = colorCode;
        Email = email;
        Avatar = avatar;
    }

    public TestDataEntity()
    {
        
    }
    
    
    
    public void someFunction(string message)
    {
        Console.WriteLine($@"Received message on the subscription: {message}");
    }
}

[TestFixture]
public class RedisTest
{
    private static IKeyValueStore _redisKeyValueStore;
    private static IPubSub _redisPubSub;
    private static TimeSpan _keepAlive = TimeSpan.FromSeconds(10);

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var client = new RedisClient(new List<string>() {"localhost:6379"}, "testPrefix", NullLogger<RedisClient>.Instance);

        _redisKeyValueStore = client;
        _redisPubSub = client;
    }

    [Test]
    public void IntTest()
    {
        // Test values 
        const string key = "Int";
        const int intSetValue = 1234;
        
        // Try get value 
        var success = _redisKeyValueStore.TryGet(key, out int value);
        
        Assert.IsFalse(success, "Value already exists in the store");
        Assert.IsTrue(_redisKeyValueStore.TrySet(key, intSetValue, _keepAlive));
        
        success = _redisKeyValueStore.TryGet(key, out int intGetValue);
        Assert.True(success);
        Assert.AreEqual(intSetValue, intGetValue);
    }
    
    [Test]
    public void StringTest()
    {
        // Test values 
        const string key = "String";
        const string stringSetValue = "Value of a string";
        
        // Try get value
        var success = _redisKeyValueStore.TryGet(key, out string value);

        Assert.IsFalse(success, "Value already exists in the store");
        Assert.IsTrue(_redisKeyValueStore.TrySet(key, stringSetValue, _keepAlive));
        
        success = _redisKeyValueStore.TryGet(key, out string stringGetValue);
        Assert.True(success);
        Assert.AreEqual(stringSetValue, stringGetValue);
    }
    
    [Test]
    [TestCase(12.34)]
    [TestCase(0.34)]
    public void DecimalTest(decimal decimalSetValue)
    {
        var key = $"Decimal_{Guid.NewGuid()}";

        var success = _redisKeyValueStore.TryGet(key, out decimal value);
        
        Assert.IsFalse(success, "Value already exists in the store");
        Assert.IsTrue(_redisKeyValueStore.TrySet(key, decimalSetValue, _keepAlive));
        
        success = _redisKeyValueStore.TryGet(key, out decimal decimalGetValue);
        Assert.True(success);
        Assert.AreEqual(decimalSetValue, decimalGetValue);
    }
    
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void BoolTest(bool boolSetValue)
    {
        var key = $"Bool_{Guid.NewGuid()}";

        var success = _redisKeyValueStore.TryGet(key, out bool value);
        Assert.IsFalse(success, "Value already exists in the store");
        Assert.IsTrue(_redisKeyValueStore.TrySet(key, boolSetValue, _keepAlive));
        
        success = _redisKeyValueStore.TryGet(key, out bool boolGetValue);
        Assert.True(success);
        Assert.AreEqual(boolSetValue, boolGetValue);
    }
    
    [Test]
    [TestCase(12.34)]
    [TestCase(0.34)]
    public void DoubleTest(double doubleSetValue)
    {
        var key = $"Double_{Guid.NewGuid()}";

        var success = _redisKeyValueStore.TryGet(key, out double value);
        Assert.IsFalse(success, "Value already exists in the store");
        Assert.True(_redisKeyValueStore.TrySet(key, doubleSetValue, _keepAlive));
        
        
        success = _redisKeyValueStore.TryGet(key, out double doubleGetValue);
        Assert.True(success);
        Assert.AreEqual(doubleSetValue, doubleGetValue);
    }
    
    
    [Test]
    public void NonExistingTest()
    {
        const string key = "NonExisting";
        const double doubleSetValue = 12.34;
        var success = _redisKeyValueStore.TryGet(key, out double doubleGetValue);
        Assert.False(success);
        Assert.AreNotEqual(doubleSetValue, doubleGetValue);
    }
    
    
    [Test]
    [Explicit("Objects needs setters")]
    public void ObjectTest()
    {
        const string key = "Complex";
        var objectSetValue = new TestDataEntity(
            Guid.NewGuid(),
            "John",
            "doe",
            "Johnie",
            "Johndoe777",
            "#000000",
            "johndoe@.nl",
            false
        );
        var success = _redisKeyValueStore.TrySet(key, objectSetValue, _keepAlive);
        Assert.True(success);
        
        success = _redisKeyValueStore.TryGet(key, out TestDataEntity objectGetValue);
        Assert.True(success);
        Assert.AreEqual(objectSetValue.Id, objectGetValue.Id);
        Assert.AreEqual(objectSetValue.UserName, objectGetValue.UserName);
    }
    
    
    [Test]
    public void SubscribeTest()
    {
        const string topic = "This topic is amazing";
        var isSubscribed = _redisPubSub.SubscribeSequentially<string>(topic, new TestDataEntity().someFunction);
        Assert.IsTrue(isSubscribed);
    }
    
    [Test]
    public void PublishTest()
    {
        const string topic = "test";
        const string message = "Published Message";
        var isPublished = _redisPubSub.TryPublish(topic, message);
        Assert.IsTrue(isPublished);
    }
    
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task PubSubTest(bool sync)
    {
        var topic = "test:pubsubtest:" + (sync ? "sync" : "async");
        var receivedMessages = new List<string>();

        var messages = new List<string>()
        {
            "TestMessage1",
            "TestMessage2",
            "TestMessage3"
        };

        var subTask = Task.Run(() =>
        {
            if (sync)
            {
                _redisPubSub.SubscribeSequentially<string>(topic,
                    (v) =>
                    {
                        receivedMessages.Add(v);
                    });
            }
            else
            {
                _redisPubSub.SubscribeConcurrently<string>(topic,
                    (v) =>
                    {
                        receivedMessages.Add(v);
                    });
            }
        });

        await Task.Delay(100);

        var pubTask = Task.Run(() =>
        {
            foreach (var message in messages)
            {
                _redisPubSub.TryPublish(topic, message);
            }
        });

        await pubTask;

        await Task.Delay(100);

        if (sync)
        {
            CollectionAssert.AreEqual(messages, receivedMessages.ToList());
        }
        else
        {
            CollectionAssert.AreEquivalent(messages, receivedMessages.ToList());
        }
    }
    
    
    [Test]
    public async Task WaitUntilAsyncTest_LateResolve()
    {
        const string key = nameof(WaitUntilAsyncTest_LateResolve);
        const string asyncSetValue = "The value for the async get";
        var timeOut = TimeSpan.FromSeconds(10);

        var success = _redisKeyValueStore.TryGet(key, out string getValue);
        Assert.IsFalse(success);
        
        var delayTime = TimeSpan.FromMilliseconds(timeOut.TotalMilliseconds / 20d);
        _ = Task.Delay(delayTime).ContinueWith(t =>
        {
            _redisKeyValueStore.TrySet(key, asyncSetValue, timeOut);
        });

        var awaitedValue = await _redisKeyValueStore.GetUntilValueAsync<string>(key, CancellationToken.None, timeOut);
        Assert.IsNotNull(awaitedValue);
        Assert.AreEqual(asyncSetValue, awaitedValue);
    }
    
    [Test]
    public async Task WaitUntilAsyncTest_ImmediateResolve()
    {
        const string key = nameof(WaitUntilAsyncTest_ImmediateResolve);
        const string asyncSetValue = "The value for the async get";
        var timeOut = TimeSpan.FromSeconds(1);
        
        Assert.True(_redisKeyValueStore.TrySet(key, asyncSetValue, timeOut));

        var awaitedGetValue = await _redisKeyValueStore.GetUntilValueAsync<string>(key, CancellationToken.None, timeOut);
        Assert.IsNotNull(awaitedGetValue);
        Assert.AreEqual(asyncSetValue, awaitedGetValue);
    }
    
    [Test]
    public async Task WaitUntilAsyncTest_TimeoutReached()
    {
        const string key = nameof(WaitUntilAsyncTest_TimeoutReached);
        var timeOut = TimeSpan.FromMilliseconds(100);

        Assert.IsFalse(_redisKeyValueStore.TryGet(key, out string asyncGetValue));
        Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await _redisKeyValueStore.GetUntilValueAsync<string>(key, CancellationToken.None, timeOut);
        });
    }
    
    [Test]
    public void TimeOutTest()
    {
        const string key = "Async";
        const string asyncSetValue = "The value for the async get";
        var timeOut = TimeSpan.FromSeconds(1);

        Assert.IsFalse(_redisKeyValueStore.TryGet(key, out string asyncGetValue));
        var delayTime = Convert.ToInt32(timeOut.TotalMilliseconds * 2);

        _ = Task.Delay(delayTime).ContinueWith(t =>
        {
            _redisKeyValueStore.TrySet(key, asyncSetValue, timeOut);
        });

        var ex = Assert.ThrowsAsync<TimeoutException>(async () => await _redisKeyValueStore.GetUntilValueAsync<string>(key, CancellationToken.None, timeOut));
    }
}