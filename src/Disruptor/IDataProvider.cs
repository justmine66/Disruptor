namespace Disruptor
{
    public interface IDataProvider<out T>
    {
        T Get(long sequence);
    }
}