// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.SqlServer.TDS.Servers
{
    public class TransientTimeoutTdsServerArguments : TdsServerArguments
    {
        public TimeSpan SleepDuration { get; set; }

        /// <summary>
        /// Flag to consider when raising Transient error.
        /// </summary>
        public bool IsEnabledTransientTimeout { get; set; }

        /// <summary>
        /// Constructor to initialize
        /// </summary>
        public TransientTimeoutTdsServerArguments()
        {
            SleepDuration = TimeSpan.FromSeconds(0);
            IsEnabledTransientTimeout = false;
        }
    }
}
