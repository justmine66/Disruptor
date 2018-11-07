using Xunit;

namespace MDA.Disruptor.Test.Support.DependencyInjection
{
    [CollectionDefinition("ObjectContainerCollection")]
    public class ObjectContainerCollection : ICollectionFixture<ObjectContainerFixture>
    {
    }
}
