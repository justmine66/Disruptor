using System;
using System.Threading;

namespace Disruptor.ConsoleTest
{
    public class BarrierTest
    {
        private static Barrier _sync;
        private static CancellationToken _token;

        public static void Test()
        {
            var source = new CancellationTokenSource();
            _token = source.Token;
            _sync = new Barrier(3);

            var charlie = new Thread(() => DriveToBoston("Charlie", TimeSpan.FromSeconds(1)));
            var mac = new Thread(() => DriveToBoston("Mac", TimeSpan.FromSeconds(2)));
            var dennis = new Thread(() => DriveToBoston("Dennis", TimeSpan.FromSeconds(3)));

            mac.Start();
            charlie.Start();
            dennis.Start();
            //source.Cancel(); 
            charlie.Join();
            mac.Join();
            dennis.Join();
        }

        static void DriveToBoston(string name, TimeSpan timeToGasStation)
        {
            try
            {
                Console.WriteLine("[{0}] Leaving House", name);
                // Perform some work 
                Thread.Sleep(timeToGasStation);
                Console.WriteLine("[{0}] Arrived at Gas Station", name);
                // Need to sync here 
                _sync.SignalAndWait(_token);
                // Perform some more work 
                Console.WriteLine("[{0}] Leaving for Boston", name);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[{0}] Caravan was cancelled! Going home!", name);
            }
        }
    }
}
