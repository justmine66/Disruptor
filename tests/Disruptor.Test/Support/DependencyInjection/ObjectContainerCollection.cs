using Xunit;

namespace Disruptor.Test.Support.DependencyInjection
{
    [CollectionDefinition("ObjectContainerCollection")]
    public class ObjectContainerCollection : ICollectionFixture<ObjectContainerFixture>
    {
    }
}
