using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.ConsoleTest
{
    public class BlockingCollectionTest
    {
        public static void Test()
        {
            var blockingCollection = new BlockingCollection<string>();

            var producer = Task.Factory.StartNew(() =>
            {
                for (var count = 0; count < 10; count++)
                {
                    blockingCollection.Add("value" + count);
                    Thread.Sleep(300);
                }

                // 集合无元素时，将会挂起等待。
                blockingCollection.CompleteAdding();
            });

            var consumer1 = Task.Factory.StartNew(() =>
            {
                foreach (var value in blockingCollection.GetConsumingEnumerable())
                {
                    Console.WriteLine("Worker 1: " + value);
                }
            });

            var consumer2 = Task.Factory.StartNew(() =>
            {
                foreach (var value in blockingCollection.GetConsumingEnumerable())
                {
                    Console.WriteLine("Worker 2: " + value);
                }
            });

            Task.WaitAll(producer, consumer1, consumer2);

            Console.WriteLine("消费完成");
        }
    }
}
