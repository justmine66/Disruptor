namespace MDA.Disruptor.Test.Support
{
    public interface ICallable<TResult>
    {
        TResult Call();
    }
}
