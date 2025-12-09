// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System;

namespace Interop.Windows.NtDll
{
    /// <summary>
    /// Options for creating/opening files with NtCreateFile.
    /// </summary>
    [Flags]
    internal enum CreateOptions : uint
    {
        /// <summary>
        /// File being created or opened must be a directory file. Disposition must be FILE_CREATE, FILE_OPEN,
        /// or FILE_OPEN_IF.
        /// </summary>
        /// <remarks>
        /// Can only be used with FILE_SYNCHRONOUS_IO_ALERT/NONALERT, FILE_WRITE_THROUGH, FILE_OPEN_FOR_BACKUP_INTENT,
        /// and FILE_OPEN_BY_FILE_ID flags.
        /// </remarks>
        FILE_DIRECTORY_FILE = 0x00000001,

        /// <summary>
        /// Applications that write data to the file must actually transfer the data into
        /// the file before any requested write operation is considered complete. This flag
        /// is set automatically if FILE_NO_INTERMEDIATE_BUFFERING is set.
        /// </summary>
        FILE_WRITE_THROUGH = 0x00000002,

        /// <summary>
        /// All accesses to the file are sequential.
        /// </summary>
        FILE_SEQUENTIAL_ONLY = 0x00000004,

        /// <summary>
        /// File cannot be cached in driver buffers. Cannot use with AppendData desired access.
        /// </summary>
        FILE_NO_INTERMEDIATE_BUFFERING = 0x00000008,

        /// <summary>
        /// All operations are performed synchronously. Any wait on behalf of the caller is
        /// subject to premature termination from alerts.
        /// </summary>
        /// <remarks>
        /// Cannot be used with FILE_SYNCHRONOUS_IO_NONALERT.
        /// Synchronous DesiredAccess flag is required. I/O system will maintain file position context.
        /// </remarks>
        FILE_SYNCHRONOUS_IO_ALERT = 0x00000010,

        /// <summary>
        /// All operations are performed synchronously. Waits in the system to synchronize I/O queuing
        /// and completion are not subject to alerts.
        /// </summary>
        /// <remarks>
        /// Cannot be used with FILE_SYNCHRONOUS_IO_ALERT.
        /// Synchronous DesiredAccess flag is required. I/O system will maintain file position context.
        /// </remarks>
        FILE_SYNCHRONOUS_IO_NONALERT = 0x00000020,

        /// <summary>
        /// File being created or opened must not be a directory file. Can be a data file, device,
        /// or volume.
        /// </summary>
        FILE_NON_DIRECTORY_FILE = 0x00000040,

        /// <summary>
        /// Create a tree connection for this file in order to open it over the network.
        /// </summary>
        /// <remarks>
        /// Not used by device and intermediate drivers.
        /// </remarks>
        FILE_CREATE_TREE_CONNECTION = 0x00000080,

        /// <summary>
        /// Complete the operation immediately with a success code of STATUS_OPLOCK_BREAK_IN_PROGRESS if
        /// the target file is oplocked.
        /// </summary>
        /// <remarks>
        /// Not compatible with ReserveOpfilter or OpenRequiringOplock.
        /// Not used by device and intermediate drivers.
        /// </remarks>
        FILE_COMPLETE_IF_OPLOCKED = 0x00000100,

        /// <summary>
        /// If the extended attributes on an existing file being opened indicate that the caller must
        /// understand extended attributes to properly interpret the file, fail the request.
        /// </summary>
        /// <remarks>
        /// Not used by device and intermediate drivers.
        /// </remarks>
        FILE_NO_EA_KNOWLEDGE = 0x00000200,

        // Behavior undocumented, defined in headers
        // FILE_OPEN_REMOTE_INSTANCE = 0x00000400,

        /// <summary>
        /// Accesses to the file can be random, so no sequential read-ahead operations should be performed
        /// on the file by FSDs or the system.
        /// </summary>
        FILE_RANDOM_ACCESS = 0x00000800,

        /// <summary>
        /// Delete the file when the last handle to it is passed to NtClose. Requires Delete flag in
        /// DesiredAccess parameter.
        /// </summary>
        FILE_DELETE_ON_CLOSE = 0x00001000,

        /// <summary>
        /// Open the file by reference number or object ID. The file name that is specified by the ObjectAttributes
        /// name parameter includes the 8 or 16 byte file reference number or ID for the file in the ObjectAttributes
        /// name field. The device name can optionally be prefixed.
        /// </summary>
        /// <remarks>
        /// NTFS supports both reference numbers and object IDs. 16 byte reference numbers are 8 byte numbers padded
        /// with zeros. ReFS only supports reference numbers (not object IDs). 8 byte and 16 byte reference numbers
        /// are not related. Note that as the UNICODE_STRING will contain raw byte data, it may not be a "valid" string.
        /// Not used by device and intermediate drivers.
        /// </remarks>
        /// <example>
        /// \??\C:\{8 bytes of binary FileID}
        /// \device\HardDiskVolume1\{16 bytes of binary ObjectID}
        /// {8 bytes of binary FileID}
        /// </example>
        FILE_OPEN_BY_FILE_ID = 0x00002000,

        /// <summary>
        /// The file is being opened for backup intent. Therefore, the system should check for certain access rights
        /// and grant the caller the appropriate access to the file before checking the DesiredAccess parameter
        /// against the file's security descriptor.
        /// </summary>
        /// <remarks>
        /// Not used by device and intermediate drivers.
        /// </remarks>
        FILE_OPEN_FOR_BACKUP_INTENT = 0x00004000,

        /// <summary>
        /// When creating a file, specifies that it should not inherit the compression bit from the parent directory.
        /// </summary>
        FILE_NO_COMPRESSION = 0x00008000,

        /// <summary>
        /// The file is being opened and an opportunistic lock (oplock) on the file is being requested as a single atomic
        /// operation.
        /// </summary>
        /// <remarks>
        /// The file system checks for oplocks before it performs the create operation and will fail the create with a
        /// return code of STATUS_CANNOT_BREAK_OPLOCK if the result would be to break an existing oplock.
        /// Not compatible with CompleteIfOplocked or ReserveOpFilter. Windows 7 and up.
        /// </remarks>
        FILE_OPEN_REQUIRING_OPLOCK = 0x00010000,

        /// <summary>
        /// CreateFile2 uses this flag to prevent opening a file that you don't have access to without specifying
        /// FILE_SHARE_READ. (Preventing users that can only read a file from denying access to other readers.)
        /// </summary>
        /// <remarks>
        /// Windows 7 and up.
        /// </remarks>
        FILE_DISALLOW_EXCLUSIVE = 0x00020000,

        /// <summary>
        /// The client opening the file or device is session aware and per session access is validated if necessary.
        /// </summary>
        /// <remarks>
        /// Windows 8 and up.
        /// </remarks>
        FILE_SESSION_AWARE = 0x00040000,

        /// <summary>
        /// This flag allows an application to request a filter opportunistic lock (oplock) to prevent other applications
        /// from getting share violations.
        /// </summary>
        /// <remarks>
        /// Not compatible with CompleteIfOplocked or OpenRequiringOplock.
        /// If there are already open handles, the create request will fail with STATUS_OPLOCK_NOT_GRANTED.
        /// </remarks>
        FILE_RESERVE_OPFILTER = 0x00100000,

        /// <summary>
        /// Open a file with a reparse point attribute, bypassing the normal reparse point processing.
        /// </summary>
        FILE_OPEN_REPARSE_POINT = 0x00200000,

        /// <summary>
        /// Causes files that are marked with the Offline attribute not to be recalled from remote storage.
        /// </summary>
        /// <remarks>
        /// More details can be found in Remote Storage documentation (see Basic Concepts).
        /// https://technet.microsoft.com/en-us/library/cc938459.aspx
        /// </remarks>
        FILE_OPEN_NO_RECALL = 0x00400000

        // Behavior undocumented, defined in headers
        // FILE_OPEN_FOR_FREE_SPACE_QUERY = 0x00800000
    }
}

#endif
