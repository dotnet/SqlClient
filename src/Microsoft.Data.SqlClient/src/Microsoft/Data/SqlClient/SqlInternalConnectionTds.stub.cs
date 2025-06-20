// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: This is only a stub class for clearing errors while merging other files.

namespace Microsoft.Data.SqlClient
{
    internal class SqlInternalConnectionTds
    {
        internal SyncAsyncLock _parserLock = null;
        
        internal bool ThreadHasParserLockForClose { get; set; }

        internal void DoomThisConnection()
        {
        }
        
        internal class SyncAsyncLock
        {
            internal bool CanBeReleasedFromAnyThread { get; set; }
            
            internal void Release() { }
            
            internal void Wait(bool canReleaseFromAnyThread) {}
        }
    }
}
