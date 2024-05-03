using System;
using System.Buffers;

namespace simplesqlclient
{
    internal sealed class SqlEnvChange
    {
        internal byte _type;
        internal byte _oldLength;
        internal int _newLength; // 7206 TDS changes makes this length an int
        internal int _length;
        internal string _newValue;
        internal string _oldValue;
        /// <summary>
        /// contains binary data, before using this field check newBinRented to see if you can take the field array or whether you should allocate and copy
        /// </summary>
        internal byte[] _newBinValue;
        /// <summary>
        /// contains binary data, before using this field check newBinRented to see if you can take the field array or whether you should allocate and copy
        /// </summary>
        internal byte[] _oldBinValue;
        internal long _newLongValue;
        internal long _oldLongValue;
        

        internal RoutingInfo _newRoutingInfo;
        internal bool _newBinRented;
        internal bool _oldBinRented;

        internal SqlEnvChange _next;

        internal void Clear()
        {
            _type = 0;
            _oldLength = 0;
            _newLength = 0;
            _length = 0;
            _newValue = null;
            _oldValue = null;
            if (_newBinValue != null)
            {
                Array.Clear(_newBinValue, 0, _newBinValue.Length);
                if (_newBinRented)
                {
                    ArrayPool<byte>.Shared.Return(_newBinValue);
                }
                _newBinValue = null;
            }
            if (_oldBinValue != null)
            {
                Array.Clear(_oldBinValue, 0, _oldBinValue.Length);
                if (_oldBinRented)
                {
                    ArrayPool<byte>.Shared.Return(_oldBinValue);
                }
                _oldBinValue = null;
            }
            _newBinRented = false;
            _oldBinRented = false;
            _newLongValue = 0;
            _oldLongValue = 0;
            _newRoutingInfo = null;
            _next = null;
        }
    }
    internal class RoutingInfo
    {
        internal byte Protocol { get; private set; }
        internal ushort Port { get; private set; }
        internal string ServerName { get; private set; }

        internal RoutingInfo(byte protocol, ushort port, string servername)
        {
            Protocol = protocol;
            Port = port;
            ServerName = servername;
        }
    }

}
