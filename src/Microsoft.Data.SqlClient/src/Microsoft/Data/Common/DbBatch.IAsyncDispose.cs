// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Data.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public partial class DbBatch : IAsyncDisposable
    {
        public virtual ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
