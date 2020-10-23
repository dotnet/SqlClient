// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysTx = System.Transactions;

namespace Microsoft.Data.SqlClient
{
    using Microsoft.Data.Common;
    static internal class AsyncHelper
    {
        internal static Task CreateContinuationTask(Task task, Action onSuccess, SqlInternalConnectionTds connectionToDoom = null, Action<Exception> onFailure = null)
        {
            if (task == null)
            {
                onSuccess();
                return null;
            }
            else
            {
                TaskCompletionSource<object> completion = new TaskCompletionSource<object>();
                ContinueTask(task, completion,
                    () => { onSuccess(); completion.SetResult(null); },
                    connectionToDoom, onFailure);
                return completion.Task;
            }
        }

        internal static Task CreateContinuationTask<T1, T2>(Task task, Action<T1, T2> onSuccess, T1 arg1, T2 arg2, SqlInternalConnectionTds connectionToDoom = null, Action<Exception> onFailure = null)
        {
            return CreateContinuationTask(task, () => onSuccess(arg1, arg2), connectionToDoom, onFailure);
        }

        internal static void ContinueTask(Task task,
                TaskCompletionSource<object> completion,
                Action onSuccess,
                SqlInternalConnectionTds connectionToDoom = null,
                Action<Exception> onFailure = null,
                Action onCancellation = null,
                Func<Exception, Exception> exceptionConverter = null,
                SqlConnection connectionToAbort = null
            )
        {
            Debug.Assert((connectionToAbort == null) || (connectionToDoom == null), "Should not specify both connectionToDoom and connectionToAbort");
            task.ContinueWith(
                tsk =>
                {
                    if (tsk.Exception != null)
                    {
                        Exception exc = tsk.Exception.InnerException;
                        if (exceptionConverter != null)
                        {
                            exc = exceptionConverter(exc);
                        }
                        try
                        {
                            if (onFailure != null)
                            {
                                onFailure(exc);
                            }
                        }
                        finally
                        {
                            completion.TrySetException(exc);
                        }
                    }
                    else if (tsk.IsCanceled)
                    {
                        try
                        {
                            if (onCancellation != null)
                            {
                                onCancellation();
                            }
                        }
                        finally
                        {
                            completion.TrySetCanceled();
                        }
                    }
                    else
                    {
                        if (connectionToDoom != null || connectionToAbort != null)
                        {
                            RuntimeHelpers.PrepareConstrainedRegions();
                            try
                            {
#if DEBUG
                                TdsParser.ReliabilitySection tdsReliabilitySection = new TdsParser.ReliabilitySection();
                                RuntimeHelpers.PrepareConstrainedRegions();
                                try {
                                    tdsReliabilitySection.Start();
#endif //DEBUG
                                onSuccess();
#if DEBUG
                                }
                                finally {
                                    tdsReliabilitySection.Stop();
                                }
#endif //DEBUG
                            }
                            catch (System.OutOfMemoryException e)
                            {
                                if (connectionToDoom != null)
                                {
                                    connectionToDoom.DoomThisConnection();
                                }
                                else
                                {
                                    connectionToAbort.Abort(e);
                                }
                                completion.SetException(e);
                                throw;
                            }
                            catch (System.StackOverflowException e)
                            {
                                if (connectionToDoom != null)
                                {
                                    connectionToDoom.DoomThisConnection();
                                }
                                else
                                {
                                    connectionToAbort.Abort(e);
                                }
                                completion.SetException(e);
                                throw;
                            }
                            catch (System.Threading.ThreadAbortException e)
                            {
                                if (connectionToDoom != null)
                                {
                                    connectionToDoom.DoomThisConnection();
                                }
                                else
                                {
                                    connectionToAbort.Abort(e);
                                }
                                completion.SetException(e);
                                throw;
                            }
                            catch (Exception e)
                            {
                                completion.SetException(e);
                            }
                        }
                        else
                        { // no connection to doom - reliability section not required
                            try
                            {
                                onSuccess();
                            }
                            catch (Exception e)
                            {
                                completion.SetException(e);
                            }
                        }
                    }
                }, TaskScheduler.Default
            );
        }


        internal static void WaitForCompletion(Task task, int timeout, Action onTimeout = null, bool rethrowExceptions = true)
        {
            try
            {
                task.Wait(timeout > 0 ? (1000 * timeout) : Timeout.Infinite);
            }
            catch (AggregateException ae)
            {
                if (rethrowExceptions)
                {
                    Debug.Assert(ae.InnerExceptions.Count == 1, "There is more than one exception in AggregateException");
                    ExceptionDispatchInfo.Capture(ae.InnerException).Throw();
                }
            }
            if (!task.IsCompleted)
            {
                task.ContinueWith(t => { var ignored = t.Exception; }); //Ensure the task does not leave an unobserved exception
                if (onTimeout != null)
                {
                    onTimeout();
                }
            }
        }

        internal static void SetTimeoutException(TaskCompletionSource<object> completion, int timeout, Func<Exception> exc, CancellationToken ctoken)
        {
            if (timeout > 0)
            {
                Task.Delay(timeout * 1000, ctoken).ContinueWith((tsk) =>
                {
                    if (!tsk.IsCanceled && !completion.Task.IsCompleted)
                    {
                        completion.TrySetException(exc());
                    }
                });
            }
        }
    }

    sealed internal class InOutOfProcHelper
    {
        private static readonly InOutOfProcHelper SingletonInstance = new InOutOfProcHelper();

        private bool _inProc = false;

        // InOutOfProcHelper detects whether it's running inside the server or not.  It does this
        //  by checking for the existence of a well-known function export on the current process.
        //  Note that calling conventions, etc. do not matter -- we'll never call the function, so
        //  only the name match or lack thereof matter.
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        private InOutOfProcHelper()
        {
            // Don't need to close this handle...
            // SxS: we use this method to check if we are running inside the SQL Server process. This call should be safe in SxS environment.
            IntPtr handle = SafeNativeMethods.GetModuleHandle(null);
            if (IntPtr.Zero != handle)
            {
                // SQLBU 359301: Currently, the server exports different names for x86 vs. AMD64 and IA64.  Supporting both names
                //  for now gives the server time to unify names across platforms without breaking currently-working ones.
                //  We can remove the obsolete name once the server is changed.
                if (IntPtr.Zero != SafeNativeMethods.GetProcAddress(handle, "_______SQL______Process______Available@0"))
                {
                    _inProc = true;
                }
                else if (IntPtr.Zero != SafeNativeMethods.GetProcAddress(handle, "______SQL______Process______Available"))
                {
                    _inProc = true;
                }
            }
        }

        internal static bool InProc
        {
            get
            {
                return SingletonInstance._inProc;
            }
        }
    }

    static internal class SQL
    {
        // The class SQL defines the exceptions that are specific to the SQL Adapter.
        // The class contains functions that take the proper informational variables and then construct
        // the appropriate exception with an error string obtained from the resource Framework.txt.
        // The exception is then returned to the caller, so that the caller may then throw from its
        // location so that the catcher of the exception will have the appropriate call stack.
        // This class is used so that there will be compile time checking of error
        // messages.  The resource Framework.txt will ensure proper string text based on the appropriate
        // locale.

        //
        // SQL specific exceptions
        //

        //
        // SQL.Connection
        //

        static internal Exception CannotGetDTCAddress()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_CannotGetDTCAddress));
        }

        static internal Exception InvalidOptionLength(string key)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InvalidOptionLength, key));
        }
        static internal Exception InvalidInternalPacketSize(string str)
        {
            return ADP.ArgumentOutOfRange(str);
        }
        static internal Exception InvalidPacketSize()
        {
            return ADP.ArgumentOutOfRange(StringsHelper.GetString(Strings.SQL_InvalidTDSPacketSize));
        }
        static internal Exception InvalidPacketSizeValue()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InvalidPacketSizeValue));
        }
        static internal Exception InvalidSSPIPacketSize()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InvalidSSPIPacketSize));
        }
        static internal Exception NullEmptyTransactionName()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_NullEmptyTransactionName));
        }
        static internal Exception SnapshotNotSupported(System.Data.IsolationLevel level)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_SnapshotNotSupported, typeof(IsolationLevel), level.ToString()));
        }
        static internal Exception UserInstanceFailoverNotCompatible()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_UserInstanceFailoverNotCompatible));
        }
        static internal Exception CredentialsNotProvided(SqlAuthenticationMethod auth)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_CredentialsNotProvided, DbConnectionStringBuilderUtil.AuthenticationTypeToString(auth)));
        }
        static internal Exception InvalidCertAuth()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_Certificate));
        }
        static internal Exception AuthenticationAndIntegratedSecurity()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_AuthenticationAndIntegratedSecurity));
        }
        static internal Exception IntegratedWithUserIDAndPassword()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_IntegratedWithUserIDAndPassword));
        }
        static internal Exception InteractiveWithPassword()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InteractiveWithPassword));
        }
        static internal Exception DeviceFlowWithUsernamePassword()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_DeviceFlowWithUsernamePassword));
        }
        static internal Exception ManagedIdentityWithPassword(string authenticationMode)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_ManagedIdentityWithPassword, authenticationMode));
        }
        static internal Exception SettingIntegratedWithCredential()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingIntegratedWithCredential));
        }
        static internal Exception SettingInteractiveWithCredential()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingInteractiveWithCredential));
        }
        static internal Exception SettingDeviceFlowWithCredential()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingDeviceFlowWithCredential));
        }
        static internal Exception SettingManagedIdentityWithCredential(string authenticationMode)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingManagedIdentityWithCredential, authenticationMode));
        }
        static internal Exception SettingCredentialWithIntegratedArgument()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_SettingCredentialWithIntegrated));
        }
        static internal Exception SettingCredentialWithInteractiveArgument()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_SettingCredentialWithInteractive));
        }
        static internal Exception SettingCredentialWithDeviceFlowArgument()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_SettingCredentialWithDeviceFlow));
        }
        static internal Exception SettingCredentialWithManagedIdentityArgument(string authenticationMode)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_SettingCredentialWithManagedIdentity, authenticationMode));
        }
        static internal Exception SettingCredentialWithIntegratedInvalid()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingCredentialWithIntegrated));
        }
        static internal Exception SettingCredentialWithInteractiveInvalid()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingCredentialWithInteractive));
        }
        static internal Exception SettingCredentialWithDeviceFlowInvalid()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingCredentialWithDeviceFlow));
        }
        static internal Exception SettingCredentialWithManagedIdentityInvalid(string authenticationMode)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SettingCredentialWithManagedIdentity, authenticationMode));
        }
        static internal Exception InvalidSQLServerVersionUnknown()
        {
            return ADP.DataAdapter(StringsHelper.GetString(Strings.SQL_InvalidSQLServerVersionUnknown));
        }
        static internal Exception SynchronousCallMayNotPend()
        {
            return new Exception(StringsHelper.GetString(Strings.Sql_InternalError));
        }
        static internal Exception ConnectionLockedForBcpEvent()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ConnectionLockedForBcpEvent));
        }
        static internal Exception AsyncConnectionRequired()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_AsyncConnectionRequired));
        }
        static internal Exception FatalTimeout()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_FatalTimeout));
        }
        static internal Exception InstanceFailure()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_InstanceFailure));
        }
        static internal Exception ChangePasswordArgumentMissing(string argumentName)
        {
            return ADP.ArgumentNull(StringsHelper.GetString(Strings.SQL_ChangePasswordArgumentMissing, argumentName));
        }
        static internal Exception ChangePasswordConflictsWithSSPI()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_ChangePasswordConflictsWithSSPI));
        }
        static internal Exception ChangePasswordRequiresYukon()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ChangePasswordRequiresYukon));
        }
        static internal Exception UnknownSysTxIsolationLevel(SysTx.IsolationLevel isolationLevel)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_UnknownSysTxIsolationLevel, isolationLevel.ToString()));
        }
        static internal Exception ChangePasswordUseOfUnallowedKey(string key)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ChangePasswordUseOfUnallowedKey, key));
        }
        static internal Exception InvalidPartnerConfiguration(string server, string database)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_InvalidPartnerConfiguration, server, database));
        }
        static internal Exception BatchedUpdateColumnEncryptionSettingMismatch()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_BatchedUpdateColumnEncryptionSettingMismatch, "SqlCommandColumnEncryptionSetting", "SelectCommand", "InsertCommand", "UpdateCommand", "DeleteCommand"));
        }
        static internal Exception MARSUnsupportedOnConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_MarsUnsupportedOnConnection));
        }

        static internal Exception CannotModifyPropertyAsyncOperationInProgress(string property)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_CannotModifyPropertyAsyncOperationInProgress, property));
        }
        static internal Exception NonLocalSSEInstance()
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_NonLocalSSEInstance));
        }

        // SQL.ActiveDirectoryAuth
        //
        static internal Exception UnsupportedAuthentication(string authentication)
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_UnsupportedAuthentication, authentication));
        }

        static internal Exception UnsupportedSqlAuthenticationMethod(SqlAuthenticationMethod authentication)
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_UnsupportedSqlAuthenticationMethod, authentication));
        }

        static internal Exception UnsupportedAuthenticationSpecified(SqlAuthenticationMethod authentication)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_UnsupportedAuthenticationSpecified, authentication));
        }

        static internal Exception CannotCreateAuthProvider(string authentication, string type, Exception e)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_CannotCreateAuthProvider, authentication, type), e);
        }

        static internal Exception CannotCreateSqlAuthInitializer(string type, Exception e)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_CannotCreateAuthInitializer, type), e);
        }

        static internal Exception CannotInitializeAuthProvider(string type, Exception e)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_CannotInitializeAuthProvider, type), e);
        }

        static internal Exception UnsupportedAuthenticationByProvider(string authentication, string type)
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_UnsupportedAuthenticationByProvider, type, authentication));
        }

        static internal Exception CannotFindAuthProvider(string authentication)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_CannotFindAuthProvider, authentication));
        }

        static internal Exception CannotGetAuthProviderConfig(Exception e)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_CannotGetAuthProviderConfig), e);
        }

        static internal Exception ParameterCannotBeEmpty(string paramName)
        {
            return ADP.ArgumentNull(StringsHelper.GetString(Strings.SQL_ParameterCannotBeEmpty, paramName));
        }

        static internal Exception ActiveDirectoryInteractiveTimeout()
        {
            return ADP.TimeoutException(Strings.SQL_Timeout_Active_Directory_Interactive_Authentication);
        }

        static internal Exception ActiveDirectoryDeviceFlowTimeout()
        {
            return ADP.TimeoutException(Strings.SQL_Timeout_Active_Directory_DeviceFlow_Authentication);
        }

        //
        // SQL.DataCommand
        //
        static internal Exception NotificationsRequireYukon()
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_NotificationsRequireYukon));
        }

        static internal ArgumentOutOfRangeException NotSupportedEnumerationValue(Type type, int value)
        {
            return ADP.ArgumentOutOfRange(StringsHelper.GetString(Strings.SQL_NotSupportedEnumerationValue, type.Name, value.ToString(System.Globalization.CultureInfo.InvariantCulture)), type.Name);
        }

        static internal ArgumentOutOfRangeException NotSupportedCommandType(CommandType value)
        {
#if DEBUG
            switch(value) {
            case CommandType.Text:
            case CommandType.StoredProcedure:
                Debug.Fail("valid CommandType " + value.ToString());
                break;
            case CommandType.TableDirect:
                break;
            default:
                Debug.Fail("invalid CommandType " + value.ToString());
                break;
            }
#endif
            return NotSupportedEnumerationValue(typeof(CommandType), (int)value);
        }
        static internal ArgumentOutOfRangeException NotSupportedIsolationLevel(IsolationLevel value)
        {
#if DEBUG
            switch(value) {
            case IsolationLevel.Unspecified:
            case IsolationLevel.ReadCommitted:
            case IsolationLevel.ReadUncommitted:
            case IsolationLevel.RepeatableRead:
            case IsolationLevel.Serializable:
            case IsolationLevel.Snapshot:
                Debug.Fail("valid IsolationLevel " + value.ToString());
                break;
            case IsolationLevel.Chaos:
                break;
            default:
                Debug.Fail("invalid IsolationLevel " + value.ToString());
                break;
            }
#endif
            return NotSupportedEnumerationValue(typeof(IsolationLevel), (int)value);
        }

        static internal Exception OperationCancelled()
        {
            Exception exception = ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_OperationCancelled));
            return exception;
        }

        static internal Exception PendingBeginXXXExists()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_PendingBeginXXXExists));
        }

        static internal ArgumentOutOfRangeException InvalidSqlDependencyTimeout(string param)
        {
            return ADP.ArgumentOutOfRange(StringsHelper.GetString(Strings.SqlDependency_InvalidTimeout), param);
        }

        static internal Exception NonXmlResult()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_NonXmlResult));
        }

        //
        // SQL.DataParameter
        //
        static internal Exception InvalidUdt3PartNameFormat()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InvalidUdt3PartNameFormat));
        }
        static internal Exception InvalidParameterTypeNameFormat()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InvalidParameterTypeNameFormat));
        }
        static internal Exception InvalidParameterNameLength(string value)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InvalidParameterNameLength, value));
        }
        static internal Exception PrecisionValueOutOfRange(byte precision)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_PrecisionValueOutOfRange, precision.ToString(CultureInfo.InvariantCulture)));
        }
        static internal Exception ScaleValueOutOfRange(byte scale)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_ScaleValueOutOfRange, scale.ToString(CultureInfo.InvariantCulture)));
        }
        static internal Exception TimeScaleValueOutOfRange(byte scale)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_TimeScaleValueOutOfRange, scale.ToString(CultureInfo.InvariantCulture)));
        }
        static internal Exception InvalidSqlDbType(SqlDbType value)
        {
            return ADP.InvalidEnumerationValue(typeof(SqlDbType), (int)value);
        }
        static internal Exception UnsupportedTVPOutputParameter(ParameterDirection direction, string paramName)
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SqlParameter_UnsupportedTVPOutputParameter,
                        direction.ToString(), paramName));
        }
        static internal Exception DBNullNotSupportedForTVPValues(string paramName)
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SqlParameter_DBNullNotSupportedForTVP, paramName));
        }
        static internal Exception InvalidTableDerivedPrecisionForTvp(string columnName, byte precision)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlParameter_InvalidTableDerivedPrecisionForTvp, precision, columnName, System.Data.SqlTypes.SqlDecimal.MaxPrecision));
        }
        static internal Exception UnexpectedTypeNameForNonStructParams(string paramName)
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SqlParameter_UnexpectedTypeNameForNonStruct, paramName));
        }
        static internal Exception SingleValuedStructNotSupported()
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.MetaType_SingleValuedStructNotSupported));
        }
        static internal Exception ParameterInvalidVariant(string paramName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParameterInvalidVariant, paramName));
        }

        static internal Exception MustSetTypeNameForParam(string paramType, string paramName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_ParameterTypeNameRequired, paramType, paramName));
        }
        static internal Exception NullSchemaTableDataTypeNotSupported(string columnName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.NullSchemaTableDataTypeNotSupported, columnName));
        }
        static internal Exception InvalidSchemaTableOrdinals()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.InvalidSchemaTableOrdinals));
        }
        static internal Exception EnumeratedRecordMetaDataChanged(string fieldName, int recordNumber)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_EnumeratedRecordMetaDataChanged, fieldName, recordNumber));
        }
        static internal Exception EnumeratedRecordFieldCountChanged(int recordNumber)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_EnumeratedRecordFieldCountChanged, recordNumber));
        }

        //
        // SQL.SqlDataAdapter
        //

        //
        // SQL.TDSParser
        //
        static internal Exception InvalidTDSVersion()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_InvalidTDSVersion));
        }
        static internal Exception ParsingError(ParsingErrorState state)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorWithState, ((int)state).ToString(CultureInfo.InvariantCulture)));
        }
        static internal Exception ParsingError(ParsingErrorState state, Exception innerException)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorWithState, ((int)state).ToString(CultureInfo.InvariantCulture)), innerException);
        }
        static internal Exception ParsingErrorValue(ParsingErrorState state, int value)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorValue, ((int)state).ToString(CultureInfo.InvariantCulture), value));
        }
        static internal Exception ParsingErrorOffset(ParsingErrorState state, int offset)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorOffset, ((int)state).ToString(CultureInfo.InvariantCulture), offset));
        }
        static internal Exception ParsingErrorFeatureId(ParsingErrorState state, int featureId)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorFeatureId, ((int)state).ToString(CultureInfo.InvariantCulture), featureId));
        }
        static internal Exception ParsingErrorToken(ParsingErrorState state, int token)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorToken, ((int)state).ToString(CultureInfo.InvariantCulture), token));
        }
        static internal Exception ParsingErrorLength(ParsingErrorState state, int length)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorLength, ((int)state).ToString(CultureInfo.InvariantCulture), length));
        }
        static internal Exception ParsingErrorStatus(ParsingErrorState state, int status)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorStatus, ((int)state).ToString(CultureInfo.InvariantCulture), status));
        }
        static internal Exception ParsingErrorLibraryType(ParsingErrorState state, int libraryType)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ParsingErrorAuthLibraryType, ((int)state).ToString(CultureInfo.InvariantCulture), libraryType));
        }

        static internal Exception MoneyOverflow(string moneyValue)
        {
            return ADP.Overflow(StringsHelper.GetString(Strings.SQL_MoneyOverflow, moneyValue));
        }
        static internal Exception SmallDateTimeOverflow(string datetime)
        {
            return ADP.Overflow(StringsHelper.GetString(Strings.SQL_SmallDateTimeOverflow, datetime));
        }
        static internal Exception SNIPacketAllocationFailure()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SNIPacketAllocationFailure));
        }
        static internal Exception TimeOverflow(string time)
        {
            return ADP.Overflow(StringsHelper.GetString(Strings.SQL_TimeOverflow, time));
        }
        static internal Exception InvalidServerCertificate()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_InvalidServerCertificate));
        }

        //
        // SQL.SqlDataReader
        //
        static internal Exception InvalidRead()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_InvalidRead));
        }

        static internal Exception NonBlobColumn(string columnName)
        {
            return ADP.InvalidCast(StringsHelper.GetString(Strings.SQL_NonBlobColumn, columnName));
        }

        static internal Exception NonCharColumn(string columnName)
        {
            return ADP.InvalidCast(StringsHelper.GetString(Strings.SQL_NonCharColumn, columnName));
        }

        static internal Exception StreamNotSupportOnColumnType(string columnName)
        {
            return ADP.InvalidCast(StringsHelper.GetString(Strings.SQL_StreamNotSupportOnColumnType, columnName));
        }

        static internal Exception StreamNotSupportOnEncryptedColumn(string columnName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_StreamNotSupportOnEncryptedColumn, columnName, "Stream"));
        }

        static internal Exception SequentialAccessNotSupportedOnEncryptedColumn(string columnName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_SequentialAccessNotSupportedOnEncryptedColumn, columnName, "CommandBehavior=SequentialAccess"));
        }

        static internal Exception TextReaderNotSupportOnColumnType(string columnName)
        {
            return ADP.InvalidCast(StringsHelper.GetString(Strings.SQL_TextReaderNotSupportOnColumnType, columnName));
        }

        static internal Exception XmlReaderNotSupportOnColumnType(string columnName)
        {
            return ADP.InvalidCast(StringsHelper.GetString(Strings.SQL_XmlReaderNotSupportOnColumnType, columnName));
        }

        static internal Exception UDTUnexpectedResult(string exceptionText)
        {
            return ADP.TypeLoad(StringsHelper.GetString(Strings.SQLUDT_Unexpected, exceptionText));
        }


        //
        // SQL.SqlDelegatedTransaction
        //
        static internal Exception CannotCompleteDelegatedTransactionWithOpenResults(SqlInternalConnectionTds internalConnection, bool marsOn)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(TdsEnums.TIMEOUT_EXPIRED, (byte)0x00, TdsEnums.MIN_ERROR_CLASS, null, (StringsHelper.GetString(Strings.ADP_OpenReaderExists, marsOn ? ADP.Command : ADP.Connection)), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));
            return SqlException.CreateException(errors, null, internalConnection);
        }
        static internal SysTx.TransactionPromotionException PromotionFailed(Exception inner)
        {
            SysTx.TransactionPromotionException e = new SysTx.TransactionPromotionException(StringsHelper.GetString(Strings.SqlDelegatedTransaction_PromotionFailed), inner);
            ADP.TraceExceptionAsReturnValue(e);
            return e;
        }

        //
        // SQL.SqlDependency
        //
        static internal Exception SqlCommandHasExistingSqlNotificationRequest()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQLNotify_AlreadyHasCommand));
        }

        static internal Exception SqlDepCannotBeCreatedInProc()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlNotify_SqlDepCannotBeCreatedInProc));
        }

        static internal Exception SqlDepDefaultOptionsButNoStart()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlDependency_DefaultOptionsButNoStart));
        }

        static internal Exception SqlDependencyDatabaseBrokerDisabled()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlDependency_DatabaseBrokerDisabled));
        }

        static internal Exception SqlDependencyEventNoDuplicate()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlDependency_EventNoDuplicate));
        }

        static internal Exception SqlDependencyDuplicateStart()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlDependency_DuplicateStart));
        }

        static internal Exception SqlDependencyIdMismatch()
        {
            // do not include the id because it may require SecurityPermission(Infrastructure) permission
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlDependency_IdMismatch));
        }

        static internal Exception SqlDependencyNoMatchingServerStart()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlDependency_NoMatchingServerStart));
        }

        static internal Exception SqlDependencyNoMatchingServerDatabaseStart()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlDependency_NoMatchingServerDatabaseStart));
        }

        static internal Exception SqlNotificationException(SqlNotificationEventArgs notify)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQLNotify_ErrorFormat, notify.Type, notify.Info, notify.Source));
        }

        //
        // SQL.SqlMetaData
        //
        static internal Exception SqlMetaDataNoMetaData()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlMetaData_NoMetadata));
        }

        static internal Exception MustSetUdtTypeNameForUdtParams()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQLUDT_InvalidUdtTypeName));
        }

        static internal Exception UnexpectedUdtTypeNameForNonUdtParams()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQLUDT_UnexpectedUdtTypeName));
        }

        static internal Exception UDTInvalidSqlType(string typeName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQLUDT_InvalidSqlType, typeName));
        }

        static internal Exception UDTInvalidSize(int maxSize, int maxSupportedSize)
        {
            throw ADP.ArgumentOutOfRange(StringsHelper.GetString(Strings.SQLUDT_InvalidSize, maxSize, maxSupportedSize));
        }


        static internal Exception InvalidSqlDbTypeForConstructor(SqlDbType type)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SqlMetaData_InvalidSqlDbTypeForConstructorFormat, type.ToString()));
        }

        static internal Exception NameTooLong(string parameterName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SqlMetaData_NameTooLong), parameterName);
        }

        static internal Exception InvalidSortOrder(SortOrder order)
        {
            return ADP.InvalidEnumerationValue(typeof(SortOrder), (int)order);
        }

        static internal Exception MustSpecifyBothSortOrderAndOrdinal(SortOrder order, int ordinal)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlMetaData_SpecifyBothSortOrderAndOrdinal, order.ToString(), ordinal));
        }

        static internal Exception TableTypeCanOnlyBeParameter()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQLTVP_TableTypeCanOnlyBeParameter));
        }
        static internal Exception UnsupportedColumnTypeForSqlProvider(string columnName, string typeName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SqlProvider_InvalidDataColumnType, columnName, typeName));
        }
        static internal Exception InvalidColumnMaxLength(string columnName, long maxLength)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SqlProvider_InvalidDataColumnMaxLength, columnName, maxLength));
        }
        static internal Exception InvalidColumnPrecScale()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SqlMisc_InvalidPrecScaleMessage));
        }
        static internal Exception NotEnoughColumnsInStructuredType()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SqlProvider_NotEnoughColumnsInStructuredType));
        }
        static internal Exception DuplicateSortOrdinal(int sortOrdinal)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlProvider_DuplicateSortOrdinal, sortOrdinal));
        }
        static internal Exception MissingSortOrdinal(int sortOrdinal)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlProvider_MissingSortOrdinal, sortOrdinal));
        }
        static internal Exception SortOrdinalGreaterThanFieldCount(int columnOrdinal, int sortOrdinal)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlProvider_SortOrdinalGreaterThanFieldCount, sortOrdinal, columnOrdinal));
        }
        static internal Exception IEnumerableOfSqlDataRecordHasNoRows()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.IEnumerableOfSqlDataRecordHasNoRows));
        }



        //
        //  SqlPipe
        //
        static internal Exception SqlPipeCommandHookedUpToNonContextConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlPipe_CommandHookedUpToNonContextConnection));
        }

        static internal Exception SqlPipeMessageTooLong(int messageLength)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SqlPipe_MessageTooLong, messageLength));
        }

        static internal Exception SqlPipeIsBusy()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlPipe_IsBusy));
        }

        static internal Exception SqlPipeAlreadyHasAnOpenResultSet(string methodName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlPipe_AlreadyHasAnOpenResultSet, methodName));
        }

        static internal Exception SqlPipeDoesNotHaveAnOpenResultSet(string methodName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlPipe_DoesNotHaveAnOpenResultSet, methodName));
        }

        //
        // : ISqlResultSet
        //
        static internal Exception SqlResultSetClosed(string methodname)
        {
            if (methodname == null)
            {
                return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlResultSetClosed2));
            }
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlResultSetClosed, methodname));
        }
        static internal Exception SqlResultSetNoData(string methodname)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_DataReaderNoData, methodname));
        }
        static internal Exception SqlRecordReadOnly(string methodname)
        {
            if (methodname == null)
            {
                return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlRecordReadOnly2));
            }
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlRecordReadOnly, methodname));
        }

        static internal Exception SqlResultSetRowDeleted(string methodname)
        {
            if (methodname == null)
            {
                return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlResultSetRowDeleted2));
            }
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlResultSetRowDeleted, methodname));
        }

        static internal Exception SqlResultSetCommandNotInSameConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlResultSetCommandNotInSameConnection));
        }

        static internal Exception SqlResultSetNoAcceptableCursor()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_SqlResultSetNoAcceptableCursor));
        }

        //
        // SQL.BulkLoad
        //
        static internal Exception BulkLoadMappingInaccessible()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadMappingInaccessible));
        }
        static internal Exception BulkLoadMappingsNamesOrOrdinalsOnly()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadMappingsNamesOrOrdinalsOnly));
        }
        static internal Exception BulkLoadCannotConvertValue(Type sourcetype, MetaType metatype, int ordinal, int rowNumber, bool isEncrypted, string columnName, string value, Exception e)
        {
            string quotedValue = string.Empty;
            if (!isEncrypted)
            {
                quotedValue = string.Format(" '{0}'", (value.Length > 100 ? value.Substring(0, 100) : value));
            }
            if (rowNumber == -1)
            {
                return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadCannotConvertValueWithoutRowNo, quotedValue, sourcetype.Name, metatype.TypeName, ordinal, columnName), e);
            }
            else
            {
                return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadCannotConvertValue, quotedValue, sourcetype.Name, metatype.TypeName, ordinal, columnName, rowNumber), e);
            }
        }
        static internal Exception BulkLoadNonMatchingColumnMapping()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadNonMatchingColumnMapping));
        }
        static internal Exception BulkLoadNonMatchingColumnName(string columnName)
        {
            return BulkLoadNonMatchingColumnName(columnName, null);
        }
        static internal Exception BulkLoadNonMatchingColumnName(string columnName, Exception e)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadNonMatchingColumnName, columnName), e);
        }
        internal static Exception BulkLoadNullEmptyColumnName(string paramName)
        {
            return ADP.Argument(string.Format(StringsHelper.GetString(Strings.SQL_ParameterCannotBeEmpty), paramName));
        }
        internal static Exception BulkLoadUnspecifiedSortOrder()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_BulkLoadUnspecifiedSortOrder));
        }
        internal static Exception BulkLoadInvalidOrderHint()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_BulkLoadInvalidOrderHint));
        }
        internal static Exception BulkLoadOrderHintInvalidColumn(string columnName)
        {
            return ADP.InvalidOperation(string.Format(StringsHelper.GetString(Strings.SQL_BulkLoadOrderHintInvalidColumn), columnName));
        }
        internal static Exception BulkLoadOrderHintDuplicateColumn(string columnName)
        {
            return ADP.InvalidOperation(string.Format(StringsHelper.GetString(Strings.SQL_BulkLoadOrderHintDuplicateColumn), columnName));
        }
        static internal Exception BulkLoadStringTooLong(string tableName, string columnName, string truncatedValue)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadStringTooLong, tableName, columnName, truncatedValue));
        }
        static internal Exception BulkLoadInvalidVariantValue()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadInvalidVariantValue));
        }
        static internal Exception BulkLoadInvalidTimeout(int timeout)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_BulkLoadInvalidTimeout, timeout.ToString(CultureInfo.InvariantCulture)));
        }
        static internal Exception BulkLoadExistingTransaction()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadExistingTransaction));
        }
        static internal Exception BulkLoadNoCollation()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadNoCollation));
        }
        static internal Exception BulkLoadConflictingTransactionOption()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_BulkLoadConflictingTransactionOption));
        }
        static internal Exception BulkLoadLcidMismatch(int sourceLcid, string sourceColumnName, int destinationLcid, string destinationColumnName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.Sql_BulkLoadLcidMismatch, sourceLcid, sourceColumnName, destinationLcid, destinationColumnName));
        }
        static internal Exception InvalidOperationInsideEvent()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadInvalidOperationInsideEvent));
        }
        static internal Exception BulkLoadMissingDestinationTable()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadMissingDestinationTable));
        }
        static internal Exception BulkLoadInvalidDestinationTable(string tableName, Exception inner)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadInvalidDestinationTable, tableName), inner);
        }
        static internal Exception BulkLoadBulkLoadNotAllowDBNull(string columnName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadNotAllowDBNull, columnName));
        }
        static internal Exception BulkLoadPendingOperation()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BulkLoadPendingOperation));
        }

        //
        // TCE - Certificate Store Provider Errors.
        //
        static internal Exception InvalidKeyEncryptionAlgorithm(string encryptionAlgorithm, string validEncryptionAlgorithm, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidKeyEncryptionAlgorithmSysErr, encryptionAlgorithm, validEncryptionAlgorithm), TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidKeyEncryptionAlgorithm, encryptionAlgorithm, validEncryptionAlgorithm), TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM);
            }
        }

        static internal Exception NullKeyEncryptionAlgorithm(bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM, StringsHelper.GetString(Strings.TCE_NullKeyEncryptionAlgorithmSysErr));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM, StringsHelper.GetString(Strings.TCE_NullKeyEncryptionAlgorithm));
            }
        }

        static internal Exception EmptyColumnEncryptionKey()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyColumnEncryptionKey), TdsEnums.TCE_PARAM_COLUMNENCRYPTION_KEY);
        }

        static internal Exception NullColumnEncryptionKey()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_COLUMNENCRYPTION_KEY, StringsHelper.GetString(Strings.TCE_NullColumnEncryptionKey));
        }

        static internal Exception EmptyEncryptedColumnEncryptionKey()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyEncryptedColumnEncryptionKey), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception NullEncryptedColumnEncryptionKey()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTED_CEK, StringsHelper.GetString(Strings.TCE_NullEncryptedColumnEncryptionKey));
        }

        static internal Exception LargeCertificatePathLength(int actualLength, int maxLength, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_LargeCertificatePathLengthSysErr, actualLength, maxLength), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_LargeCertificatePathLength, actualLength, maxLength), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception NullCertificatePath(string[] validLocations, bool isSystemOp)
        {
            Debug.Assert(2 == validLocations.Length);
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, StringsHelper.GetString(Strings.TCE_NullCertificatePathSysErr, validLocations[0], validLocations[1], @"/"));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, StringsHelper.GetString(Strings.TCE_NullCertificatePath, validLocations[0], validLocations[1], @"/"));
            }
        }

        static internal Exception NullCspKeyPath(bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, StringsHelper.GetString(Strings.TCE_NullCspPathSysErr, @"/"));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, StringsHelper.GetString(Strings.TCE_NullCspPath, @"/"));
            }
        }

        static internal Exception NullCngKeyPath(bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, StringsHelper.GetString(Strings.TCE_NullCngPathSysErr, @"/"));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, StringsHelper.GetString(Strings.TCE_NullCngPath, @"/"));
            }
        }

        static internal Exception InvalidCertificatePath(string actualCertificatePath, string[] validLocations, bool isSystemOp)
        {
            Debug.Assert(2 == validLocations.Length);
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCertificatePathSysErr, actualCertificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCertificatePath, actualCertificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCspPath(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCspPathSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCspPath, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCngPath(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCngPathSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCngPath, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCspName(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCspNameSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCspName, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCngName(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCngNameSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCngName, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCspKeyId(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCspKeyIdSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCspKeyId, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCngKeyId(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCngKeyIdSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCngKeyId, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCspName(string cspName, string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCspNameSysErr, cspName, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCspName, cspName, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCspKeyIdentifier(string keyIdentifier, string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCspKeyIdSysErr, keyIdentifier, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCspKeyId, keyIdentifier, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCngKey(string masterKeyPath, string cngProviderName, string keyIdentifier, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCngKeySysErr, masterKeyPath, cngProviderName, keyIdentifier), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCngKey, masterKeyPath, cngProviderName, keyIdentifier), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCertificateLocation(string certificateLocation, string certificatePath, string[] validLocations, bool isSystemOp)
        {
            Debug.Assert(2 == validLocations.Length);
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCertificateLocationSysErr, certificateLocation, certificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCertificateLocation, certificateLocation, certificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCertificateStore(string certificateStore, string certificatePath, string validCertificateStore, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCertificateStoreSysErr, certificateStore, certificatePath, validCertificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCertificateStore, certificateStore, certificatePath, validCertificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCertificateThumbprint(string certificatePath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCertificateThumbprintSysErr, certificatePath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyCertificateThumbprint, certificatePath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception CertificateNotFound(string thumbprint, string certificateLocation, string certificateStore, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_CertificateNotFoundSysErr, thumbprint, certificateLocation, certificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_CertificateNotFound, thumbprint, certificateLocation, certificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidAlgorithmVersionInEncryptedCEK(byte actual, byte expected)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidAlgorithmVersionInEncryptedCEK, actual.ToString(@"X2"), expected.ToString(@"X2")), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCiphertextLengthInEncryptedCEK(int actual, int expected, string certificateName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCiphertextLengthInEncryptedCEK, actual, expected, certificateName), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCiphertextLengthInEncryptedCEKCsp(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCiphertextLengthInEncryptedCEKCsp, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCiphertextLengthInEncryptedCEKCng(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCiphertextLengthInEncryptedCEKCng, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignatureInEncryptedCEK(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidSignatureInEncryptedCEK, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignatureInEncryptedCEKCsp(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidSignatureInEncryptedCEKCsp, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignatureInEncryptedCEKCng(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidSignatureInEncryptedCEKCng, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCertificateSignature(string certificatePath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCertificateSignature, certificatePath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignature(string masterKeyPath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidSignature, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception CertificateWithNoPrivateKey(string keyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_CertificateWithNoPrivateKeySysErr, keyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(StringsHelper.GetString(Strings.TCE_CertificateWithNoPrivateKey, keyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        //
        // TCE - Cryptographic Algorithms Error messages
        //
        static internal Exception NullColumnEncryptionKeySysErr()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTIONKEY, StringsHelper.GetString(Strings.TCE_NullColumnEncryptionKeySysErr));
        }

        static internal Exception InvalidKeySize(string algorithmName, int actualKeylength, int expectedLength)
        {
            return ADP.Argument(StringsHelper.GetString(
                                Strings.TCE_InvalidKeySize,
                                algorithmName,
                                actualKeylength,
                                expectedLength), TdsEnums.TCE_PARAM_ENCRYPTIONKEY);
        }

        static internal Exception InvalidEncryptionType(string algorithmName, SqlClientEncryptionType encryptionType, params SqlClientEncryptionType[] validEncryptionTypes)
        {
            const string valueSeparator = @", ";
            return ADP.Argument(StringsHelper.GetString(
                                Strings.TCE_InvalidEncryptionType,
                                algorithmName,
                                encryptionType.ToString(),
                                string.Join(valueSeparator, validEncryptionTypes.Select((validEncryptionType => @"'" + validEncryptionType + @"'")))), TdsEnums.TCE_PARAM_ENCRYPTIONTYPE);
        }

        static internal Exception NullPlainText()
        {
            return ADP.ArgumentNull(StringsHelper.GetString(Strings.TCE_NullPlainText));
        }

        static internal Exception VeryLargeCiphertext(long cipherTextLength, long maxCipherTextSize, long plainTextLength)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_VeryLargeCiphertext, cipherTextLength, maxCipherTextSize, plainTextLength));
        }

        static internal Exception NullCipherText()
        {
            return ADP.ArgumentNull(StringsHelper.GetString(Strings.TCE_NullCipherText));
        }

        static internal Exception InvalidCipherTextSize(int actualSize, int minimumSize)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCipherTextSize, actualSize, minimumSize), TdsEnums.TCE_PARAM_CIPHERTEXT);
        }

        static internal Exception InvalidAlgorithmVersion(byte actual, byte expected)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidAlgorithmVersion, actual.ToString(@"X2"), expected.ToString(@"X2")), TdsEnums.TCE_PARAM_CIPHERTEXT);
        }

        static internal Exception InvalidAuthenticationTag()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidAuthenticationTag), TdsEnums.TCE_PARAM_CIPHERTEXT);
        }

        static internal Exception NullColumnEncryptionAlgorithm(string supportedAlgorithms)
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM, StringsHelper.GetString(Strings.TCE_NullColumnEncryptionAlgorithm, supportedAlgorithms));
        }

        //
        // TCE - Errors from sp_describe_parameter_encryption
        //
        static internal Exception UnexpectedDescribeParamFormatParameterMetadata()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UnexpectedDescribeParamFormatParameterMetadata, "sp_describe_parameter_encryption"));
        }

        static internal Exception UnexpectedDescribeParamFormatAttestationInfo(string enclaveType)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UnexpectedDescribeParamFormatAttestationInfo, "sp_describe_parameter_encryption", enclaveType));
        }

        static internal Exception InvalidEncryptionKeyOrdinalEnclaveMetadata(int ordinal, int maxOrdinal)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_InvalidEncryptionKeyOrdinalEnclaveMetadata, ordinal, maxOrdinal));
        }
        static internal Exception InvalidEncryptionKeyOrdinalParameterMetadata(int ordinal, int maxOrdinal)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_InvalidEncryptionKeyOrdinalParameterMetadata, ordinal, maxOrdinal));
        }

        public static Exception MultipleRowsReturnedForAttestationInfo()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_MultipleRowsReturnedForAttestationInfo, "sp_describe_parameter_encryption"));
        }

        static internal Exception ParamEncryptionMetadataMissing(string paramName, string procedureName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_ParamEncryptionMetaDataMissing, "sp_describe_parameter_encryption", paramName, procedureName));
        }

        static internal Exception ParamInvalidForceColumnEncryptionSetting(string paramName, string procedureName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_ParamInvalidForceColumnEncryptionSetting, TdsEnums.TCE_PARAM_FORCE_COLUMN_ENCRYPTION, paramName, procedureName, "SqlParameter"));
        }

        static internal Exception ParamUnExpectedEncryptionMetadata(string paramName, string procedureName)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_ParamUnExpectedEncryptionMetadata, paramName, procedureName, TdsEnums.TCE_PARAM_FORCE_COLUMN_ENCRYPTION, "SqlParameter"));
        }

        static internal Exception ProcEncryptionMetadataMissing(string procedureName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_ProcEncryptionMetaDataMissing, "sp_describe_parameter_encryption", procedureName));
        }

        static internal Exception InvalidKeyStoreProviderName(string providerName, List<string> systemProviders, List<string> customProviders)
        {
            const string valueSeparator = @", ";
            string systemProviderStr = string.Join(valueSeparator, systemProviders.Select(provider => $"'{provider}'"));
            string customProviderStr = string.Join(valueSeparator, customProviders.Select(provider => $"'{provider}'"));
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidKeyStoreProviderName, providerName, systemProviderStr, customProviderStr));
        }

        static internal Exception UnableToVerifyColumnMasterKeySignature(Exception innerException)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_UnableToVerifyColumnMasterKeySignature, innerException.Message), innerException);
        }

        static internal Exception ColumnMasterKeySignatureVerificationFailed(string cmkPath)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_ColumnMasterKeySignatureVerificationFailed, cmkPath));
        }

        static internal Exception ColumnMasterKeySignatureNotFound(string cmkPath)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_ColumnMasterKeySignatureNotFound, cmkPath));
        }

        //
        // TCE - Errors from secure channel Communication
        //
        internal static Exception ExceptionWhenGeneratingEnclavePackage(Exception innerException)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_ExceptionWhenGeneratingEnclavePackage, innerException.Message), innerException);
        }

        static internal Exception FailedToEncryptRegisterRulesBytePackage(Exception innerException)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_FailedToEncryptRegisterRulesBytePackage, innerException.Message), innerException);
        }

        static internal Exception InvalidKeyIdUnableToCastToUnsignedShort(int keyId, Exception innerException)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidKeyIdUnableToCastToUnsignedShort, keyId, innerException.Message), innerException);
        }

        static internal Exception InvalidDatabaseIdUnableToCastToUnsignedInt(int databaseId, Exception innerException)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidDatabaseIdUnableToCastToUnsignedInt, databaseId, innerException.Message), innerException);
        }

        static internal Exception InvalidAttestationParameterUnableToConvertToUnsignedInt(string variableName, int intValue, string enclaveType, Exception innerException)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidAttestationParameterUnableToConvertToUnsignedInt, enclaveType, intValue, variableName, innerException.Message), innerException);
        }

        static internal Exception OffsetOutOfBounds(string argument, string type, string method)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_OffsetOutOfBounds, type, method));
        }

        static internal Exception InsufficientBuffer(string argument, string type, string method)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InsufficientBuffer, argument, type, method));
        }

        static internal Exception ColumnEncryptionKeysNotFound()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_ColumnEncryptionKeysNotFound));
        }

        //
        // TCE - Errors when performing attestation
        //

        static internal Exception AttestationInfoNotReturnedFromSqlServer(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_AttestationInfoNotReturnedFromSQLServer, enclaveType, enclaveAttestationUrl));
        }

        //
        // TCE - Errors when establishing secure channel
        //
        static internal Exception NullArgumentInConstructorInternal(string argumentName, string objectUnderConstruction)
        {
            return ADP.ArgumentNull(argumentName, StringsHelper.GetString(Strings.TCE_NullArgumentInConstructorInternal, argumentName, objectUnderConstruction));
        }

        static internal Exception EmptyArgumentInConstructorInternal(string argumentName, string objectUnderConstruction)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyArgumentInConstructorInternal, argumentName, objectUnderConstruction));
        }

        static internal Exception NullArgumentInternal(string argumentName, string type, string method)
        {
            return ADP.ArgumentNull(argumentName, StringsHelper.GetString(Strings.TCE_NullArgumentInternal, argumentName, type, method));
        }

        static internal Exception EmptyArgumentInternal(string argumentName, string type, string method)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_EmptyArgumentInternal, argumentName, type, method));
        }

        //
        // TCE - enclave provider/configuration errors
        //
        static internal Exception CannotGetSqlColumnEncryptionEnclaveProviderConfig(Exception innerException)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_CannotGetSqlColumnEncryptionEnclaveProviderConfig, innerException.Message), innerException);
        }

        static internal Exception CannotCreateSqlColumnEncryptionEnclaveProvider(string providerName, string type, Exception innerException)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_CannotCreateSqlColumnEncryptionEnclaveProvider, providerName, type, innerException.Message), innerException);
        }

        static internal Exception SqlColumnEncryptionEnclaveProviderNameCannotBeEmpty()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_SqlColumnEncryptionEnclaveProviderNameCannotBeEmpty));
        }

        static internal Exception NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe(string enclaveType)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe, "sp_describe_parameter_encryption", enclaveType));
        }

        static internal Exception NoAttestationUrlSpecifiedForEnclaveBasedQueryGeneratingEnclavePackage(string enclaveType)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_NoAttestationUrlSpecifiedForEnclaveBasedQueryGeneratingEnclavePackage, enclaveType));
        }

        static internal Exception EnclaveTypeNullForEnclaveBasedQuery()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_EnclaveTypeNullForEnclaveBasedQuery));
        }

        static internal Exception EnclaveProvidersNotConfiguredForEnclaveBasedQuery()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_EnclaveProvidersNotConfiguredForEnclaveBasedQuery));
        }

        static internal Exception EnclaveProviderNotFound(string enclaveType, string attestationProtocol)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_EnclaveProviderNotFound, enclaveType, attestationProtocol));
        }

        static internal Exception AttestationProtocolNotSpecifiedForGeneratingEnclavePackage()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_AttestationProtocolNotSpecifiedForGeneratingEnclavePackage));
        }

        static internal Exception NullEnclaveSessionReturnedFromProvider(string enclaveType, string attestationUrl)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_NullEnclaveSessionReturnedFromProvider, enclaveType, attestationUrl));
        }

        //
        // TCE- Generic toplevel failuStrings.
        //
        static internal Exception GetExceptionArray(string serverName, string errorMessage, Exception e)
        {
            // Create and throw an exception array
            SqlErrorCollection sqlErs = new SqlErrorCollection();
            Exception exceptionToInclude = (null != e.InnerException) ? e.InnerException : e;
            sqlErs.Add(new SqlError(infoNumber: 0, errorState: (byte)0x00, errorClass: (byte)TdsEnums.MIN_ERROR_CLASS, server: serverName, errorMessage: errorMessage, procedure: null, lineNumber: 0));

            if (e is SqlException)
            {
                SqlException exThrown = (SqlException)e;
                SqlErrorCollection errorList = exThrown.Errors;
                for (int i = 0; i < exThrown.Errors.Count; i++)
                {
                    sqlErs.Add(errorList[i]);
                }
            }
            else
            {
                sqlErs.Add(new SqlError(infoNumber: 0, errorState: (byte)0x00, errorClass: (byte)TdsEnums.MIN_ERROR_CLASS, server: serverName, errorMessage: e.Message, procedure: null, lineNumber: 0));
            }

            return SqlException.CreateException(sqlErs, "", null, exceptionToInclude);
        }

        static internal Exception ParamEncryptionFailed(string paramName, string serverName, Exception e)
        {
            return GetExceptionArray(serverName, StringsHelper.GetString(Strings.TCE_ParamEncryptionFailed, paramName), e);
        }

        static internal Exception ParamDecryptionFailed(string paramName, string serverName, Exception e)
        {
            return GetExceptionArray(serverName, StringsHelper.GetString(Strings.TCE_ParamDecryptionFailed, paramName), e);
        }

        static internal Exception ColumnDecryptionFailed(string columnName, string serverName, Exception e)
        {
            return GetExceptionArray(serverName, StringsHelper.GetString(Strings.TCE_ColumnDecryptionFailed, columnName), e);
        }

        //
        // TCE- Client side query processing errors.
        //
        static internal Exception UnknownColumnEncryptionAlgorithm(string algorithmName, string supportedAlgorithms)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UnknownColumnEncryptionAlgorithm, algorithmName, supportedAlgorithms));
        }

        static internal Exception UnknownColumnEncryptionAlgorithmId(int algoId, string supportAlgorithmIds)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UnknownColumnEncryptionAlgorithmId, algoId, supportAlgorithmIds), TdsEnums.TCE_PARAM_CIPHER_ALGORITHM_ID);
        }

        static internal Exception UnsupportedNormalizationVersion(byte version)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UnsupportedNormalizationVersion, version, "'1'", "SQL Server"));
        }

        static internal Exception UnrecognizedKeyStoreProviderName(string providerName, List<string> systemProviders, List<string> customProviders)
        {
            const string valueSeparator = @", ";
            string systemProviderStr = string.Join(valueSeparator, systemProviders.Select(provider => @"'" + provider + @"'"));
            string customProviderStr = string.Join(valueSeparator, customProviders.Select(provider => @"'" + provider + @"'"));
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UnrecognizedKeyStoreProviderName, providerName, systemProviderStr, customProviderStr));
        }

        static internal Exception InvalidDataTypeForEncryptedParameter(string parameterName, int actualDataType, int expectedDataType)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_NullProviderValue, parameterName, actualDataType, expectedDataType));
        }

        static internal Exception KeyDecryptionFailed(string providerName, string keyHex, Exception e)
        {
            if (providerName.Equals(SqlColumnEncryptionCertificateStoreProvider.ProviderName))
            {
                return GetExceptionArray(null, StringsHelper.GetString(Strings.TCE_KeyDecryptionFailedCertStore, providerName, keyHex), e);
            }
            else
            {
                return GetExceptionArray(null, StringsHelper.GetString(Strings.TCE_KeyDecryptionFailed, providerName, keyHex), e);
            }
        }

        static internal Exception UntrustedKeyPath(string keyPath, string serverName)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UntrustedKeyPath, keyPath, serverName));
        }

        static internal Exception UnsupportedDatatypeEncryption(string dataType)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_UnsupportedDatatype, dataType));
        }

        static internal Exception ThrowDecryptionFailed(string keyStr, string valStr, Exception e)
        {
            return GetExceptionArray(null, StringsHelper.GetString(Strings.TCE_DecryptionFailed, keyStr, valStr), e);
        }

        static internal Exception NullEnclaveSessionDuringQueryExecution(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_NullEnclaveSessionDuringQueryExecution, enclaveType, enclaveAttestationUrl));
        }

        static internal Exception NullEnclavePackageForEnclaveBasedQuery(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_NullEnclavePackageForEnclaveBasedQuery, enclaveType, enclaveAttestationUrl));
        }

        //
        // TCE- SQL connection related error messages
        //
        static internal Exception TceNotSupported()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_NotSupportedByServer, "SQL Server"));
        }

        static internal Exception EnclaveComputationsNotSupported()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_EnclaveComputationsNotSupported));
        }

        internal static Exception AttestationURLNotSupported()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_AttestationURLNotSupported));
        }

        internal static Exception AttestationProtocolNotSupported()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_AttestationProtocolNotSupported));
        }

        static internal Exception EnclaveTypeNotReturned()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_EnclaveTypeNotReturned));
        }

        static internal Exception EnclaveTypeNotSupported(string enclaveType)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_EnclaveTypeNotSupported, enclaveType));
        }

        static internal Exception AttestationProtocolNotSupportEnclaveType(string attestationProtocolStr, string enclaveType)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_AttestationProtocolNotSupportEnclaveType, attestationProtocolStr, enclaveType));
        }

        //
        // TCE- Extensibility related error messages
        //
        static internal Exception CanOnlyCallOnce()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.TCE_CanOnlyCallOnce));
        }

        static internal Exception NullCustomKeyStoreProviderDictionary()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS, StringsHelper.GetString(Strings.TCE_NullCustomKeyStoreProviderDictionary));
        }

        static internal Exception InvalidCustomKeyStoreProviderName(string providerName, string prefix)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.TCE_InvalidCustomKeyStoreProviderName, providerName, prefix), TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS);
        }

        static internal Exception NullProviderValue(string providerName)
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS, StringsHelper.GetString(Strings.TCE_NullProviderValue, providerName));
        }

        static internal Exception EmptyProviderName()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS, StringsHelper.GetString(Strings.TCE_EmptyProviderName));
        }

        //
        // transactions.
        //
        static internal Exception ConnectionDoomed()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ConnectionDoomed));
        }

        static internal Exception OpenResultCountExceeded()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_OpenResultCountExceeded));
        }

        //
        // Global Transactions.
        //
        static internal Exception GlobalTransactionsNotEnabled()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.GT_Disabled));
        }

        static internal Exception UnsupportedSysTxForGlobalTransactions()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.GT_UnsupportedSysTxVersion));
        }

        static internal readonly byte[] AttentionHeader = new byte[] {
            TdsEnums.MT_ATTN,               // Message Type
            TdsEnums.ST_EOM,                // Status
            TdsEnums.HEADER_LEN >> 8,       // length - upper byte
            TdsEnums.HEADER_LEN & 0xff,     // length - lower byte
            0,                              // spid
            0,                              // spid
            0,                              // packet (out of band)
            0                               // window
        };

        //
        // MultiSubnetFailover
        //

        /// <summary>
        /// used to block two scenarios if MultiSubnetFailover is true:
        /// * server-provided failover partner - raising SqlException in this case
        /// * connection string with failover partner and MultiSubnetFailover=true - rasing argument one in this case with the same message
        /// </summary>
        static internal Exception MultiSubnetFailoverWithFailoverPartner(bool serverProvidedFailoverPartner, SqlInternalConnectionTds internalConnection)
        {
            string msg = StringsHelper.GetString(Strings.SQLMSF_FailoverPartnerNotSupported);
            if (serverProvidedFailoverPartner)
            {
                // VSTFDEVDIV\DevDiv2\179041 - replacing InvalidOperation with SQL exception
                SqlErrorCollection errors = new SqlErrorCollection();
                errors.Add(new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, null, msg, "", 0));
                SqlException exc = SqlException.CreateException(errors, null, internalConnection);
                exc._doNotReconnect = true; // disable open retry logic on this error
                return exc;
            }
            else
            {
                return ADP.Argument(msg);
            }
        }

        static internal Exception MultiSubnetFailoverWithMoreThan64IPs()
        {
            string msg = GetSNIErrorMessage((int)SNINativeMethodWrapper.SniSpecialErrors.MultiSubnetFailoverWithMoreThan64IPs);
            return ADP.InvalidOperation(msg);
        }

        static internal Exception MultiSubnetFailoverWithInstanceSpecified()
        {
            string msg = GetSNIErrorMessage((int)SNINativeMethodWrapper.SniSpecialErrors.MultiSubnetFailoverWithInstanceSpecified);
            return ADP.Argument(msg);
        }

        static internal Exception MultiSubnetFailoverWithNonTcpProtocol()
        {
            string msg = GetSNIErrorMessage((int)SNINativeMethodWrapper.SniSpecialErrors.MultiSubnetFailoverWithNonTcpProtocol);
            return ADP.Argument(msg);
        }

        //
        // Read-only routing
        //

        static internal Exception ROR_FailoverNotSupportedConnString()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQLROR_FailoverNotSupported));
        }

        static internal Exception ROR_FailoverNotSupportedServer(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, null, (StringsHelper.GetString(Strings.SQLROR_FailoverNotSupported)), "", 0));
            SqlException exc = SqlException.CreateException(errors, null, internalConnection);
            exc._doNotReconnect = true;
            return exc;
        }

        static internal Exception ROR_RecursiveRoutingNotSupported(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, null, (StringsHelper.GetString(Strings.SQLROR_RecursiveRoutingNotSupported)), "", 0));
            SqlException exc = SqlException.CreateException(errors, null, internalConnection);
            exc._doNotReconnect = true;
            return exc;
        }

        static internal Exception ROR_UnexpectedRoutingInfo(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, null, (StringsHelper.GetString(Strings.SQLROR_UnexpectedRoutingInfo)), "", 0));
            SqlException exc = SqlException.CreateException(errors, null, internalConnection);
            exc._doNotReconnect = true;
            return exc;
        }

        static internal Exception ROR_InvalidRoutingInfo(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, null, (StringsHelper.GetString(Strings.SQLROR_InvalidRoutingInfo)), "", 0));
            SqlException exc = SqlException.CreateException(errors, null, internalConnection);
            exc._doNotReconnect = true;
            return exc;
        }

        static internal Exception ROR_TimeoutAfterRoutingInfo(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, null, (StringsHelper.GetString(Strings.SQLROR_TimeoutAfterRoutingInfo)), "", 0));
            SqlException exc = SqlException.CreateException(errors, null, internalConnection);
            exc._doNotReconnect = true;
            return exc;
        }

        //
        // Connection resiliency
        //
        static internal SqlException CR_ReconnectTimeout()
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(TdsEnums.TIMEOUT_EXPIRED, (byte)0x00, TdsEnums.MIN_ERROR_CLASS, null, SQLMessage.Timeout(), "", 0, TdsEnums.SNI_WAIT_TIMEOUT));
            SqlException exc = SqlException.CreateException(errors, "");
            return exc;
        }

        static internal SqlException CR_ReconnectionCancelled()
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.MIN_ERROR_CLASS, null, SQLMessage.OperationCancelled(), "", 0));
            SqlException exc = SqlException.CreateException(errors, "");
            return exc;
        }

        static internal Exception CR_NextAttemptWillExceedQueryTimeout(SqlException innerException, Guid connectionId)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.MIN_ERROR_CLASS, null, StringsHelper.GetString(Strings.SQLCR_NextAttemptWillExceedQueryTimeout), "", 0));
            SqlException exc = SqlException.CreateException(errors, "", connectionId, innerException);
            return exc;
        }

        static internal Exception CR_EncryptionChanged(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.FATAL_ERROR_CLASS, null, StringsHelper.GetString(Strings.SQLCR_EncryptionChanged), "", 0));
            SqlException exc = SqlException.CreateException(errors, "", internalConnection);
            return exc;
        }

        static internal SqlException CR_AllAttemptsFailed(SqlException innerException, Guid connectionId)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.MIN_ERROR_CLASS, null, StringsHelper.GetString(Strings.SQLCR_AllAttemptsFailed), "", 0));
            SqlException exc = SqlException.CreateException(errors, "", connectionId, innerException);
            return exc;
        }

        static internal SqlException CR_NoCRAckAtReconnection(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.FATAL_ERROR_CLASS, null, StringsHelper.GetString(Strings.SQLCR_NoCRAckAtReconnection), "", 0));
            SqlException exc = SqlException.CreateException(errors, "", internalConnection);
            return exc;
        }

        static internal SqlException CR_TDSVersionNotPreserved(SqlInternalConnectionTds internalConnection)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.FATAL_ERROR_CLASS, null, StringsHelper.GetString(Strings.SQLCR_TDSVestionNotPreserved), "", 0));
            SqlException exc = SqlException.CreateException(errors, "", internalConnection);
            return exc;
        }

        static internal SqlException CR_UnrecoverableServer(Guid connectionId)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.FATAL_ERROR_CLASS, null, StringsHelper.GetString(Strings.SQLCR_UnrecoverableServer), "", 0));
            SqlException exc = SqlException.CreateException(errors, "", connectionId);
            return exc;
        }

        static internal SqlException CR_UnrecoverableClient(Guid connectionId)
        {
            SqlErrorCollection errors = new SqlErrorCollection();
            errors.Add(new SqlError(0, 0, TdsEnums.FATAL_ERROR_CLASS, null, StringsHelper.GetString(Strings.SQLCR_UnrecoverableClient), "", 0));
            SqlException exc = SqlException.CreateException(errors, "", connectionId);
            return exc;
        }
        internal static Exception Azure_ManagedIdentityException(string msg)
        {
            SqlErrorCollection errors = new SqlErrorCollection
            {
                new SqlError(0, (byte)0x00, TdsEnums.FATAL_ERROR_CLASS, null, msg, "", 0)
            };
            SqlException exc = SqlException.CreateException(errors, null);
            exc._doNotReconnect = true; // disable open retry logic on this error
            return exc;
        }

        //
        // Merged Provider
        //
        static internal Exception BatchedUpdatesNotAvailableOnContextConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_BatchedUpdatesNotAvailableOnContextConnection));
        }
        static internal Exception ContextAllowsLimitedKeywords()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ContextAllowsLimitedKeywords));
        }
        static internal Exception ContextAllowsOnlyTypeSystem2005()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ContextAllowsOnlyTypeSystem2005));
        }
        static internal Exception ContextConnectionIsInUse()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ContextConnectionIsInUse));
        }
        static internal Exception ContextUnavailableOutOfProc()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ContextUnavailableOutOfProc));
        }
        static internal Exception ContextUnavailableWhileInProc()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_ContextUnavailableWhileInProc));
        }
        static internal Exception NestedTransactionScopesNotSupported()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_NestedTransactionScopesNotSupported));
        }
        static internal Exception NotAvailableOnContextConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_NotAvailableOnContextConnection));
        }
        static internal Exception NotificationsNotAvailableOnContextConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_NotificationsNotAvailableOnContextConnection));
        }
        static internal Exception UnexpectedSmiEvent(Microsoft.Data.SqlClient.Server.SmiEventSink_Default.UnexpectedEventType eventType)
        {
            Debug.Assert(false, "UnexpectedSmiEvent: " + eventType.ToString());    // Assert here, because these exceptions will most likely be eaten by the server.
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_UnexpectedSmiEvent, (int)eventType));
        }
        static internal Exception UserInstanceNotAvailableInProc()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_UserInstanceNotAvailableInProc));
        }
        static internal Exception ArgumentLengthMismatch(string arg1, string arg2)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_ArgumentLengthMismatch, arg1, arg2));
        }
        static internal Exception InvalidSqlDbTypeOneAllowedType(SqlDbType invalidType, string method, SqlDbType allowedType)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_InvalidSqlDbTypeWithOneAllowedType, invalidType, method, allowedType));
        }
        static internal Exception SqlPipeErrorRequiresSendEnd()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SQL_PipeErrorRequiresSendEnd));
        }
        static internal Exception TooManyValues(string arg)
        {
            return ADP.Argument(StringsHelper.GetString(Strings.SQL_TooManyValues), arg);
        }
        static internal Exception StreamWriteNotSupported()
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_StreamWriteNotSupported));
        }
        static internal Exception StreamReadNotSupported()
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_StreamReadNotSupported));
        }
        static internal Exception StreamSeekNotSupported()
        {
            return ADP.NotSupported(StringsHelper.GetString(Strings.SQL_StreamSeekNotSupported));
        }
        static internal System.Data.SqlTypes.SqlNullValueException SqlNullValue()
        {
            System.Data.SqlTypes.SqlNullValueException e = new System.Data.SqlTypes.SqlNullValueException();
            ADP.TraceExceptionAsReturnValue(e);
            return e;
        }
        // SQLBU 402363: Exception to prevent Parameter.Size data corruption case from working.
        //  This should be temporary until changing to correct behavior can be safely implemented.
        static internal Exception ParameterSizeRestrictionFailure(int index)
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.OleDb_CommandParameterError, index.ToString(CultureInfo.InvariantCulture), "SqlParameter.Size"));
        }
        static internal Exception SubclassMustOverride()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlMisc_SubclassMustOverride));
        }

        /// <summary>
        /// gets a message for SNI error (sniError must be valid, non-zero error code)
        /// </summary>
        static internal string GetSNIErrorMessage(int sniError)
        {
            Debug.Assert(sniError > 0 && sniError <= (int)SNINativeMethodWrapper.SniSpecialErrors.MaxErrorValue, "SNI error is out of range");

            string errorMessageId = string.Format("SNI_ERROR_{0}", sniError);
            return StringsHelper.GetString(errorMessageId);
        }

        // BulkLoad
        internal const string WriteToServer = "WriteToServer";

        // Default values for SqlDependency and SqlNotificationRequest
        internal const int SqlDependencyTimeoutDefault = 0;
        internal const int SqlDependencyServerTimeout = 5 * 24 * 3600; // 5 days - used to compute default TTL of the dependency
        internal const string SqlNotificationServiceDefault = "SqlQueryNotificationService";
        internal const string SqlNotificationStoredProcedureDefault = "SqlQueryNotificationStoredProcedure";

        // constant strings
        internal const string Transaction = "Transaction";
        internal const string Connection = "Connection";
    }

    sealed internal class SQLMessage
    {

        // UNDONE - TODO - BUG - need to possibly re-work this since Dbnetlib removed.

        private SQLMessage() { /* prevent utility class from being insantiated*/ }

        // The class SQLMessage defines the error messages that are specific to the SqlDataAdapter
        // that are caused by a netlib error.  The functions will be called and then return the
        // appropriate error message from the resource Framework.txt.  The SqlDataAdapter will then
        // take the error message and then create a SqlError for the message and then place
        // that into a SqlException that is either thrown to the user or cached for throwing at
        // a later time.  This class is used so that there will be compile time checking of error
        // messages.  The resource Framework.txt will ensure proper string text based on the appropriate
        // locale.

        static internal string CultureIdError()
        {
            return StringsHelper.GetString(Strings.SQL_CultureIdError);
        }
        static internal string EncryptionNotSupportedByClient()
        {
            return StringsHelper.GetString(Strings.SQL_EncryptionNotSupportedByClient);
        }
        static internal string EncryptionNotSupportedByServer()
        {
            return StringsHelper.GetString(Strings.SQL_EncryptionNotSupportedByServer);
        }
        static internal string CTAIPNotSupportedByServer()
        {
            return StringsHelper.GetString(Strings.SQL_CTAIPNotSupportedByServer);
        }
        static internal string OperationCancelled()
        {
            return StringsHelper.GetString(Strings.SQL_OperationCancelled);
        }
        static internal string SevereError()
        {
            return StringsHelper.GetString(Strings.SQL_SevereError);
        }
        static internal string SSPIInitializeError()
        {
            return StringsHelper.GetString(Strings.SQL_SSPIInitializeError);
        }
        static internal string SSPIGenerateError()
        {
            return StringsHelper.GetString(Strings.SQL_SSPIGenerateError);
        }
        static internal string Timeout()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_Execution);
        }
        static internal string Timeout_PreLogin_Begin()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_PreLogin_Begin);
        }
        static internal string Timeout_PreLogin_InitializeConnection()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_PreLogin_InitializeConnection);
        }
        static internal string Timeout_PreLogin_SendHandshake()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_PreLogin_SendHandshake);
        }
        static internal string Timeout_PreLogin_ConsumeHandshake()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_PreLogin_ConsumeHandshake);
        }
        static internal string Timeout_Login_Begin()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_Login_Begin);
        }
        static internal string Timeout_Login_ProcessConnectionAuth()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_Login_ProcessConnectionAuth);
        }
        static internal string Timeout_PostLogin()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_PostLogin);
        }
        static internal string Timeout_FailoverInfo()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_FailoverInfo);
        }
        static internal string Timeout_RoutingDestination()
        {
            return StringsHelper.GetString(Strings.SQL_Timeout_RoutingDestinationInfo);
        }
        static internal string Duration_PreLogin_Begin(long PreLoginBeginDuration)
        {
            return StringsHelper.GetString(Strings.SQL_Duration_PreLogin_Begin, PreLoginBeginDuration);
        }
        static internal string Duration_PreLoginHandshake(long PreLoginBeginDuration, long PreLoginHandshakeDuration)
        {
            return StringsHelper.GetString(Strings.SQL_Duration_PreLoginHandshake, PreLoginBeginDuration, PreLoginHandshakeDuration);
        }
        static internal string Duration_Login_Begin(long PreLoginBeginDuration, long PreLoginHandshakeDuration, long LoginBeginDuration)
        {
            return StringsHelper.GetString(Strings.SQL_Duration_Login_Begin, PreLoginBeginDuration, PreLoginHandshakeDuration, LoginBeginDuration);
        }
        static internal string Duration_Login_ProcessConnectionAuth(long PreLoginBeginDuration, long PreLoginHandshakeDuration, long LoginBeginDuration, long LoginAuthDuration)
        {
            return StringsHelper.GetString(Strings.SQL_Duration_Login_ProcessConnectionAuth, PreLoginBeginDuration, PreLoginHandshakeDuration, LoginBeginDuration, LoginAuthDuration);
        }
        static internal string Duration_PostLogin(long PreLoginBeginDuration, long PreLoginHandshakeDuration, long LoginBeginDuration, long LoginAuthDuration, long PostLoginDuration)
        {
            return StringsHelper.GetString(Strings.SQL_Duration_PostLogin, PreLoginBeginDuration, PreLoginHandshakeDuration, LoginBeginDuration, LoginAuthDuration, PostLoginDuration);
        }
        static internal string UserInstanceFailure()
        {
            return StringsHelper.GetString(Strings.SQL_UserInstanceFailure);
        }
        static internal string PreloginError()
        {
            return StringsHelper.GetString(Strings.Snix_PreLogin);
        }
        static internal string ExClientConnectionId()
        {
            return StringsHelper.GetString(Strings.SQL_ExClientConnectionId);
        }
        static internal string ExErrorNumberStateClass()
        {
            return StringsHelper.GetString(Strings.SQL_ExErrorNumberStateClass);
        }
        static internal string ExOriginalClientConnectionId()
        {
            return StringsHelper.GetString(Strings.SQL_ExOriginalClientConnectionId);
        }
        static internal string ExRoutingDestination()
        {
            return StringsHelper.GetString(Strings.SQL_ExRoutingDestination);
        }
    }

    /// <summary>
    /// This class holds helper methods to escape Microsoft SQL Server identifiers, such as table, schema, database or other names
    /// </summary>
    static internal class SqlServerEscapeHelper
    {

        /// <summary>
        /// Escapes the identifier with square brackets. The input has to be in unescaped form, like the parts received from MultipartIdentifier.ParseMultipartIdentifier.
        /// </summary>
        /// <param name="name">name of the identifier, in unescaped form</param>
        /// <returns>escapes the name with [], also escapes the last close bracket with double-bracket</returns>
        static internal string EscapeIdentifier(string name)
        {
            Debug.Assert(!ADP.IsEmpty(name), "null or empty identifiers are not allowed");
            return "[" + name.Replace("]", "]]") + "]";
        }

        /// <summary>
        /// Same as above EscapeIdentifier, except that output is written into StringBuilder
        /// </summary>
        static internal void EscapeIdentifier(StringBuilder builder, string name)
        {
            Debug.Assert(builder != null, "builder cannot be null");
            Debug.Assert(!ADP.IsEmpty(name), "null or empty identifiers are not allowed");

            builder.Append("[");
            builder.Append(name.Replace("]", "]]"));
            builder.Append("]");
        }

        /// <summary>
        ///  Escape a string to be used inside TSQL literal, such as N'somename' or 'somename'
        /// </summary>
        static internal string EscapeStringAsLiteral(string input)
        {
            Debug.Assert(input != null, "input string cannot be null");
            return input.Replace("'", "''");
        }

        /// <summary>
        /// Escape a string as a TSQL literal, wrapping it around with single quotes.
        /// Use this method to escape input strings to prevent SQL injection
        /// and to get correct behavior for embedded quotes.
        /// </summary>
        /// <param name="input">unescaped string</param>
        /// <returns>escaped and quoted literal string</returns>
        static internal string MakeStringLiteral(string input)
        {
            if (ADP.IsEmpty(input))
            {
                return "''";
            }
            else
            {
                return "'" + EscapeStringAsLiteral(input) + "'";
            }
        }
    }

    /// <summary>
    /// This class holds methods invoked on System.Transactions through reflection for Global Transactions
    /// </summary>
    static internal class SysTxForGlobalTransactions
    {

        private static readonly Lazy<MethodInfo> _enlistPromotableSinglePhase = new Lazy<MethodInfo>(() =>
            typeof(SysTx.Transaction).GetMethod("EnlistPromotableSinglePhase", new Type[] { typeof(SysTx.IPromotableSinglePhaseNotification), typeof(Guid) }));

        private static readonly Lazy<MethodInfo> _setDistributedTransactionIdentifier = new Lazy<MethodInfo>(() =>
            typeof(SysTx.Transaction).GetMethod("SetDistributedTransactionIdentifier", new Type[] { typeof(SysTx.IPromotableSinglePhaseNotification), typeof(Guid) }));

        private static readonly Lazy<MethodInfo> _getPromotedToken = new Lazy<MethodInfo>(() =>
            typeof(SysTx.Transaction).GetMethod("GetPromotedToken"));

        /// <summary>
        /// Enlists the given IPromotableSinglePhaseNotification and Non-MSDTC Promoter type into a transaction
        /// </summary>
        /// <returns>The MethodInfo instance to be invoked. Null if the method doesn't exist</returns>
        public static MethodInfo EnlistPromotableSinglePhase
        {
            get
            {
                return _enlistPromotableSinglePhase.Value;
            }
        }

        /// <summary>
        /// Sets the given DistributedTransactionIdentifier for a Transaction instance.
        /// Needs to be invoked when using a Non-MSDTC Promoter type
        /// </summary>
        /// <returns>The MethodInfo instance to be invoked. Null if the method doesn't exist</returns>
        public static MethodInfo SetDistributedTransactionIdentifier
        {
            get
            {
                return _setDistributedTransactionIdentifier.Value;
            }
        }

        /// <summary>
        /// Gets the Promoted Token for a Transaction
        /// </summary>
        /// <returns>The MethodInfo instance to be invoked. Null if the method doesn't exist</returns>
        public static MethodInfo GetPromotedToken
        {
            get
            {
                return _getPromotedToken.Value;
            }
        }
    }
}//namespace
