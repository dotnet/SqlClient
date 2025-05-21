// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlTypes
{
    /// <summary>
    /// This type provides workarounds for the separation between System.Data.Common
    /// and Microsoft.Data.SqlClient.  The latter wants to access internal members of the former, and
    /// this class provides ways to do that.  We must review and update this implementation any time the
    /// implementation of the corresponding types in System.Data.Common change.
    /// </summary>
    internal static partial class SqlTypeWorkarounds
    {
        #region Work around inability to access SqlXml.CreateSqlXmlReader
        private static readonly XmlReaderSettings s_defaultXmlReaderSettings = new() { ConformanceLevel = ConformanceLevel.Fragment };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsCloseInput = new() { ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsAsyncCloseInput = new() { Async = true, ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };

        internal const SqlCompareOptions SqlStringValidSqlCompareOptionMask =
            SqlCompareOptions.IgnoreCase | SqlCompareOptions.IgnoreWidth |
            SqlCompareOptions.IgnoreNonSpace | SqlCompareOptions.IgnoreKanaType |
            SqlCompareOptions.BinarySort | SqlCompareOptions.BinarySort2;

        internal static XmlReader SqlXmlCreateSqlXmlReader(Stream stream, bool closeInput, bool async)
        {
            Debug.Assert(closeInput || !async, "Currently we do not have pre-created settings for !closeInput+async");

            XmlReaderSettings settingsToUse = closeInput ?
                (async ? s_defaultXmlReaderSettingsAsyncCloseInput : s_defaultXmlReaderSettingsCloseInput) :
                s_defaultXmlReaderSettings;

            return XmlReader.Create(stream, settingsToUse);
        }

        internal static XmlReader SqlXmlCreateSqlXmlReader(TextReader textReader, bool closeInput, bool async)
        {
            Debug.Assert(closeInput || !async, "Currently we do not have pre-created settings for !closeInput+async");

            XmlReaderSettings settingsToUse = closeInput ?
               (async ? s_defaultXmlReaderSettingsAsyncCloseInput : s_defaultXmlReaderSettingsCloseInput) :
               s_defaultXmlReaderSettings;

            return XmlReader.Create(textReader, settingsToUse);
        }
        #endregion
    }
}
