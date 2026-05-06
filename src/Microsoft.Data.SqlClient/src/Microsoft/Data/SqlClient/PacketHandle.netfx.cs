// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: Introduce polymorphism to remove need for this level of indirection
#if NETFRAMEWORK

using System;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// This structure is used for transporting packet handle references between the
    /// TdsParserStateObject base class and Managed or Native implementations. It prevents the
    /// native IntPtr type from being boxed and prevents the need to cast from object which loses
    /// compile time type safety. It carries type information so that assertions about the type of
    /// handle can be made in the implemented abstract methods.
    /// </summary>
    /// <remarks>
    /// It is a ref struct so that it can only be used to transport the handles and not store them.
    /// If you change this type you must also change the version for the other platform.
    /// </remarks>
    internal readonly ref struct PacketHandle
    {
        /// <summary>
        /// PacketHandle is transporting a native pointer. The NativePointer field is valid.
        /// A PacketHandle has this type when managed code is referencing a pointer to a
        /// packet which has been read from the native SNI layer.
        /// </summary>
        public const int NativePointerType = 1;

        /// <summary>
        /// PacketHandle is transporting a native packet. The NativePacket field is valid.
        /// A PacketHandle has this type when managed code is directly referencing a packet
        /// which is due to be passed to the native SNI layer.
        /// </summary>
        public const int NativePacketType = 2;

        // @TODO: To auto-properties
        public readonly SNIPacket NativePacket;
        public readonly IntPtr NativePointer;
        public readonly int Type;

        private PacketHandle(IntPtr nativePointer, SNIPacket nativePacket, int type)
        {
            Type = type;
            NativePointer = nativePointer;
            NativePacket = nativePacket;
        }

        public static PacketHandle FromNativePacket(SNIPacket nativePacket) =>
            new PacketHandle(nativePointer: IntPtr.Zero, nativePacket, NativePacketType);

        public static PacketHandle FromNativePointer(IntPtr nativePointer) =>
            new PacketHandle(nativePointer, nativePacket: null, NativePointerType);
    }
}

#endif
