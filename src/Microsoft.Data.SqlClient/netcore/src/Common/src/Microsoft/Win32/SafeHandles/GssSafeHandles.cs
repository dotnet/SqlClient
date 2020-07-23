// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static Interop.NetSecurityNative;

namespace Microsoft.Win32.SafeHandles
{
    /// <summary>
    /// Wrapper around a gss_name_t_desc*
    /// </summary>
    internal sealed class SafeGssNameHandle : SafeHandle
    {
        public static SafeGssNameHandle CreateUser(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "Invalid user name passed to SafeGssNameHandle create");
            Status status = ImportUserName(
                out Status minorStatus, name, Encoding.UTF8.GetByteCount(name), out SafeGssNameHandle retHandle);

            if (status != Status.GSS_S_COMPLETE)
            {
                retHandle.Dispose();
                throw new GssApiException(status, minorStatus);
            }

            return retHandle;
        }

        public static SafeGssNameHandle CreatePrincipal(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "Invalid principal passed to SafeGssNameHandle create");
            Status status = ImportPrincipalName(
                out Status minorStatus, name, Encoding.UTF8.GetByteCount(name), out SafeGssNameHandle retHandle);

            if (status != Status.GSS_S_COMPLETE)
            {
                retHandle.Dispose();
                throw new GssApiException(status, minorStatus);
            }

            return retHandle;
        }

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        protected override bool ReleaseHandle()
        {
            Status status = ReleaseName(out _, ref handle);
            SetHandle(IntPtr.Zero);
            return status == Status.GSS_S_COMPLETE;
        }

        private SafeGssNameHandle() : base(IntPtr.Zero, true) { }
    }

    /// <summary>
    /// Wrapper around a gss_cred_id_t_desc_struct*
    /// </summary>
    internal class SafeGssCredHandle : SafeHandle
    {
        /// <summary>
        ///  returns the handle for the given credentials.
        ///  The method returns an invalid handle if the username is null or empty.
        /// </summary>
        public static SafeGssCredHandle Create(string username, string password, bool isNtlmOnly)
        {
            if (string.IsNullOrEmpty(username))
            {
                return new SafeGssCredHandle();
            }

            SafeGssCredHandle retHandle = null;
            using (SafeGssNameHandle userHandle = SafeGssNameHandle.CreateUser(username))
            {
                Status status;
                Status minorStatus;
                if (string.IsNullOrEmpty(password))
                {
                    status = InitiateCredSpNego(out minorStatus, userHandle, out retHandle);
                }
                else
                {
                    status = InitiateCredWithPassword(out minorStatus, isNtlmOnly, userHandle, password, Encoding.UTF8.GetByteCount(password), out retHandle);
                }

                if (status != Status.GSS_S_COMPLETE)
                {
                    retHandle.Dispose();
                    throw new GssApiException(status, minorStatus);
                }
            }

            return retHandle;
        }

        private SafeGssCredHandle()
            : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        protected override bool ReleaseHandle()
        {
            Status status = ReleaseCred(out _, ref handle);
            SetHandle(IntPtr.Zero);
            return status == Status.GSS_S_COMPLETE;
        }
    }

    internal sealed class SafeGssContextHandle : SafeHandle
    {
        public SafeGssContextHandle() : base(IntPtr.Zero, true) { }

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        protected override bool ReleaseHandle()
        {
            Status status = DeleteSecContext(out _, ref handle);
            SetHandle(IntPtr.Zero);
            return status == Status.GSS_S_COMPLETE;
        }
    }
}
