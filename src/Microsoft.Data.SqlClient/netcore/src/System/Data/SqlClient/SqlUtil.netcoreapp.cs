// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Data.SqlClient
{
    using SR = System.Strings;

    internal static partial class SQL
    {
        //
        // Always Encrypted - Certificate Store Provider Errors.
        //
        static internal Exception InvalidKeyEncryptionAlgorithm(string encryptionAlgorithm, string validEncryptionAlgorithm, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidKeyEncryptionAlgorithmSysErr, encryptionAlgorithm, validEncryptionAlgorithm), TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidKeyEncryptionAlgorithm, encryptionAlgorithm, validEncryptionAlgorithm), TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM);
            }
        }

        static internal Exception NullKeyEncryptionAlgorithm(bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM, System.SR.GetString(SR.TCE_NullKeyEncryptionAlgorithmSysErr));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM, System.SR.GetString(SR.TCE_NullKeyEncryptionAlgorithm));
            }
        }

        static internal Exception EmptyColumnEncryptionKey()
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_EmptyColumnEncryptionKey), TdsEnums.TCE_PARAM_COLUMNENCRYPTION_KEY);
        }

        static internal Exception NullColumnEncryptionKey()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_COLUMNENCRYPTION_KEY, System.SR.GetString(SR.TCE_NullColumnEncryptionKey));
        }

        static internal Exception EmptyEncryptedColumnEncryptionKey()
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_EmptyEncryptedColumnEncryptionKey), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception NullEncryptedColumnEncryptionKey()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTED_CEK, System.SR.GetString(SR.TCE_NullEncryptedColumnEncryptionKey));
        }

        static internal Exception LargeCertificatePathLength(int actualLength, int maxLength, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_LargeCertificatePathLengthSysErr, actualLength, maxLength), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_LargeCertificatePathLength, actualLength, maxLength), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception NullCertificatePath(string[] validLocations, bool isSystemOp)
        {
            Debug.Assert(2 == validLocations.Length);
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, System.SR.GetString(SR.TCE_NullCertificatePathSysErr, validLocations[0], validLocations[1], @"/"));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, System.SR.GetString(SR.TCE_NullCertificatePath, validLocations[0], validLocations[1], @"/"));
            }
        }

        static internal Exception NullCspKeyPath(bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, System.SR.GetString(SR.TCE_NullCspPathSysErr, @"/"));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, System.SR.GetString(SR.TCE_NullCspPath, @"/"));
            }
        }

        static internal Exception NullCngKeyPath(bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, System.SR.GetString(SR.TCE_NullCngPathSysErr, @"/"));
            }
            else
            {
                return ADP.ArgumentNull(TdsEnums.TCE_PARAM_MASTERKEY_PATH, System.SR.GetString(SR.TCE_NullCngPath, @"/"));
            }
        }

        static internal Exception InvalidCertificatePath(string actualCertificatePath, string[] validLocations, bool isSystemOp)
        {
            Debug.Assert(2 == validLocations.Length);
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCertificatePathSysErr, actualCertificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCertificatePath, actualCertificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCspPath(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCspPathSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCspPath, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCngPath(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCngPathSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCngPath, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCspName(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCspNameSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCspName, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCngName(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCngNameSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCngName, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCspKeyId(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCspKeyIdSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCspKeyId, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCngKeyId(string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCngKeyIdSysErr, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCngKeyId, masterKeyPath, @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCspName(string cspName, string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCspNameSysErr, cspName, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCspName, cspName, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCspKeyIdentifier(string keyIdentifier, string masterKeyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCspKeyIdSysErr, keyIdentifier, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCspKeyId, keyIdentifier, masterKeyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCngKey(string masterKeyPath, string cngProviderName, string keyIdentifier, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCngKeySysErr, masterKeyPath, cngProviderName, keyIdentifier), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCngKey, masterKeyPath, cngProviderName, keyIdentifier), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCertificateLocation(string certificateLocation, string certificatePath, string[] validLocations, bool isSystemOp)
        {
            Debug.Assert(2 == validLocations.Length);
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCertificateLocationSysErr, certificateLocation, certificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCertificateLocation, certificateLocation, certificatePath, validLocations[0], validLocations[1], @"/"), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidCertificateStore(string certificateStore, string certificatePath, string validCertificateStore, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCertificateStoreSysErr, certificateStore, certificatePath, validCertificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCertificateStore, certificateStore, certificatePath, validCertificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception EmptyCertificateThumbprint(string certificatePath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCertificateThumbprintSysErr, certificatePath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_EmptyCertificateThumbprint, certificatePath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception CertificateNotFound(string thumbprint, string certificateLocation, string certificateStore, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_CertificateNotFoundSysErr, thumbprint, certificateLocation, certificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_CertificateNotFound, thumbprint, certificateLocation, certificateStore), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        static internal Exception InvalidAlgorithmVersionInEncryptedCEK(byte actual, byte expected)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidAlgorithmVersionInEncryptedCEK, actual.ToString(@"X2"), expected.ToString(@"X2")), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCiphertextLengthInEncryptedCEK(int actual, int expected, string certificateName)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCiphertextLengthInEncryptedCEK, actual, expected, certificateName), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCiphertextLengthInEncryptedCEKCsp(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCiphertextLengthInEncryptedCEKCsp, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCiphertextLengthInEncryptedCEKCng(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCiphertextLengthInEncryptedCEKCng, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignatureInEncryptedCEK(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidSignatureInEncryptedCEK, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignatureInEncryptedCEKCsp(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidSignatureInEncryptedCEKCsp, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignatureInEncryptedCEKCng(int actual, int expected, string masterKeyPath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidSignatureInEncryptedCEKCng, actual, expected, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidCertificateSignature(string certificatePath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCertificateSignature, certificatePath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception InvalidSignature(string masterKeyPath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidSignature, masterKeyPath), TdsEnums.TCE_PARAM_ENCRYPTED_CEK);
        }

        static internal Exception CertificateWithNoPrivateKey(string keyPath, bool isSystemOp)
        {
            if (isSystemOp)
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_CertificateWithNoPrivateKeySysErr, keyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
            else
            {
                return ADP.Argument(System.SR.GetString(SR.TCE_CertificateWithNoPrivateKey, keyPath), TdsEnums.TCE_PARAM_MASTERKEY_PATH);
            }
        }

        //
        // Always Encrypted - Cryptographic Algorithms Error messages
        //
        static internal Exception NullColumnEncryptionKeySysErr()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTIONKEY, System.SR.GetString(SR.TCE_NullColumnEncryptionKeySysErr));
        }

        static internal Exception InvalidKeySize(string algorithmName, int actualKeylength, int expectedLength)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidKeySize, algorithmName, actualKeylength, expectedLength), TdsEnums.TCE_PARAM_ENCRYPTIONKEY);
        }

        static internal Exception InvalidEncryptionType(string algorithmName, SqlClientEncryptionType encryptionType, params SqlClientEncryptionType[] validEncryptionTypes)
        {
            const string valueSeparator = @", ";
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidEncryptionType, algorithmName, encryptionType.ToString(), string.Join(valueSeparator, validEncryptionTypes.Select((validEncryptionType => @"'" + validEncryptionType + @"'")))), TdsEnums.TCE_PARAM_ENCRYPTIONTYPE);
        }

        static internal Exception NullPlainText()
        {
            return ADP.ArgumentNull(System.SR.GetString(SR.TCE_NullPlainText));
        }

        static internal Exception NullCipherText()
        {
            return ADP.ArgumentNull(System.SR.GetString(SR.TCE_NullCipherText));
        }

        static internal Exception InvalidCipherTextSize(int actualSize, int minimumSize)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCipherTextSize, actualSize, minimumSize), TdsEnums.TCE_PARAM_CIPHERTEXT);
        }

        static internal Exception InvalidAlgorithmVersion(byte actual, byte expected)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidAlgorithmVersion, actual.ToString(@"X2"), expected.ToString(@"X2")), TdsEnums.TCE_PARAM_CIPHERTEXT);
        }

        static internal Exception InvalidAuthenticationTag()
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidAuthenticationTag), TdsEnums.TCE_PARAM_CIPHERTEXT);
        }

        static internal Exception NullColumnEncryptionAlgorithm(string supportedAlgorithms)
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_ENCRYPTION_ALGORITHM, System.SR.GetString(SR.TCE_NullColumnEncryptionAlgorithm, supportedAlgorithms));
        }

        //
        // Always Encrypted - Errors from sp_describe_parameter_encryption
        //
        static internal Exception InvalidKeyStoreProviderName(string providerName, List<string> systemProviders, List<string> customProviders)
        {
            const string valueSeparator = @", ";
            string systemProviderStr = string.Join(valueSeparator, systemProviders.Select(provider => $"'{provider}'"));
            string customProviderStr = string.Join(valueSeparator, customProviders.Select(provider => $"'{provider}'"));
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidKeyStoreProviderName, providerName, systemProviderStr, customProviderStr));
        }

        static internal Exception UnableToVerifyColumnMasterKeySignature(Exception innerExeption)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_UnableToVerifyColumnMasterKeySignature, innerExeption.Message), innerExeption);
        }

        static internal Exception ColumnMasterKeySignatureVerificationFailed(string cmkPath)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_ColumnMasterKeySignatureVerificationFailed, cmkPath));
        }

        static internal Exception ColumnMasterKeySignatureNotFound(string cmkPath)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_ColumnMasterKeySignatureNotFound, cmkPath));
        }

        //
        // TCE - Errors from secure channel Communication
        //
        internal static Exception ExceptionWhenGeneratingEnclavePackage(Exception innerExeption)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_ExceptionWhenGeneratingEnclavePackage, innerExeption.Message), innerExeption);
        }

        static internal Exception FailedToEncryptRegisterRulesBytePackage(Exception innerExeption)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_FailedToEncryptRegisterRulesBytePackage, innerExeption.Message), innerExeption);
        }

        static internal Exception InvalidKeyIdUnableToCastToUnsignedShort(int keyId, Exception innerException)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidKeyIdUnableToCastToUnsignedShort, keyId, innerException.Message), innerException);
        }

        static internal Exception InvalidDatabaseIdUnableToCastToUnsignedInt(int databaseId, Exception innerException)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidDatabaseIdUnableToCastToUnsignedInt, databaseId, innerException.Message), innerException);
        }

        static internal Exception InvalidAttestationParameterUnableToConvertToUnsignedInt(string variableName, int intValue, string enclaveType, Exception innerException)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidAttestationParameterUnableToConvertToUnsignedInt, enclaveType, intValue, variableName, innerException.Message), innerException);
        }

        static internal Exception OffsetOutOfBounds(string argument, string type, string method)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_OffsetOutOfBounds, type, method));
        }

        static internal Exception InsufficientBuffer(string argument, string type, string method)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InsufficientBuffer, argument, type, method));
        }

        static internal Exception ColumnEncryptionKeysNotFound()
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_ColumnEncryptionKeysNotFound));
        }

        //
        // TCE - Errors when performing attestation
        //

        static internal Exception AttestationInfoNotReturnedFromSqlServer(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_AttestationInfoNotReturnedFromSQLServer, enclaveType, enclaveAttestationUrl));
        }

        //
        // Always Encrypted - Errors when establishing secure channel
        //
        static internal Exception NullArgumentInConstructorInternal(string argumentName, string objectUnderConstruction)
        {
            return ADP.ArgumentNull(argumentName, System.SR.GetString(SR.TCE_NullArgumentInConstructorInternal, argumentName, objectUnderConstruction));
        }

        static internal Exception EmptyArgumentInConstructorInternal(string argumentName, string objectUnderConstruction)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_EmptyArgumentInConstructorInternal, argumentName, objectUnderConstruction));
        }

        static internal Exception NullArgumentInternal(string argumentName, string type, string method)
        {
            return ADP.ArgumentNull(argumentName, System.SR.GetString(SR.TCE_NullArgumentInternal, argumentName, type, method));
        }

        static internal Exception EmptyArgumentInternal(string argumentName, string type, string method)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_EmptyArgumentInternal, argumentName, type, method));
        }

        //
        // TCE - enclave provider/configuration errors
        //
        static internal Exception CannotGetSqlColumnEncryptionEnclaveProviderConfig(Exception innerException)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_CannotGetSqlColumnEncryptionEnclaveProviderConfig, innerException.Message), innerException);
        }

        static internal Exception CannotCreateSqlColumnEncryptionEnclaveProvider(string providerName, string type, Exception innerException)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_CannotCreateSqlColumnEncryptionEnclaveProvider, providerName, type, innerException.Message), innerException);
        }

        static internal Exception SqlColumnEncryptionEnclaveProviderNameCannotBeEmpty()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_SqlColumnEncryptionEnclaveProviderNameCannotBeEmpty));
        }

        static internal Exception NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe(string enclaveType)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe, "sp_describe_parameter_encryption", enclaveType));
        }

        static internal Exception NoAttestationUrlSpecifiedForEnclaveBasedQueryGeneratingEnclavePackage(string enclaveType)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_NoAttestationUrlSpecifiedForEnclaveBasedQueryGeneratingEnclavePackage, enclaveType));
        }

        static internal Exception EnclaveTypeNullForEnclaveBasedQuery()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveTypeNullForEnclaveBasedQuery));
        }

        static internal Exception EnclaveProvidersNotConfiguredForEnclaveBasedQuery()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveProvidersNotConfiguredForEnclaveBasedQuery));
        }

        static internal Exception EnclaveProviderNotFound(string enclaveType)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveProviderNotFound, enclaveType));
        }

        static internal Exception NullEnclaveSessionReturnedFromProvider(string enclaveType, string attestationUrl)
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_NullEnclaveSessionReturnedFromProvider, enclaveType, attestationUrl));
        }

        //
        // Always Encrypted - Generic toplevel failures.
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

        //
        // Always Encrypted - Client side query processing errors.
        //
        static internal Exception UnknownColumnEncryptionAlgorithm(string algorithmName, string supportedAlgorithms)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_UnknownColumnEncryptionAlgorithm, algorithmName, supportedAlgorithms));
        }

        static internal Exception UnknownColumnEncryptionAlgorithmId(int algoId, string supportAlgorithmIds)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_UnknownColumnEncryptionAlgorithmId, algoId, supportAlgorithmIds), TdsEnums.TCE_PARAM_CIPHER_ALGORITHM_ID);
        }

        static internal Exception UnsupportedNormalizationVersion(byte version)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_UnsupportedNormalizationVersion, version, "'1'", "SQL Server"));
        }

        static internal Exception UnrecognizedKeyStoreProviderName(string providerName, List<string> systemProviders, List<string> customProviders)
        {
            const string valueSeparator = @", ";
            string systemProviderStr = string.Join(valueSeparator, systemProviders.Select(provider => @"'" + provider + @"'"));
            string customProviderStr = string.Join(valueSeparator, customProviders.Select(provider => @"'" + provider + @"'"));
            return ADP.Argument(System.SR.GetString(SR.TCE_UnrecognizedKeyStoreProviderName, providerName, systemProviderStr, customProviderStr));
        }

        static internal Exception InvalidDataTypeForEncryptedParameter(string parameterName, int actualDataType, int expectedDataType)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_NullProviderValue, parameterName, actualDataType, expectedDataType));
        }

        static internal Exception KeyDecryptionFailed(string providerName, string keyHex, Exception e)
        {

            if (providerName.Equals(SqlColumnEncryptionCertificateStoreProvider.ProviderName))
            {
                return GetExceptionArray(null, System.SR.GetString(SR.TCE_KeyDecryptionFailedCertStore, providerName, keyHex), e);
            }
            else
            {
                return GetExceptionArray(null, System.SR.GetString(SR.TCE_KeyDecryptionFailed, providerName, keyHex), e);
            }
        }

        static internal Exception UntrustedKeyPath(string keyPath, string serverName)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_UntrustedKeyPath, keyPath, serverName));
        }

        static internal Exception UnsupportedDatatypeEncryption(string dataType)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_UnsupportedDatatype, dataType));
        }

        static internal Exception ThrowDecryptionFailed(string keyStr, string valStr, Exception e)
        {
            return GetExceptionArray(null, System.SR.GetString(SR.TCE_DecryptionFailed, keyStr, valStr), e);
        }

        static internal Exception NullEnclaveSessionDuringQueryExecution(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_NullEnclaveSessionDuringQueryExecution, enclaveType, enclaveAttestationUrl));
        }

        static internal Exception NullEnclavePackageForEnclaveBasedQuery(string enclaveType, string enclaveAttestationUrl)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_NullEnclavePackageForEnclaveBasedQuery, enclaveType, enclaveAttestationUrl));
        }

        //
        // TCE- SQL connection related error messages
        //
        static internal Exception TceNotSupported()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_NotSupportedByServer, "SQL Server"));
        }

        static internal Exception EnclaveComputationsNotSupported()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveComputationsNotSupported));
        }

        static internal Exception EnclaveTypeNotReturned()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_EnclaveTypeNotReturned));
        }

        //
        // TCE- Extensibility related error messages
        //
        static internal Exception CanOnlyCallOnce()
        {
            return ADP.InvalidOperation(System.SR.GetString(SR.TCE_CanOnlyCallOnce));
        }

        static internal Exception NullCustomKeyStoreProviderDictionary()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS, System.SR.GetString(SR.TCE_NullCustomKeyStoreProviderDictionary));
        }

        static internal Exception InvalidCustomKeyStoreProviderName(string providerName, string prefix)
        {
            return ADP.Argument(System.SR.GetString(SR.TCE_InvalidCustomKeyStoreProviderName, providerName, prefix), TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS);
        }

        static internal Exception NullProviderValue(string providerName)
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS, System.SR.GetString(SR.TCE_NullProviderValue, providerName));
        }

        static internal Exception EmptyProviderName()
        {
            return ADP.ArgumentNull(TdsEnums.TCE_PARAM_CLIENT_KEYSTORE_PROVIDERS, System.SR.GetString(SR.TCE_EmptyProviderName));
        }
    }
}