// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SqlServer.TDS.Servers
{
    public class TransientTdsErrorTdsServerArguments : TdsServerArguments
    {
        /// <summary>
        /// Transient error number to be raised by server.
        /// </summary>
        public uint Number = 0;

        /// <summary>
        /// Transient error message to be raised by server.
        /// </summary>
        public string Message = string.Empty;

        /// <summary>
        /// Flag to consider when raising Transient error.
        /// </summary>
        public bool IsEnabledTransientError = false;

        /// <summary>
        /// The number of times the transient error should be raised.
        /// </summary>
        public int RepeatCount = 1;
    }
}
