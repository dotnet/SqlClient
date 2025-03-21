// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.SqlClient.Tests;

namespace Microsoft.Data.SqlClient
{
#if NETFRAMEWORK
    using PacketHandle = IntPtr; 
#elif NETCOREAPP
    internal struct PacketHandle
    {
    }
#endif
    internal partial class TdsParserStateObject
    {
        internal int ObjectID = 1;

        internal class SQL
        {
            internal static Exception InvalidInternalPacketSize(string v) => throw new Exception(v ?? nameof(InvalidInternalPacketSize));

            internal static Exception ParsingError(ParsingErrorState state) => throw new Exception(state.ToString());
        }

        internal static class SqlClientEventSource
        {
            internal static class Log
            {
                internal static void TryAdvancedTraceBinEvent(string message, params object[] values)
                {
                }
            }
        }

        private enum SnapshotStatus
        {
            NotActive,
            ReplayStarting,
            ReplayRunning
        }

        internal enum TdsParserState
        {
            Closed,
            OpenNotLoggedIn,
            OpenLoggedIn,
            Broken,
        }

        private uint GetSniPacket(PacketHandle packet, ref uint dataSize)
        {
            return SniPacketGetData(packet, _inBuff, ref dataSize);
        }

        private class StringsHelper
        {
            internal static string GetString(string sqlMisc_InvalidArraySizeMessage) => Strings.SqlMisc_InvalidArraySizeMessage;
        }

        internal class Strings
        {
            internal static string SqlMisc_InvalidArraySizeMessage = nameof(SqlMisc_InvalidArraySizeMessage);

        }

        public class Parser
        {
            internal object ProcessSNIError(TdsParserStateObject tdsParserStateObject) => "ProcessSNIError";
            public TdsParserState State = TdsParserState.OpenLoggedIn;
        }

        sealed internal class LastIOTimer
        {
            internal long _value;
        }

        internal sealed class Snapshot
        {
            public List<PacketData> List;

            public Snapshot() => List = new List<PacketData>();
            [DebuggerStepThrough]
            internal void AssertCurrent() { }
            [DebuggerStepThrough]
            internal void AppendPacketData(byte[] buffer, int read) => List.Add(new PacketData(buffer, 0, read));
            [DebuggerStepThrough]
            internal void MoveNext()
            {

            }
        }

        public List<PacketData> Input;
        public PacketData Current;
        public bool IsAsync { get => _snapshot != null; }

        public int _packetSize;

        internal Snapshot _snapshot;
        public int _inBytesRead;
        public int _inBytesUsed;
        public byte[] _inBuff;
        [DebuggerStepThrough]
        public TdsParserStateObject(List<PacketData> input, int packetSize, bool isAsync)
        {
            _packetSize = packetSize;
            _inBuff = new byte[_packetSize];
            Input = input;
            if (isAsync)
            {
                _snapshot = new Snapshot();
            }
        }
        [DebuggerStepThrough]
        private uint SniPacketGetData(PacketHandle packet, byte[] inBuff, ref uint dataSize)
        {
            Span<byte> target = inBuff.AsSpan(0, _packetSize);
            Span<byte> source = Current.Array.AsSpan(Current.Start, Current.Length);
            source.CopyTo(target);
            dataSize = (uint)Current.Length;
            return TdsEnums.SNI_SUCCESS;
        }

        [DebuggerStepThrough]
        void SetBuffer(byte[] buffer, int inBytesUsed, int inBytesRead)
        {
            _inBuff = buffer;
            _inBytesUsed = inBytesUsed;
            _inBytesRead = inBytesRead;
        }

        //  stubs
        private LastIOTimer _lastSuccessfulIOTimer = new LastIOTimer();
        private Parser _parser = new Parser();
        private SnapshotStatus _snapshotStatus = SnapshotStatus.NotActive;

        [DebuggerStepThrough]
        private void SniReadStatisticsAndTracing() { }
        [DebuggerStepThrough]
        private void AssertValidState() { }

        [DebuggerStepThrough]
        private void AddError(object value) => throw new Exception(value as string ?? "AddError");

        internal static class LocalAppContextSwitches
        {
            public static bool UseCompatibilityProcessSni
            {
                get
                {
                    var switchesType = typeof(SqlCommand).Assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");

                    return (bool)switchesType.GetProperty(nameof(UseCompatibilityProcessSni), BindingFlags.Public | BindingFlags.Static).GetValue(null);
                }
            }

            public static bool UseCompatibilityAsyncBehaviour
            {
                get
                {
                    var switchesType = typeof(SqlCommand).Assembly.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");

                    return (bool)switchesType.GetProperty(nameof(UseCompatibilityAsyncBehaviour), BindingFlags.Public | BindingFlags.Static).GetValue(null);
                }
            }
        }

        #if NETFRAMEWORK
        private SniNativeWrapperImpl _native;
        internal SniNativeWrapperImpl SniNativeWrapper
        {
            get
            {
                if (_native == null)
                {
                    _native = new SniNativeWrapperImpl(this);
                }
                return _native;
            }
        }

        internal class SniNativeWrapperImpl
        {
            private readonly TdsParserStateObject _parent;
            internal SniNativeWrapperImpl(TdsParserStateObject parent) => _parent = parent;

            internal uint SniPacketGetData(PacketHandle packet, byte[] inBuff, ref uint dataSize) => _parent.SniPacketGetData(packet, inBuff, ref dataSize);
        }
        #endif
    }

    internal static class TdsEnums
    {
        public const uint SNI_SUCCESS = 0;        // The operation completed successfully.
        // header constants
        public const int HEADER_LEN = 8;
        public const int HEADER_LEN_FIELD_OFFSET = 2;
        public const int SPID_OFFSET = 4;
    }

    internal enum ParsingErrorState
    {
        CorruptedTdsStream = 18,
        ProcessSniPacketFailed = 19,
    }
}
