using System.Threading.Tasks;

namespace Disruptor.Dsl
{
    public interface IConsumerInfo
    {
        ISequence[] GetSequences();
        ISequenceBarrier GetBarrier();
        bool IsEndOfChain();
        Task StartAsync(IAsyncExecutor executor);
        void Halt();
        void MarkAsUsedInBarrier();
        bool IsRunning();
    }
}
