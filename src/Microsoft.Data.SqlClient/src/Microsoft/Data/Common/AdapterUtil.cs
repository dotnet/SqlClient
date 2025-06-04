// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.SqlClient;
using IsolationLevel = System.Data.IsolationLevel;
using Microsoft.SqlServer.Server;
using System.Security.Authentication;

#if NETFRAMEWORK
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Interop.Windows.Kernel32;
#endif

namespace Microsoft.Data.Common
{
    /// <summary>
    /// The class ADP defines the exceptions that are specific to the Adapters.
    /// The class contains functions that take the proper informational variables and then construct
    /// the appropriate exception with an error string obtained from the resource framework.
    /// The exception is then returned to the caller, so that the caller may then throw from its
    /// location so that the catcher of the exception will have the appropriate call stack.
    /// This class is used so that there will be compile time checking of error messages.
    /// The resource Framework.txt will ensure proper string text based on the appropriate locale.
    /// </summary>
    internal static partial class ADP
    {
        // NOTE: Initializing a Task in SQL CLR requires the "UNSAFE" permission set (http://msdn.microsoft.com/en-us/library/ms172338.aspx)
        // Therefore we are lazily initializing these Tasks to avoid forcing customers to use the "UNSAFE" set when they are actually using no Async features
        // @TODO: These are not necessary because the TPL has optimized commonly used task return values like true and false.
        private static Task<bool> s_trueTask;
        internal static Task<bool> TrueTask => s_trueTask ??= Task.FromResult(true);

        private static Task<bool> s_falseTask;
        internal static Task<bool> FalseTask => s_falseTask ??= Task.FromResult(false);

        internal const CompareOptions DefaultCompareOptions = CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth | CompareOptions.IgnoreCase;
        internal const int DefaultConnectionTimeout = DbConnectionStringDefaults.ConnectTimeout;
        /// <summary>
        /// Infinite connection timeout identifier in seconds
        /// </summary>
        internal const int InfiniteConnectionTimeout = 0;
        /// <summary>
        /// Max duration for buffer in seconds
        /// </summary>
        internal const int MaxBufferAccessTokenExpiry = 600;

        #region UDT
#if NETFRAMEWORK
        private static readonly MethodInfo s_method = typeof(InvalidUdtException).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static);
#endif
        /// <summary>
        /// Calls "InvalidUdtException.Create" method when an invalid UDT occurs.
        /// </summary>
        internal static InvalidUdtException CreateInvalidUdtException(Type udtType, string resourceReasonName)
        {
            InvalidUdtException e =
#if NETFRAMEWORK
                (InvalidUdtException)s_method.Invoke(null, new object[] { udtType, resourceReasonName });
            ADP.TraceExceptionAsReturnValue(e);
#else
                InvalidUdtException.Create(udtType, resourceReasonName);
#endif
            return e;
        }
        #endregion

        static private void TraceException(string trace, Exception e)
        {
            Debug.Assert(e != null, "TraceException: null Exception");
            if (e is not null)
            {
                SqlClientEventSource.Log.TryTraceEvent(trace, e);
            }
        }

        internal static void TraceExceptionAsReturnValue(Exception e)
        {
            TraceException("<comm.ADP.TraceException|ERR|THROW> '{0}'", e);
        }

        internal static void TraceExceptionWithoutRethrow(Exception e)
        {
            Debug.Assert(IsCatchableExceptionType(e), "Invalid exception type, should have been re-thrown!");
            TraceException("<comm.ADP.TraceException|ERR|CATCH> '{0}'", e);
        }

        internal static bool IsEmptyArray(string[] array) => (array is null) || (array.Length == 0);

        internal static bool IsNull(object value)
        {
            if ((value is null) || (DBNull.Value == value))
            {
                return true;
            }
            INullable nullable = (value as INullable);
            return ((nullable is not null) && nullable.IsNull);
        }

        internal static Exception ExceptionWithStackTrace(Exception e)
        {
            try
            {
                throw e;
            }
            catch (Exception caught)
            {
                return caught;
            }
        }

        internal static Timer UnsafeCreateTimer(TimerCallback callback, object state, int dueTime, int period)
        {
            // Don't capture the current ExecutionContext and its AsyncLocals onto 
            // a global timer causing them to live forever
            bool restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                return new Timer(callback, state, dueTime, period);
            }
            finally
            {
                // Restore the current ExecutionContext
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

#region COM+ exceptions
        internal static ArgumentException Argument(string error)
        {
            ArgumentException e = new(error);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentException Argument(string error, Exception inner)
        {
            ArgumentException e = new(error, inner);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentException Argument(string error, string parameter)
        {
            ArgumentException e = new(error, parameter);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentNullException ArgumentNull(string parameter)
        {
            ArgumentNullException e = new(parameter);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentNullException ArgumentNull(string parameter, string error)
        {
            ArgumentNullException e = new(parameter, error);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentOutOfRangeException ArgumentOutOfRange(string parameterName)
        {
            ArgumentOutOfRangeException e = new(parameterName);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentOutOfRangeException ArgumentOutOfRange(string message, string parameterName)
        {
            ArgumentOutOfRangeException e = new(parameterName, message);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static IndexOutOfRangeException IndexOutOfRange(string error)
        {
            IndexOutOfRangeException e = new(error);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static IndexOutOfRangeException IndexOutOfRange(int value)
        {
            IndexOutOfRangeException e = new(value.ToString(CultureInfo.InvariantCulture));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static IndexOutOfRangeException IndexOutOfRange()
        {
            IndexOutOfRangeException e = new();
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static InvalidOperationException InvalidOperation(string error, Exception inner)
        {
            InvalidOperationException e = new(error, inner);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static OverflowException Overflow(string error) => Overflow(error, null);

        internal static OverflowException Overflow(string error, Exception inner)
        {
            OverflowException e = new(error, inner);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static TimeoutException TimeoutException(string error, Exception inner = null)
        {
            TimeoutException e = new(error, inner);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static TypeLoadException TypeLoad(string error)
        {
            TypeLoadException e = new(error);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static InvalidCastException InvalidCast()
        {
            InvalidCastException e = new();
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static InvalidCastException InvalidCast(string error)
        {
            return InvalidCast(error, null);
        }

        internal static InvalidCastException InvalidCast(string error, Exception inner)
        {
            InvalidCastException e = new(error, inner);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static InvalidOperationException InvalidOperation(string error)
        {
            InvalidOperationException e = new(error);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static IOException IO(string error)
        {
            IOException e = new(error);
            TraceExceptionAsReturnValue(e);
            return e;
        }
        internal static IOException IO(string error, Exception inner)
        {
            IOException e = new(error, inner);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static NotSupportedException NotSupported()
        {
            NotSupportedException e = new();
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static NotSupportedException NotSupported(string error)
        {
            NotSupportedException e = new(error);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static InvalidOperationException DataAdapter(string error) => InvalidOperation(error);

        private static InvalidOperationException Provider(string error) => InvalidOperation(error);

        internal static ArgumentException InvalidMultipartName(string property, string value)
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_InvalidMultipartName, StringsHelper.GetString(property), value));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentException InvalidMultipartNameIncorrectUsageOfQuotes(string property, string value)
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_InvalidMultipartNameQuoteUsage, StringsHelper.GetString(property), value));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentException InvalidMultipartNameToManyParts(string property, string value, int limit)
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_InvalidMultipartNameToManyParts, StringsHelper.GetString(property), value, limit));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ObjectDisposedException ObjectDisposed(object instance)
        {
            ObjectDisposedException e = new(instance.GetType().Name);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static InvalidOperationException MethodCalledTwice(string method)
        {
            InvalidOperationException e = new(StringsHelper.GetString(Strings.ADP_CalledTwice, method));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentOutOfRangeException ArgumentOutOfRange(string message, string parameterName, object value)
        {
            ArgumentOutOfRangeException e = new(parameterName, value, message);
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static AuthenticationException SSLCertificateAuthenticationException(string message)
        {
            AuthenticationException e = new(message);
            TraceExceptionAsReturnValue(e);
            return e;
        }
        #endregion

        #region Helper Functions
        internal static ArgumentOutOfRangeException NotSupportedEnumerationValue(Type type, string value, string method)
            => ArgumentOutOfRange(StringsHelper.GetString(Strings.ADP_NotSupportedEnumerationValue, type.Name, value, method), type.Name);

        internal static void CheckArgumentNull(object value, string parameterName)
        {
            if (value is null)
            {
                throw ArgumentNull(parameterName);
            }
        }

        internal static bool IsCatchableExceptionType(Exception e)
        {
            // only StackOverflowException & ThreadAbortException are sealed classes
            // a 'catchable' exception is defined by what it is not.
            Debug.Assert(e != null, "Unexpected null exception!");
            Type type = e.GetType();

            return ((type != typeof(StackOverflowException)) &&
                    (type != typeof(OutOfMemoryException)) &&
                    (type != typeof(ThreadAbortException)) &&
                    (type != typeof(NullReferenceException)) &&
                    (type != typeof(AccessViolationException)) &&
                    !typeof(SecurityException).IsAssignableFrom(type));
        }

        internal static bool IsCatchableOrSecurityExceptionType(Exception e)
        {
            // a 'catchable' exception is defined by what it is not.
            // since IsCatchableExceptionType defined SecurityException as not 'catchable'
            // this method will return true for SecurityException has being catchable.

            // the other way to write this method is, but then SecurityException is checked twice
            // return ((e is SecurityException) || IsCatchableExceptionType(e));

            // only StackOverflowException & ThreadAbortException are sealed classes
            Debug.Assert(e != null, "Unexpected null exception!");
            Type type = e.GetType();

            return ((type != typeof(StackOverflowException)) &&
                    (type != typeof(OutOfMemoryException)) &&
                    (type != typeof(ThreadAbortException)) &&
                    (type != typeof(NullReferenceException)) &&
                    (type != typeof(AccessViolationException)));
        }

        // Invalid Enumeration
        internal static ArgumentOutOfRangeException InvalidEnumerationValue(Type type, int value)
            => ArgumentOutOfRange(StringsHelper.GetString(Strings.ADP_InvalidEnumerationValue, type.Name, value.ToString(CultureInfo.InvariantCulture)), type.Name);

        internal static ArgumentOutOfRangeException InvalidCommandBehavior(CommandBehavior value)
        {
            Debug.Assert((0 > (int)value) || ((int)value > 0x3F), "valid CommandType " + value.ToString());

            return InvalidEnumerationValue(typeof(CommandBehavior), (int)value);
        }

        internal static void ValidateCommandBehavior(CommandBehavior value)
        {
            if (((int)value < 0) || (0x3F < (int)value))
            {
                throw InvalidCommandBehavior(value);
            }
        }

        internal static ArgumentOutOfRangeException InvalidUserDefinedTypeSerializationFormat(Format value)
        {
#if DEBUG
            switch (value)
            {
                case Format.Unknown:
                case Format.Native:
                case Format.UserDefined:
                    Debug.Assert(false, "valid UserDefinedTypeSerializationFormat " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(Format), (int)value);
        }

        internal static ArgumentOutOfRangeException NotSupportedUserDefinedTypeSerializationFormat(Format value, string method)
            => NotSupportedEnumerationValue(typeof(Format), value.ToString(), method);

        internal static ArgumentException InvalidArgumentLength(string argumentName, int limit)
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidArgumentLength, argumentName, limit));

        internal static ArgumentException MustBeReadOnly(string argumentName) => Argument(StringsHelper.GetString(Strings.ADP_MustBeReadOnly, argumentName));

        // TODO(ADO-37652): This should be removed in favour of simply using
        // the SqlAuthenticationProviderException's message.
        internal static Exception CreateSqlException(SqlAuthenticationProviderException providerException, SqlConnectionString connectionOptions, SqlInternalConnectionTds sender, string username)
        {
            // Error[0]
            SqlErrorCollection sqlErs = new();

            sqlErs.Add(new SqlError(0, (byte)0x00, (byte)TdsEnums.MIN_ERROR_CLASS,
                                    connectionOptions.DataSource,
                                    StringsHelper.GetString(Strings.SQL_MSALFailure, username, connectionOptions.Authentication.ToString("G")),
                                    providerException.Action, 0));

            // Error[1]
            if (!string.IsNullOrEmpty(providerException.Message))
            {
                sqlErs.Add(new SqlError(0, (byte)0x00, (byte)TdsEnums.MIN_ERROR_CLASS,
                                        connectionOptions.DataSource, providerException.Message,
                                        providerException.Action, 0));
            }
            return SqlException.CreateException(sqlErs, "", sender, innerException: null, batchCommand: null);
        }

#endregion

#region CommandBuilder, Command, BulkCopy
        /// <summary>
        /// This allows the caller to determine if it is an error or not for the quotedString to not be quoted
        /// </summary>
        /// <returns>The return value is true if the string was quoted and false if it was not</returns>
        internal static bool RemoveStringQuotes(string quotePrefix, string quoteSuffix, string quotedString, out string unquotedString)
        {
            int prefixLength = quotePrefix is null ? 0 : quotePrefix.Length;
            int suffixLength = quoteSuffix is null ? 0 : quoteSuffix.Length;

            if ((suffixLength + prefixLength) == 0)
            {
                unquotedString = quotedString;
                return true;
            }

            if (quotedString is null)
            {
                unquotedString = quotedString;
                return false;
            }

            int quotedStringLength = quotedString.Length;

            // is the source string too short to be quoted
            if (quotedStringLength < prefixLength + suffixLength)
            {
                unquotedString = quotedString;
                return false;
            }

            // is the prefix present?
            if (prefixLength > 0)
            {
                if (!quotedString.StartsWith(quotePrefix, StringComparison.Ordinal))
                {
                    unquotedString = quotedString;
                    return false;
                }
            }

            // is the suffix present?
            if (suffixLength > 0)
            {
                if (!quotedString.EndsWith(quoteSuffix, StringComparison.Ordinal))
                {
                    unquotedString = quotedString;
                    return false;
                }
                unquotedString = quotedString.Substring(prefixLength, quotedStringLength - (prefixLength + suffixLength))
                                             .Replace(quoteSuffix + quoteSuffix, quoteSuffix);
            }
            else
            {
                unquotedString = quotedString.Substring(prefixLength, quotedStringLength - prefixLength);
            }
            return true;
        }

        internal static string BuildQuotedString(string quotePrefix, string quoteSuffix, string unQuotedString)
        {
            var resultString = new StringBuilder(unQuotedString.Length + quoteSuffix.Length + quoteSuffix.Length);
            AppendQuotedString(resultString, quotePrefix, quoteSuffix, unQuotedString);
            return resultString.ToString();
        }

        internal static string AppendQuotedString(StringBuilder buffer, string quotePrefix, string quoteSuffix, string unQuotedString)
        {
            Debug.Assert(buffer is not null, "buffer parameter must be initialized!");

            if (!string.IsNullOrEmpty(quotePrefix))
            {
                buffer.Append(quotePrefix);
            }

            // Assuming that the suffix is escaped by doubling it. i.e. foo"bar becomes "foo""bar".
            if (!string.IsNullOrEmpty(quoteSuffix))
            {
                int start = buffer.Length;
                buffer.Append(unQuotedString);
                buffer.Replace(quoteSuffix, quoteSuffix + quoteSuffix, start, unQuotedString.Length);
                buffer.Append(quoteSuffix);
            }
            else
            {
                buffer.Append(unQuotedString);
            }

            return buffer.ToString();
        }

        internal static string BuildMultiPartName(string[] strings)
        {
            StringBuilder bld = new();
            // Assume we want to build a full multi-part name with all parts except trimming separators for
            // leading empty names (null or empty strings, but not whitespace). Separators in the middle 
            // should be added, even if the name part is null/empty, to maintain proper location of the parts.
            for (int i = 0; i < strings.Length; i++)
            {
                if (0 < bld.Length)
                {
                    bld.Append('.');
                }
                if (strings[i] is not null && 0 != strings[i].Length)
                {
                    bld.Append(BuildQuotedString("[", "]", strings[i]));
                }
            }
            return bld.ToString();
        }

        // global constant strings
        internal const string ColumnEncryptionSystemProviderNamePrefix = "MSSQL_";
        internal const string Command = "Command";
        internal const string Connection = "Connection";
        internal const string Parameter = "Parameter";
        internal const string ParameterName = "ParameterName";
        internal const string ParameterSetPosition = "set_Position";

        internal const int DefaultCommandTimeout = 30;
        internal const float FailoverTimeoutStep = 0.08F;    // fraction of timeout to use for fast failover connections

        internal const int CharSize = UnicodeEncoding.CharSize;

        internal static Delegate FindBuilder(MulticastDelegate mcd)
        {
            foreach (Delegate del in mcd?.GetInvocationList())
            {
                if (del.Target is DbCommandBuilder)
                    return del;
            }

            return null;
        }

        internal static long TimerCurrent() => DateTime.UtcNow.ToFileTimeUtc();

        internal static long TimerFromSeconds(int seconds)
        {
            long result = checked((long)seconds * TimeSpan.TicksPerSecond);
            return result;
        }

        internal static long TimerFromMilliseconds(long milliseconds)
        {
            long result = checked(milliseconds * TimeSpan.TicksPerMillisecond);
            return result;
        }

        internal static bool TimerHasExpired(long timerExpire)
        {
            bool result = TimerCurrent() > timerExpire;
            return result;
        }

        internal static long TimerRemaining(long timerExpire)
        {
            long timerNow = TimerCurrent();
            long result = checked(timerExpire - timerNow);
            return result;
        }

        internal static long TimerRemainingMilliseconds(long timerExpire)
        {
            long result = TimerToMilliseconds(TimerRemaining(timerExpire));
            return result;
        }

        internal static long TimerRemainingSeconds(long timerExpire)
        {
            long result = TimerToSeconds(TimerRemaining(timerExpire));
            return result;
        }

        internal static long TimerToMilliseconds(long timerValue)
        {
            long result = timerValue / TimeSpan.TicksPerMillisecond;
            return result;
        }

        private static long TimerToSeconds(long timerValue)
        {
            long result = timerValue / TimeSpan.TicksPerSecond;
            return result;
        }

        /// <summary>
        /// Note: In Longhorn you'll be able to rename a machine without
        /// rebooting.  Therefore, don't cache this machine name.
        /// </summary>
#if NETFRAMEWORK
        [EnvironmentPermission(SecurityAction.Assert, Read = "COMPUTERNAME")]
#endif
        internal static string MachineName() => Environment.MachineName;

        internal static Transaction GetCurrentTransaction()
        {
            Transaction transaction = Transaction.Current;
            return transaction;
        }

        internal static bool IsDirection(DbParameter value, ParameterDirection condition)
        {
#if DEBUG
            switch (condition)
            { // @perfnote: Enum.IsDefined
                case ParameterDirection.Input:
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                case ParameterDirection.ReturnValue:
                    break;
                default:
                    throw ADP.InvalidParameterDirection(condition);
            }
#endif
            return (condition == (condition & value.Direction));
        }

        internal static void IsNullOrSqlType(object value, out bool isNull, out bool isSqlType)
        {
            if ((value is null) || (value == DBNull.Value))
            {
                isNull = true;
                isSqlType = false;
            }
            else
            {
                if (value is INullable nullable)
                {
                    isNull = nullable.IsNull;
                    // Duplicated from DataStorage.cs
                    // For back-compat, SqlXml is not in this list
                    isSqlType = ((value is SqlBinary) ||
                                (value is SqlBoolean) ||
                                (value is SqlByte) ||
                                (value is SqlBytes) ||
                                (value is SqlChars) ||
                                (value is SqlDateTime) ||
                                (value is SqlDecimal) ||
                                (value is SqlDouble) ||
                                (value is SqlGuid) ||
                                (value is SqlInt16) ||
                                (value is SqlInt32) ||
                                (value is SqlInt64) ||
                                (value is SqlMoney) ||
                                (value is SqlSingle) ||
                                (value is SqlString));
                }
                else
                {
                    isNull = false;
                    isSqlType = false;
                }
            }
        }

        private static Version s_systemDataVersion;

        internal static Version GetAssemblyVersion()
        {
            // NOTE: Using lazy thread-safety since we don't care if two threads both happen to update the value at the same time
            if (s_systemDataVersion is null)
            {
                s_systemDataVersion = new Version(ThisAssembly.InformationalVersion);
            }

            return s_systemDataVersion;
        }


        private const string ONDEMAND_PREFIX = "-ondemand";
        private const string AZURE_SYNAPSE = "-ondemand.sql.azuresynapse.";
        private const string FABRIC_DATAWAREHOUSE = "datawarehouse.fabric.microsoft.com";
        private const string PBI_DATAWAREHOUSE = "datawarehouse.pbidedicated.microsoft.com";
        private const string PBI_DATAWAREHOUSE2 = ".pbidedicated.microsoft.com";
        private const string PBI_DATAWAREHOUSE3 = ".pbidedicated.windows.net";
        private const string AZURE_SQL = ".database.windows.net";
        private const string AZURE_SQL_GERMANY = ".database.cloudapi.de";
        private const string AZURE_SQL_USGOV = ".database.usgovcloudapi.net";
        private const string AZURE_SQL_CHINA = ".database.chinacloudapi.cn";
        private const string AZURE_SQL_FABRIC = ".database.fabric.microsoft.com";

        /// <summary>
        /// Represents a collection of Azure SQL Server endpoint URLs for various regions and environments.
        /// </summary>
        /// <remarks>This array includes endpoint URLs for Azure SQL in global, Germany, US Government,
        /// China, and Fabric environments. These endpoints are used to identify and interact with Azure SQL services 
        /// in their respective regions or environments.</remarks>
        internal static readonly string[] s_azureSqlServerEndpoints = { AZURE_SQL,
                                                                        AZURE_SQL_GERMANY,
                                                                        AZURE_SQL_USGOV,
                                                                        AZURE_SQL_CHINA,
                                                                        AZURE_SQL_FABRIC };
        
        /// <summary>
        /// Contains endpoint strings for Azure SQL Server on-demand services.
        /// Each entry is a combination of the ONDEMAND_PREFIX and a specific Azure SQL endpoint string.
        /// Example format: "ondemand.database.windows.net".
        /// </summary>
        internal static readonly string[] s_azureSqlServerOnDemandEndpoints = { ONDEMAND_PREFIX + AZURE_SQL,
                                                                                ONDEMAND_PREFIX + AZURE_SQL_GERMANY,
                                                                                ONDEMAND_PREFIX + AZURE_SQL_USGOV,
                                                                                ONDEMAND_PREFIX + AZURE_SQL_CHINA,
                                                                                ONDEMAND_PREFIX + AZURE_SQL_FABRIC };
        /// <summary>
        /// Represents a collection of endpoint identifiers for Azure Synapse and related services.
        /// </summary>
        /// <remarks>This array contains predefined endpoint strings used to identify Azure Synapse and
        /// associated services, such as Fabric Data Warehouse and Power BI Data Warehouse.</remarks>
        internal static readonly string[] s_azureSynapseEndpoints = { FABRIC_DATAWAREHOUSE,
                                                                      PBI_DATAWAREHOUSE,
                                                                      PBI_DATAWAREHOUSE2,
                                                                      PBI_DATAWAREHOUSE3 };

        internal static readonly string[] s_azureSynapseOnDemandEndpoints = [.. s_azureSqlServerOnDemandEndpoints, .. s_azureSynapseEndpoints];

        internal static bool IsAzureSynapseOnDemandEndpoint(string dataSource)
        {
            return IsEndpoint(dataSource, s_azureSynapseOnDemandEndpoints)
                || dataSource.IndexOf(AZURE_SYNAPSE, StringComparison.OrdinalIgnoreCase) >= 0; 
        }
        
        internal static bool IsAzureSqlServerEndpoint(string dataSource)
        {
            return IsEndpoint(dataSource, s_azureSqlServerEndpoints);
        }

        // This method assumes dataSource parameter is in TCP connection string format.
        private static bool IsEndpoint(string dataSource, string[] endpoints)
        {
            int length = dataSource.Length;
            // remove server port
            int foundIndex = dataSource.LastIndexOf(',');
            if (foundIndex >= 0)
            {
                length = foundIndex;
            }

            // Safeguard LastIndexOf call to avoid ArgumentOutOfRangeException when length is 0
            if (length > 0)
            {
                // check for the instance name
                foundIndex = dataSource.LastIndexOf('\\', length - 1, length - 1);
            }
            else
            {
                foundIndex = -1;
            }

            if (foundIndex > 0)
            {
                length = foundIndex;
            }

            // trim trailing whitespace
            while (length > 0 && char.IsWhiteSpace(dataSource[length - 1]))
            {
                length -= 1;
            }

            // check if servername ends with any endpoints
            foreach (var endpoint in endpoints)
            {
                if (length >= endpoint.Length)
                {
                    if (string.Compare(dataSource, length - endpoint.Length, endpoint, 0, endpoint.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static ArgumentException SingleValuedProperty(string propertyName, string value)
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_SingleValuedProperty, propertyName, value));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentException DoubleValuedProperty(string propertyName, string value1, string value2)
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_DoubleValuedProperty, propertyName, value1, value2));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static ArgumentException InvalidPrefixSuffix()
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_InvalidPrefixSuffix));
            TraceExceptionAsReturnValue(e);
            return e;
        }
#endregion

#region DbConnectionOptions, DataAccess
        internal static ArgumentException ConnectionStringSyntax(int index) => Argument(StringsHelper.GetString(Strings.ADP_ConnectionStringSyntax, index));

        internal static ArgumentException KeywordNotSupported(string keyword) => Argument(StringsHelper.GetString(Strings.ADP_KeywordNotSupported, keyword));

        internal static Exception InvalidConnectionOptionValue(string key) => InvalidConnectionOptionValue(key, null);

        internal static Exception InvalidConnectionOptionValue(string key, Exception inner)
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidConnectionOptionValue, key), inner);

        internal static Exception InvalidConnectionOptionValueLength(string key, int limit)
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidConnectionOptionValueLength, key, limit));

        internal static Exception MissingConnectionOptionValue(string key, string requiredAdditionalKey)
            => Argument(StringsHelper.GetString(Strings.ADP_MissingConnectionOptionValue, key, requiredAdditionalKey));

        internal static InvalidOperationException InvalidDataDirectory() => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidDataDirectory));

        internal static ArgumentException CollectionRemoveInvalidObject(Type itemType, ICollection collection)
            => Argument(StringsHelper.GetString(Strings.ADP_CollectionRemoveInvalidObject, itemType.Name, collection.GetType().Name)); // MDAC 68201

        internal static ArgumentNullException CollectionNullValue(string parameter, Type collection, Type itemType)
            => ArgumentNull(parameter, StringsHelper.GetString(Strings.ADP_CollectionNullValue, collection.Name, itemType.Name));

        internal static IndexOutOfRangeException CollectionIndexInt32(int index, Type collection, int count)
            => IndexOutOfRange(StringsHelper.GetString(Strings.ADP_CollectionIndexInt32, index.ToString(CultureInfo.InvariantCulture), collection.Name, count.ToString(CultureInfo.InvariantCulture)));

        internal static IndexOutOfRangeException CollectionIndexString(Type itemType, string propertyName, string propertyValue, Type collection)
            => IndexOutOfRange(StringsHelper.GetString(Strings.ADP_CollectionIndexString, itemType.Name, propertyName, propertyValue, collection.Name));

        internal static InvalidCastException CollectionInvalidType(Type collection, Type itemType, object invalidValue)
            => InvalidCast(StringsHelper.GetString(Strings.ADP_CollectionInvalidType, collection.Name, itemType.FullName, invalidValue.GetType().FullName));

        internal static ArgumentException ConvertFailed(Type fromType, Type toType, Exception innerException)
            => ADP.Argument(StringsHelper.GetString(Strings.SqlConvert_ConvertFailed, fromType.FullName, toType.FullName), innerException);

        internal static ArgumentException InvalidMinMaxPoolSizeValues()
            => ADP.Argument(StringsHelper.GetString(Strings.ADP_InvalidMinMaxPoolSizeValues));
#endregion

#region DbConnection
        private static string ConnectionStateMsg(ConnectionState state)
        { // MDAC 82165, if the ConnectionState enum to msg the localization looks weird
            return state switch
            {
                (ConnectionState.Closed) => StringsHelper.GetString(Strings.ADP_ConnectionStateMsg_Closed),
                (ConnectionState.Connecting | ConnectionState.Broken) => StringsHelper.GetString(Strings.ADP_ConnectionStateMsg_Closed),
                (ConnectionState.Connecting) => StringsHelper.GetString(Strings.ADP_ConnectionStateMsg_Connecting),
                (ConnectionState.Open) => StringsHelper.GetString(Strings.ADP_ConnectionStateMsg_Open),
                (ConnectionState.Open | ConnectionState.Executing) => StringsHelper.GetString(Strings.ADP_ConnectionStateMsg_OpenExecuting),
                (ConnectionState.Open | ConnectionState.Fetching) => StringsHelper.GetString(Strings.ADP_ConnectionStateMsg_OpenFetching),
                _ => StringsHelper.GetString(Strings.ADP_ConnectionStateMsg, state.ToString()),
            };
        }

        internal static InvalidOperationException NoConnectionString()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_NoConnectionString));

        internal static NotImplementedException MethodNotImplemented([CallerMemberName] string methodName = "")
        {
            NotImplementedException e = new(methodName);
            TraceExceptionAsReturnValue(e);
            return e;
        }
#endregion

#region Stream
        internal static Exception StreamClosed([CallerMemberName] string method = "") => InvalidOperation(StringsHelper.GetString(Strings.ADP_StreamClosed, method));

        static internal Exception InvalidSeekOrigin(string parameterName) => ArgumentOutOfRange(StringsHelper.GetString(Strings.ADP_InvalidSeekOrigin), parameterName);

        internal static IOException ErrorReadingFromStream(Exception internalException) => IO(StringsHelper.GetString(Strings.SqlMisc_StreamErrorMessage), internalException);
#endregion

#region Generic Data Provider Collection
        internal static ArgumentException ParametersIsNotParent(Type parameterType, ICollection collection)
            => Argument(StringsHelper.GetString(Strings.ADP_CollectionIsNotParent, parameterType.Name, collection.GetType().Name));

        internal static ArgumentException ParametersIsParent(Type parameterType, ICollection collection)
            => Argument(StringsHelper.GetString(Strings.ADP_CollectionIsNotParent, parameterType.Name, collection.GetType().Name));
#endregion

#region ConnectionUtil
        internal enum InternalErrorCode
        {
            UnpooledObjectHasOwner = 0,
            UnpooledObjectHasWrongOwner = 1,
            PushingObjectSecondTime = 2,
            PooledObjectHasOwner = 3,
            PooledObjectInPoolMoreThanOnce = 4,
            CreateObjectReturnedNull = 5,
            NewObjectCannotBePooled = 6,
            NonPooledObjectUsedMoreThanOnce = 7,
            AttemptingToPoolOnRestrictedToken = 8,
            //          ConnectionOptionsInUse                                  =  9,
            ConvertSidToStringSidWReturnedNull = 10,
            //          UnexpectedTransactedObject                              = 11,
            AttemptingToConstructReferenceCollectionOnStaticObject = 12,
            AttemptingToEnlistTwice = 13,
            CreateReferenceCollectionReturnedNull = 14,
            PooledObjectWithoutPool = 15,
            UnexpectedWaitAnyResult = 16,
            SynchronousConnectReturnedPending = 17,
            CompletedConnectReturnedPending = 18,

            NameValuePairNext = 20,
            InvalidParserState1 = 21,
            InvalidParserState2 = 22,
            InvalidParserState3 = 23,

            InvalidBuffer = 30,

            UnimplementedSMIMethod = 40,
            InvalidSmiCall = 41,

            SqlDependencyObtainProcessDispatcherFailureObjectHandle = 50,
            SqlDependencyProcessDispatcherFailureCreateInstance = 51,
            
            SqlDependencyCommandHashIsNotAssociatedWithNotification = 53,

            UnknownTransactionFailure = 60,
        }

        internal static Exception InternalError(InternalErrorCode internalError)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InternalProviderError, (int)internalError));

        internal static Exception ClosedConnectionError() => InvalidOperation(StringsHelper.GetString(Strings.ADP_ClosedConnectionError));
        internal static Exception ConnectionAlreadyOpen(ConnectionState state)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_ConnectionAlreadyOpen, ADP.ConnectionStateMsg(state)));

        internal static Exception TransactionPresent() => InvalidOperation(StringsHelper.GetString(Strings.ADP_TransactionPresent));

        internal static Exception LocalTransactionPresent() => InvalidOperation(StringsHelper.GetString(Strings.ADP_LocalTransactionPresent));

        internal static Exception OpenConnectionPropertySet(string property, ConnectionState state)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_OpenConnectionPropertySet, property, ADP.ConnectionStateMsg(state)));

        internal static Exception EmptyDatabaseName() => Argument(StringsHelper.GetString(Strings.ADP_EmptyDatabaseName));

        internal enum ConnectionError
        {
            BeginGetConnectionReturnsNull,
            GetConnectionReturnsNull,
            ConnectionOptionsMissing,
            CouldNotSwitchToClosedPreviouslyOpenedState,
        }

        internal static Exception InternalConnectionError(ConnectionError internalError)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InternalConnectionError, (int)internalError));

        internal static Exception InvalidConnectRetryCountValue() => Argument(StringsHelper.GetString(Strings.SQLCR_InvalidConnectRetryCountValue));

        internal static Exception InvalidConnectRetryIntervalValue() => Argument(StringsHelper.GetString(Strings.SQLCR_InvalidConnectRetryIntervalValue));
#endregion

#region DbDataReader
        internal static Exception DataReaderClosed([CallerMemberName] string method = "")
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_DataReaderClosed, method));

        internal static ArgumentOutOfRangeException InvalidSourceBufferIndex(int maxLen, long srcOffset, string parameterName)
            => ArgumentOutOfRange(StringsHelper.GetString(Strings.ADP_InvalidSourceBufferIndex,
                                                          maxLen.ToString(CultureInfo.InvariantCulture),
                                                          srcOffset.ToString(CultureInfo.InvariantCulture)), parameterName);

        internal static ArgumentOutOfRangeException InvalidDestinationBufferIndex(int maxLen, int dstOffset, string parameterName)
            => ArgumentOutOfRange(StringsHelper.GetString(Strings.ADP_InvalidDestinationBufferIndex,
                                                          maxLen.ToString(CultureInfo.InvariantCulture),
                                                          dstOffset.ToString(CultureInfo.InvariantCulture)), parameterName);

        internal static IndexOutOfRangeException InvalidBufferSizeOrIndex(int numBytes, int bufferIndex)
            => IndexOutOfRange(StringsHelper.GetString(Strings.SQL_InvalidBufferSizeOrIndex,
                                                       numBytes.ToString(CultureInfo.InvariantCulture),
                                                       bufferIndex.ToString(CultureInfo.InvariantCulture)));

        internal static Exception InvalidDataLength(long length)
            => IndexOutOfRange(StringsHelper.GetString(Strings.SQL_InvalidDataLength, length.ToString(CultureInfo.InvariantCulture)));

        internal static bool CompareInsensitiveInvariant(string strvalue, string strconst)
            => 0 == CultureInfo.InvariantCulture.CompareInfo.Compare(strvalue, strconst, CompareOptions.IgnoreCase);

        internal static int DstCompare(string strA, string strB) // this is null safe
            => CultureInfo.CurrentCulture.CompareInfo.Compare(strA, strB, ADP.DefaultCompareOptions);

        internal static void SetCurrentTransaction(Transaction transaction) => Transaction.Current = transaction;

        internal static Exception NonSeqByteAccess(long badIndex, long currIndex, string method)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_NonSeqByteAccess,
                                                        badIndex.ToString(CultureInfo.InvariantCulture),
                                                        currIndex.ToString(CultureInfo.InvariantCulture),
                                                        method));

        internal static Exception NegativeParameter(string parameterName) => InvalidOperation(StringsHelper.GetString(Strings.ADP_NegativeParameter, parameterName));

        internal static Exception InvalidXmlMissingColumn(string collectionName, string columnName)
            => Argument(StringsHelper.GetString(Strings.MDF_InvalidXmlMissingColumn, collectionName, columnName));

        internal static InvalidOperationException AsyncOperationPending() => InvalidOperation(StringsHelper.GetString(Strings.ADP_PendingAsyncOperation));
#endregion

#region IDbCommand
        // IDbCommand.CommandType
        static internal ArgumentOutOfRangeException InvalidCommandType(CommandType value)
        {
#if DEBUG
            switch (value)
            {
                case CommandType.Text:
                case CommandType.StoredProcedure:
                case CommandType.TableDirect:
                    Debug.Assert(false, "valid CommandType " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(CommandType), (int)value);
        }

        internal static Exception TooManyRestrictions(string collectionName)
            => Argument(StringsHelper.GetString(Strings.MDF_TooManyRestrictions, collectionName));

        internal static Exception CommandTextRequired(string method)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_CommandTextRequired, method));

        internal static Exception UninitializedParameterSize(int index, Type dataType)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_UninitializedParameterSize, index.ToString(CultureInfo.InvariantCulture), dataType.Name));

        internal static Exception PrepareParameterType(DbCommand cmd)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_PrepareParameterType, cmd.GetType().Name));

        internal static Exception PrepareParameterSize(DbCommand cmd)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_PrepareParameterSize, cmd.GetType().Name));

        internal static Exception PrepareParameterScale(DbCommand cmd, string type)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_PrepareParameterScale, cmd.GetType().Name, type));

        internal static Exception MismatchedAsyncResult(string expectedMethod, string gotMethod)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_MismatchedAsyncResult, expectedMethod, gotMethod));

        // IDataParameter.SourceVersion
        internal static ArgumentOutOfRangeException InvalidDataRowVersion(DataRowVersion value)
        {
#if DEBUG
            switch (value)
            {
                case DataRowVersion.Default:
                case DataRowVersion.Current:
                case DataRowVersion.Original:
                case DataRowVersion.Proposed:
                    Debug.Fail($"Invalid DataRowVersion {value}");
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(DataRowVersion), (int)value);
        }

        internal static ArgumentOutOfRangeException NotSupportedCommandBehavior(CommandBehavior value, string method)
            => NotSupportedEnumerationValue(typeof(CommandBehavior), value.ToString(), method);

        internal static ArgumentException BadParameterName(string parameterName)
        {
            ArgumentException e = new(StringsHelper.GetString(Strings.ADP_BadParameterName, parameterName));
            TraceExceptionAsReturnValue(e);
            return e;
        }

        internal static Exception DeriveParametersNotSupported(IDbCommand value)
            => DataAdapter(StringsHelper.GetString(Strings.ADP_DeriveParametersNotSupported, value.GetType().Name, value.CommandType.ToString()));

        internal static Exception NoStoredProcedureExists(string sproc) => InvalidOperation(StringsHelper.GetString(Strings.ADP_NoStoredProcedureExists, sproc));
#endregion

#region DbMetaDataFactory
        internal static Exception DataTableDoesNotExist(string collectionName)
            => Argument(StringsHelper.GetString(Strings.MDF_DataTableDoesNotExist, collectionName));

        // IDbCommand.UpdateRowSource
        internal static ArgumentOutOfRangeException InvalidUpdateRowSource(UpdateRowSource value)
        {
#if DEBUG
            switch (value)
            {
                case UpdateRowSource.None:
                case UpdateRowSource.OutputParameters:
                case UpdateRowSource.FirstReturnedRecord:
                case UpdateRowSource.Both:
                    Debug.Fail("valid UpdateRowSource " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(UpdateRowSource), (int)value);
        }

        internal static Exception QueryFailed(string collectionName, Exception e)
            => InvalidOperation(StringsHelper.GetString(Strings.MDF_QueryFailed, collectionName), e);

        internal static Exception NoColumns() => Argument(StringsHelper.GetString(Strings.MDF_NoColumns));

        internal static InvalidOperationException ConnectionRequired(string method)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_ConnectionRequired, method));

        internal static InvalidOperationException OpenConnectionRequired(string method, ConnectionState state)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_OpenConnectionRequired, method, ADP.ConnectionStateMsg(state)));

        internal static Exception OpenReaderExists(bool marsOn) => OpenReaderExists(null, marsOn);

        internal static Exception OpenReaderExists(Exception e, bool marsOn)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_OpenReaderExists, marsOn ? ADP.Command : ADP.Connection), e);

        internal static Exception InvalidXml() => Argument(StringsHelper.GetString(Strings.MDF_InvalidXml));

        internal static Exception InvalidXmlInvalidValue(string collectionName, string columnName)
            => Argument(StringsHelper.GetString(Strings.MDF_InvalidXmlInvalidValue, collectionName, columnName));

        internal static Exception CollectionNameIsNotUnique(string collectionName)
            => Argument(StringsHelper.GetString(Strings.MDF_CollectionNameISNotUnique, collectionName));

        internal static Exception UnableToBuildCollection(string collectionName)
            => Argument(StringsHelper.GetString(Strings.MDF_UnableToBuildCollection, collectionName));

        internal static Exception UndefinedCollection(string collectionName)
            => Argument(StringsHelper.GetString(Strings.MDF_UndefinedCollection, collectionName));

        internal static Exception UnsupportedVersion(string collectionName) => Argument(StringsHelper.GetString(Strings.MDF_UnsupportedVersion, collectionName));

        internal static Exception AmbiguousCollectionName(string collectionName)
            => Argument(StringsHelper.GetString(Strings.MDF_AmbiguousCollectionName, collectionName));

        internal static Exception MissingDataSourceInformationColumn() => Argument(StringsHelper.GetString(Strings.MDF_MissingDataSourceInformationColumn));

        internal static Exception IncorrectNumberOfDataSourceInformationRows()
            => Argument(StringsHelper.GetString(Strings.MDF_IncorrectNumberOfDataSourceInformationRows));

        internal static Exception MissingRestrictionColumn() => Argument(StringsHelper.GetString(Strings.MDF_MissingRestrictionColumn));

        internal static Exception MissingRestrictionRow() => Argument(StringsHelper.GetString(Strings.MDF_MissingRestrictionRow));

        internal static Exception UndefinedPopulationMechanism(string populationMechanism)
#if NETFRAMEWORK
            => Argument(StringsHelper.GetString(Strings.MDF_UndefinedPopulationMechanism, populationMechanism));
#else
            => throw new NotImplementedException();
#endif
#endregion

#region DbConnectionPool and related
        internal static Exception PooledOpenTimeout()
            => ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_PooledOpenTimeout));

        internal static Exception NonPooledOpenTimeout()
            => ADP.TimeoutException(StringsHelper.GetString(Strings.ADP_NonPooledOpenTimeout));
#endregion

#region DbProviderException
        internal static InvalidOperationException TransactionConnectionMismatch()
            => Provider(StringsHelper.GetString(Strings.ADP_TransactionConnectionMismatch));

        internal static InvalidOperationException TransactionRequired(string method)
            => Provider(StringsHelper.GetString(Strings.ADP_TransactionRequired, method));

        internal static InvalidOperationException TransactionCompletedButNotDisposed() => Provider(StringsHelper.GetString(Strings.ADP_TransactionCompletedButNotDisposed));

#endregion

#region SqlMetaData, SqlTypes
        internal static Exception InvalidMetaDataValue() => ADP.Argument(StringsHelper.GetString(Strings.ADP_InvalidMetaDataValue));

        internal static InvalidOperationException NonSequentialColumnAccess(int badCol, int currCol)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_NonSequentialColumnAccess,
                                                        badCol.ToString(CultureInfo.InvariantCulture),
                                                        currCol.ToString(CultureInfo.InvariantCulture)));
#endregion

#region IDataParameter
        internal static ArgumentException InvalidDataType(TypeCode typecode) => Argument(StringsHelper.GetString(Strings.ADP_InvalidDataType, typecode.ToString()));

        internal static ArgumentException UnknownDataType(Type dataType) => Argument(StringsHelper.GetString(Strings.ADP_UnknownDataType, dataType.FullName));

        internal static ArgumentException DbTypeNotSupported(DbType type, Type enumtype)
            => Argument(StringsHelper.GetString(Strings.ADP_DbTypeNotSupported, type.ToString(), enumtype.Name));

        internal static ArgumentException UnknownDataTypeCode(Type dataType, TypeCode typeCode)
            => Argument(StringsHelper.GetString(Strings.ADP_UnknownDataTypeCode, ((int)typeCode).ToString(CultureInfo.InvariantCulture), dataType.FullName));

        internal static ArgumentException InvalidOffsetValue(int value)
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidOffsetValue, value.ToString(CultureInfo.InvariantCulture)));

        internal static ArgumentException InvalidSizeValue(int value)
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidSizeValue, value.ToString(CultureInfo.InvariantCulture)));

        internal static ArgumentException ParameterValueOutOfRange(decimal value)
            => ADP.Argument(StringsHelper.GetString(Strings.ADP_ParameterValueOutOfRange, value.ToString((IFormatProvider)null)));

        internal static ArgumentException ParameterValueOutOfRange(SqlDecimal value) => ADP.Argument(StringsHelper.GetString(Strings.ADP_ParameterValueOutOfRange, value.ToString()));

        internal static ArgumentException ParameterValueOutOfRange(string value) => ADP.Argument(StringsHelper.GetString(Strings.ADP_ParameterValueOutOfRange, value));

        internal static ArgumentException VersionDoesNotSupportDataType(string typeName) => Argument(StringsHelper.GetString(Strings.ADP_VersionDoesNotSupportDataType, typeName));

        internal static Exception ParameterConversionFailed(object value, Type destType, Exception inner)
        {
            Debug.Assert(value != null, "null value on conversion failure");
            Debug.Assert(inner != null, "null inner on conversion failure");

            Exception e;
            string message = StringsHelper.GetString(Strings.ADP_ParameterConversionFailed, value.GetType().Name, destType.Name);
            if (inner is ArgumentException)
            {
                e = new ArgumentException(message, inner);
            }
            else if (inner is FormatException)
            {
                e = new FormatException(message, inner);
            }
            else if (inner is InvalidCastException)
            {
                e = new InvalidCastException(message, inner);
            }
            else if (inner is OverflowException)
            {
                e = new OverflowException(message, inner);
            }
            else
            {
                e = inner;
            }
            TraceExceptionAsReturnValue(e);
            return e;
        }
#endregion

#region IDataParameterCollection
        internal static Exception ParametersMappingIndex(int index, DbParameterCollection collection) => CollectionIndexInt32(index, collection.GetType(), collection.Count);

        internal static Exception ParametersSourceIndex(string parameterName, DbParameterCollection collection, Type parameterType)
            => CollectionIndexString(parameterType, ADP.ParameterName, parameterName, collection.GetType());

        internal static Exception ParameterNull(string parameter, DbParameterCollection collection, Type parameterType)
            => CollectionNullValue(parameter, collection.GetType(), parameterType);

        internal static Exception InvalidParameterType(DbParameterCollection collection, Type parameterType, object invalidValue)
            => CollectionInvalidType(collection.GetType(), parameterType, invalidValue);
#endregion

#region IDbTransaction
        internal static Exception ParallelTransactionsNotSupported(DbConnection obj)
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_ParallelTransactionsNotSupported, obj.GetType().Name));

        internal static Exception TransactionZombied(DbTransaction obj) => InvalidOperation(StringsHelper.GetString(Strings.ADP_TransactionZombied, obj.GetType().Name));
#endregion

#region DbProviderConfigurationHandler
        internal static InvalidOperationException InvalidMixedUsageOfSecureAndClearCredential()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfSecureAndClearCredential));

        internal static ArgumentException InvalidMixedArgumentOfSecureAndClearCredential()
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfSecureAndClearCredential));

        internal static InvalidOperationException InvalidMixedUsageOfSecureCredentialAndIntegratedSecurity()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfSecureCredentialAndIntegratedSecurity));

        internal static ArgumentException InvalidMixedArgumentOfSecureCredentialAndIntegratedSecurity()
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfSecureCredentialAndIntegratedSecurity));

        internal static InvalidOperationException InvalidMixedUsageOfAccessTokenAndIntegratedSecurity()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndIntegratedSecurity));

        static internal InvalidOperationException InvalidMixedUsageOfAccessTokenAndUserIDPassword()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndUserIDPassword));

        static internal InvalidOperationException InvalidMixedUsageOfAccessTokenAndAuthentication()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndAuthentication));

        static internal Exception InvalidMixedUsageOfCredentialAndAccessToken()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfCredentialAndAccessToken));

        static internal Exception InvalidMixedUsageOfAccessTokenAndTokenCallback()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndTokenCallback));

        internal static Exception InvalidMixedUsageOfAccessTokenCallbackAndAuthentication()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAuthenticationAndTokenCallback));

        internal static Exception InvalidMixedUsageOfAccessTokenCallbackAndIntegratedSecurity()
            => InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenCallbackAndIntegratedSecurity));
        #endregion

        internal static readonly IntPtr s_ptrZero = IntPtr.Zero;
#if NETFRAMEWORK
#region netfx project only
        internal static Task<T> CreatedTaskWithException<T>(Exception ex)
        {
            TaskCompletionSource<T> completion = new();
            completion.SetException(ex);
            return completion.Task;
        }

        internal static Task<T> CreatedTaskWithCancellation<T>()
        {
            TaskCompletionSource<T> completion = new();
            completion.SetCanceled();
            return completion.Task;
        }

        //
        // Helper Functions
        //
        internal static void CheckArgumentLength(string value, string parameterName)
        {
            CheckArgumentNull(value, parameterName);
            if (0 == value.Length)
            {
                throw Argument(StringsHelper.GetString(Strings.ADP_EmptyString, parameterName)); // MDAC 94859
            }
        }

        // IDbConnection.BeginTransaction, OleDbTransaction.Begin
        internal static ArgumentOutOfRangeException InvalidIsolationLevel(IsolationLevel value)
        {
#if DEBUG
            switch (value)
            {
                case IsolationLevel.Unspecified:
                case IsolationLevel.Chaos:
                case IsolationLevel.ReadUncommitted:
                case IsolationLevel.ReadCommitted:
                case IsolationLevel.RepeatableRead:
                case IsolationLevel.Serializable:
                case IsolationLevel.Snapshot:
                    Debug.Assert(false, "valid IsolationLevel " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(IsolationLevel), (int)value);
        }

        // DBDataPermissionAttribute.KeyRestrictionBehavior
        internal static ArgumentOutOfRangeException InvalidKeyRestrictionBehavior(KeyRestrictionBehavior value)
        {
#if DEBUG
            switch (value)
            {
                case KeyRestrictionBehavior.PreventUsage:
                case KeyRestrictionBehavior.AllowOnly:
                    Debug.Assert(false, "valid KeyRestrictionBehavior " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(KeyRestrictionBehavior), (int)value);
        }

        // IDataParameter.Direction
        internal static ArgumentOutOfRangeException InvalidParameterDirection(ParameterDirection value)
        {
#if DEBUG
            switch (value)
            {
                case ParameterDirection.Input:
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                case ParameterDirection.ReturnValue:
                    Debug.Assert(false, "valid ParameterDirection " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(ParameterDirection), (int)value);
        }

        //
        // DbConnectionOptions, DataAccess
        //
        internal static ArgumentException InvalidKeyname(string parameterName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidKey), parameterName);
        }
        internal static ArgumentException InvalidValue(string parameterName)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidValue), parameterName);
        }
        internal static ArgumentException InvalidMixedArgumentOfSecureCredentialAndContextConnection()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfSecureCredentialAndContextConnection));
        }
        internal static InvalidOperationException InvalidMixedUsageOfAccessTokenAndContextConnection()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndContextConnection));
        }
        internal static Exception InvalidMixedUsageOfAccessTokenAndCredential()
        {
            return ADP.InvalidOperation(StringsHelper.GetString(Strings.ADP_InvalidMixedUsageOfAccessTokenAndCredential));
        }

        //
        // DBDataPermission, DataAccess, Odbc
        //
        internal static Exception InvalidXMLBadVersion()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidXMLBadVersion));
        }
        internal static Exception NotAPermissionElement()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_NotAPermissionElement));
        }
        internal static Exception PermissionTypeMismatch()
        {
            return Argument(StringsHelper.GetString(Strings.ADP_PermissionTypeMismatch));
        }

        //
        // DbDataReader
        //
        internal static Exception NumericToDecimalOverflow()
        {
            return InvalidCast(StringsHelper.GetString(Strings.ADP_NumericToDecimalOverflow));
        }

        //
        // : IDbCommand
        //
        internal static Exception InvalidCommandTimeout(int value, string name)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidCommandTimeout, value.ToString(CultureInfo.InvariantCulture)), name);
        }

        //
        // : DbDataAdapter
        //
        internal static InvalidOperationException ComputerNameEx(int lastError)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_ComputerNameEx, lastError));
        }

        //
        // : SNI
        //
        internal static PlatformNotSupportedException SNIPlatformNotSupported(string platform) => new(StringsHelper.GetString(Strings.SNI_PlatformNotSupportedNetFx, platform));

        // global constant strings
        internal const float FailoverTimeoutStepForTnir = 0.125F; // Fraction of timeout to use in case of Transparent Network IP resolution.
        internal const int MinimumTimeoutForTnirMs = 500; // The first login attempt in  Transparent network IP Resolution 

        internal static readonly int s_ptrSize = IntPtr.Size;
        internal static readonly IntPtr s_invalidPtr = new(-1); // use for INVALID_HANDLE

        internal static readonly bool s_isWindowsNT = (PlatformID.Win32NT == Environment.OSVersion.Platform);
        internal static readonly bool s_isPlatformNT5 = (ADP.s_isWindowsNT && (Environment.OSVersion.Version.Major >= 5));

        [FileIOPermission(SecurityAction.Assert, AllFiles = FileIOPermissionAccess.PathDiscovery)]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static string GetFullPath(string filename)
        { // MDAC 77686
            return Path.GetFullPath(filename);
        }

        // TODO: cache machine name and listen to longhorn event to reset it
        internal static string GetComputerNameDnsFullyQualified()
        {
            const int ComputerNameDnsFullyQualified = 3; // winbase.h, enum COMPUTER_NAME_FORMAT
            const int ERROR_MORE_DATA = 234; // winerror.h

            string value;
            if (s_isPlatformNT5)
            {
                int length = 0; // length parameter must be zero if buffer is null
                                // query for the required length
                                // VSTFDEVDIV 479551 - ensure that GetComputerNameEx does not fail with unexpected values and that the length is positive
                int getComputerNameExError = 0;
                if (0 == Kernel32Safe.GetComputerNameEx(ComputerNameDnsFullyQualified, null, ref length))
                {
                    getComputerNameExError = Marshal.GetLastWin32Error();
                }
                if ((getComputerNameExError != 0 && getComputerNameExError != ERROR_MORE_DATA) || length <= 0)
                {
                    throw ADP.ComputerNameEx(getComputerNameExError);
                }

                StringBuilder buffer = new(length);
                length = buffer.Capacity;
                if (0 == Kernel32Safe.GetComputerNameEx(ComputerNameDnsFullyQualified, buffer, ref length))
                {
                    throw ADP.ComputerNameEx(Marshal.GetLastWin32Error());
                }

                // Note: In Longhorn you'll be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                value = buffer.ToString();
            }
            else
            {
                value = ADP.MachineName();
            }
            return value;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static IntPtr IntPtrOffset(IntPtr pbase, int offset)
        {
            if (4 == ADP.s_ptrSize)
            {
                return (IntPtr)checked(pbase.ToInt32() + offset);
            }
            Debug.Assert(8 == ADP.s_ptrSize, "8 != IntPtr.Size"); // MDAC 73747
            return (IntPtr)checked(pbase.ToInt64() + offset);
        }

#endregion
#else
#region netcore project only

        //
        // COM+ exceptions
        //
        internal static PlatformNotSupportedException DbTypeNotSupported(string dbType) => new(StringsHelper.GetString(Strings.SQL_DbTypeNotSupportedOnThisPlatform, dbType));

        // IDbConnection.BeginTransaction, OleDbTransaction.Begin
        internal static ArgumentOutOfRangeException InvalidIsolationLevel(IsolationLevel value)
        {
#if DEBUG
            switch (value)
            {
                case IsolationLevel.Unspecified:
                case IsolationLevel.Chaos:
                case IsolationLevel.ReadUncommitted:
                case IsolationLevel.ReadCommitted:
                case IsolationLevel.RepeatableRead:
                case IsolationLevel.Serializable:
                case IsolationLevel.Snapshot:
                    Debug.Fail("valid IsolationLevel " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(IsolationLevel), (int)value);
        }

        // ConnectionUtil
        internal static Exception IncorrectPhysicalConnectionType() => new ArgumentException(StringsHelper.GetString(StringsHelper.SNI_IncorrectPhysicalConnectionType));

        // IDataParameter.Direction
        internal static ArgumentOutOfRangeException InvalidParameterDirection(ParameterDirection value)
        {
#if DEBUG
            switch (value)
            {
                case ParameterDirection.Input:
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                case ParameterDirection.ReturnValue:
                    Debug.Fail("valid ParameterDirection " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(ParameterDirection), (int)value);
        }

        //
        // : IDbCommand
        //
        internal static Exception InvalidCommandTimeout(int value, [CallerMemberName] string property = "")
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidCommandTimeout, value.ToString(CultureInfo.InvariantCulture)), property);
#endregion
#endif
    }
}
