// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    // -------------------------------------------------------------------------------------------------
    // this class helps allows the user to create association between source- and targetcolumns
    //
    //
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/SqlBulkCopyColumnMapping/*'/>
    public sealed class SqlBulkCopyColumnMapping
    {
        internal string _destinationColumnName;
        internal int _destinationColumnOrdinal;
        internal string _sourceColumnName;
        internal int _sourceColumnOrdinal;

        // devnote: we don't want the user to detect the columnordinal after WriteToServer call.
        // _sourceColumnOrdinal(s) will be copied to _internalSourceColumnOrdinal when WriteToServer executes.
        internal int _internalDestinationColumnOrdinal;
        internal int _internalSourceColumnOrdinal;   // -1 indicates an undetermined value

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/DestinationColumn/*'/>
        public string DestinationColumn
        {
            get
            {
                if (_destinationColumnName != null)
                {
                    return _destinationColumnName;
                }
                return string.Empty;
            }
            set
            {
                _destinationColumnOrdinal = _internalDestinationColumnOrdinal = -1;
                _destinationColumnName = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/DestinationOrdinal/*'/>
        public int DestinationOrdinal
        {
            get
            {
                return _destinationColumnOrdinal;
            }
            set
            {
                if (value >= 0)
                {
                    _destinationColumnName = null;
                    _destinationColumnOrdinal = _internalDestinationColumnOrdinal = value;
                }
                else
                {
                    throw ADP.IndexOutOfRange(value);
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/SourceColumn/*'/>
        public string SourceColumn
        {
            get
            {
                if (_sourceColumnName != null)
                {
                    return _sourceColumnName;
                }
                return string.Empty;
            }
            set
            {
                _sourceColumnOrdinal = _internalSourceColumnOrdinal = -1;
                _sourceColumnName = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/SourceOrdinal/*'/>
        public int SourceOrdinal
        {
            get
            {
                return _sourceColumnOrdinal;
            }
            set
            {
                if (value >= 0)
                {
                    _sourceColumnName = null;
                    _sourceColumnOrdinal = _internalSourceColumnOrdinal = value;
                }
                else
                {
                    throw ADP.IndexOutOfRange(value);
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="default"]/*'/>
        public SqlBulkCopyColumnMapping()
        {
            _internalSourceColumnOrdinal = -1;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnStringAnddestinationColumnString"]/*'/>
        public SqlBulkCopyColumnMapping(string sourceColumn, string destinationColumn)
        {
            SourceColumn = sourceColumn;
            DestinationColumn = destinationColumn;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnOrdinalIntegerAnddestinationColumnString"]/*'/>
        public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, string destinationColumn)
        {
            SourceOrdinal = sourceColumnOrdinal;
            DestinationColumn = destinationColumn;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnStringAnddestinationOrdinalInteger"]/*'/>
        public SqlBulkCopyColumnMapping(string sourceColumn, int destinationOrdinal)
        {
            SourceColumn = sourceColumn;
            DestinationOrdinal = destinationOrdinal;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopyColumnMapping.xml' path='docs/members[@name="SqlBulkCopyColumnMapping"]/ctor[@name="sourceColumnOrdinalIntegerAnddestinationOrdinalInteger"]/*'/>
        public SqlBulkCopyColumnMapping(int sourceColumnOrdinal, int destinationOrdinal)
        {
            SourceOrdinal = sourceColumnOrdinal;
            DestinationOrdinal = destinationOrdinal;
        }
    }
}
