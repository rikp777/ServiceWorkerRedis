using StackExchange.Redis;

namespace MockAppRedis.Extensions;

public class RedisPubSub
{
    public void Subscribe()
    {
        using (ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("127.0.0.1:6379"))
        {
            ISubscriber sub = redis.GetSubscriber();

            var subscribeChannelMessageQueue = sub.Subscribe("messages");
            subscribeChannelMessageQueue.OnMessage(async message =>
            {
                var feedUrl = message.ToString().Remove(0, "messages".Length + 1);
                Console.WriteLine($"Feed received: {feedUrl}");
            });

            
            Console.WriteLine("Subcribed messages");
            Console.ReadKey();
        }
    }

    public void Publish()
    {
        using (ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("127.0.0.1:6379"))
        {
            ISubscriber sub = redis.GetSubscriber();
            
            Console.WriteLine("Please enter any character and exit to exit");

            string input;

            do
            {
                input = Console.ReadLine();
                sub.PublishAsync("messages", input).ConfigureAwait(false);
                Console.WriteLine("Message successfully send");
            } while (input != "exit");
            Console.WriteLine("Exit");
        }
    }
    
}