// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    ///
    /// </summary>
    public class AzureADTokenRequestContext
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="resource"></param>
        public AzureADTokenRequestContext(string resource) { Resource = resource; }

        /// <summary>
        ///
        /// </summary>
        public string Resource { get; }
    }
}
