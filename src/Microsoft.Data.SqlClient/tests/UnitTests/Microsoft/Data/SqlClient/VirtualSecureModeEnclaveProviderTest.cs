// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests the enclave public key binding check on the VSM/HGS attestation path. A genuine VBS enclave
/// commits to its public key by placing SHA-256(public key) in the first 32 bytes of the report's
/// signature-covered EnclaveData, so the binding must accept a matching key and reject any other.
/// </summary>
public class VirtualSecureModeEnclaveProviderTest
{
    private const int EnclaveReportPackageHeaderSize = 6 * sizeof(uint); // 24
    private const int EnclaveReportSize = (sizeof(uint) * 2) + 64 + 152;  // ReportSize + ReportVersion + EnclaveData + EnclaveIdentity = 224

    /// <summary>
    /// A report whose EnclaveData commits to the key used for the session passes the binding check.
    /// </summary>
    [Fact]
    public void VerifyEnclavePublicKeyBinding_GenuineKey_Succeeds()
    {
        // Arrange
        byte[] enclaveKey = Encoding.UTF8.GetBytes("genuine-enclave-public-key-blob");
        EnclaveReportPackage testPackage = BuildReportPackage(Sha256(enclaveKey));
        EnclavePublicKey testKey = new EnclavePublicKey(enclaveKey);

        // Act / Assert
        // Report commits to enclaveKey and the session uses enclaveKey: the binding holds, so no exception.
        VirtualizationBasedSecurityEnclaveProviderBase.VerifyEnclavePublicKeyBinding(testPackage, testKey);
    }

    /// <summary>
    /// A report whose committed data does not match the session's enclave public key is rejected.
    /// </summary>
    [Fact]
    public void VerifyEnclavePublicKeyBinding_SwappedKey_Throws()
    {
        // Arrange
        byte[] committedKeyBytes = Encoding.UTF8.GetBytes("committed-enclave-public-key-blob");

        // The signed report commits to committedKey...
        EnclaveReportPackage testPackage = BuildReportPackage(Sha256(committedKeyBytes));

        // ...but a different enclave public key is offered for the session.
        byte[] substitutedKeyBytes = Encoding.UTF8.GetBytes("substituted-enclave-public-key");
        EnclavePublicKey substitutedKey = new EnclavePublicKey(substitutedKeyBytes);


        // Act
        Action action = () => VirtualizationBasedSecurityEnclaveProviderBase.VerifyEnclavePublicKeyBinding(
            testPackage,
            substitutedKey);

        // Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(action);
        Assert.Equal(Strings.VerifyEnclaveKeyBindingFailed, ex.Message);
    }

    // Builds a minimal EnclaveReportPackage whose report EnclaveData begins with the given 32-byte
    // binding value. The signature is empty because this targets the binding, not the report signature.
    private static EnclaveReportPackage BuildReportPackage(byte[] enclaveDataFirst32)
    {
        byte[] payload = new byte[EnclaveReportPackageHeaderSize + EnclaveReportSize];
        int offset = 0;

        void WriteUInt(uint value)
        {
            BitConverter.GetBytes(value).CopyTo(payload, offset);
            offset += sizeof(uint);
        }

        // EnclaveReportPackageHeader
        WriteUInt((uint)payload.Length);      // PackageSize
        WriteUInt(1);                         // Version
        WriteUInt(1);                         // SignatureScheme
        WriteUInt(EnclaveReportSize);         // SignedStatementSize
        WriteUInt(0);                         // SignatureSize
        WriteUInt(0);                         // Reserved

        // EnclaveReport
        WriteUInt(EnclaveReportSize);         // ReportSize
        WriteUInt(1);                         // ReportVersion
        Array.Copy(enclaveDataFirst32, 0, payload, offset, 32); // EnclaveData: first 32 bytes = SHA-256(public key)

        return new EnclaveReportPackage(payload);
    }

    private static byte[] Sha256(byte[] data)
    {
        using SHA256 sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }
}
