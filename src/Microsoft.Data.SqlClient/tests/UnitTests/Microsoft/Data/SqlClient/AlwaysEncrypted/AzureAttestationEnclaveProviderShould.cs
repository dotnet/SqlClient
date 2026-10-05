// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for <see cref="AzureAttestationEnclaveProvider"/> that don't need the Azure extension,
    /// Microsoft.Data.SqlClient.Extensions.Azure, which this test project does not reference.
    /// </summary>
    public class AzureAttestationEnclaveProviderShould
    {
        /// <summary>
        /// Verifies that Azure Attestation fails with an actionable error when the Azure extension,
        /// which validates the attestation token, is not present.
        /// </summary>
        [Fact]
        public void GetEnclaveSession_WithoutAzureExtension_ThrowsExtensionNotFound()
        {
            Assert.Null(AzureAttestationTokenValidatorBinding.Instance);

            EnclaveSessionParameters parameters = new EnclaveSessionParameters(
                serverName: "unit-test-server",
                attestationUrl: "https://unit.test.invalid/attest/VbsEnclave",
                database: "unit-test-db");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EnclaveDelegate.Instance.GetEnclaveSession(
                    SqlConnectionAttestationProtocol.AAS,
                    "VBS",
                    parameters,
                    generateCustomData: true,
                    isRetry: false,
                    out _,
                    out _,
                    out _));

            Assert.Equal(Strings.TCE_AzureAttestationExtensionNotFound, exception.Message);
        }

        /// <summary>
        /// Verifies that claim values are encoded as unpadded base64url, as JWT claims are.
        /// </summary>
        [Theory]
        [InlineData(new byte[] { }, "")]
        [InlineData(new byte[] { 0x66 }, "Zg")]
        [InlineData(new byte[] { 0x66, 0x6f }, "Zm8")]
        [InlineData(new byte[] { 0x66, 0x6f, 0x6f }, "Zm9v")]
        [InlineData(new byte[] { 0xfb, 0xff, 0xbf }, "-_-_")]
        [InlineData(new byte[] { 0x3e, 0x3f }, "Pj8")]
        public void Base64UrlEncode_ProducesUnpaddedBase64Url(byte[] data, string expected)
        {
            Assert.Equal(expected, AzureAttestationEnclaveProvider.Base64UrlEncode(data));
        }
    }
}
