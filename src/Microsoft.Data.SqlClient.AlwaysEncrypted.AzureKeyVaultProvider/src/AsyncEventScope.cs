// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    /// <summary>
    /// Event source scope that can be used in async methods.
    /// </summary>
    /// <remarks>
    /// <see cref="SqlClientEventScope"/> is a <c>ref struct</c> and therefore cannot be held across an
    /// <c>await</c> boundary. This reference type equivalent emits the same scope enter/leave events and
    /// is only used on async paths, where its allocation is negligible compared to the network I/O performed.
    /// </remarks>
    internal sealed class AsyncEventScope : IDisposable
    {
        private readonly long _scopeId;
        private bool _disposed;

        private AsyncEventScope(long scopeId) => _scopeId = scopeId;

        /// <summary>
        /// Creates a new scope for a class with the calling member name.
        /// </summary>
        /// <param name="className">The name of the class entering the scope.</param>
        /// <param name="memberName">The name of the calling member (auto-populated by the compiler).</param>
        internal static AsyncEventScope Create(string className, [CallerMemberName] string memberName = "")
            => new(SqlClientEventSource.Log.TryScopeEnterEvent(className, memberName));

        /// <summary>
        /// Leaves the event scope if the scope ID is non-zero.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_scopeId != 0)
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(_scopeId);
            }
        }
    }
}
