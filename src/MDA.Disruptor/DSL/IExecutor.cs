using System.Threading.Tasks;

namespace MDA.Disruptor.DSL
{
    public interface IExecutor
    {
        Task ExecuteAsync(IRunnable command);
        void Execute(IRunnable command);
    }
}
