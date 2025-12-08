// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

namespace Interop.Windows
{
    // https://msdn.microsoft.com/en-us/library/windows/desktop/ms681382.aspx
    internal class SystemErrors
    {
        internal const int ERROR_SUCCESS = 0x00;
        
        internal const int ERROR_FILE_NOT_FOUND = 0x2;
        internal const int ERROR_INVALID_HANDLE = 0x6;
        internal const int ERROR_SHARING_VIOLATION = 0x20;
        internal const int ERROR_INVALID_PARAMETER = 0x57;

        /// <summary>
        /// The system cannot find message text for the provided error number.
        /// </summary>
        internal const int ERROR_MR_MID_NOT_FOUND = 317;
    }
}

#endif
