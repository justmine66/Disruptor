using Disruptor.Dsl;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Disruptor.Test.Dsl.Stubs
{
    public class StubExecutor : IAsyncExecutor
    {
        private readonly BlockingCollection<Thread> _threads = new BlockingCollection<Thread>();
        private volatile bool _ignoreExecutions;
        private int _executionCount;

        public int ExecutionCount => _executionCount;

        public Task ExecuteAsync(IRunnable command)
        {
            Interlocked.Increment(ref _executionCount);

            if (_ignoreExecutions)
                return Task.CompletedTask;

            var thread = new Thread(() =>
              {
                  try
                  {
                      command.Run();
                  }
                  catch (Exception e)
                  {
                      Console.WriteLine(e);
                  }
              });

            _threads.Add(thread);
            thread.Start();

            return Task.CompletedTask;
        }

        public void JoinAllThreads()
        {
            foreach (var thread in _threads.GetConsumingEnumerable())
            {
                if (!thread.IsAlive) continue;
                // if the thread has not terminated after the amount of time specified.
                if (thread.Join(5000)) continue;
                // double join
                thread.Interrupt();
                Assert.False(thread.Join(5000), $"Failed to stop thread: {thread}");
            }
        }

        public void IgnoreExecutions()
        {
            _ignoreExecutions = true;
        }
    }
}
