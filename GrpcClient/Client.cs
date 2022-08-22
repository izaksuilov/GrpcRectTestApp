using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace GrpcClient
{
    public class Client
    {
        private GrpcChannel? _grpcChannel;
        private Greeter.GreeterClient? _client;
        public void Connect()
        {
            _grpcChannel?.Dispose();
            _grpcChannel = GrpcChannel.ForAddress("http://localhost:5242");
            _client = new Greeter.GreeterClient(_grpcChannel);
        }
        public bool Start() => _client?.Start(new Empty()).OperationSucess ?? false;
        public bool Stop() => _client?.Stop(new Empty()).OperationSucess ?? false;
        public Size GetFieldSize() => _client?.GetFieldSize(new Empty()) ?? new Size();

        public async IAsyncEnumerable<Rectangle> GetArrayRectAsync([EnumeratorCancellation] CancellationToken token)
        {
            if (_client == null)
                yield break;
            CancellationToken _token = new CancellationToken();
            using (var call = _client.GetArrayRect(new Empty()))
            {
                while (await call.ResponseStream.MoveNext(_token).ConfigureAwait(false))//не смог разобраться
                    yield return call.ResponseStream.Current;
            }
        }
    }
}
