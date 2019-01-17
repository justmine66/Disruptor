using System;
using System.Threading;

namespace Disruptor.ConsoleTest.Example
{
    public class NamedEventHandler<T> : IEventHandler<T>, ILifecycleAware
    {
        private string _oldName;
        private readonly string _name;

        public NamedEventHandler(string name)
        {
            _name = name;
        }

        public void OnEvent(T @event, long sequence, bool endOfBatch)
        {
            
        }

        public void OnShutdown()
        {
            var currentThread = Thread.CurrentThread;
            currentThread.Name = _oldName;
        }

        public void OnStart()
        {
            var currentThread = Thread.CurrentThread;
            _oldName = currentThread.Name;
            currentThread.Name = _name;
        }
    }
}
