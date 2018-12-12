// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System;

namespace Microsoft.Data.SqlClient
{
    using SR = System.Strings;

    internal static partial class SQL
    {
        //
        // TCE - Errors from secure channel Communication
        //
        internal static Exception ExceptionWhenGeneratingEnclavePackage(Exception innerExeption)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_ExceptionWhenGeneratingEnclavePackage, innerExeption.Message), innerExeption);
        }

        internal static Exception FailedToEncryptRegisterRulesBytePackage(Exception innerExeption)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_FailedToEncryptRegisterRulesBytePackage, innerExeption.Message), innerExeption);
        }

        internal static Exception InvalidKeyIdUnableToCastToUnsignedShort(int keyId, Exception innerException)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidKeyIdUnableToCastToUnsignedShort, keyId, innerException.Message), innerException);
        }

        internal static Exception InvalidDatabaseIdUnableToCastToUnsignedInt(int databaseId, Exception innerException)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidDatabaseIdUnableToCastToUnsignedInt, databaseId, innerException.Message), innerException);
        }

        internal static Exception InvalidAttestationParameterUnableToConvertToUnsignedInt(string variableName, int intValue, string enclaveType, Exception innerException)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidAttestationParameterUnableToConvertToUnsignedInt, enclaveType, intValue, variableName, innerException.Message), innerException);
        }

        internal static Exception OffsetOutOfBounds(string argument, string type, string method)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_OffsetOutOfBounds, type, method));
        }

        internal static Exception InsufficientBuffer(string argument, string type, string method)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InsufficientBuffer, argument, type, method));
        }

        internal static Exception ColumnEncryptionKeysNotFound()
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_ColumnEncryptionKeysNotFound));
        }

        //
        // TCE - Errors when performing attestation
        //

        internal static Exception AttestationInfoNotReturnedFromSqlServer(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_AttestationInfoNotReturnedFromSQLServer, enclaveType, enclaveAttestationUrl));
        }

        //
        // TCE - enclave provider/configuration errors
        //
        internal static Exception CannotGetSqlColumnEncryptionEnclaveProviderConfig(Exception innerException)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_CannotGetSqlColumnEncryptionEnclaveProviderConfig, innerException.Message), innerException);
        }

        internal static Exception CannotCreateSqlColumnEncryptionEnclaveProvider(string providerName, string type, Exception innerException)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_CannotCreateSqlColumnEncryptionEnclaveProvider, providerName, type, innerException.Message), innerException);
        }

        internal static Exception SqlColumnEncryptionEnclaveProviderNameCannotBeEmpty()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_SqlColumnEncryptionEnclaveProviderNameCannotBeEmpty));
        }

        internal static Exception NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe(string enclaveType)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe, "sp_describe_parameter_encryption", enclaveType));
        }

        internal static Exception NoAttestationUrlSpecifiedForEnclaveBasedQueryGeneratingEnclavePackage(string enclaveType)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_NoAttestationUrlSpecifiedForEnclaveBasedQueryGeneratingEnclavePackage, enclaveType));
        }

        internal static Exception EnclaveTypeNullForEnclaveBasedQuery()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveTypeNullForEnclaveBasedQuery));
        }

        internal static Exception EnclaveProvidersNotConfiguredForEnclaveBasedQuery()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveProvidersNotConfiguredForEnclaveBasedQuery));
        }

        internal static Exception EnclaveProviderNotFound(string enclaveType)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveProviderNotFound, enclaveType));
        }

        internal static Exception NullEnclaveSessionReturnedFromProvider(string enclaveType, string attestationUrl)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_NullEnclaveSessionReturnedFromProvider, enclaveType, attestationUrl));
        }

        //
        // Always Encrypted - Client side query processing errors.
        //

        internal static Exception InvalidDataTypeForEncryptedParameter(string parameterName, int actualDataType, int expectedDataType)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_NullProviderValue, parameterName, actualDataType, expectedDataType));
        }

        internal static Exception NullEnclaveSessionDuringQueryExecution(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_NullEnclaveSessionDuringQueryExecution, enclaveType, enclaveAttestationUrl));
        }

        internal static Exception NullEnclavePackageForEnclaveBasedQuery(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_NullEnclavePackageForEnclaveBasedQuery, enclaveType, enclaveAttestationUrl));
        }
    }
}