// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for <see cref="EnclaveDelegate"/>'s choice of enclave provider. This test project
    /// doesn't reference the Azure extension, Microsoft.Data.SqlClient.Extensions.Azure, so these
    /// cover applications without it.
    /// </summary>
    public class EnclaveDelegateShould
    {
        /// <summary>
        /// Azure Attestation fails with an actionable error, before any round trip to the server,
        /// when the Azure extension that validates the attestation token is not present.
        /// </summary>
        [Fact]
        public void GetEnclaveSession_AasWithoutAzureExtension_ThrowsExtensionNotFound()
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
    }
}
