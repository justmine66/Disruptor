namespace MDA.Disruptor.DSL
{
    public interface IConsumerInfo
    {
        ISequence[] GetSequences();
        ISequenceBarrier GetBarrier();
        bool IsEndOfChain();
        void Start(IExecutor executor);
        void Halt();
        void MarkAsUsedInBarrier();
        bool IsRunning();
    }
}
