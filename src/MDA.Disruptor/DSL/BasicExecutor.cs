using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MDA.Disruptor.DSL
{
    public class BasicExecutor : IExecutor
    {
        private readonly TaskScheduler _scheduler;

        private BlockingCollection<Thread> _threads;

        public BasicExecutor(TaskScheduler scheduler)
        {
            _scheduler = scheduler;
            Initialize();
        }

        private void Initialize()
        {
            _threads = new BlockingCollection<Thread>();
        }

        public Task ExecuteAsync(IRunnable command)
        {
            return Task.Factory.StartNew(() => Execute(command),
                CancellationToken.None,
                TaskCreationOptions.LongRunning, _scheduler);
        }

        private void Execute(IRunnable command)
        {
            try
            {
                command.Run();
            }
            finally
            {
                _threads.Add(Thread.CurrentThread);
            }
        }
    }
}
