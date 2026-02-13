// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using Microsoft.Data.SqlClient.Connection;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiContextProvider.xml' path='docs/members[@name="SspiContextProvider"]/SspiContextProvider/*'/>
    public abstract class SspiContextProvider
    {
        private TdsParser _parser = null!;
        private ServerInfo _serverInfo = null!;

        private SspiAuthenticationParameters? _primaryAuthParams;
        private SspiAuthenticationParameters? _secondaryAuthParams;

        private protected TdsParserStateObject _physicalStateObj = null!;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiContextProvider.xml' path='docs/members[@name="SspiContextProvider"]/ctor/*'/>
        protected SspiContextProvider()
        {
        }

#if NET
        /// <remarks>
        /// <see cref="ManagedSni.ResolvedServerSpn"/> for details as to what <paramref name="primaryServerSpn"/> and <paramref name="secondaryServerSpn"/> means and why there are two.
        /// </remarks>
#endif
        internal void Initialize(
            ServerInfo serverInfo,
            TdsParserStateObject physicalStateObj,
            TdsParser parser,
            string primaryServerSpn,
            string? secondaryServerSpn = null
            )
        {
            _parser = parser;
            _physicalStateObj = physicalStateObj;
            _serverInfo = serverInfo;

            var options = parser.Connection.ConnectionOptions;

            SqlClientEventSource.Log.StateDumpEvent("<SspiContextProvider> Initializing provider {0} with SPN={1} and alternate={2}", GetType().FullName, primaryServerSpn, secondaryServerSpn);

            _primaryAuthParams = CreateAuthParams(options, primaryServerSpn);

            if (secondaryServerSpn is { })
            {
                _secondaryAuthParams = CreateAuthParams(options, secondaryServerSpn);
            }

            Initialize();

            static SspiAuthenticationParameters CreateAuthParams(SqlConnectionString connString, string serverSpn) => new(connString.DataSource, serverSpn)
            {
                DatabaseName = connString.InitialCatalog,
                UserId = connString.UserID,
                Password = connString.Password,
            };
        }

        private protected virtual void Initialize()
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SspiContextProvider.xml' path='docs/members[@name="SspiContextProvider"]/GenerateContext/*'/>
        protected abstract bool GenerateContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams);

        internal void WriteSSPIContext(ReadOnlySpan<byte> receivedBuff, IBufferWriter<byte> outgoingBlobWriter)
        {
            using var _ = TrySNIEventScope.Create(nameof(SspiContextProvider));

            if (_primaryAuthParams is { })
            {
                if (RunGenerateSspiClientContext(receivedBuff, outgoingBlobWriter, _primaryAuthParams))
                {
                    return;
                }

                // remove _primaryAuth from future attempts as it failed
                _primaryAuthParams = null;
            }

            if (_secondaryAuthParams is { })
            {
                if (RunGenerateSspiClientContext(receivedBuff, outgoingBlobWriter, _secondaryAuthParams))
                {
                    return;
                }

                // remove _secondaryAuthParams from future attempts as it failed
                _secondaryAuthParams = null;
            }

            // If we've hit here, the SSPI context provider implementation failed to generate the SSPI context.
            SSPIError(SQLMessage.SSPIGenerateError(), TdsEnums.GEN_CLIENT_CONTEXT);
        }

        private bool RunGenerateSspiClientContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
        {
            try
            {
                SqlClientEventSource.Log.TryTraceEvent("{0}.{1} | Info | SPN={2}", GetType().FullName, nameof(GenerateContext), authParams.Resource);

                return GenerateContext(incomingBlob, outgoingBlobWriter, authParams);
            }
            catch (Exception e)
            {
                SSPIError(e.Message + Environment.NewLine + e.StackTrace, TdsEnums.GEN_CLIENT_CONTEXT);
                return false;
            }
        }

        private protected void SSPIError(string error, string procedure)
        {
            Debug.Assert(!string.IsNullOrEmpty(procedure), "TdsParser.SSPIError called with an empty or null procedure string");
            Debug.Assert(!string.IsNullOrEmpty(error), "TdsParser.SSPIError called with an empty or null error string");

            _physicalStateObj.AddError(new SqlError(0, (byte)0x00, (byte)TdsEnums.MIN_ERROR_CLASS, _serverInfo.ResolvedServerName, error, procedure, 0));
            _parser.ThrowExceptionAndWarning(_physicalStateObj);
        }
    }
}
