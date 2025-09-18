// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.SqlServer.TDS.Servers
{
    public class TransientDelayTdsServerArguments : TdsServerArguments
    {
        /// <summary>
        /// The duration for which the server should sleep before responding to a request.
        /// </summary>
        public TimeSpan DelayDuration = TimeSpan.FromSeconds(0);

        /// <summary>
        /// Flag to consider when simulating a delay on the next request.
        /// </summary>
        public bool IsEnabledTransientDelay = false;

        /// <summary>
        /// Flag to consider when simulating a delay on each request.
        /// </summary>
        public bool IsEnabledPermanentDelay = false;

        /// <summary>
        /// The number of logins during which the delay should be applied.
        /// </summary>
        public int RepeatCount = 1;
    }
}
