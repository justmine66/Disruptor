using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MDA.Disruptor.Bootstrap.Impl
{
    public class NewSingleThreadExecutor : IExecutor
    {
        private readonly TaskScheduler _scheduler;

        private readonly BlockingCollection<Thread> _threads;

        public NewSingleThreadExecutor(TaskScheduler scheduler)
        {
            _scheduler = scheduler;
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
            var workerThread = Thread.CurrentThread;
            _threads.Add(workerThread);

            try
            {
                command.Run();
            }
            finally
            {
                _threads.Take();
            }
        }

        public override string ToString()
        {
            return "BasicExecutor{" +
                   "threads=" + DumpThreadInfo() +
                   '}';
        }

        private string DumpThreadInfo()
        {
            var sb = new StringBuilder();
            foreach (var t in _threads)
            {
                sb.Append("{");
                sb.Append("name=").Append(t.Name).Append(",");
                sb.Append("id=").Append(t.ManagedThreadId).Append(",");
                sb.Append("state=").Append(t.ThreadState);
                sb.Append("}");
            }

            return sb.ToString();
        }
    }
}
