using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.IO;


namespace Microsoft.Data.SqlClientX.Handlers
{
    internal interface IHandler<TRequest> where TRequest : HandlerRequest
    {
        public abstract ValueTask Handle(TRequest request, bool isAsync, CancellationToken ct);

        public void SetNext(IHandler<TRequest> handler);
    }

    internal abstract class Handler<TRequest> : IHandler<TRequest> where TRequest : HandlerRequest
    {
        private IHandler<TRequest> _nextHandler;

        public IHandler<TRequest> NextHandler { get => _nextHandler; set => _nextHandler = value; }

        public abstract ValueTask Handle(TRequest request, bool isAsync, CancellationToken ct);

        public virtual void SetNext(IHandler<TRequest> handler)
        {
            NextHandler = handler;
        }
    }

    internal enum HandlerRequestType
    {
        ConnectionRequest
    }

    internal abstract class HandlerRequest
    {
        public HandlerRequestType RequestType { get; internal set; }
        public Exception Exception { get; set; }

    }

    internal class ConnectionRequest : HandlerRequest
    {
        public string ConnectionString { get; set; }

        public SqlConnectionStringBuilder ConnectionStringBuilder { get; internal set; }
        public DataSource DataSource { get; internal set; }
        public Stream TransportStream { get; internal set; }
        public TdsStream TdsReadStream { get; internal set; }

        public ConnectionRequest(string connectionString)
        {
            RequestType = HandlerRequestType.ConnectionRequest;
            ConnectionString = connectionString;
            ConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        }
    }
}
