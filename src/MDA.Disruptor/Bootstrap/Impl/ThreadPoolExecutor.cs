using System.Threading.Tasks;

namespace MDA.Disruptor.Bootstrap.Impl
{
    public class ThreadPoolExecutor : IExecutor
    {
        public async Task ExecuteAsync(IRunnable command)
        {
            await Task.Factory.StartNew(command.Run);
        }
    }
}
