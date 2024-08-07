// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.FeatureExtAck
{
    /// <summary>
    /// Feature identifier.
    /// </summary>
    internal enum FeatureId : byte
    {
        /// <summary>
        /// Session recovery.
        /// </summary>
        SessionRecovery = 0x01,

        /// <summary>
        /// Federated authentication.
        /// </summary>
        FedAuth = 0x02,

        /// <summary>
        /// Column encryption.
        /// </summary>
        ColumnEncryption = 0x04,

        /// <summary>
        /// Global transactions.
        /// </summary>
        GlobalTransactions = 0x05,

        /// <summary>
        /// Azure SQL Support.
        /// </summary>
        AzureSqlSupport = 0x08,

        /// <summary>
        /// Feature terminator.
        /// </summary>
        Terminator = 0xFF
    }
}
