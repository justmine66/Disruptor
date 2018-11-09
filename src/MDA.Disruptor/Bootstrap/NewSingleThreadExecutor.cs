using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MDA.Disruptor.Bootstrap
{
    public class NewSingleThreadExecutor : IExecutor
    {
        private readonly TaskScheduler _scheduler;

        private BlockingCollection<Thread> _threads;

        public NewSingleThreadExecutor(TaskScheduler scheduler)
        {
            _scheduler = scheduler;
            Initialize();
        }

        private void Initialize()
        {
            _threads = new BlockingCollection<Thread>();
        }

        public async Task ExecuteAsync(IRunnable command)
        {
            await Task.Factory.StartNew(() => Execute(command),
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
