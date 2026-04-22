// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: Merge with SessionHandle.windows (and/or introduce polymorphism to handle this indirection)
// @TODO: Also, why do we have native handle type defined in Unix which doesn't ever have a native handle type?
#if NET && _UNIX

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

        // @TODO: Auto-properties
        public readonly ManagedSni.SniHandle ManagedHandle;
        public readonly int Type;

        public SessionHandle(ManagedSni.SniHandle managedHandle, int type)
        {
            Type = type;
            ManagedHandle = managedHandle;
        }

        public bool IsNull => ManagedHandle is null;

        public static SessionHandle FromManagedSession(ManagedSni.SniHandle managedSessionHandle) =>
            new SessionHandle(managedSessionHandle, ManagedHandleType);
    }
}

#endif
