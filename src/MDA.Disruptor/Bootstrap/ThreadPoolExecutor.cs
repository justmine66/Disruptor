using System.Threading.Tasks;

namespace MDA.Disruptor.Bootstrap
{
    public class ThreadPoolExecutor : IExecutor
    {
        public async Task ExecuteAsync(IRunnable command)
        {
            await Task.Factory.StartNew(command.Run);
        }
    }
}
