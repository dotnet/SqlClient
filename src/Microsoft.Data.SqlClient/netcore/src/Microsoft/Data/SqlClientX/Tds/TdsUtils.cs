// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;

namespace Microsoft.Data.SqlClientX.Tds
{
    internal class TdsUtils
    {
        public static bool IsNull(MetaType mt, ulong length)
        {
            // null bin and char types have a length of -1 to represent null
            if (mt.IsPlp)
            {
                return (TdsEnums.SQL_PLP_NULL == length);
            }

            // HOTFIX #50000415: for image/text, 0xFFFF is the length, not representing null
            if ((TdsEnums.VARNULL == length) && !mt.IsLong)
            {
                return true;
            }

            // other types have a length of 0 to represent null
            // long and non-PLP types will always return false because these types are either char or binary
            // this is expected since for long and non-plp types isnull is checked based on textptr field and not the length
            return ((TdsEnums.FIXEDNULL == length) && !mt.IsCharType && !mt.IsBinType);
        }

        public static bool IsValidAttestationProtocol(SqlConnectionAttestationProtocol attestationProtocol, string enclaveType)
        {
            switch (enclaveType.ToUpper())
            {
                case TdsEnums.ENCLAVE_TYPE_VBS:
                    if (attestationProtocol != SqlConnectionAttestationProtocol.AAS
                        && attestationProtocol != SqlConnectionAttestationProtocol.HGS
                        && attestationProtocol != SqlConnectionAttestationProtocol.None)
                    {
                        return false;
                    }
                    break;

                case TdsEnums.ENCLAVE_TYPE_SGX:
#if ENCLAVE_SIMULATOR
                    if (attestationProtocol != SqlConnectionAttestationProtocol.AAS
                        && attestationProtocol != SqlConnectionAttestationProtocol.None)
#else
                    if (attestationProtocol != SqlConnectionAttestationProtocol.AAS)
#endif
                    {
                        return false;
                    }
                    break;

#if ENCLAVE_SIMULATOR
                case TdsEnums.ENCLAVE_TYPE_SIMULATOR:
                    if (attestationProtocol != SqlConnectionAttestationProtocol.None)
                    {
                        return false;
                    }
                    break;
#endif
                default:
                    // if we reach here, the enclave type is not supported
                    throw SQL.EnclaveTypeNotSupported(enclaveType);
            }

            return true;
        }

        // Fires a single InfoMessageEvent
        internal static void FireInfoMessageEvent(TdsContext context, SqlError error)
        {
            string serverVersion = null;

            if (context.ParserState == TdsParserState.OpenLoggedIn)
            {
                serverVersion = context.ConnectionState.ServerVersion;
            }

            SqlException exc = SqlException.CreateException(new() { error }, serverVersion, context, innerException: null, batchCommand: null);

            if (context.TdsEventListener != null)
            {
                context.TdsEventListener.OnInfoMessage(new SqlInfoMessageEventArgs(exc), out _);
            }
        }

        internal static void ThrowExceptionAndWarning(TdsContext tdsContext, SqlCommand command = null, bool callerHasConnectionLock = false)
        {
            SqlException exception = null;

            // This function should only be called when there was an error or warning.  If there aren't any
            // errors, the handler will be called for the warning(s).  If there was an error, the warning(s) will
            // be copied to the end of the error collection so that the user may see all the errors and also the
            // warnings that occurred.
            // can be deleted)
            //_errorAndWarningsLock lock is implemented inside GetFullErrorAndWarningCollection
            SqlErrorCollection temp = tdsContext.ErrorWarningsState.GetFullErrorAndWarningCollection(out bool breakConnection);

            Debug.Assert(temp != null, "TdsUtils::ThrowExceptionAndWarning: null errors collection!");
            Debug.Assert(temp.Count > 0, "TdsUtils::ThrowExceptionAndWarning called with no exceptions or warnings!");

            // TODO Implement State management in Tds Parser
            // Don't break the connection if it is already closed
            // breakConnection &= (TdsParserState.Closed != tdsContext.ParserState);

            // TODO Implement BreakConnection workflow

            //if (breakConnection)
            //{
            //    if ((tdsContext.ParserState == TdsParserState.OpenNotLoggedIn) && (tdsContext.ConnectionOptions.MultiSubnetFailover || tdsContext._loginWithFailover) && (temp.Count == 1) && ((temp[0].Number == TdsEnums.TIMEOUT_EXPIRED) || (temp[0].Number == TdsEnums.SNI_WAIT_TIMEOUT)))
            //    {
            //        // For Multisubnet Failover we slice the timeout to make reconnecting faster (with the assumption that the server will not failover instantaneously)
            //        // However, when timeout occurs we need to not doom the internal connection and also to mark the TdsParser as closed such that the login will be will retried
            //        breakConnection = false;
            //        Disconnect();
            //    }
            //    else
            //    {
            //        tdsContext.ParserState = TdsParserState.Broken;
            //    }
            //}

            if (temp != null && temp.Count > 0)
            {
                // Construct the exception now that we've collected all the errors
                string serverVersion = null;
                if (tdsContext.ParserState == TdsParserState.OpenLoggedIn)
                {
                    serverVersion = tdsContext.ConnectionState.ServerVersion;
                }

                if (temp.Count == 1 && temp[0].Exception != null)
                {
                    exception = SqlException.CreateException(temp, serverVersion, tdsContext, temp[0].Exception, command?.GetBatchCommand(temp[0].BatchIndex));
                }
                else
                {
                    SqlBatchCommand batchCommand = null;
                    if (temp[0]?.BatchIndex is var index and >= 0 && command is not null)
                    {
                        batchCommand = command.GetBatchCommand(index.Value);
                    }
                    exception = SqlException.CreateException(temp, serverVersion, tdsContext, innerException: null, batchCommand: batchCommand);
                }
            }

            if (exception != null)
            {
                // TODO Implement BreakConnection workflow
                //if (breakConnection)
                //{
                //    // report exception to pending async operation
                //    // before OnConnectionClosed overrides the exception
                //    // due to connection close notification through references
                //    TaskCompletionSource<object> taskSource = stateObj._networkPacketTaskSource;
                //    if (taskSource != null)
                //    {
                //        taskSource.TrySetException(ADP.ExceptionWithStackTrace(exception));
                //    }
                //}

                // sqlConnector.OnError(exception);

                // TODO consider supporting Async Close if needed.
            }
        }
    }
}
#endif
