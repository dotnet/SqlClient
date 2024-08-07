// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange
{
    /// <summary>
    /// Environment change token sub type.
    /// </summary>
    internal enum EnvChangeTokenSubType : byte
    {
        /// <summary>
        /// Database.
        /// </summary>
        Database = 1,

        /// <summary>
        /// Language.
        /// </summary>
        Language = 2,

        /// <summary>
        /// Character set.
        /// </summary>
        CharacterSet = 3,

        /// <summary>
        /// Packet size.
        /// </summary>
        PacketSize = 4,

        /// <summary>
        /// Unicode data sorting local id.
        /// </summary>
        UnicodeDataSortingLocalId = 5,

        /// <summary>
        /// Unicode data sorting comparison flags.
        /// </summary>
        UnicodeDataSortingComparisonFlags = 6,

        /// <summary>
        /// Sql collation.
        /// </summary>
        SqlCollation = 7,

        /// <summary>
        /// Begin transaction.
        /// </summary>
        BeginTransaction = 8,

        /// <summary>
        /// Commit transaction.
        /// </summary>
        CommitTransaction = 9,

        /// <summary>
        /// Rollback transaction.
        /// </summary>
        RollbackTransaction = 10,

        /// <summary>
        /// Enlist DTC transaction.
        /// </summary>
        EnlistDtcTransaction = 11,

        /// <summary>
        /// Deflect transaction.
        /// </summary>
        DefectTransaction = 12,

        /// <summary>
        /// Database mirroring partner (Real time log shipping).
        /// </summary>
        DatabaseMirroringPartner = 13,

        /// <summary>
        /// Promote transaction.
        /// </summary>
        PromoteTransaction = 15,

        /// <summary>
        /// Transaction manager address.
        /// </summary>
        TransactionManagerAddress = 16,

        /// <summary>
        /// Transaction ended.
        /// </summary>
        TransactionEnded = 17,

        /// <summary>
        /// Reset connection.
        /// </summary>
        ResetConnection = 18,

        /// <summary>
        /// User instance name.
        /// </summary>
        UserInstanceName = 19,

        /// <summary>
        /// Routing.
        /// </summary>
        Routing = 20
    }
}
