// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.Tds.State;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange
{
    internal class EnvChangeTokenProcessor : TokenProcessor
    {
        public override void ProcessTokenData(Token token, ref TdsContext tdsContext, RunBehavior runBehavior)
        {
            switch (token)
            {
                case TransactionEnvChangeToken:
                    var transToken = token as TransactionEnvChangeToken;
                    // When we get notification from the server of a new
                    // transaction, we move any pending transaction over to
                    // the current transaction, then we store the token in it.
                    // if there isn't a pending transaction, then it's either
                    // a TSQL transaction or a distributed transaction.
                    Debug.Assert(null == tdsContext.TransactionState.CurrentTransaction, "non-null current transaction with an ENV Change");
                    tdsContext.TransactionState.UpdateCurrentTransaction(tdsContext.TransactionState.PendingTransaction);
                    tdsContext.TransactionState.UpdatePendingTransaction(null);

                    if (null != tdsContext.TransactionState.CurrentTransaction)
                    {
                        tdsContext.TransactionState.CurrentTransaction.TransactionId = transToken.NewValue.ReadInt64LE();   // this is defined as a ULongLong in the server and in the TDS Spec.
                    }
                    else
                    {
                        TransactionType transactionType = (EnvChangeTokenSubType.BeginTransaction == transToken.SubType)
                            ? TransactionType.LocalFromTSQL
                            : TransactionType.Distributed;
                        // TODO Initialize SqlInternalTransaction
                        // _tdsContext.TdsTransactionState.UpdateCurrentTransaction(new SqlInternalTransaction(_connHandler, transactionType, null, env._newLongValue));
                    }
                    //if (null != _statistics && !_statisticsIsInTransaction)
                    //{
                    //    _statistics.SafeIncrement(ref _statistics._transactions);
                    //}
                    // _statisticsIsInTransaction = true;
                    tdsContext.TransactionState._retainedTransactionId = SqlInternalTransaction.NullTransactionId;
                    // When we get notification of a completed transaction
                    // we null out the current transaction.
                    if (null != tdsContext.TransactionState.CurrentTransaction)
                    {
#if DEBUG
                        // Check null for case where Begin and Rollback obtained in the same message.
                        if (SqlInternalTransaction.NullTransactionId != tdsContext.TransactionState.CurrentTransaction.TransactionId)
                        {
                            Debug.Assert(tdsContext.TransactionState.CurrentTransaction.TransactionId != transToken.NewValue.ReadInt64LE(), "transaction id's are not equal!");
                        }
#endif

                        if (EnvChangeTokenSubType.CommitTransaction == transToken.SubType)
                        {
                            tdsContext.TransactionState.CurrentTransaction.Completed(TransactionState.Committed);
                        }
                        else if (EnvChangeTokenSubType.RollbackTransaction == transToken.SubType)
                        {
                            //  Hold onto transaction id if distributed tran is rolled back.  This must
                            //  be sent to the server on subsequent executions even though the transaction
                            //  is considered to be rolled back.
                            if (tdsContext.TransactionState.CurrentTransaction.IsDistributed && tdsContext.TransactionState.CurrentTransaction.IsActive)
                            {
                                tdsContext.TransactionState._retainedTransactionId = transToken.OldValue.ReadInt64LE();
                            }
                            tdsContext.TransactionState.CurrentTransaction.Completed(TransactionState.Aborted);
                        }
                        else
                        {
                            tdsContext.TransactionState.CurrentTransaction.Completed(TransactionState.Unknown);
                        }
                        tdsContext.TransactionState.UpdateCurrentTransaction(null);
                    }
                    // _statisticsIsInTransaction = false;
                    break;

                case DatabaseEnvChangeToken:
                    var databaseToken = token as DatabaseEnvChangeToken;
                    tdsContext.ConnectionState.CurrentDatabase = databaseToken.NewValue;
                    break;

                case LanguageEnvChangeToken:
                    var languageToken = token as LanguageEnvChangeToken;
                    tdsContext.ConnectionState.CurrentLanguage = languageToken.NewValue;
                    break;

                case PacketSizeEnvChangeToken:
                    var packetSizeToken = token as PacketSizeEnvChangeToken;
                    tdsContext.ConnectionState.CurrentPacketSize = packetSizeToken.NewValue;
                    break;

                case DatabaseMirroringPartnerEnvChangeToken:
                    var dbMirrorToken = token as DatabaseMirroringPartnerEnvChangeToken;
                    //if (ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly)
                    //{
                    //    throw SQL.ROR_FailoverNotSupportedServer(this);
                    //}
                    tdsContext.ConnectionState.CurrentFailoverPartner = dbMirrorToken.NewValue;
                    break;

                case PromoteTransactionEnvChangeToken:
                    var promoteDtcToken = token as PromoteTransactionEnvChangeToken;
                    // Copy Promote dtc token value to Tds Context.
                    promoteDtcToken.NewValue.CopyTo(tdsContext.TransactionState.PromotedDtcToken, 0, promoteDtcToken.NewValue.Length);
                    break;
                case EnvChangeToken<string>:
                    var uiToken = token as EnvChangeToken<string>;
                    if (uiToken.SubType == EnvChangeTokenSubType.UserInstanceName)
                    {
                        tdsContext.ConnectionState.UserInstanceName = uiToken.NewValue;
                    }
                    break;
                case RoutingEnvChangeToken:
                    var routingToken = token as RoutingEnvChangeToken;
                    // SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnEnvChange|ADV> {0}, Received routing info", ObjectID);
                    if (string.IsNullOrEmpty(routingToken.NewValue.Server) || routingToken.NewValue.Protocol != 0 || routingToken.NewValue.Port == 0)
                    {
                        // throw SQL.ROR_InvalidRoutingInfo(this);
                    }
                    tdsContext.ConnectionState.RoutingInfo = routingToken.NewValue;
                    break;
                default:
                    // Debug.Fail("Missed token in EnvChange!");
                    break;
            }
        }
    }
}
#endif
