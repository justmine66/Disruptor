using Disruptor.Exceptions;
using Disruptor.Impl;
using System.Threading;

namespace Disruptor.Test.Support
{
    public class DummyEventProcessor : IEventProcessor
    {
        private readonly ISequence _sequence;

        private int _running = 0;

        public DummyEventProcessor(ISequence sequence)
        {
            _sequence = sequence;
        }

        public DummyEventProcessor()
        {
            _sequence = new Sequence();
        }

        public void Run()
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) == 1)
            {
                throw new IllegalStateException("Already running");
            }
        }

        public ISequence GetSequence()
        {
            return _sequence;
        }

        public void Halt()
        {
            Interlocked.Exchange(ref _running, 0);
        }

        public bool IsRunning()
        {
            return _running == 1;
        }
    }
}
