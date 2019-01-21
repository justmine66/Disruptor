using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Dsl.Impl
{
    public class BasicExecutor : IAsyncExecutor
    {
        private readonly TaskScheduler _scheduler;

        private readonly ImmutableList<Thread> _threads;

        public BasicExecutor(TaskScheduler scheduler = default(TaskScheduler))
        {
            _scheduler = scheduler ?? TaskScheduler.Default;
            _threads = ImmutableList.Create<Thread>();
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
                _threads.Remove(workerThread);
            }
        }

        public override string ToString()
        {
            return $"{{threads:{DumpThreadInfo()}}}";
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
                sb.Append("},");
            }

            var output = sb.ToString();

            return $"[{output.Substring(0, output.Length - 1)}]";
        }
    }
}
