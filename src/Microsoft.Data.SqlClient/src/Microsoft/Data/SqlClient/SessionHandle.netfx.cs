// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: Merge with other implementations (and/or introduce polymorphism to handle this indirection)
#if NETFRAMEWORK

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// This structure is used for transporting packet handle references between the
    /// TdsParserStateObject base class and Managed or Native implementations. It carries type
    /// information so that assertions about the type of handle can be made in the implemented
    /// abstract methods.
    /// </summary>
    /// <remarks>
    /// It is a ref struct so that it can only be used to transport the handles and not store them
    /// If you change this type you must also change the version for the other platform.
    /// </remarks>
    internal readonly ref struct SessionHandle
    {
        // @TODO: Make internal, auto-property
        public readonly SNIHandle NativeHandle;

        public SessionHandle(SNIHandle nativeHandle)
        {
            NativeHandle = nativeHandle;
        }

        public bool IsNull
        {
            get => NativeHandle is null;
        }

        public static SessionHandle FromNativeHandle(SNIHandle nativeSessionHandle) =>
            new SessionHandle(nativeSessionHandle);
    }
}

#endif
