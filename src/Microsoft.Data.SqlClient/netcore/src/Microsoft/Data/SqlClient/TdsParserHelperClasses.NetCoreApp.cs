// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Security.Authentication;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient
{
    internal enum EncryptionOptions
    {
        OFF,
        ON,
        NOT_SUP,
        REQ,
        LOGIN
    }

    internal sealed partial class _SqlMetaDataSet
    {
        internal ReadOnlyCollection<DbColumn> _dbColumnSchema;

        private _SqlMetaDataSet(_SqlMetaDataSet original)
        {
            _id = original._id;
            _hiddenColumnCount = original._hiddenColumnCount;
            _visibleColumnMap = original._visibleColumnMap;
            _dbColumnSchema = original._dbColumnSchema;
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

    internal static class SslProtocolsHelper
    {
        private static string ToFriendlyName(this SslProtocols protocol)
        {
            string name;

            /* The SslProtocols.Tls13 is supported by netcoreapp3.1 and later
             * This driver does not support this version yet!
            if ((protocol & SslProtocols.Tls13) == SslProtocols.Tls13)
            {
                name = "TLS 1.3";
            }*/
            if ((protocol & SslProtocols.Tls12) == SslProtocols.Tls12)
            {
                name = "TLS 1.2";
            }
#if NET8_0_OR_GREATER
#pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
            else if ((protocol & SslProtocols.Tls11) == SslProtocols.Tls11)
            {
                name = "TLS 1.1";
            }
            else if ((protocol & SslProtocols.Tls) == SslProtocols.Tls)
            {
                name = "TLS 1.0";
            }
#if NET8_0_OR_GREATER
#pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
#endif
#pragma warning disable CS0618 // Type or member is obsolete: SSL is deprecated
            else if ((protocol & SslProtocols.Ssl3) == SslProtocols.Ssl3)
            {
                name = "SSL 3.0";
            }
            else if ((protocol & SslProtocols.Ssl2) == SslProtocols.Ssl2)
#pragma warning restore CS0618 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
            {
                name = "SSL 2.0";
            }
            else
            {
                name = protocol.ToString();
            }

            return name;
        }

        /// <summary>
        /// check the negotiated secure protocol if it's under TLS 1.2
        /// </summary>
        /// <param name="protocol"></param>
        /// <returns>Localized warning message</returns>
        public static string GetProtocolWarning(this SslProtocols protocol)
        {
            string message = string.Empty;
#if NET8_0_OR_GREATER
#pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
#pragma warning disable CS0618 // Type or member is obsolete : SSL is depricated
            if ((protocol & (SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11)) != SslProtocols.None)
#pragma warning restore CS0618 // Type or member is obsolete : SSL is depricated
#if NET8_0_OR_GREATER
#pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
#endif
            {
                message = StringsHelper.Format(Strings.SEC_ProtocolWarning, protocol.ToFriendlyName());
            }
            return message;
        }
    }
}
