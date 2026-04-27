// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Interop.Windows.NtDll
{
    /// <summary>
    /// File creation disposition when calling directly to NT APIs.
    /// </summary>
    internal enum CreateDisposition : uint
    {
        /// <summary>
        /// Default. Replace or create. Deletes existing file instead of overwriting.
        /// </summary>
        /// <remarks>
        /// As this potentially deletes it requires that DesiredAccess must include Delete.
        /// This has no equivalent in CreateFile.
        /// </remarks>
        FILE_SUPERSEDE = 0,

        /// <summary>
        /// Open if the file exists or fail if it doesn't exist. Equivalent to OPEN_EXISTING or
        /// <see cref="System.IO.FileMode.Open"/>.
        /// </summary>
        /// <remarks>
        /// TruncateExisting also uses Open and then manually truncates the file
        /// by calling NtSetInformationFile with FileAllocationInformation and an
        /// allocation size of 0.
        /// </remarks>
        FILE_OPEN = 1,

        /// <summary>
        /// Create if the file doesn't exist or fail if it does exist. Equivalent to CREATE_NEW
        /// or <see cref="System.IO.FileMode.CreateNew"/>.
        /// </summary>
        FILE_CREATE = 2,

        /// <summary>
        /// Open if the file exists or create if it doesn't exist. Equivalent to OPEN_ALWAYS or
        /// <see cref="System.IO.FileMode.OpenOrCreate"/>.
        /// </summary>
        FILE_OPEN_IF = 3,

        /// <summary>
        /// Open and overwrite if the file exists or fail if it doesn't exist. Equivalent to
        /// TRUNCATE_EXISTING or <see cref="System.IO.FileMode.Truncate"/>.
        /// </summary>
        FILE_OVERWRITE = 4,

        /// <summary>
        /// Open and overwrite if the file exists or create if it doesn't exist. Equivalent to
        /// CREATE_ALWAYS or <see cref="System.IO.FileMode.Create"/>.
        /// </summary>
        FILE_OVERWRITE_IF = 5
    }
}
