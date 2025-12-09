// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: Merge with other implementations (and/or introduce polymorphism to handle this indirection)
#if NET && _WINDOWS

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// This structure is used for transporting packet handle references between the
    /// TdsParserStateObject base class and Managed or Native implementations. It carries type
    /// information so that assertions about the type of handle can be made in the implemented
    /// abstract methods.
    /// </summary>
    /// <remarks>
    /// It is a ref struct so that it can only be used to transport the handles and not store them.
    /// If you change this type you must also change the version for the other platform.
    /// </remarks>
    internal readonly ref struct SessionHandle
    {
        public const int NativeHandleType = 1;
        public const int ManagedHandleType = 2;

        // @TODO: Make auto-properties
        public readonly ManagedSni.SniHandle ManagedHandle;
        public readonly SNIHandle NativeHandle;
        public readonly int Type;

        public SessionHandle(ManagedSni.SniHandle managedHandle, SNIHandle nativeHandle, int type)
        {
            Type = type;
            ManagedHandle = managedHandle;
            NativeHandle = nativeHandle;
        }

        public bool IsNull => (Type == NativeHandleType) ? NativeHandle is null : ManagedHandle is null;

        public static SessionHandle FromManagedSession(ManagedSni.SniHandle managedSessionHandle) =>
            new SessionHandle(managedSessionHandle, nativeHandle: null, ManagedHandleType);

        public static SessionHandle FromNativeHandle(SNIHandle nativeSessionHandle) =>
            new SessionHandle(managedHandle: null, nativeSessionHandle, NativeHandleType);
    }
}

#endif
