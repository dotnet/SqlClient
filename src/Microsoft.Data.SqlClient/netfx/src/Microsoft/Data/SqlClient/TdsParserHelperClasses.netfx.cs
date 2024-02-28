using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    internal enum EncryptionOptions
    {
        OFF,
        ON,
        NOT_SUP,
        REQ,
        LOGIN,
        OPTIONS_MASK = 0x3f,
        CTAIP = 0x40,
        CLIENT_CERT = 0x80,
    }

    internal sealed partial class SqlLoginAck
    {
        internal string programName;

        internal bool isVersion8;
    }

    internal sealed partial class _SqlMetaDataSet
    {
        private _SqlMetaDataSet(_SqlMetaDataSet original)
        {
            id = original.id;
            _hiddenColumnCount = original._hiddenColumnCount;
            _visibleColumnMap = original._visibleColumnMap;
            _schemaTable = original._schemaTable;
            if (original._metaDataArray == null)
            {
                _metaDataArray = null;
            }
            else
            {
                _metaDataArray = new _SqlMetaData[original._metaDataArray.Length];
                for (int idx = 0; idx < _metaDataArray.Length; idx++)
                {
                    _metaDataArray[idx] = (_SqlMetaData)original._metaDataArray[idx].Clone();
                }
            }
        }
    }

    internal sealed partial class SqlReturnValue
    {
        internal ushort parmIndex;      //2005 or later only
    }

    internal static class SslProtocolsHelper
    {
        // protocol versions from native sni
        [Flags]
        private enum NativeProtocols
        {
            SP_PROT_SSL2_SERVER = 0x00000004,
            SP_PROT_SSL2_CLIENT = 0x00000008,
            SP_PROT_SSL3_SERVER = 0x00000010,
            SP_PROT_SSL3_CLIENT = 0x00000020,
            SP_PROT_TLS1_0_SERVER = 0x00000040,
            SP_PROT_TLS1_0_CLIENT = 0x00000080,
            SP_PROT_TLS1_1_SERVER = 0x00000100,
            SP_PROT_TLS1_1_CLIENT = 0x00000200,
            SP_PROT_TLS1_2_SERVER = 0x00000400,
            SP_PROT_TLS1_2_CLIENT = 0x00000800,
            SP_PROT_TLS1_3_SERVER = 0x00001000,
            SP_PROT_TLS1_3_CLIENT = 0x00002000,
            SP_PROT_SSL2 = SP_PROT_SSL2_SERVER | SP_PROT_SSL2_CLIENT,
            SP_PROT_SSL3 = SP_PROT_SSL3_SERVER | SP_PROT_SSL3_CLIENT,
            SP_PROT_TLS1_0 = SP_PROT_TLS1_0_SERVER | SP_PROT_TLS1_0_CLIENT,
            SP_PROT_TLS1_1 = SP_PROT_TLS1_1_SERVER | SP_PROT_TLS1_1_CLIENT,
            SP_PROT_TLS1_2 = SP_PROT_TLS1_2_SERVER | SP_PROT_TLS1_2_CLIENT,
            SP_PROT_TLS1_3 = SP_PROT_TLS1_3_SERVER | SP_PROT_TLS1_3_CLIENT,
            SP_PROT_NONE = 0x0
        }

        private static string ToFriendlyName(this NativeProtocols protocol)
        {
            string name;

            if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_3_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_3_SERVER))
            {
                name = "TLS 1.3";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_2_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_2_SERVER))
            {
                name = "TLS 1.2";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_1_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_1_SERVER))
            {
                name = "TLS 1.1";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_0_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_0_SERVER))
            {
                name = "TLS 1.0";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_SSL3_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_SSL3_SERVER))
            {
                name = "SSL 3.0";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_SSL2_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_SSL2_SERVER))
            {
                name = "SSL 2.0";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_NONE))
            {
                name = "None";
            }
            else
            {
                throw new ArgumentException(StringsHelper.GetString(StringsHelper.net_invalid_enum, nameof(NativeProtocols)), nameof(NativeProtocols));
            }
            return name;
        }

        /// <summary>
        /// check the negotiated secure protocol if it's under TLS 1.2
        /// </summary>
        /// <param name="protocol"></param>
        /// <returns>Localized warning message</returns>
        public static string GetProtocolWarning(uint protocol)
        {
            var nativeProtocol = (NativeProtocols)protocol;
            string message = string.Empty;
            if ((nativeProtocol & (NativeProtocols.SP_PROT_SSL2 | NativeProtocols.SP_PROT_SSL3 | NativeProtocols.SP_PROT_TLS1_1)) != NativeProtocols.SP_PROT_NONE)
            {
                message = StringsHelper.GetString(Strings.SEC_ProtocolWarning, nativeProtocol.ToFriendlyName());
            }
            return message;
        }
    }
}
