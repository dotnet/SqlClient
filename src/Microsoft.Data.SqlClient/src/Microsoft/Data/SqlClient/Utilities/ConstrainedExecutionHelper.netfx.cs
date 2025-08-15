// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Data.SqlClient.Utilities
{
    internal static class ConstrainedExecutionHelper
    {
        internal static void RunWithConnectionAbortAndCleanup(Action action, SqlConnection connectionToAbort)
        {
            TdsParser bestEffortCleanupTarget = null;
            
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(connectionToAbort);
                action();
            }
            catch (OutOfMemoryException e)
            {
                connectionToAbort.Abort(e);
                throw;
            }
            catch (StackOverflowException e)
            {
                connectionToAbort.Abort(e);
                throw;
            }
            catch (ThreadAbortException e)
            {
                connectionToAbort.Abort(e);
                SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                
                throw;
            }
        }
    }
}

#endif
