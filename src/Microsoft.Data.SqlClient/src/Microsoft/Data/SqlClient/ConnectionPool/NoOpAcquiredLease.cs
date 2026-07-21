// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// A no-op <see cref="RateLimitLease"/> that is always acquired and performs no work on
    /// dispose. Used as a stand-in when no rate limiter is configured so the open path can
    /// treat the lease as unconditional. Stateless and safe to share across all callers; access
    /// the singleton via <see cref="Instance"/>.
    /// </summary>
    internal sealed class NoOpAcquiredLease : RateLimitLease
    {
        /// <summary>
        /// The shared singleton instance. Because it is stateless and its <see cref="Dispose"/>
        /// releases nothing, the same instance is handed to every caller that opens without a
        /// configured limiter and may be disposed any number of times, on any thread, without
        /// affecting other callers.
        /// </summary>
        public static readonly NoOpAcquiredLease Instance = new();

        private NoOpAcquiredLease()
        {
        }

        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames => Array.Empty<string>();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            // No resources to release. This is intentionally idempotent: the shared Instance is
            // handed to every no-limiter open, so Dispose may be called repeatedly and
            // concurrently with no effect.
        }
    }
}
