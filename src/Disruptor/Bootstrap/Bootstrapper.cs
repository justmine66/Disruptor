using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Disruptor.Bootstrap
{
    public class Bootstrapper
    {
        public static IServiceProvider RegisterServices(
            ServiceCollection services,
            ILoggerFactory loggerFactory)
        {
            return services.AddLogging()
                .BuildServiceProvider();
        }
    }
}
