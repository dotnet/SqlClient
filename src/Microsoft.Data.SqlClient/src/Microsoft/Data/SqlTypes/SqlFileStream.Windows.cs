// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Permissions;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SqlFileStream/*' />
    public sealed partial class SqlFileStream : System.IO.Stream
    {
        // NOTE: if we ever unseal this class, be sure to specify the Name, SafeFileHandle, and
        //   TransactionContext accessors as virtual methods. Doing so now on a sealed class
        //   generates a compiler error (CS0549)

	// For EventTrace output
        private static int _objectTypeCount; // EventSource counter
        internal int ObjectID { get; } = Interlocked.Increment(ref _objectTypeCount);

        // from System.IO.FileStream implementation
        //  DefaultBufferSize = 4096;
        // SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent
        //   potential exceptions during Close/Finalization. Since System.IO.FileStream will
        //   not allow for a zero byte buffer, we'll create a one byte buffer which, in normal
        //   usage, will not be used and the user buffer will automatically flush directly to
        //   the disk cache. In pathological scenarios where the client is writing a single
        //   byte at a time, we'll explicitly call flush ourselves.
        internal const int DefaultBufferSize = 1;

        private const ushort IoControlCodeFunctionCode = 2392;
        // netcore private const int ERROR_MR_MID_NOT_FOUND = 317;
        // netcore #region Definitions from devioctl.h
        // netcore private const ushort FILE_DEVICE_FILE_SYSTEM = 0x0009;
        // netcore #endregion

        private System.IO.FileStream _m_fs;
        private string _m_path;
        private byte[] _m_txn;
        private bool _m_disposed;
        private static byte[] s_eaNameString = new byte[]
        {
            (byte)'F', (byte)'i', (byte)'l', (byte)'e', (byte)'s', (byte)'t', (byte)'r', (byte)'e', (byte)'a', (byte)'m', (byte)'_',
            (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s', (byte)'a', (byte)'c', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'_',
            (byte)'T', (byte)'a', (byte)'g', (byte) '\0'
        };

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor1/*' />
        public SqlFileStream(string path, byte[] transactionContext, FileAccess access) :
            this(path, transactionContext, access, FileOptions.None, 0)
        { }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ctor2/*' />
        public SqlFileStream(string path, byte[] transactionContext, FileAccess access, FileOptions options, long allocationSize)
        {
            // netcore using (TryEventScope.Create(SqlClientEventSource.Log.TryScopeEnterEvent("SqlFileStream.ctor | API | Object Id {0} | Access {1} | Options {2} | Path '{3}'", ObjectID, (int)access, (int)options, path)))
            // netfx using (TryEventScope.Create(SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlFileStream.ctor|API> {0} access={1} options={2} path='{3}'", ObjectID, (int)access, (int)options, path)))
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

                _m_disposed = false;
                _m_fs = null;

                OpenSqlFileStream(path, transactionContext, access, options, allocationSize);

                // only set internal state once the file has actually been successfully opened
                Name = path;
                TransactionContext = transactionContext;
            }
        }

        #region destructor/dispose code

        // NOTE: this destructor will only be called only if the Dispose
        //   method is not called by a client, giving the class a chance
        //   to finalize properly (i.e., free unmanaged resources)
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/dtor/*' />
        ~SqlFileStream()
        {
            Dispose(false);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Dispose/*' />
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!_m_disposed)
                {
                    try
                    {
                        if (disposing)
                        {
                            if (_m_fs != null)
                            {
                                _m_fs.Close();
                                _m_fs = null;
                            }
                        }
                    }
                    finally
                    {
                        _m_disposed = true;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
        #endregion

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Name/*' />
        public string Name
        {
            get
            {
                // Assert that path has been properly processed via GetFullPathInternal
                // (e.g. m_path hasn't been set directly)
                AssertPathFormat(_m_path);
                return _m_path;
            }
            // netfx [ResourceExposure(ResourceScope.None)] // SxS: the file name is not exposed
            // netfx [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
            private set
            {
                // should be validated by callers of this method
                Debug.Assert(value != null);
                Debug.Assert(!_m_disposed);

                _m_path = GetFullPathInternal(value);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/TransactionContext/*' />
        public byte[] TransactionContext
        {
            get
            {
                if (_m_txn == null)
                    return null;

                return (byte[])_m_txn.Clone();
            }
            private set
            {
                // should be validated by callers of this method
                Debug.Assert(value != null);
                Debug.Assert(!_m_disposed);

                _m_txn = (byte[])value.Clone();
            }
        }

        #region System.IO.Stream methods

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanRead/*' />
        public override bool CanRead
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.CanRead;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanSeek/*' />
        // If CanSeek is false, Position, Seek, Length, and SetLength should throw.
        public override bool CanSeek
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.CanSeek;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanTimeout/*' />
        // netfx [ComVisible(false)]
        public override bool CanTimeout
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.CanTimeout;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/CanWrite/*' />
        public override bool CanWrite
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.CanWrite;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Length/*' />
        public override long Length
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.Length;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Position/*' />
        public override long Position
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.Position;
            }
            set
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                _m_fs.Position = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadTimeout/*' />
        // netfx [ComVisible(false)]
        public override int ReadTimeout
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.ReadTimeout;
            }
            set
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                _m_fs.ReadTimeout = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteTimeout/*' />
        // netfx [ComVisible(false)]
        public override int WriteTimeout
        {
            get
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                return _m_fs.WriteTimeout;
            }
            set
            {
                if (_m_disposed)
                    throw ADP.ObjectDisposed(this);

                _m_fs.WriteTimeout = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Flush/*' />
        public override void Flush()
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            _m_fs.Flush();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginRead/*' />
#if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
#endif
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            return _m_fs.BeginRead(buffer, offset, count, callback, state);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndRead/*' />
        public override int EndRead(IAsyncResult asyncResult)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            return _m_fs.EndRead(asyncResult);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/BeginWrite/*' />
#if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
#endif
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            IAsyncResult asyncResult = _m_fs.BeginWrite(buffer, offset, count, callback, state);

            // SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent
            //   potential exceptions during Close/Finalization. Since System.IO.FileStream will
            //   not allow for a zero byte buffer, we'll create a one byte buffer which, in normal
            //   usage, will not be used and the user buffer will automatically flush directly to
            //   the disk cache. In pathological scenarios where the client is writing a single
            //   byte at a time, we'll explicitly call flush ourselves.
            if (count == 1)
            {
                // calling flush here will mimic the internal control flow of System.IO.FileStream
                _m_fs.Flush();
            }

            return asyncResult;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/EndWrite/*' />
        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            _m_fs.EndWrite(asyncResult);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Seek/*' />
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            return _m_fs.Seek(offset, origin);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/SetLength/*' />
        public override void SetLength(long value)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            _m_fs.SetLength(value);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Read/*' />
        public override int Read([In, Out] byte[] buffer, int offset, int count)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            return _m_fs.Read(buffer, offset, count);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/ReadByte/*' />
        public override int ReadByte()
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            return _m_fs.ReadByte();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/Write/*' />
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            _m_fs.Write(buffer, offset, count);

            // SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent
            //   potential exceptions during Close/Finalization. Since System.IO.FileStream will
            //   not allow for a zero byte buffer, we'll create a one byte buffer which, in normal
            //   usage, will cause System.IO.FileStream to utilize the user-supplied buffer and
            //   automatically flush the data directly to the disk cache. In pathological scenarios
            //   where the user is writing a single byte at a time, we'll explicitly call flush ourselves.
            if (count == 1)
            {
                // calling flush here will mimic the internal control flow of System.IO.FileStream
                _m_fs.Flush();
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlFileStream.xml' path='docs/members[@name="SqlFileStream"]/WriteByte/*' />
        public override void WriteByte(byte value)
        {
            if (_m_disposed)
                throw ADP.ObjectDisposed(this);

            _m_fs.WriteByte(value);

            // SQLBUVSTS# 193123 - disable lazy flushing of written data in order to prevent
            //   potential exceptions during Close/Finalization. Since our internal buffer is
            //   only a single byte in length, the provided user data will always be cached.
            //   As a result, we need to be sure to flush the data to disk ourselves.

            // calling flush here will mimic the internal control flow of System.IO.FileStream
            _m_fs.Flush();
        }

        #endregion

        // netfx static private readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

        // netfx // path length limitations:
        // netfx // 1. path length storage (in bytes) in UNICODE_STRING is limited to UInt16.MaxValue bytes = Int16.MaxValue chars
        // netfx // 2. GetFullPathName API of kernel32 does not accept paths with length (in chars) greater than 32766
        // netfx //    (32766 is actually Int16.MaxValue - 1, while (-1) is for NULL termination)
        // netfx // We must check for the lowest value between the the two
        // netfx private const int MaxWin32PathLength = Int16.MaxValue - 1;

        [Conditional("DEBUG")]
        static private void AssertPathFormat(string path)
        {
            Debug.Assert(path != null);
            Debug.Assert(path == path.Trim());
            Debug.Assert(path.Length > 0);
            // netfx Debug.Assert(path.Length <= MaxWin32PathLength);
            // netfx Debug.Assert(path.IndexOfAny(InvalidPathChars) < 0);
            Debug.Assert(path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase));
            // netfx Debug.Assert(!path.StartsWith(@"\\.\", StringComparison.Ordinal));
        }

        // netfx // SQLBUVSTS01 bugs 192677 and 193221: we cannot use System.IO.Path.GetFullPath for two reasons:
        // netfx // * it requires PathDiscovery permissions, which is unnecessary for SqlFileStream since we 
        // netfx //   are dealing with network path
        // netfx // * it is limited to 260 length while in our case file path can be much longer
        // netfx // To overcome the above limitations we decided to use GetFullPathName function from kernel32.dll
        // netfx [ResourceExposure(ResourceScope.Machine)]
        // netfx [ResourceConsumption(ResourceScope.Machine)]
        static private string GetFullPathInternal(string path)
        {
            //-----------------------------------------------------------------
            // precondition validation

            // should be validated by callers of this method
            // NOTE: if this method moves elsewhere, this assert should become an actual runtime check
            //   as the implicit assumptions here cannot be relied upon in an inter-class context
            Debug.Assert(path != null);

            // remove leading and trailing whitespace
            path = path.Trim();
            if (path.Length == 0)
            {
                // netcore throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_InvalidPath), "path");
                // netfx throw ADP.Argument(StringsHelper.GetString(StringsHelper.SqlFileStream_InvalidPath), "path");
            }

            // netfx // check for the path length before we normalize it with GetFullPathName
            // netfx if (path.Length > MaxWin32PathLength)
            // netfx {
            // netfx     // cannot use PathTooLongException here since our length limit is 32K while
            // netfx     // PathTooLongException error message states that the path should be limited to 260
            // netfx    throw ADP.Argument(StringsHelper.GetString(StringsHelper.SqlFileStream_InvalidPath), "path");
            // netfx }

            // netfx // GetFullPathName does not check for invalid characters so we still have to validate them before
            // netfx if (path.IndexOfAny(InvalidPathChars) >= 0)
            // netfx {
            // netfx     throw ADP.Argument(StringsHelper.GetString(StringsHelper.SqlFileStream_InvalidPath), "path");
            // netfx }

            // netcore // make sure path is not DOS device path
            // netcore if (!path.StartsWith(@"\\", StringComparison.Ordinal) && !System.IO.PathInternal.IsDevice(path.AsSpan()))
            // netcore {
            // netcore     throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_InvalidPath), "path");
            // netcore }
            // netfx // make sure path is a UNC path
            // netfx if (!path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
            // netfx {
            // netfx     throw ADP.Argument(StringsHelper.GetString(StringsHelper.SqlFileStream_InvalidPath), "path");
            // netfx }

            //-----------------------------------------------------------------

            // normalize the path
            // netcore path = System.IO.Path.GetFullPath(path);
            // netfx path = UnsafeNativeMethods.SafeGetFullPathName(path);

            // netfx // we do not expect windows API to return invalid paths
            // netfx Debug.Assert(path.Length <= MaxWin32PathLength, "GetFullPathName returns path longer than max expected!");

            // netcore // make sure path is a UNC path
            // netcore if (System.IO.PathInternal.IsDeviceUNC(path.AsSpan()))
            // netcore {
            // netcore     throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_PathNotValidDiskResource), "path");
            // netcore }

            // netfx // CONSIDER: is this a precondition validation that can be done above? Or must the path be normalized first?
            // netfx // after normalization, we have to ensure that the path does not attempt to refer to a root device, etc.
            // netfx if (path.StartsWith(@"\\.\", StringComparison.Ordinal))
            // netfx {
            // netfx     throw ADP.Argument(StringsHelper.GetString(StringsHelper.SqlFileStream_PathNotValidDiskResource), "path");
            // netfx }

            return path;
        }

        // netfx ---
        static private void DemandAccessPermission
            (
                string path,
                System.IO.FileAccess access
            )
        {
            // ensure we demand on valid path
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
                    Debug.Assert(access == System.IO.FileAccess.ReadWrite);
                    demandPermissions = FileIOPermissionAccess.Read | FileIOPermissionAccess.Write;
                    break;
            }

            FileIOPermission filePerm;
            bool pathTooLong = false;

            // check for read and/or write permissions
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
                // SQLBUVSTS bugs 192677 and 203422: currently, FileIOPermission does not support path longer than MAX_PATH (260)
                // so we cannot demand permissions for long files. We are going to open bug for FileIOPermission to
                // support this.

                // In the meanwhile, we agreed to have try-catch block on the permission demand instead of checking the path length.
                // This way, if/when the 260-chars limitation is fixed in FileIOPermission, we will not need to change our code

                // since we do not want to relax security checks, we have to demand this permission for AllFiles in order to continue!
                // Note: demand for AllFiles will fail in scenarios where the running code does not have this permission (such as ASP.Net)
                // and the only workaround will be reducing the total path length, which means reducing the length of SqlFileStream path
                // components, such as instance name, table name, etc.. to fit into 260 characters
                filePerm = new FileIOPermission(PermissionState.Unrestricted);
                filePerm.AllFiles = demandPermissions;

                filePerm.Demand();
            }
        }
        // --- netfx

        private unsafe void OpenSqlFileStream
            (
                string path,
                byte[] transactionContext,
                System.IO.FileAccess access,
                System.IO.FileOptions options,
                long allocationSize
            )
        {
            //-----------------------------------------------------------------
            // precondition validation

            // these should be checked by any caller of this method
            // ensure we have validated and normalized the path before
            Debug.Assert(path != null);
            Debug.Assert(transactionContext != null);

            // netcore if (access != System.IO.FileAccess.Read && access != System.IO.FileAccess.Write && access != System.IO.FileAccess.ReadWrite)
            // netfx if (access != FileAccess.Read && access != FileAccess.Write && access != FileAccess.ReadWrite)
                throw ADP.ArgumentOutOfRange("access");

            // FileOptions is a set of flags, so AND the given value against the set of values we do not support
            // netcore if ((options & ~(System.IO.FileOptions.WriteThrough | System.IO.FileOptions.Asynchronous | System.IO.FileOptions.RandomAccess | System.IO.FileOptions.SequentialScan)) != 0)
            // netfx if ((options & ~(FileOptions.WriteThrough | FileOptions.Asynchronous | FileOptions.RandomAccess | FileOptions.SequentialScan)) != 0)
                throw ADP.ArgumentOutOfRange("options");

            //-----------------------------------------------------------------
            // normalize the provided path
            // * compress path to remove any occurrences of '.' or '..'
            // * trim whitespace from the beginning and end of the path
            // * ensure that the path starts with '\\'
            // * ensure that the path does not start with '\\.\'
            // netfx // * ensure that the path is not longer than Int16.MaxValue
            path = GetFullPathInternal(path);

            // netfx // ensure the running code has permission to read/write the file
            // netfx DemandAccessPermission(path, access);

            // netfx FileFullEaInformation eaBuffer = null;
            // netfx SecurityQualityOfService qos = null;
            // netfx UnicodeString objectName = null;

            Microsoft.Win32.SafeHandles.SafeFileHandle hFile = null;
            // netcore Interop.NtDll.DesiredAccess nDesiredAccess = Interop.NtDll.DesiredAccess.FILE_READ_ATTRIBUTES | Interop.NtDll.DesiredAccess.SYNCHRONIZE;
            // netfx int nDesiredAccess = UnsafeNativeMethods.FILE_READ_ATTRIBUTES | UnsafeNativeMethods.SYNCHRONIZE;
            // netcore Interop.NtDll.CreateOptions dwCreateOptions = 0;
            // netfx UInt32 dwCreateOptions = 0;
            // netcore Interop.NtDll.CreateDisposition dwCreateDisposition = 0;
            // netfx UInt32 dwCreateDisposition = 0;
            System.IO.FileShare shareAccess = System.IO.FileShare.None;

            switch (access)
            {
                case System.IO.FileAccess.Read:
                    // netcore nDesiredAccess |= Interop.NtDll.DesiredAccess.FILE_READ_DATA;
                    // netfx nDesiredAccess |= UnsafeNativeMethods.FILE_READ_DATA;
                    shareAccess = System.IO.FileShare.Delete | System.IO.FileShare.ReadWrite;
                    // netcore dwCreateDisposition = Interop.NtDll.CreateDisposition.FILE_OPEN;
                    // netfx dwCreateDisposition = (uint)UnsafeNativeMethods.CreationDisposition.FILE_OPEN;
                    break;

                case System.IO.FileAccess.Write:
                    // netcore nDesiredAccess |= Interop.NtDll.DesiredAccess.FILE_WRITE_DATA;
                    // netfx nDesiredAccess |= UnsafeNativeMethods.FILE_WRITE_DATA;
                    shareAccess = System.IO.FileShare.Delete | System.IO.FileShare.Read;
                    // netcore dwCreateDisposition = Interop.NtDll.CreateDisposition.FILE_OVERWRITE;
                    // netfx dwCreateDisposition = (uint)UnsafeNativeMethods.CreationDisposition.FILE_OVERWRITE;
                    break;

                case System.IO.FileAccess.ReadWrite:
                default:
                    // we validate the value of 'access' parameter in the beginning of this method
                    Debug.Assert(access == System.IO.FileAccess.ReadWrite);

                    // netcore nDesiredAccess |= Interop.NtDll.DesiredAccess.FILE_READ_DATA | Interop.NtDll.DesiredAccess.FILE_WRITE_DATA;
                    // netfx nDesiredAccess |= UnsafeNativeMethods.FILE_READ_DATA | UnsafeNativeMethods.FILE_WRITE_DATA;
                    shareAccess = System.IO.FileShare.Delete | System.IO.FileShare.Read;
                    // netcore dwCreateDisposition = Interop.NtDll.CreateDisposition.FILE_OVERWRITE;
                    // netfx dwCreateDisposition = (uint)UnsafeNativeMethods.CreationDisposition.FILE_OVERWRITE;
                    break;
            }

            if ((options & System.IO.FileOptions.WriteThrough) != 0)
            {
                // netcore dwCreateOptions |= Interop.NtDll.CreateOptions.FILE_WRITE_THROUGH;
                // netfx dwCreateOptions |= (uint)UnsafeNativeMethods.CreateOption.FILE_WRITE_THROUGH;
            }

            if ((options & System.IO.FileOptions.Asynchronous) == 0)
            {
                // netcore dwCreateOptions |= Interop.NtDll.CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT;
                // netfx dwCreateOptions |= (uint)UnsafeNativeMethods.CreateOption.FILE_SYNCHRONOUS_IO_NONALERT;
            }

            if ((options & System.IO.FileOptions.SequentialScan) != 0)
            {
                // netcore dwCreateOptions |= Interop.NtDll.CreateOptions.FILE_SEQUENTIAL_ONLY;
                // netfx dwCreateOptions |= (uint)UnsafeNativeMethods.CreateOption.FILE_SEQUENTIAL_ONLY;
            }

            if ((options & System.IO.FileOptions.RandomAccess) != 0)
            {
                // netcore dwCreateOptions |= Interop.NtDll.CreateOptions.FILE_RANDOM_ACCESS;
                // netfx dwCreateOptions |= (uint)UnsafeNativeMethods.CreateOption.FILE_RANDOM_ACCESS;
            }

            try
            {
                // netfx ---
                eaBuffer = new FileFullEaInformation(transactionContext);

                qos = new SecurityQualityOfService(UnsafeNativeMethods.SecurityImpersonationLevel.SecurityAnonymous,
                    false, false);
                // --- netfx

                // NOTE: the Name property is intended to reveal the publicly available moniker for the
                //   FILESTREAM attributed column data. We will not surface the internal processing that
                //   takes place to create the mappedPath.
                string mappedPath = InitializeNtPath(sPath);
                // netcore int retval = 0;
                // netcore Interop.Kernel32.SetThreadErrorMode(Interop.Kernel32.SEM_FAILCRITICALERRORS, out uint oldMode);
                // netfx objectName = new UnicodeString(mappedPath);

                // netfx ---
                UnsafeNativeMethods.OBJECT_ATTRIBUTES oa;
                oa.length = Marshal.SizeOf(typeof(UnsafeNativeMethods.OBJECT_ATTRIBUTES));
                oa.rootDirectory = IntPtr.Zero;
                oa.attributes = (int)UnsafeNativeMethods.Attributes.CaseInsensitive;
                oa.securityDescriptor = IntPtr.Zero;
                oa.securityQualityOfService = qos;
                oa.objectName = objectName;

                uint oldMode;
                uint retval = 0;

                UnsafeNativeMethods.SetErrorModeWrapper(UnsafeNativeMethods.SEM_FAILCRITICALERRORS, out oldMode);
                // --- netfx

                try
                {
                    // netcore ---
                    if (transactionContext.Length >= ushort.MaxValue)
                        throw ADP.ArgumentOutOfRange("transactionContext");

                    int headerSize = sizeof(Interop.NtDll.FILE_FULL_EA_INFORMATION);
                    int fullSize = headerSize + transactionContext.Length + s_eaNameString.Length;

                    byte[] buffer = ArrayPool<byte>.Shared.Rent(fullSize);

                    fixed (byte* b = buffer)
                    {
                        Interop.NtDll.FILE_FULL_EA_INFORMATION* ea = (Interop.NtDll.FILE_FULL_EA_INFORMATION*)b;
                        ea->NextEntryOffset = 0;
                        ea->Flags = 0;
                        ea->EaNameLength = (byte)(s_eaNameString.Length - 1); // Length does not include terminating null character.
                        ea->EaValueLength = (ushort)transactionContext.Length;

                        // We could continue to do pointer math here, chose to use Span for convenience to
                        // make sure we get the other members in the right place.
                        Span<byte> data = buffer.AsSpan(headerSize);
                        s_eaNameString.AsSpan().CopyTo(data);
                        data = data.Slice(s_eaNameString.Length);
                        transactionContext.AsSpan().CopyTo(data);

                        (int status, IntPtr handle) = Interop.NtDll.CreateFile(path: mappedPath.AsSpan(),
                                                                                rootDirectory: IntPtr.Zero,
                                                                                createDisposition: dwCreateDisposition,
                                                                                desiredAccess: nDesiredAccess,
                                                                                shareAccess: nShareAccess,
                                                                                fileAttributes: 0,
                                                                                createOptions: dwCreateOptions,
                                                                                eaBuffer: b,
                                                                                eaLength: (uint)fullSize);

                        SqlClientEventSource.Log.TryAdvancedTraceEvent("SqlFileStream.OpenSqlFileStream | ADV | Object Id {0}, Desired Access 0x{1}, Allocation Size {2}, File Attributes 0, Share Access 0x{3}, Create Disposition 0x{4}, Create Options 0x{5}", ObjectID, (int)nDesiredAccess, allocationSize, (int)nShareAccess, dwCreateDisposition, dwCreateOptions);

                        retval = status;
                        hFile = new SafeFileHandle(handle, true);
                    }

                    ArrayPool<byte>.Shared.Return(buffer);
                    // --- netcore
                    // netfx ---
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlFileStream.OpenSqlFileStream|ADV> {0}, desiredAccess=0x{1}, allocationSize={2}, " +
                       "fileAttributes=0x{3}, shareAccess=0x{4}, dwCreateDisposition=0x{5}, createOptions=0x{6}", ObjectID, (int)nDesiredAccess, allocationSize, 0, (int)shareAccess, dwCreateDisposition, dwCreateOptions);

                    retval = UnsafeNativeMethods.NtCreateFile(out hFile, nDesiredAccess,
                        ref oa, out UnsafeNativeMethods.IO_STATUS_BLOCK _, ref allocationSize,
                        0, shareAccess, dwCreateDisposition, dwCreateOptions,
                        eaBuffer, (uint)eaBuffer.Length);
                    // --- netfx
                }
                finally
                {
                    // netcore Interop.Kernel32.SetThreadErrorMode(oldMode, out oldMode);
                    // netfx UnsafeNativeMethods.SetErrorModeWrapper(oldMode, out oldMode);
                }

                switch (retval)
                {
                    case 0:
                        break;

                    // netcore case Interop.Errors.ERROR_SHARING_VIOLATION:
                    // netfx case UnsafeNativeMethods.STATUS_SHARING_VIOLATION:
                        // netcore throw ADP.InvalidOperation(StringsHelper.GetString(Strings.SqlFileStream_FileAlreadyInTransaction));
                        // netfx throw ADP.InvalidOperation(StringsHelper.GetString(StringsHelper.SqlFileStream_FileAlreadyInTransaction));

                    // netcore case Interop.Errors.ERROR_INVALID_PARAMETER:
                    // netfx case UnsafeNativeMethods.STATUS_INVALID_PARAMETER:
                        // netcore throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_InvalidParameter));
                        // netfx throw ADP.Argument(StringsHelper.GetString(StringsHelper.SqlFileStream_InvalidParameter));

                    // netcore case Interop.Errors.ERROR_FILE_NOT_FOUND:
                    // netfx case UnsafeNativeMethods.STATUS_OBJECT_NAME_NOT_FOUND:
                        {
                            System.IO.DirectoryNotFoundException e = new System.IO.DirectoryNotFoundException();
                            ADP.TraceExceptionAsReturnValue(e);
                            throw e;
                        }
                    default:
                        {
                            // netcore uint error = Interop.NtDll.RtlNtStatusToDosError(retval);
                            // netfx uint error = UnsafeNativeMethods.RtlNtStatusToDosError(retval);
                            // netcore if (error == ERROR_MR_MID_NOT_FOUND)
                            // netfx if (error == UnsafeNativeMethods.ERROR_MR_MID_NOT_FOUND)
                            {
                                // status code could not be mapped to a Win32 error code
                                error = (uint)retval;
                            }

                            System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception(unchecked((int)error));
                            ADP.TraceExceptionAsReturnValue(e);
                            throw e;
                        }
                }

                if (hFile.IsInvalid)
                {
                    // netcore System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception(Interop.Errors.ERROR_INVALID_HANDLE);
                    // netfx System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception(UnsafeNativeMethods.ERROR_INVALID_HANDLE);
                    ADP.TraceExceptionAsReturnValue(e);
                    throw e;
                }

                // netfx UnsafeNativeMethods.FileType fileType = UnsafeNativeMethods.GetFileType(hFile);
                // netfx if (fileType != UnsafeNativeMethods.FileType.Disk)
                // netcore if (Interop.Kernel32.GetFileType(hFile) != Interop.Kernel32.FileTypes.FILE_TYPE_DISK)
                {
                    hFile.Dispose();
                    // netcore throw ADP.Argument(StringsHelper.GetString(Strings.SqlFileStream_PathNotValidDiskResource));
                    // netfx throw ADP.Argument(StringsHelper.GetString(StringsHelper.SqlFileStream_PathNotValidDiskResource));
                }

                // if the user is opening the SQL FileStream in read/write mode, we assume that they want to scan
                // through current data and then append new data to the end, so we need to tell SQL Server to preserve
                // the existing file contents.
                if (access == System.IO.FileAccess.ReadWrite)
                {
                    // netcore ---
                    uint ioControlCode = Interop.Kernel32.CTL_CODE(FILE_DEVICE_FILE_SYSTEM,
                        IoControlCodeFunctionCode, (byte)Interop.Kernel32.IoControlTransferType.METHOD_BUFFERED,
                        (byte)Interop.Kernel32.IoControlCodeAccess.FILE_ANY_ACCESS);
                    // --- netcore
                    // netfx ---
                    uint ioControlCode = UnsafeNativeMethods.CTL_CODE(UnsafeNativeMethods.FILE_DEVICE_FILE_SYSTEM,
                        IoControlCodeFunctionCode, (byte)UnsafeNativeMethods.Method.METHOD_BUFFERED,
                        (byte)UnsafeNativeMethods.Access.FILE_ANY_ACCESS);
                    uint cbBytesReturned = 0;
                    // --- netfx

                    // netcore if (!Interop.Kernel32.DeviceIoControl(hFile, ioControlCode, IntPtr.Zero, 0, IntPtr.Zero, 0, out uint cbBytesReturned, IntPtr.Zero))
                    // netfx if (!UnsafeNativeMethods.DeviceIoControl(hFile, ioControlCode, IntPtr.Zero, 0, IntPtr.Zero, 0, out cbBytesReturned, IntPtr.Zero))
                    {
                        System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                        ADP.TraceExceptionAsReturnValue(e);
                        throw e;
                    }
                }

                // now that we've successfully opened a handle on the path and verified that it is a file,
                // use the SafeFileHandle to initialize our internal System.IO.FileStream instance
                // netcore ---
                System.Diagnostics.Debug.Assert(_m_fs == null);
                _m_fs = new System.IO.FileStream(hFile, access, DefaultBufferSize, ((options & System.IO.FileOptions.Asynchronous) != 0));
                // --- netcore
                // netfx ---
                // NOTE: need to assert UnmanagedCode permissions for this constructor. This is relatively benign
                //   in that we've done much the same validation as in the FileStream(string path, ...) ctor case
                //   most notably, validating that the handle type corresponds to an on-disk file.
                bool bRevertAssert = false;
                try
                {
                    SecurityPermission sp = new SecurityPermission(SecurityPermissionFlag.UnmanagedCode);
                    sp.Assert();
                    bRevertAssert = true;

                    System.Diagnostics.Debug.Assert(m_fs == null);
                    m_fs = new System.IO.FileStream(hFile, access, DefaultBufferSize, ((options & System.IO.FileOptions.Asynchronous) != 0));
                }
                finally
                {
                    if (bRevertAssert)
                        SecurityPermission.RevertAssert();
                }

                // --- netfx
            }
            catch
            {
                if (hFile != null && !hFile.IsInvalid)
                    hFile.Dispose();

                throw;
            }
            // netfx ---
            finally
            {
                if (eaBuffer != null)
                {
                    eaBuffer.Dispose();
                    eaBuffer = null;
                }

                if (qos != null)
                {
                    qos.Dispose();
                    qos = null;
                }

                if (objectName != null)
                {
                    objectName.Dispose();
                    objectName = null;
                }
            }
            // --- netfx
        }

        #region private helper methods

        // This method exists to ensure that the requested path name is unique so that SMB/DNS is prevented
        // from collapsing a file open request to a file handle opened previously. In the SQL FILESTREAM case,
        // this would likely be a file open in another transaction, so this mechanism ensures isolation.
        static private string InitializeNtPath(string path)
        {
            // Ensure we have validated and normalized the path before
            AssertPathFormat(path);

            // netcore string formatPath = @"\??\UNC\{0}\{1}";

            string uniqueId = Guid.NewGuid().ToString("N");
            // netcore return System.IO.PathInternal.IsDeviceUNC(path)
            // netcore     ? string.Format(CultureInfo.InvariantCulture, @"{0}\{1}", path.Replace(@"\\.", @"\??"), uniqueId)
            // netcore     : string.Format(CultureInfo.InvariantCulture, @"\??\UNC\{0}\{1}", path.Trim('\\'), uniqueId);
            // netfx return String.Format(CultureInfo.InvariantCulture, formatPath, path.Trim('\\'), uniqueId);
        }
        
        #endregion

        // netfx ---
    //-------------------------------------------------------------------------
    // UnicodeString
    //
    // Description: this class encapsulates the marshalling of data from a
    //   managed representation of the UNICODE_STRING struct into native code.
    //   As part of this task, it manages memory that is allocated in the
    //   native heap into which the managed representation is blitted. The
    //   class also implements a SafeHandle pattern to ensure that memory is
    //   not leaked in "exceptional" circumstances such as Thread.Abort().
    //
    //-------------------------------------------------------------------------

    internal class UnicodeString : SafeHandleZeroOrMinusOneIsInvalid
    {
        public UnicodeString(string path)
            : base(true)
        {
            Initialize(path);
        }

        // NOTE: SafeHandle's critical finalizer will call ReleaseHandle for us
        protected override bool ReleaseHandle()
        {
            if (base.handle == IntPtr.Zero)
                return true;

            Marshal.FreeHGlobal(base.handle);
            base.handle = IntPtr.Zero;

            return true;
        }

        private void Initialize(string path)
        {
            // pre-condition should be validated in public interface
            System.Diagnostics.Debug.Assert(path.Length <= (UInt16.MaxValue / sizeof(char)));

            UnsafeNativeMethods.UNICODE_STRING objectName;
            objectName.length = (UInt16)(path.Length * sizeof(char));
            objectName.maximumLength = (UInt16)(path.Length * sizeof(char));
            objectName.buffer = path;

            IntPtr pbBuffer = IntPtr.Zero;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            }
            finally
            {
                pbBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(objectName));
                if (pbBuffer != IntPtr.Zero)
                    SetHandle(pbBuffer);
            }

            bool mustRelease = false;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                DangerousAddRef(ref mustRelease);
                IntPtr ptr = DangerousGetHandle();

                Marshal.StructureToPtr(objectName, ptr, false);
            }
            finally
            {
                if (mustRelease)
                    DangerousRelease();
            }
        }
    }

    //-------------------------------------------------------------------------
    // SecurityQualityOfService
    //
    // Description: this class encapsulates the marshalling of data from a
    //   managed representation of the SECURITY_QUALITY_OF_SERVICE struct into 
    //   native code. As part of this task, it pins the struct in the managed
    //   heap to ensure that it is not moved around (since the struct consists
    //   of simple types, the type does not need to be blitted into native
    //   memory). The class also implements a SafeHandle pattern to ensure that 
    //   the struct is unpinned in "exceptional" circumstances such as 
    //   Thread.Abort().
    //
    //-------------------------------------------------------------------------

    internal class SecurityQualityOfService : SafeHandleZeroOrMinusOneIsInvalid
    {
        UnsafeNativeMethods.SECURITY_QUALITY_OF_SERVICE m_qos;
        private GCHandle m_hQos;

        public SecurityQualityOfService
            (
                UnsafeNativeMethods.SecurityImpersonationLevel impersonationLevel,
                bool effectiveOnly,
                bool dynamicTrackingMode
            )
            : base(true)
        {
            Initialize(impersonationLevel, effectiveOnly, dynamicTrackingMode);
        }

        protected override bool ReleaseHandle()
        {
            if (m_hQos.IsAllocated)
                m_hQos.Free();

            base.handle = IntPtr.Zero;

            return true;
        }

        internal void Initialize
            (
                UnsafeNativeMethods.SecurityImpersonationLevel impersonationLevel,
                bool effectiveOnly,
                bool dynamicTrackingMode
            )
        {
            m_qos.length = (uint)Marshal.SizeOf(typeof(UnsafeNativeMethods.SECURITY_QUALITY_OF_SERVICE));
            // VSTFDevDiv # 547461 [Backport SqlFileStream fix on Win7 to QFE branch]
            // Win7 enforces correct values for the _SECURITY_QUALITY_OF_SERVICE.qos member.
            m_qos.impersonationLevel = (int)impersonationLevel;

            m_qos.effectiveOnly = effectiveOnly ? (byte)1 : (byte)0;
            m_qos.contextDynamicTrackingMode = dynamicTrackingMode ? (byte)1 : (byte)0;

            IntPtr pbBuffer = IntPtr.Zero;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            }
            finally
            {
                // pin managed objects
                m_hQos = GCHandle.Alloc(m_qos, GCHandleType.Pinned);

                pbBuffer = m_hQos.AddrOfPinnedObject();

                if (pbBuffer != IntPtr.Zero)
                    SetHandle(pbBuffer);
            }
        }
    }

    //-------------------------------------------------------------------------
    // FileFullEaInformation
    //
    // Description: this class encapsulates the marshalling of data from a
    //   managed representation of the FILE_FULL_EA_INFORMATION struct into 
    //   native code. As part of this task, it manages memory that is allocated 
    //   in the native heap into which the managed representation is blitted. 
    //   The class also implements a SafeHandle pattern to ensure that memory
    //   is not leaked in "exceptional" circumstances such as Thread.Abort().
    //
    //-------------------------------------------------------------------------

    internal class FileFullEaInformation : SafeHandleZeroOrMinusOneIsInvalid
    {
        private string EA_NAME_STRING = "Filestream_Transaction_Tag";
        private int m_cbBuffer;

        public FileFullEaInformation(byte[] transactionContext)
            : base(true)
        {
            m_cbBuffer = 0;
            InitializeEaBuffer(transactionContext);
        }

        protected override bool ReleaseHandle()
        {
            m_cbBuffer = 0;

            if (base.handle == IntPtr.Zero)
                return true;

            Marshal.FreeHGlobal(base.handle);
            base.handle = IntPtr.Zero;

            return true;
        }

        public int Length
        {
            get
            {
                return m_cbBuffer;
            }
        }

        private void InitializeEaBuffer(byte[] transactionContext)
        {
            if (transactionContext.Length >= UInt16.MaxValue)
                throw ADP.ArgumentOutOfRange("transactionContext");

            UnsafeNativeMethods.FILE_FULL_EA_INFORMATION eaBuffer;
            eaBuffer.nextEntryOffset = 0;
            eaBuffer.flags = 0;
            eaBuffer.EaName = 0;

            // string will be written as ANSI chars, so Length == ByteLength in this case
            eaBuffer.EaNameLength = (byte)EA_NAME_STRING.Length;
            eaBuffer.EaValueLength = (ushort)transactionContext.Length;

            // allocate sufficient memory to contain the FILE_FULL_EA_INFORMATION struct and
            //   the contiguous name/value pair in eaName (note: since the struct already
            //   contains one byte for eaName, we don't need to allocate a byte for the 
            //   null character separator).
            m_cbBuffer = Marshal.SizeOf(eaBuffer) + eaBuffer.EaNameLength + eaBuffer.EaValueLength;

            IntPtr pbBuffer = IntPtr.Zero;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
            }
            finally
            {
                pbBuffer = Marshal.AllocHGlobal(m_cbBuffer);
                if (pbBuffer != IntPtr.Zero)
                    SetHandle(pbBuffer);
            }

            bool mustRelease = false;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                DangerousAddRef(ref mustRelease);
                IntPtr ptr = DangerousGetHandle();

                // write struct into buffer
                Marshal.StructureToPtr(eaBuffer, ptr, false);

                // write property name into buffer
                System.Text.ASCIIEncoding ascii = new System.Text.ASCIIEncoding();
                byte[] asciiName = ascii.GetBytes(EA_NAME_STRING);

                // calculate offset at which to write the name/value pair
                System.Diagnostics.Debug.Assert(Marshal.OffsetOf(typeof(UnsafeNativeMethods.FILE_FULL_EA_INFORMATION), "EaName").ToInt64() <= (Int64)Int32.MaxValue);
                int cbOffset = Marshal.OffsetOf(typeof(UnsafeNativeMethods.FILE_FULL_EA_INFORMATION), "EaName").ToInt32();
                for (int i = 0; cbOffset < m_cbBuffer && i < eaBuffer.EaNameLength; i++, cbOffset++)
                {
                    Marshal.WriteByte(ptr, cbOffset, asciiName[i]);
                }

                System.Diagnostics.Debug.Assert(cbOffset < m_cbBuffer);

                // write null character separator
                Marshal.WriteByte(ptr, cbOffset, 0);
                cbOffset++;

                System.Diagnostics.Debug.Assert(cbOffset < m_cbBuffer || transactionContext.Length == 0 && cbOffset == m_cbBuffer);

                // write transaction context ID
                for (int i = 0; cbOffset < m_cbBuffer && i < eaBuffer.EaValueLength; i++, cbOffset++)
                {
                    Marshal.WriteByte(ptr, cbOffset, transactionContext[i]);
                }
            }
            finally
            {
                if (mustRelease)
                    DangerousRelease();
            }
        }
        // --- netfx
    }
}
