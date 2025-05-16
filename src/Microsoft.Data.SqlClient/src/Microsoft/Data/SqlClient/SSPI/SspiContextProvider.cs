using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    internal abstract class SspiContextProvider
    {
        private TdsParser _parser = null!;
        private ServerInfo _serverInfo = null!;

        private List<SspiAuthenticationParameters>? _authParams;
        private SspiAuthenticationParameters? _authParam;

        private protected TdsParserStateObject _physicalStateObj = null!;

        internal void Initialize(
            ServerInfo serverInfo,
            TdsParserStateObject physicalStateObj,
            TdsParser parser,
#if NETFRAMEWORK
            string serverSpn
#else
            string[] serverSpns
#endif
            )
        {
            _parser = parser;
            _physicalStateObj = physicalStateObj;
            _serverInfo = serverInfo;

#if NETFRAMEWORK
            _authParam = CreateAuthParams(serverSpn);
#else
            _authParams = [.. serverSpns.Select(CreateAuthParams)];
#endif
            Initialize();
        }

        private protected virtual void Initialize()
        {
        }

        protected abstract bool GenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams);

        internal void WriteSSPIContext(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter)
        {
            using var _ = TrySNIEventScope.Create(nameof(SspiContextProvider));

            if (TryRunSingle(receivedBuff, outgoingBlobWriter) || TryRunMultiple(receivedBuff, outgoingBlobWriter))
            {
                // If we've hit here, the SSPI context provider implementation failed to generate the SSPI context.
                SSPIError(SQLMessage.SSPIGenerateError(), TdsEnums.GEN_CLIENT_CONTEXT);
            }
        }

        /// <summary>
        /// If we only have a single auth param, we know it's the correct one to use.
        /// </summary>
        private bool TryRunSingle(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter)
        {
            return _authParam is { } && RunGenerateSspiClientContext(receivedBuff, outgoingBlobWriter, _authParam);
        }

        /// <summary>
        /// If we have multiple, we need to loop through them, and then identify the correct one for future use.
        /// </summary>
        private bool TryRunMultiple(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter)
        {
            if (_authParams is { })
            {
                foreach (var authParam in _authParams)
                {
                    if (RunGenerateSspiClientContext(receivedBuff, outgoingBlobWriter, authParam))
                    {
                        // Reset the _authParams to only have a single one going forward to always call the context with that one
                        _authParam = authParam;
                        _authParams = null;
                        return true;
                    }
                }
            }

            return false;
        }

        private SspiAuthenticationParameters CreateAuthParams(string serverSpn)
        {
            var options = _parser.Connection.ConnectionOptions;

            return new SspiAuthenticationParameters(options.DataSource, serverSpn)
            {
                DatabaseName = options.InitialCatalog,
                UserId = options.UserID,
                Password = options.Password,
            };
        }

        private bool RunGenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | SPN={1}", GetType().FullName, nameof(GenerateSspiClientContext), authParams.Resource);

                return GenerateSspiClientContext(incomingBlob, outgoingBlobWriter, authParams);
            }
            catch (Exception e)
            {
                SSPIError(e.Message + Environment.NewLine + e.StackTrace, TdsEnums.GEN_CLIENT_CONTEXT);
                return false;
            }
        }

        protected void SSPIError(string error, string procedure)
        {
            Debug.Assert(!string.IsNullOrEmpty(procedure), "TdsParser.SSPIError called with an empty or null procedure string");
            Debug.Assert(!string.IsNullOrEmpty(error), "TdsParser.SSPIError called with an empty or null error string");

            _physicalStateObj.AddError(new SqlError(0, (byte)0x00, (byte)TdsEnums.MIN_ERROR_CLASS, _serverInfo.ResolvedServerName, error, procedure, 0));
            _parser.ThrowExceptionAndWarning(_physicalStateObj);
        }
    }
}
