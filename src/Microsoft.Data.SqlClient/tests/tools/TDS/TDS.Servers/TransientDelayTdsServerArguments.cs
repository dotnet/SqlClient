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
        public TimeSpan SleepDuration = TimeSpan.FromSeconds(0);

        /// <summary>
        /// Flag to consider when simulating a timeout on the next request.
        /// </summary>
        public bool IsEnabledTransientTimeout = false;

        /// <summary>
        /// Flag to consider when simulating a timeout on each request.
        /// </summary>
        public bool IsEnabledPermanentTimeout = false;

        /// <summary>
        /// The number of times the transient error should be raised.
        /// </summary>
        public int RepeatCount = 1;
    }
}
