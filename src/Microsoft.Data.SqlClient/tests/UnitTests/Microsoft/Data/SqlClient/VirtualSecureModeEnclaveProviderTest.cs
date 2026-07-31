// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
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
        EnclaveReportPackage package = BuildReportPackage(Sha256(enclaveKey));

        // Act / Assert
        // Report commits to enclaveKey and the session uses enclaveKey: the binding holds, so no exception.
        InvokeVerifyEnclavePublicKeyBinding(package, new EnclavePublicKey(enclaveKey));
    }

    /// <summary>
    /// A report whose committed data does not match the session's enclave public key is rejected.
    /// </summary>
    [Fact]
    public void VerifyEnclavePublicKeyBinding_SwappedKey_Throws()
    {
        // Arrange
        byte[] committedKey = Encoding.UTF8.GetBytes("committed-enclave-public-key-blob");
        // The signed report commits to committedKey...
        EnclaveReportPackage package = BuildReportPackage(Sha256(committedKey));

        // ...but a different enclave public key is offered for the session.
        byte[] substitutedKey = Encoding.UTF8.GetBytes("substituted-enclave-public-key");

        // Act
        Action action = () => InvokeVerifyEnclavePublicKeyBinding(package, new EnclavePublicKey(substitutedKey));

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

    // Invokes the private binding method via reflection, unwrapping the reflection exception so the
    // caller sees the real ArgumentException.
    private static void InvokeVerifyEnclavePublicKeyBinding(EnclaveReportPackage package, EnclavePublicKey enclavePublicKey)
    {
        MethodInfo method = typeof(VirtualizationBasedSecurityEnclaveProviderBase)
            .GetMethod("VerifyEnclavePublicKeyBinding", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("VerifyEnclavePublicKeyBinding not found");

        var provider = new HostGuardianServiceEnclaveProvider();

        try
        {
            method.Invoke(provider, new object[] { package, enclavePublicKey });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static byte[] Sha256(byte[] data)
    {
        using SHA256 sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }
}
