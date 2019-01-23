using Disruptor.ConsoleTest.Example;
using System;
using System.Threading.Tasks;

namespace Disruptor.ConsoleTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //new FalseSharingTest().StartTest();
            //MonitorTest.Test();
            //CountdownEventTest.Test();
            //CountdownEventTest.Test1();
            //YieldSleep0Sleep1Test.Test();
            //BarrierTest.Test();
            //await SequentialThreeConsumers.RunAsync();
            BlockingCollectionTest.Test();

            Console.Read();
        }
    }
}
