using System.Threading.Tasks;

namespace MDA.Disruptor.Bootstrap
{
    /// <summary>
    /// Providing threads for running the command.
    /// </summary>
    public interface IExecutor
    {
        /// <summary>
        /// Execute the given commands asynchronously in other thread.
        /// </summary>
        /// <param name="command">needs to be executed.</param>
        /// <returns></returns>
        Task ExecuteAsync(IRunnable command);
    }
}
