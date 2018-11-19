using System.Threading.Tasks;

namespace Disruptor.Bootstrap
{
    public interface IConsumerInfo
    {
        ISequence[] GetSequences();
        ISequenceBarrier GetBarrier();
        bool IsEndOfChain();
        Task StartAsync(IExecutor executor);
        void Halt();
        void MarkAsUsedInBarrier();
        bool IsRunning();
    }
}
