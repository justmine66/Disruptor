using Disruptor.Bootstrap;
using System;
using System.Threading.Tasks;

namespace Disruptor.ConsoleTest.Example
{
    public class SequentialThreeConsumers
    {
        private class MyEvent
        {
            public object A { get; set; }
            public object B { get; set; }
            public object C { get; set; }
            public object D { get; set; }

            public override string ToString()
            {
                return $"[A: {A},B: {B},C: {C},D: {D}]";
            }
        }

        private class EventFactory : IEventFactory<MyEvent>
        {
            public MyEvent NewInstance()
            {
                return new MyEvent();
            }
        }

        private class EventHandler1 : IEventHandler<MyEvent>
        {
            public void OnEvent(MyEvent @event, long sequence, bool endOfBatch)
            {
                @event.B = @event.A;

                Console.WriteLine($"{nameof(EventHandler1)}=>{@event}");
            }
        }

        private class EventHandler2 : IEventHandler<MyEvent>
        {
            public void OnEvent(MyEvent @event, long sequence, bool endOfBatch)
            {
                @event.C = @event.B;

                Console.WriteLine($"{nameof(EventHandler2)}=>{@event}");
            }
        }

        private class EventHandler3 : IEventHandler<MyEvent>
        {
            public void OnEvent(MyEvent @event, long sequence, bool endOfBatch)
            {
                @event.D = @event.C;

                Console.WriteLine($"{nameof(EventHandler3)}=>{@event}");
            }
        }

        public static async Task RunAsync()
        {
            var factory = new EventFactory();
            var disruptor = new Disruptor<MyEvent>(factory, 1024, TaskScheduler.Default);

            var handler1 = new EventHandler1();
            var handler2 = new EventHandler2();
            var handler3 = new EventHandler3();
            disruptor.HandleEventsWith(handler1)
                .Then(handler2)
                .Then(handler3);

            await disruptor.StartAsync();
        }
    }
}
