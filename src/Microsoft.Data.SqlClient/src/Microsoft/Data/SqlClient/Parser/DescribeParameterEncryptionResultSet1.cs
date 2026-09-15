// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser;

// Fields in the first resultset of "sp_describe_parameter_encryption".
// We expect the server to return the fields in the resultset in the same order as mentioned below.
// If the server changes the below order, then transparent parameter encryption will break.
internal enum DescribeParameterEncryptionResultSet1
{
    KeyOrdinal = 0,
    DbId,
    KeyId,
    KeyVersion,
    KeyMdVersion,
    EncryptedKey,
    ProviderName,
    KeyPath,
    KeyEncryptionAlgorithm,
    IsRequestedByEnclave,
    KeySignature,
}
