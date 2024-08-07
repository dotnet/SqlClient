// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Data.SqlClientX.Tds.Tokens.LoginAck
{
    /// <summary>
    /// SQL interface type.
    /// </summary>
    internal enum SqlInterfaceType : byte
    {
        /// <summary>
        /// Default.
        /// </summary>
        Default = 0,

        /// <summary>
        /// T-SQL.
        /// </summary>
        TSql = 1
    }
}
