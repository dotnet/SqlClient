// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Interop_TEMP.Windows;

internal partial class Interop
{
    internal partial class NtDll
    {
        // https://msdn.microsoft.com/en-us/library/bb432380.aspx
        // https://msdn.microsoft.com/en-us/library/windows/hardware/ff566424.aspx
        [DllImport(Libraries.NtDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private unsafe static extern int NtCreateFile(
            out IntPtr FileHandle,
            DesiredAccess DesiredAccess,
            ref OBJECT_ATTRIBUTES ObjectAttributes,
            out IO_STATUS_BLOCK IoStatusBlock,
            long* AllocationSize,
            System.IO.FileAttributes FileAttributes,
            System.IO.FileShare ShareAccess,
            CreateDisposition CreateDisposition,
            CreateOptions CreateOptions,
            void* EaBuffer,
            uint EaLength);

        internal static unsafe (int status, IntPtr handle) CreateFile(
            string path,
            byte[] eaName,
            byte[] eaValue,

            DesiredAccess desiredAccess,
            FileAttributes fileAttributes,
            FileShare shareAccess,
            CreateDisposition createDisposition,
            CreateOptions createOptions

            #if NETFRAMEWORK
            ,ImpersonationLevel impersonationLevel,
            bool isDynamicTracking,
            bool isEffectiveOnly
            #endif
        )
        {
            // Acquire space for the file extended attribute
            int eaHeaderSize = sizeof(FILE_FULL_EA_INFORMATION);
            int eaBufferSize = eaHeaderSize + eaName.Length + eaValue.Length;
            Span<byte> eaBuffer = stackalloc byte[eaBufferSize];

            // Fix the position of the path and the extended attribute buffer
            fixed (char* pPath = path)
            fixed (byte* pEaBuffer = eaBuffer)
            {
                // Generate a unicode string object from the path
                UnicodeString ucPath = new UnicodeString(pPath, path.Length);

                #if NETFRAMEWORK
                // Generate a Security QOS object
                SecurityQualityOfService qos = new SecurityQualityOfService(
                    impersonationLevel,
                    isDynamicTracking,
                    isEffectiveOnly);
                SecurityQualityOfService* pQos = &qos;
                #else
                SecurityQualityOfService* pQos = null;
                #endif

                // Generate the object attributes object that defines what we're opening
                OBJECT_ATTRIBUTES attributes = new OBJECT_ATTRIBUTES(
                    objectName: &ucPath,
                    attributes: ObjectAttributes.OBJ_CASE_INSENSITIVE,
                    rootDirectory: IntPtr.Zero,
                    securityQos: pQos);

                // Set the contents of the extended information
                // NOTE: This chunk of code treats a byte[] as FILE_FULL_EA_INFORMATION. Since we
                //    do not have a direct reference to a FILE_FULL_EA_INFORMATION, we have to use
                //    the -> operator to dereference the object before accessing its members.
                //    However, the byte[] is longer than the FILE_FULL_EA_INFORMATION struct in
                //    order to contain the name and value. Since byte[] are reference types, we
                //    cannot store the name/value directly in the struct (in memory it would be
                //    stored as a pointer). So in the second chunk, we copy the name/value to the
                //    byte[] after the FILE_FULL_EA_INFORMATION struct.
                // Step 1) Write the header
                FILE_FULL_EA_INFORMATION* pEaObj = (FILE_FULL_EA_INFORMATION*)pEaBuffer;
                pEaObj->NextEntryOffset = 0;
                pEaObj->Flags = 0;
                pEaObj->EaNameLength = (byte)(eaName.Length - 1); // Null terminator is not included
                pEaObj->EaValueLength = (ushort)eaValue.Length;

                // Step 2) Write the contents
                eaName.AsSpan().CopyTo(eaBuffer.Slice(eaHeaderSize));
                eaValue.AsSpan().CopyTo(eaBuffer.Slice(eaHeaderSize + eaName.Length));

                // Make the interop call
                int status = NtCreateFile(
                    out IntPtr handle,
                    desiredAccess,
                    ref attributes,
                    IoStatusBlock: out _,
                    AllocationSize: null,
                    fileAttributes,
                    shareAccess,
                    createDisposition,
                    createOptions,
                    pEaBuffer,
                    (uint) eaBufferSize);
                return (status, handle);
            }
        }

        /// <summary>
        /// <a href="https://msdn.microsoft.com/en-us/library/windows/hardware/ff557749.aspx">OBJECT_ATTRIBUTES</a> structure.
        /// The OBJECT_ATTRIBUTES structure specifies attributes that can be applied to objects or object handles by routines 
        /// that create objects and/or return handles to objects.
        /// </summary>
        internal unsafe struct OBJECT_ATTRIBUTES
        {
            public uint Length;

            /// <summary>
            /// Optional handle to root object directory for the given ObjectName.
            /// Can be a file system directory or object manager directory.
            /// </summary>
            public IntPtr RootDirectory;

            /// <summary>
            /// Name of the object. Must be fully qualified if RootDirectory isn't set.
            /// Otherwise is relative to RootDirectory.
            /// </summary>
            public UnicodeString* ObjectName;

            public ObjectAttributes Attributes;

            /// <summary>
            /// If null, object will receive default security settings.
            /// </summary>
            public void* SecurityDescriptor;

            /// <summary>
            /// Optional quality of service to be applied to the object. Used to indicate
            /// security impersonation level and context tracking mode (dynamic or static).
            /// </summary>
            public SecurityQualityOfService* SecurityQoS;

            /// <summary>
            /// Equivalent of InitializeObjectAttributes macro with the exception that you can directly set SQOS.
            /// </summary>
            public unsafe OBJECT_ATTRIBUTES(
                UnicodeString* objectName,
                ObjectAttributes attributes,
                IntPtr rootDirectory,
                SecurityQualityOfService* securityQos)
            {
                Length = (uint)sizeof(OBJECT_ATTRIBUTES);
                RootDirectory = rootDirectory;
                ObjectName = objectName;
                Attributes = attributes;
                SecurityDescriptor = null;
                SecurityQoS = securityQos;
            }
        }

        [Flags]
        public enum ObjectAttributes : uint
        {
            // https://msdn.microsoft.com/en-us/library/windows/hardware/ff564586.aspx
            // https://msdn.microsoft.com/en-us/library/windows/hardware/ff547804.aspx

            /// <summary>
            /// This handle can be inherited by child processes of the current process.
            /// </summary>
            OBJ_INHERIT = 0x00000002,

            /// <summary>
            /// This flag only applies to objects that are named within the object manager.
            /// By default, such objects are deleted when all open handles to them are closed.
            /// If this flag is specified, the object is not deleted when all open handles are closed.
            /// </summary>
            OBJ_PERMANENT = 0x00000010,

            /// <summary>
            /// Only a single handle can be open for this object.
            /// </summary>
            OBJ_EXCLUSIVE = 0x00000020,

            /// <summary>
            /// Lookups for this object should be case insensitive.
            /// </summary>
            OBJ_CASE_INSENSITIVE = 0x00000040,

            /// <summary>
            /// Create on existing object should open, not fail with STATUS_OBJECT_NAME_COLLISION.
            /// </summary>
            OBJ_OPENIF = 0x00000080,

            /// <summary>
            /// Open the symbolic link, not its target.
            /// </summary>
            OBJ_OPENLINK = 0x00000100,

            // Only accessible from kernel mode
            // OBJ_KERNEL_HANDLE

            // Access checks enforced, even in kernel mode
            // OBJ_FORCE_ACCESS_CHECK
            // OBJ_VALID_ATTRIBUTES = 0x000001F2
        }

        /// <summary>
        /// File creation disposition when calling directly to NT APIs.
        /// </summary>
        public enum CreateDisposition : uint
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
            /// Open if exists or fail if doesn't exist. Equivalent to OPEN_EXISTING or
            /// <see cref="System.IO.FileMode.Open"/>.
            /// </summary>
            /// <remarks>
            /// TruncateExisting also uses Open and then manually truncates the file
            /// by calling NtSetInformationFile with FileAllocationInformation and an
            /// allocation size of 0.
            /// </remarks>
            FILE_OPEN = 1,

            /// <summary>
            /// Create if doesn't exist or fail if does exist. Equivalent to CREATE_NEW
            /// or <see cref="System.IO.FileMode.CreateNew"/>.
            /// </summary>
            FILE_CREATE = 2,

            /// <summary>
            /// Open if exists or create if doesn't exist. Equivalent to OPEN_ALWAYS or
            /// <see cref="System.IO.FileMode.OpenOrCreate"/>.
            /// </summary>
            FILE_OPEN_IF = 3,

            /// <summary>
            /// Open and overwrite if exists or fail if doesn't exist. Equivalent to
            /// TRUNCATE_EXISTING or <see cref="System.IO.FileMode.Truncate"/>.
            /// </summary>
            FILE_OVERWRITE = 4,

            /// <summary>
            /// Open and overwrite if exists or create if doesn't exist. Equivalent to
            /// CREATE_ALWAYS or <see cref="System.IO.FileMode.Create"/>.
            /// </summary>
            FILE_OVERWRITE_IF = 5
        }

        /// <summary>
        /// Options for creating/opening files with NtCreateFile.
        /// </summary>
        [Flags]
        public enum CreateOptions : uint
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

        /// <summary>
        /// System.IO.FileAccess looks up these values when creating handles
        /// </summary>
        /// <remarks>
        /// File Security and Access Rights
        /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa364399.aspx
        /// </remarks>
        [Flags]
        public enum DesiredAccess : uint
        {
            // File Access Rights Constants
            // https://msdn.microsoft.com/en-us/library/windows/desktop/gg258116.aspx

            /// <summary>
            /// For a file, the right to read data from the file.
            /// </summary>
            /// <remarks>
            /// Directory version of this flag is <see cref="FILE_LIST_DIRECTORY"/>.
            /// </remarks>
            FILE_READ_DATA = 0x0001,

            /// <summary>
            /// For a directory, the right to list the contents.
            /// </summary>
            /// <remarks>
            /// File version of this flag is <see cref="FILE_READ_DATA"/>.
            /// </remarks>
            FILE_LIST_DIRECTORY = 0x0001,

            /// <summary>
            /// For a file, the right to write data to the file.
            /// </summary>
            /// <remarks>
            /// Directory version of this flag is <see cref="FILE_ADD_FILE"/>.
            /// </remarks>
            FILE_WRITE_DATA = 0x0002,

            /// <summary>
            /// For a directory, the right to create a file in a directory.
            /// </summary>
            /// <remarks>
            /// File version of this flag is <see cref="FILE_WRITE_DATA"/>.
            /// </remarks>
            FILE_ADD_FILE = 0x0002,

            /// <summary>
            /// For a file, the right to append data to a file. <see cref="FILE_WRITE_DATA"/> is needed
            /// to overwrite existing data.
            /// </summary>
            /// <remarks>
            /// Directory version of this flag is <see cref="FILE_ADD_SUBDIRECTORY"/>.
            /// </remarks>
            FILE_APPEND_DATA = 0x0004,

            /// <summary>
            /// For a directory, the right to create a subdirectory.
            /// </summary>
            /// <remarks>
            /// File version of this flag is <see cref="FILE_APPEND_DATA"/>.
            /// </remarks>
            FILE_ADD_SUBDIRECTORY = 0x0004,

            /// <summary>
            /// For a named pipe, the right to create a pipe instance.
            /// </summary>
            FILE_CREATE_PIPE_INSTANCE = 0x0004,

            /// <summary>
            /// The right to read extended attributes.
            /// </summary>
            FILE_READ_EA = 0x0008,

            /// <summary>
            /// The right to write extended attributes.
            /// </summary>
            FILE_WRITE_EA = 0x0010,

            /// <summary>
            /// The right to execute the file.
            /// </summary>
            /// <remarks>
            /// Directory version of this flag is <see cref="FILE_TRAVERSE"/>.
            /// </remarks>
            FILE_EXECUTE = 0x0020,

            /// <summary>
            /// For a directory, the right to traverse the directory.
            /// </summary>
            /// <remarks>
            /// File version of this flag is <see cref="FILE_EXECUTE"/>.
            /// </remarks>
            FILE_TRAVERSE = 0x0020,

            /// <summary>
            /// For a directory, the right to delete a directory and all
            /// the files it contains, including read-only files.
            /// </summary>
            FILE_DELETE_CHILD = 0x0040,

            /// <summary>
            /// The right to read attributes.
            /// </summary>
            FILE_READ_ATTRIBUTES = 0x0080,

            /// <summary>
            /// The right to write attributes.
            /// </summary>
            FILE_WRITE_ATTRIBUTES = 0x0100,

            /// <summary>
            /// All standard and specific rights. [FILE_ALL_ACCESS]
            /// </summary>
            FILE_ALL_ACCESS = DELETE | READ_CONTROL | WRITE_DAC | WRITE_OWNER | 0x1FF,

            /// <summary>
            /// The right to delete the object.
            /// </summary>
            DELETE = 0x00010000,

            /// <summary>
            /// The right to read the information in the object's security descriptor.
            /// Doesn't include system access control list info (SACL).
            /// </summary>
            READ_CONTROL = 0x00020000,

            /// <summary>
            /// The right to modify the discretionary access control list (DACL) in the
            /// object's security descriptor.
            /// </summary>
            WRITE_DAC = 0x00040000,

            /// <summary>
            /// The right to change the owner in the object's security descriptor.
            /// </summary>
            WRITE_OWNER = 0x00080000,

            /// <summary>
            /// The right to use the object for synchronization. Enables a thread to wait until the object
            /// is in the signaled state. This is required if opening a synchronous handle.
            /// </summary>
            SYNCHRONIZE = 0x00100000,

            /// <summary>
            /// Same as READ_CONTROL.
            /// </summary>
            STANDARD_RIGHTS_READ = READ_CONTROL,

            /// <summary>
            /// Same as READ_CONTROL.
            /// </summary>
            STANDARD_RIGHTS_WRITE = READ_CONTROL,

            /// <summary>
            /// Same as READ_CONTROL.
            /// </summary>
            STANDARD_RIGHTS_EXECUTE = READ_CONTROL,

            /// <summary>
            /// Maps internally to <see cref="FILE_READ_ATTRIBUTES"/> | <see cref="FILE_READ_DATA"/> | <see cref="FILE_READ_EA"/>
            /// | <see cref="STANDARD_RIGHTS_READ"/> | <see cref="SYNCHRONIZE"/>.
            /// (For directories, <see cref="FILE_READ_ATTRIBUTES"/> | <see cref="FILE_LIST_DIRECTORY"/> | <see cref="FILE_READ_EA"/>
            /// | <see cref="STANDARD_RIGHTS_READ"/> | <see cref="SYNCHRONIZE"/>.)
            /// </summary>
            FILE_GENERIC_READ = 0x80000000, // GENERIC_READ

            /// <summary>
            /// Maps internally to <see cref="FILE_APPEND_DATA"/> | <see cref="FILE_WRITE_ATTRIBUTES"/> | <see cref="FILE_WRITE_DATA"/>
            /// | <see cref="FILE_WRITE_EA"/> | <see cref="STANDARD_RIGHTS_READ"/> | <see cref="SYNCHRONIZE"/>.
            /// (For directories, <see cref="FILE_ADD_SUBDIRECTORY"/> | <see cref="FILE_WRITE_ATTRIBUTES"/> | <see cref="FILE_ADD_FILE"/> AddFile
            /// | <see cref="FILE_WRITE_EA"/> | <see cref="STANDARD_RIGHTS_READ"/> | <see cref="SYNCHRONIZE"/>.)
            /// </summary>
            FILE_GENERIC_WRITE = 0x40000000, // GENERIC WRITE

            /// <summary>
            /// Maps internally to <see cref="FILE_EXECUTE"/> | <see cref="FILE_READ_ATTRIBUTES"/> | <see cref="STANDARD_RIGHTS_EXECUTE"/>
            /// | <see cref="SYNCHRONIZE"/>.
            /// (For directories, <see cref="FILE_DELETE_CHILD"/> | <see cref="FILE_READ_ATTRIBUTES"/> | <see cref="STANDARD_RIGHTS_EXECUTE"/>
            /// | <see cref="SYNCHRONIZE"/>.)
            /// </summary>
            FILE_GENERIC_EXECUTE = 0x20000000 // GENERIC_EXECUTE
        }
    }
}
