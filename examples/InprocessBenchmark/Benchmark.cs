using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using Proto;
using Proto.Mailbox;

namespace InprocessBenchmark
{
    [MemoryDiagnoser]
    [CoreJob]
    public class Benchmark
    {
        private readonly int _clientCount = Environment.ProcessorCount * 2;
        private const int BatchSize = 100;
        
        [Params(300, 700, 900)]
        public int Tps { get; set; }

        [Params(1000, 100000,1000000)]
        public int MessageCount { get; set; }

        [Benchmark]
        public void InprocessBenchmark()
        {
            var d = new ThreadPoolDispatcher {Throughput = Tps};
            
            var clients = new PID[_clientCount];
            var echos = new PID[_clientCount];
            var completions = new TaskCompletionSource<bool>[_clientCount];


            var echoProps = Actor.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Program.Msg msg:
                        msg.Sender.Tell(msg);
                        break;
                }
                return Actor.Done;
            }).WithDispatcher(d);

            for (var i = 0; i < _clientCount; i++)
            {
                var tsc = new TaskCompletionSource<bool>();
                completions[i] = tsc;
                var clientProps = Actor.FromProducer(() => new Program.PingActor(tsc, MessageCount, BatchSize))
                    .WithDispatcher(d);

                clients[i] = Actor.Spawn(clientProps);
                echos[i] = Actor.Spawn(echoProps);
            }
            var tasks = completions.Select(tsc => tsc.Task).ToArray();

            for (var i = 0; i < _clientCount; i++)
            {
                var client = clients[i];
                var echo = echos[i];

                client.Tell(new Program.Start(echo));
            }
            Task.WaitAll(tasks);

        }
    }
}
