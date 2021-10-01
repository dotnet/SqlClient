// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Microsoft.Data.Common
{
    internal static partial class ADP
    {
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

        internal static void TraceExceptionForCapture(Exception e)
        {
            Debug.Assert(ADP.IsCatchableExceptionType(e), "Invalid exception type, should have been re-thrown!");
            TraceException("<comm.ADP.TraceException|ERR|CATCH> '{0}'", e);
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
        internal static Exception InvalidCommandTimeout(int value)
        {
            return Argument(StringsHelper.GetString(Strings.ADP_InvalidCommandTimeout, value.ToString(CultureInfo.InvariantCulture)), ADP.CommandTimeout);
        }

        //
        // : DbDataAdapter
        //
        internal static InvalidOperationException ComputerNameEx(int lastError)
        {
            return InvalidOperation(StringsHelper.GetString(Strings.ADP_ComputerNameEx, lastError));
        }

        // global constant strings
        internal const string Append = "Append";
        internal const string BeginExecuteNonQuery = "BeginExecuteNonQuery";
        internal const string BeginExecuteReader = "BeginExecuteReader";
        internal const string BeginTransaction = "BeginTransaction";
        internal const string BeginExecuteXmlReader = "BeginExecuteXmlReader";
        internal const string ChangeDatabase = "ChangeDatabase";
        internal const string Cancel = "Cancel";
        internal const string Clone = "Clone";
        internal const string CommitTransaction = "CommitTransaction";
        internal const string CommandTimeout = "CommandTimeout";
        internal const string ConnectionString = "ConnectionString";
        internal const string DataSetColumn = "DataSetColumn";
        internal const string DataSetTable = "DataSetTable";
        internal const string Delete = "Delete";
        internal const string DeleteCommand = "DeleteCommand";
        internal const string DeriveParameters = "DeriveParameters";
        internal const string EndExecuteNonQuery = "EndExecuteNonQuery";
        internal const string EndExecuteReader = "EndExecuteReader";
        internal const string EndExecuteXmlReader = "EndExecuteXmlReader";
        internal const string ExecuteReader = "ExecuteReader";
        internal const string ExecuteRow = "ExecuteRow";
        internal const string ExecuteNonQuery = "ExecuteNonQuery";
        internal const string ExecuteScalar = "ExecuteScalar";
        internal const string ExecuteSqlScalar = "ExecuteSqlScalar";
        internal const string ExecuteXmlReader = "ExecuteXmlReader";
        internal const string Fill = "Fill";
        internal const string FillPage = "FillPage";
        internal const string FillSchema = "FillSchema";
        internal const string GetBytes = "GetBytes";
        internal const string GetChars = "GetChars";
        internal const string GetOleDbSchemaTable = "GetOleDbSchemaTable";
        internal const string GetProperties = "GetProperties";
        internal const string GetSchema = "GetSchema";
        internal const string GetSchemaTable = "GetSchemaTable";
        internal const string GetServerTransactionLevel = "GetServerTransactionLevel";
        internal const string Insert = "Insert";
        internal const string Open = "Open";
        internal const string ParameterBuffer = "buffer";
        internal const string ParameterCount = "count";
        internal const string ParameterDestinationType = "destinationType";
        internal const string ParameterIndex = "index";
        internal const string ParameterOffset = "offset";
        internal const string ParameterService = "Service";
        internal const string ParameterTimeout = "Timeout";
        internal const string ParameterUserData = "UserData";
        internal const string Prepare = "Prepare";
        internal const string QuoteIdentifier = "QuoteIdentifier";
        internal const string Read = "Read";
        internal const string ReadAsync = "ReadAsync";
        internal const string Remove = "Remove";
        internal const string RollbackTransaction = "RollbackTransaction";
        internal const string SaveTransaction = "SaveTransaction";
        internal const string SetProperties = "SetProperties";
        internal const string SourceColumn = "SourceColumn";
        internal const string SourceVersion = "SourceVersion";
        internal const string SourceTable = "SourceTable";
        internal const string UnquoteIdentifier = "UnquoteIdentifier";
        internal const string Update = "Update";
        internal const string UpdateCommand = "UpdateCommand";
        internal const string UpdateRows = "UpdateRows";

        internal const int DecimalMaxPrecision = 29;
        internal const int DecimalMaxPrecision28 = 28;  // there are some cases in Odbc where we need that ...
        internal const float FailoverTimeoutStepForTnir = 0.125F; // Fraction of timeout to use in case of Transparent Network IP resolution.
        internal const int MinimumTimeoutForTnirMs = 500; // The first login attempt in  Transparent network IP Resolution 

        internal static readonly IntPtr s_ptrZero = new(0); // IntPtr.Zero
        internal static readonly int s_ptrSize = IntPtr.Size;
        internal static readonly IntPtr s_invalidPtr = new(-1); // use for INVALID_HANDLE
        internal static readonly IntPtr s_recordsUnaffected = new(-1);

        internal static readonly HandleRef s_nullHandleRef = new(null, IntPtr.Zero);

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
                if (0 == SafeNativeMethods.GetComputerNameEx(ComputerNameDnsFullyQualified, null, ref length))
                {
                    getComputerNameExError = Marshal.GetLastWin32Error();
                }
                if ((getComputerNameExError != 0 && getComputerNameExError != ERROR_MORE_DATA) || length <= 0)
                {
                    throw ADP.ComputerNameEx(getComputerNameExError);
                }

                StringBuilder buffer = new(length);
                length = buffer.Capacity;
                if (0 == SafeNativeMethods.GetComputerNameEx(ComputerNameDnsFullyQualified, buffer, ref length))
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

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        internal static object LocalMachineRegistryValue(string subkey, string queryvalue)
        { // MDAC 77697
            (new RegistryPermission(RegistryPermissionAccess.Read, "HKEY_LOCAL_MACHINE\\" + subkey)).Assert(); // MDAC 62028
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subkey, false))
                {
                    return key?.GetValue(queryvalue);
                }
            }
            catch (SecurityException e)
            {
                // Even though we assert permission - it's possible there are
                // ACL's on registry that cause SecurityException to be thrown.
                ADP.TraceExceptionWithoutRethrow(e);
                return null;
            }
            finally
            {
                RegistryPermission.RevertAssert();
            }
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

        internal static bool IsEmpty(string str) => string.IsNullOrEmpty(str);
    }
}
