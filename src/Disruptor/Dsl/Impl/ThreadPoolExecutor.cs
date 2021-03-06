﻿using System.Threading.Tasks;

namespace Disruptor.Dsl.Impl
{
    public class ThreadPoolExecutor : IAsyncExecutor
    {
        public async Task ExecuteAsync(IRunnable command)
        {
            await Task.Factory.StartNew(command.Run);
        }
    }
}
