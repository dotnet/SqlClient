// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.Data.Common
{
    /// <summary>
    /// This class defines the data structure for ActivityId used for correlated tracing between client (bid trace event) and server (XEvent).
    /// It also includes all the APIs used to access the ActivityId. Note: ActivityId is thread based which is stored in TLS.
    /// </summary>

    internal static class ActivityCorrelator
    {
        internal sealed class ActivityId
        {
            internal readonly Guid Id;
            internal readonly uint Sequence;

            internal ActivityId(uint sequence)
            {
                this.Id = Guid.NewGuid();
                this.Sequence = sequence;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", this.Id, this.Sequence);
            }
        }

        // Declare the ActivityId which will be stored in TLS. The Id is unique for each thread.
        // The Sequence number will be incremented when each event happens.
        // Correlation along threads is consistent with the current XEvent mechanism at server.
        [ThreadStatic]
        private static ActivityId t_tlsActivity;

        /// <summary>
        /// Get the current ActivityId
        /// </summary>
        internal static ActivityId Current
        {
            get
            {
                if (t_tlsActivity == null)
                {
                    t_tlsActivity = new ActivityId(1);
                }
                return t_tlsActivity;
            }
        }

        /// <summary>
        /// Increment the sequence number and generate the new ActivityId
        /// </summary>
        /// <returns>ActivityId</returns>
        internal static ActivityId Next()
        {
            t_tlsActivity = new ActivityId( (t_tlsActivity?.Sequence ?? 0) + 1);

            return t_tlsActivity;
        }
    }
}
