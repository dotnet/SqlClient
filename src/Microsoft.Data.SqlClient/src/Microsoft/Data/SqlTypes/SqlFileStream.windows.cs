// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Interop.Windows;
using Interop.Windows.Kernel32;
using Interop.Windows.NtDll;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Win32.SafeHandles;

#if NETFRAMEWORK
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
#endif

namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SqlFileStream/*' />
    public sealed class SqlFileStream : Stream
    {
        // NOTE: if we ever unseal this class, be sure to specify the Name, SafeFileHandle, and
        //   TransactionContext accessors as virtual methods. Doing so now on a sealed class
        //   generates a compiler error (CS0549)

        #region Constants

        /// <remarks>
        /// From System.IO.FileStream implementation: DefaultBufferSize = 4096;
        /// SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent potential
        /// exceptions during Close/Finalization. Since System.IO.FileStream will not allow for a
        /// zero byte buffer, we'll create a one byte buffer which, in normal usage, will not be
        /// used and the user buffer will automatically flush directly to the disk cache. In
        /// pathological scenarios where the client is writing a single byte at a time, we'll
        /// explicitly call flush ourselves.
        /// </remarks>
        private const int DefaultBufferSize = 1;

        private const FileOptions AllowedOptions = FileOptions.WriteThrough |
                                                   FileOptions.Asynchronous |
                                                   FileOptions.RandomAccess |
                                                   FileOptions.SequentialScan;

        private const ushort IoControlCodeFunctionCode = 2392;

        /// <summary>
        /// Used as the extended attribute name string. Preallocated as a byte array for ease of
        /// copying into the EA struct. Value is "Filestream_Transaction_Tag"
        /// </summary>
        private static readonly byte[] EaNameString = new byte[]
        {
            (byte)'F', (byte)'i', (byte)'l', (byte)'e', (byte)'s', (byte)'t', (byte)'r', (byte)'e', (byte)'a',
            (byte)'m', (byte)'_', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s', (byte)'a', (byte)'c',
            (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'_', (byte)'T', (byte)'a', (byte)'g', (byte) '\0',
        };

        #if NETFRAMEWORK
        private const short MaxWin32PathLengthChars = short.MaxValue - 1;
        private static readonly char[] InvalidPathCharacters = Path.GetInvalidPathChars();
        #endif

        #endregion

        #region Member Variables

	    /// <summary>
	    /// Counter for how many instances have been created, used in EventSource.
	    /// </summary>
        private static int _objectTypeCount;

        private readonly string _path;
        private readonly byte[] _transactionContext;
        private readonly int _objectId = Interlocked.Increment(ref _objectTypeCount);

        private FileStream _fileStream;
        private bool _isDisposed;

        #endregion

        #region Construction / Destruction

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor1/*' />
        public SqlFileStream(string path, byte[] transactionContext, FileAccess access)
            : this(path, transactionContext, access, FileOptions.None, 0)
        { }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor2/*' />
        public SqlFileStream(
            string path,
            byte[] transactionContext,
            FileAccess access,
            FileOptions options,
            long allocationSize)
        {
            // @TODO: Adopt netcore style format
            #if NETFRAMEWORK
            const string scopeFormat = "<sc.SqlFileStream.ctor|API> {0} access={1} options={2} path='{3}'";
            #else
            const string scopeFormat = "SqlFileStream.ctor | API | Object Id {0} | Access {1} | Options {2} | Path '{3}'";
            #endif

            long scopeId = SqlClientEventSource.Log.TryScopeEnterEvent(scopeFormat, _objectId, (int)access, (int)options, path);
            using (SqlClientEventScope.Create(scopeId))
            {
                //-----------------------------------------------------------------
                // precondition validation
                if (transactionContext == null)
                {
                    throw ADP.ArgumentNull("transactionContext");
                }

                if (path == null)
                {
                    throw ADP.ArgumentNull("path");
                }
                //-----------------------------------------------------------------

                _isDisposed = false;
                _fileStream = null;

                string normalizedPath = GetFullPathInternal(path);
                OpenSqlFileStream(normalizedPath, transactionContext, access, options, allocationSize);

                // only set internal state once the file has actually been successfully opened
                _path = normalizedPath;
                _transactionContext = (byte[])transactionContext.Clone();
            }
        }

        // NOTE: this destructor will only be called only if the Dispose
        //   method is not called by a client, giving the class a chance
        //   to finalize properly (i.e., free unmanaged resources)
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/dtor/*' />
        ~SqlFileStream()
        {
            Dispose(false);
        }

        #endregion

        #region Properties

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanRead/*' />
        public override bool CanRead
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.CanRead;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanSeek/*' />
        public override bool CanSeek
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.CanSeek;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanTimeout/*' />
        #if NETFRAMEWORK
        [ComVisible(false)]
        #endif
        public override bool CanTimeout
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.CanTimeout;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanWrite/*' />
        public override bool CanWrite
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.CanWrite;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Length/*' />
        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.Length;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Name/*' />
        public string Name
        {
            get
            {
                // Assert that path has been properly processed via GetFullPathInternal
                // (e.g. m_path hasn't been set directly)
                AssertPathFormat(_path);
                return _path;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Position/*' />
        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.Position;
            }
            set
            {
                ThrowIfDisposed();
                _fileStream.Position = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadTimeout/*' />
        #if NETFRAMEWORK
        [ComVisible(false)]
        #endif
        public override int ReadTimeout
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.ReadTimeout;
            }
            set
            {
                ThrowIfDisposed();
                _fileStream.ReadTimeout = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/TransactionContext/*' />
        public byte[] TransactionContext =>
            (byte[]) _transactionContext?.Clone();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteTimeout/*' />
        #if NETFRAMEWORK
        [ComVisible(false)]
        #endif
        public override int WriteTimeout
        {
            get
            {
                ThrowIfDisposed();
                return _fileStream.WriteTimeout;
            }
            set
            {
                ThrowIfDisposed();
                _fileStream.WriteTimeout = value;
            }
        }

        #endregion

        #region Public Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginRead/*' />
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();
            return _fileStream.BeginRead(buffer, offset, count, callback, state);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginWrite/*' />
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            ThrowIfDisposed();

            IAsyncResult asyncResult = _fileStream.BeginWrite(buffer, offset, count, callback, state);

            // SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent
            //   potential exceptions during Close/Finalization. Since System.IO.FileStream will
            //   not allow for a zero byte buffer, we'll create a one byte buffer which, in normal
            //   usage, will not be used and the user buffer will automatically flush directly to
            //   the disk cache. In pathological scenarios where the client is writing a single
            //   byte at a time, we'll explicitly call flush ourselves.
            if (count == 1)
            {
                // calling flush here will mimic the internal control flow of System.IO.FileStream
                _fileStream.Flush();
            }

            return asyncResult;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndRead/*' />
        public override int EndRead(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            return _fileStream.EndRead(asyncResult);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndWrite/*' />
        public override void EndWrite(IAsyncResult asyncResult)
        {
            ThrowIfDisposed();
            _fileStream.EndWrite(asyncResult);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Flush/*' />
        public override void Flush()
        {
            ThrowIfDisposed();
            _fileStream.Flush();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Read/*' />
        public override int Read([In, Out] byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            return _fileStream.Read(buffer, offset, count);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadByte/*' />
        public override int ReadByte()
        {
            ThrowIfDisposed();
            return _fileStream.ReadByte();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Seek/*' />
        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            return _fileStream.Seek(offset, origin);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SetLength/*' />
        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            _fileStream.SetLength(value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Write/*' />
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();

            _fileStream.Write(buffer, offset, count);

            // SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent
            //   potential exceptions during Close/Finalization. Since System.IO.FileStream will
            //   not allow for a zero byte buffer, we'll create a one byte buffer which, in normal
            //   usage, will cause System.IO.FileStream to utilize the user-supplied buffer and
            //   automatically flush the data directly to the disk cache. In pathological scenarios
            //   where the user is writing a single byte at a time, we'll explicitly call flush ourselves.
            if (count == 1)
            {
                // calling flush here will mimic the internal control flow of System.IO.FileStream
                _fileStream.Flush();
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteByte/*' />
        public override void WriteByte(byte value)
        {
            ThrowIfDisposed();

            _fileStream.WriteByte(value);

            // SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent
            //   potential exceptions during Close/Finalization. Since our internal buffer is
            //   only a single byte in length, the provided user data will always be cached.
            //   As a result, we need to be sure to flush the data to disk ourselves.

            // calling flush here will mimic the internal control flow of System.IO.FileStream
            _fileStream.Flush();
        }

        #endregion

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Dispose/*' />
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!_isDisposed)
                {
                    try
                    {
                        if (disposing)
                        {
                            if (_fileStream != null)
                            {
                                _fileStream.Close();
                                _fileStream = null;
                            }
                        }
                    }
                    finally
                    {
                        _isDisposed = true;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        #region Private Helper Methods

        [Conditional("DEBUG")]
        private static void AssertPathFormat(string path)
        {
            Debug.Assert(path != null);
            Debug.Assert(path == path.Trim());
            Debug.Assert(path.Length > 0);
            Debug.Assert(path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase));

            #if NETFRAMEWORK
            // * Path length storage (in bytes) in UNICODE_STRING is limited to ushort.MaxValue
            //   *bytes* (short.MaxValue *chars*)
            // * GetFullPathName API of kernel32 dos not accept paths with length (in chars)
            //   greater than 32766 (which is short.MaxValue - 1, where -1 allows for NULL termination)
            Debug.Assert(path.Length <= MaxWin32PathLengthChars);
            Debug.Assert(path.IndexOfAny(InvalidPathCharacters) < 0);
            #endif
        }

        #if NETFRAMEWORK
        private static void DemandAccessPermission (string path, FileAccess access)
        {
            // Ensure we demand for a valid path
            AssertPathFormat(path);

            FileIOPermissionAccess demandPermissions;
            switch (access)
            {
                case FileAccess.Read:
                    demandPermissions = FileIOPermissionAccess.Read;
                    break;

                case FileAccess.Write:
                    demandPermissions = FileIOPermissionAccess.Write;
                    break;

                case FileAccess.ReadWrite:
                default:
                    // the caller have to validate the value of 'access' parameter
                    Debug.Assert(access is FileAccess.ReadWrite);
                    demandPermissions = FileIOPermissionAccess.Read | FileIOPermissionAccess.Write;
                    break;
            }

            FileIOPermission filePerm;
            bool pathTooLong = false;

            // Check for read and/or write permissions
            try
            {
                filePerm = new FileIOPermission(demandPermissions, path);
                filePerm.Demand();
            }
            catch (PathTooLongException e)
            {
                pathTooLong = true;
                ADP.TraceExceptionWithoutRethrow(e);
            }

            if (pathTooLong)
            {
                // SQLBUVSTS bugs 192677 and 203422: currently, FileIOPermission does not support
                // path longer than MAX_PATH (260) so we cannot demand permissions for long files.
                // We are going to open bug for FileIOPermission to support this.

                // In the meanwhile, we agreed to have try-catch block on the permission demand
                // instead of checking the path length. This way, if/when the 260-chars limitation
                // is fixed in FileIOPermission, we will not need to change our code

                // since we do not want to relax security checks, we have to demand this permission
                // for AllFiles in order to continue!
                // Note: demand for AllFiles will fail in scenarios where the running code does not
                // have this permission (such as ASP.Net) and the only workaround will be reducing
                // the total path length, which means reducing the length of SqlFileStream path
                // components, such as instance name, table name, etc. to fit into 260 characters.
                filePerm = new FileIOPermission(PermissionState.Unrestricted) { AllFiles = demandPermissions };
                filePerm.Demand();
            }
        }
        #endif

        #if NETFRAMEWORK
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        #endif
        private static string GetFullPathInternal(string path)
        {
            //-----------------------------------------------------------------
            // Precondition Validation

            // Remove leading and trailing whitespace
            path = path.Trim();
            if (path.Length == 0)
            {
                throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_InvalidPath), "path");
            }

            // Make sure path is a UNC path and not a DOS device path
            if (!path.StartsWith(@"\\", StringComparison.Ordinal) || IsDevicePath(path))
            {
                throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_InvalidPath), "path");
            }
            //-----------------------------------------------------------------

            // Normalize the path
            #if NETFRAMEWORK
            // In netfx, the System.IO.Path.GetFullPath requires PathDiscovery permission, which is
            // not necessary since we are dealing with network paths. Thus, we are going directly
            // to the GetFullPathName function in kernel32.dll (SQLBUVSTS01 192677, 193221)
            path = GetFullPathNameNetfx(path);
            #else
            path = Path.GetFullPath(path);
            #endif

            // Validate after normalization
            // Make sure path is a UNC path (not a device or device UNC path)
            if (IsDevicePath(path) || IsDeviceUncPath(path))
            {
                throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_PathNotValidDiskResource), "path");
            }

            return path;
        }

        #if NETFRAMEWORK
        /// <summary>
        /// Makes the call to GetFullPathName in kernel32.dll and handles special conditions that
        /// may arise.
        /// </summary>
        /// <remarks>
        /// Do not use this in netcore - Path.GetFullPathName does not require additional
        /// permissions like netfx does.
        /// </remarks>
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        private static string GetFullPathNameNetfx(string path)
        {
            // In the most common case where (SqlFileStream),the 'full path' is expected to be the
            // same size as the provided path. However, we still need to allocate one extra char
            // for null termination.
            // Note: StringBuilder has a magical capability to be compatible with char*, and also
            //    provides wide-character support w/o messing around with encodings.
            StringBuilder buffer = new StringBuilder(path.Length + 1);

            // If everything goes correctly, we only need to call this once
            int fullPathLength = Kernel32.GetFullPathName(path, buffer.Capacity, buffer, IntPtr.Zero);

            // If our buffer was smaller than required, the buffer will be empty, but the full
            // path size will be the size we should reallocate to.
            if (fullPathLength > buffer.Capacity)
            {
                // Reallocate the buffer and try again
                buffer.Capacity = fullPathLength;
                fullPathLength = Kernel32.GetFullPathName(path, buffer.Capacity, buffer, IntPtr.Zero);
            }

            // If the method tells us the full path length is 0, then we have an error condition.
            if (fullPathLength == 0)
            {
                // GetFullPathName call failed
                int lastError = Marshal.GetLastWin32Error();
                if (lastError == 0)
                {
                    // We've found that in some cases, GetFullPathName will fail but does not set the
                    // last error correctly. For example, when the path provided is longer than 32k,
                    // the return value will be 0 (indicating failure), but GetLastWin32Error is also
                    // zero (indicating success).
                    // In this case, we will throw an invalid path exception.
                    throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_InvalidPath), "path");
                }

                // In any other error condition, we will throw a Win32 exception.
                Win32Exception e = new Win32Exception(lastError);
                ADP.TraceExceptionAsReturnValue(e);
                throw e;
            }

            // this should not happen since we already reallocate
            Debug.Assert(fullPathLength <= buffer.Capacity,
                string.Format(
                CultureInfo.InvariantCulture,
                "second call to GetFullPathName returned greater size: {0} > {1}",
                fullPathLength,
                buffer.Capacity));

            Debug.Assert(buffer.Length <= MaxWin32PathLengthChars,
                "kernel32.dll GetFullPathName returned path longer than max");

            return buffer.ToString();
        }
        #endif

        /// <summary>
        /// This method exists to ensure that the requested path name is unique so that SMB/DNS is
        /// prevented from collapsing a file open request to a file handle opened previously. In
        /// the SQL FILESTREAM case, this would likely be a file open in another transaction, so
        /// this mechanism ensures isolation.
        /// </summary>
        private static string InitializeNtPath(string path)
        {
            // Ensure we have validated and normalized the path before
            AssertPathFormat(path);

            string uniqueId = Guid.NewGuid().ToString("N");
            return IsDeviceUncPath(path)
                ? string.Format(CultureInfo.InvariantCulture, @"{0}\{1}", path.Replace(@"\\.", @"\??"), uniqueId)
                : string.Format(CultureInfo.InvariantCulture, @"\??\UNC\{0}\{1}", path.Trim('\\'), uniqueId);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the path uses any of the DOS device path syntaxes
        /// <list type='bullet'>
        ///   <item><c>\\.\</c></item>
        ///   <item><c>\\?\</c></item>
        ///   <item><c>\??\</c></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// Implementation lifted from System.IO.PathInternal
        /// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/IO/PathInternal.Windows.cs
        /// </remarks>
        private static bool IsDevicePath(string path)
        {
            return IsExtendedPath(path)
                   ||
                   (
                       path.Length >= 4
                       && IsDirectorySeparator(path[0])
                       && IsDirectorySeparator(path[1])
                       && (path[2] == '.' || path[2] == '?')
                       && IsDirectorySeparator(path[3])
                   );
        }

        /// <summary>
        /// Returns true if the path is a device UNC path:
        /// <list type="bullet">
        ///   <item><c>\\.\UNC\</c></item>
        ///   <item><c>\\?\UNC\</c></item>
        ///   <item><c>\??\UNC\</c></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// Implementation lifted from System.IO.PathInternal
        /// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/IO/PathInternal.Windows.cs
        /// </remarks>
        private static bool IsDeviceUncPath(string path)
        {
            return path.Length >= 8
                   && IsDevicePath(path)
                   && IsDirectorySeparator(path[7])
                   && path[4] == 'U'
                   && path[5] == 'N'
                   && path[6] == 'C';
        }

        /// <summary>
        /// Returns <see langword="true"/> if the given character is a directory separator.
        /// </summary>
        /// <remarks>
        /// Implementation lifted from System.IO.PathInternal.
        /// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/IO/PathInternal.Windows.cs
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDirectorySeparator(char c) =>
            c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        /// <summary>
        /// Returns <see langword='true' /> if the path uses the canonical form of extended syntax
        /// (<c>\\?\</c> or <c>\??\</c>). If the path matches exactly (cannot use alternative
        /// directory separators) Windows will skip normalization and path length checks.
        /// </summary>
        /// <remarks>
        /// Implementation lifted from System.IO.PathInternal.
        /// https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/IO/PathInternal.Windows.cs
        /// </remarks>
        private static bool IsExtendedPath(string path)
        {
            return path.Length >= 4
                   && path[0] == '\\'
                   && (path[1] == '\\' || path[1] == '?')
                   && path[2] == '?'
                   && path[3] == '\\';
        }

        private static FileStream OpenFileStream(SafeFileHandle fileHandle, FileAccess access, FileOptions options)
        {
            #if NETFRAMEWORK
            // NOTE: We need to assert UnmanagedCode permissions for this constructor. This is
            //   relatively benign in that we've done much the same validation as in the
            //   FileStream(string path, ...) ctor case most notably, validating that the handle
            //   type corresponds to an on-disk file.
            // This likely only applies in partially trusted environments and is not required in
            // netcore since CAS was removed.
            bool bRevertAssert = false;
            try
            {
                SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                sp.Assert();
                bRevertAssert = true;

                return new FileStream(fileHandle, access, DefaultBufferSize,(options & FileOptions.Asynchronous) != 0);
            }
            finally
            {
                if (bRevertAssert)
                {
                    CodeAccessPermission.RevertAssert();
                }
            }
            #else
            return new FileStream(fileHandle, access, DefaultBufferSize, (options & FileOptions.Asynchronous) != 0);
            #endif
        }

        private void OpenSqlFileStream(
            string path,
            byte[] transactionContext,
            FileAccess access,
            FileOptions options,
            long allocationSize)
        {
            if (access is not (FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite))
            {
                throw ADP.ArgumentOutOfRange("access");
            }

            if ((options & ~AllowedOptions) != 0)
            {
                throw ADP.ArgumentOutOfRange("options");
            }

            #if NETFRAMEWORK
            // Ensure the running code has permission to read/write the file
            DemandAccessPermission(path, access);
            #endif

            CreateOptions createOptions = 0;
            CreateDisposition createDisposition = 0;
            DesiredAccess desiredAccess = DesiredAccess.FILE_READ_ATTRIBUTES |
                                          DesiredAccess.SYNCHRONIZE;
            FileShare shareAccess = 0;

            switch (access)
            {
                case FileAccess.Read:
                    desiredAccess |= DesiredAccess.FILE_READ_DATA;
                    shareAccess = FileShare.Delete | FileShare.ReadWrite;
                    createDisposition = CreateDisposition.FILE_OPEN;
                    break;

                case FileAccess.Write:
                    desiredAccess |= DesiredAccess.FILE_WRITE_DATA;
                    shareAccess = FileShare.Delete | FileShare.Read;
                    createDisposition = CreateDisposition.FILE_OVERWRITE;
                    break;

                case FileAccess.ReadWrite:
                    desiredAccess |= DesiredAccess.FILE_READ_DATA |
                                     DesiredAccess.FILE_WRITE_DATA;
                    shareAccess = FileShare.Delete | FileShare.Read;
                    createDisposition = CreateDisposition.FILE_OVERWRITE;
                    break;

                // Note: default case is heuristically unreachable due to check above.
            }

            if ((options & FileOptions.WriteThrough) != 0)
            {
                createOptions |= CreateOptions.FILE_WRITE_THROUGH;
            }

            if ((options & FileOptions.Asynchronous) == 0)
            {
                createOptions |= CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT;
            }

            if ((options & FileOptions.SequentialScan) != 0)
            {
                createOptions |= CreateOptions.FILE_SEQUENTIAL_ONLY;
            }

            if ((options & FileOptions.RandomAccess) != 0)
            {
                createOptions |= CreateOptions.FILE_RANDOM_ACCESS;
            }

            SafeFileHandle fileHandle = null;
            try
            {
                // NOTE: the Name property is intended to reveal the publicly available moniker for the
                //   FILESTREAM attributed column data. We will not surface the internal processing that
                //   takes place to create the mappedPath.
                string mappedPath = InitializeNtPath(path);

                Kernel32.SetThreadErrorMode(Kernel32.SEM_FAILCRITICALERRORS, out uint oldMode);

                // Make the interop call to open the file
                int retval;
                try
                {
                    if (transactionContext.Length >= ushort.MaxValue)
                    {
                        throw ADP.ArgumentOutOfRange("transactionContext");
                    }

                    IntPtr handle;

                    #if NETFRAMEWORK
                    const string traceEventMessage = "<sc.SqlFileStream.OpenSqlFileStream|ADV> {0}, desiredAccess=0x{1}, allocationSize={2}, fileAttributes=0x00, shareAccess=0x{3}, dwCreateDisposition=0x{4}, createOptions=0x{5}";
                    (retval, handle) = NtDll.CreateFile(
                        path: mappedPath,
                        eaName: EaNameString,
                        eaValue: transactionContext,
                        desiredAccess: desiredAccess,
                        fileAttributes: 0,
                        shareAccess: shareAccess,
                        createDisposition: createDisposition,
                        createOptions: createOptions,
                        impersonationLevel: ImpersonationLevel.SecurityAnonymous,
                        isDynamicTracking: false,
                        isEffectiveOnly: false);
                    #else
                    const string traceEventMessage = "SqlFileStream.OpenSqlFileStream | ADV | Object Id {0}, Desired Access 0x{1}, Allocation Size {2}, File Attributes 0, Share Access 0x{3}, Create Disposition 0x{4}, Create Options 0x{5}";
                    (retval, handle) = NtDll.CreateFile(
                        path: mappedPath,
                        eaName: EaNameString,
                        eaValue: transactionContext,
                        desiredAccess: desiredAccess,
                        fileAttributes: 0,
                        shareAccess: shareAccess,
                        createDisposition: createDisposition,
                        createOptions: createOptions);
                    #endif

                    SqlClientEventSource.Log.TryAdvancedTraceEvent(traceEventMessage, _objectId, (int)desiredAccess, allocationSize, (int)shareAccess, createDisposition, createOptions);

                    fileHandle = new SafeFileHandle(handle, true);
                }
                finally
                {
                    Kernel32.SetThreadErrorMode(oldMode, out oldMode);
                }

                // Handle error codes from the interop call
                switch (retval)
                {
                    case 0:
                        break;

                    case SystemErrors.ERROR_SHARING_VIOLATION:
                        throw ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlFileStream_FileAlreadyInTransaction));

                    case SystemErrors.ERROR_INVALID_PARAMETER:
                        throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_InvalidParameter));

                    case SystemErrors.ERROR_FILE_NOT_FOUND:
                        DirectoryNotFoundException dirNotFoundException = new DirectoryNotFoundException();
                        ADP.TraceExceptionAsReturnValue(dirNotFoundException);
                        throw dirNotFoundException;

                    default:
                        uint error = NtDll.RtlNtStatusToDosError(retval);
                        if (error == SystemErrors.ERROR_MR_MID_NOT_FOUND)
                        {
                            // status code could not be mapped to a Win32 error code
                            error = (uint)retval;
                        }

                        Win32Exception win32Exception = new Win32Exception(unchecked((int)error));
                        ADP.TraceExceptionAsReturnValue(win32Exception);
                        throw win32Exception;
                }

                // Make sure the file handle is usable for us
                if (fileHandle.IsInvalid)
                {
                    Win32Exception e = new Win32Exception(SystemErrors.ERROR_INVALID_HANDLE);
                    ADP.TraceExceptionAsReturnValue(e);
                    throw e;
                }

                if (Kernel32.GetFileType(fileHandle) != FileTypes.FILE_TYPE_DISK)
                {
                    fileHandle.Dispose();
                    throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_PathNotValidDiskResource));
                }

                // If the user is opening the SQL FileStream in read/write mode, we assume that
                // they want to scan through current data and then append new data to the end, so
                // we need to tell SQL Server to preserve the existing file contents.
                if (access is FileAccess.ReadWrite)
                {
                    uint ioControlCode = Kernel32.CtlCode(
                        Kernel32.FILE_DEVICE_FILE_SYSTEM,
                        IoControlCodeFunctionCode,
                        (byte)IoControlTransferType.METHOD_BUFFERED,
                        (byte)IoControlCodeAccess.FILE_ANY_ACCESS);

                    if (!Kernel32.DeviceIoControl(fileHandle, ioControlCode, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                    {
                        Win32Exception e = new Win32Exception(Marshal.GetLastWin32Error());
                        ADP.TraceExceptionAsReturnValue(e);
                        throw e;
                    }
                }

                // Now that we've successfully opened a handle on the path and verified that it is
                // a file, use the SafeFileHandle to initialize our internal FileStream instance.
                Debug.Assert(_fileStream is null);
                _fileStream = OpenFileStream(fileHandle, access, options);
            }
            catch
            {
                if (fileHandle is not null && !fileHandle.IsInvalid)
                {
                    fileHandle.Dispose();
                }

                throw;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw ADP.ObjectDisposed(this);
            }
        }

        #endregion
    }
}

#endif
