// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.Common
{
    internal class ADPHelper : IDisposable
    {
        List<string> _originalAzureSqlServerEndpoints;

        internal ADPHelper()
        {
            _originalAzureSqlServerEndpoints = [.. ADP.s_azureSqlServerEndpoints];
        }

        internal void AddAzureSqlServerEndpoint(string endpoint)
        {
            ADP.s_azureSqlServerEndpoints.Add(endpoint);
        }

        public void Dispose()
        {
            ADP.s_azureSqlServerEndpoints.Clear();
            ADP.s_azureSqlServerEndpoints.AddRange(_originalAzureSqlServerEndpoints);
        }
    }
}
