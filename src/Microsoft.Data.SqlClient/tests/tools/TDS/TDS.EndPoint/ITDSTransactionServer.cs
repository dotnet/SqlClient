// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SqlServer.TDS.EndPoint
{
    /// <summary>Optional transaction-manager support; existing fake peers are not required to implement it.</summary>
    public interface ITDSTransactionServer
    {
        /// <summary>Processes an actual transaction-manager request on its originating session.</summary>
        TDSMessageCollection OnTransactionManagerRequest(ITDSServerSession session, TDSMessage message);
    }
}
