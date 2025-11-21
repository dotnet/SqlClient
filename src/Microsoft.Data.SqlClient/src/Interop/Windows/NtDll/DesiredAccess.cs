// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System;

namespace Interop.Windows.NtDll
{
    /// <summary>
    /// System.IO.FileAccess looks up these values when creating handles
    /// </summary>
    /// <remarks>
    /// File Security and Access Rights
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa364399.aspx
    /// </remarks>
    [Flags]
    internal enum DesiredAccess : uint
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

#endif
