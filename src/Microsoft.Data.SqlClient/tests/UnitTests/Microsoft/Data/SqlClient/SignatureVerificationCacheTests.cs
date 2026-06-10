// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class SignatureVerificationCacheTests
    {
        [Fact]
        public void GetSignatureVerificationResult_ReturnsFalseForCachedFailure()
        {
            ColumnMasterKeyMetadataSignatureVerificationCache cache = ColumnMasterKeyMetadataSignatureVerificationCache.Instance;
            string keyStoreName = $"TEST_PROVIDER_{Guid.NewGuid():N}";
            string masterKeyPath = $"https://unit-test/{Guid.NewGuid():N}";
            byte[] signature = [1, 2, 3, 4];

            cache.AddSignatureVerificationResult(keyStoreName, masterKeyPath, allowEnclaveComputations: true, signature, result: false);

            Assert.Equal(SignatureVerificationResult.False, cache.GetSignatureVerificationResult(keyStoreName, masterKeyPath, allowEnclaveComputations: true, signature));
        }

        [Fact]
        public void GetSignatureVerificationResult_ReturnsTrueForCachedSuccess()
        {
            ColumnMasterKeyMetadataSignatureVerificationCache cache = ColumnMasterKeyMetadataSignatureVerificationCache.Instance;
            string keyStoreName = $"TEST_PROVIDER_{Guid.NewGuid():N}";
            string masterKeyPath = $"https://unit-test/{Guid.NewGuid():N}";
            byte[] signature = [4, 3, 2, 1];

            cache.AddSignatureVerificationResult(keyStoreName, masterKeyPath, allowEnclaveComputations: true, signature, result: true);

            Assert.Equal(SignatureVerificationResult.True, cache.GetSignatureVerificationResult(keyStoreName, masterKeyPath, allowEnclaveComputations: true, signature));
        }

        [Fact]
        public void GetSignatureVerificationResult_ReturnsNotFoundForCacheMiss()
        {
            ColumnMasterKeyMetadataSignatureVerificationCache cache = ColumnMasterKeyMetadataSignatureVerificationCache.Instance;
            string keyStoreName = $"TEST_PROVIDER_{Guid.NewGuid():N}";
            string masterKeyPath = $"https://unit-test/{Guid.NewGuid():N}";
            byte[] signature = [9, 9, 9, 9];

            Assert.Equal(SignatureVerificationResult.NotFound, cache.GetSignatureVerificationResult(keyStoreName, masterKeyPath, allowEnclaveComputations: true, signature));
        }
    }
}