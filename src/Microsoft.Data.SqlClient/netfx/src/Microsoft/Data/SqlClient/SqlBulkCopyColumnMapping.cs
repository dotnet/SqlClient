//------------------------------------------------------------------------------
// <copyright file="SqlBulkCopyColumnMapping.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">mithomas</owner>
// <owner current="true" primary="false">blained</owner>
//------------------------------------------------------------------------------

// Todo: rename the file
// Caution! ndp\fx\src\data\netmodule\sources needs to follow this change

namespace Microsoft.Data.SqlClient
{
    using Microsoft.Data.Common;

    // -------------------------------------------------------------------------------------------------
    // this class helps allows the user to create association between source- and targetcolumns
    //
    //

    public sealed class SqlBulkCopyColumnMapping {
        internal string         _destinationColumnName;
        internal int            _destinationColumnOrdinal;
        internal string         _sourceColumnName;
        internal int            _sourceColumnOrdinal;

        // devnote: we don't want the user to detect the columnordinal after WriteToServer call.
        // _sourceColumnOrdinal(s) will be copied to _internalSourceColumnOrdinal when WriteToServer executes.
        internal int            _internalDestinationColumnOrdinal;
        internal int            _internalSourceColumnOrdinal;   // -1 indicates an undetermined value
        internal System.Data.SqlClient.SqlBulkCopyColumnMapping _sysBulkCopyColumnMapping;

        internal System.Data.SqlClient.SqlBulkCopyColumnMapping SysBulkCopyColumnMapping
        {
            get
            {
                return _sysBulkCopyColumnMapping;
            }
            set
            {
                _sysBulkCopyColumnMapping = value;
            }
        }

        public SqlBulkCopyColumnMapping(System.Data.SqlClient.SqlBulkCopyColumnMapping sqlBulkCopyColumnMapping)
        {
            SysBulkCopyColumnMapping = sqlBulkCopyColumnMapping;
        }

        public string DestinationColumn {
            get {
                if(SysBulkCopyColumnMapping != null)
                {
                    return SysBulkCopyColumnMapping.DestinationColumn;
                }
                if (_destinationColumnName != null) {
                    return _destinationColumnName;
                }
                return string.Empty;
            }
            set {
                if (SysBulkCopyColumnMapping != null)
                {
                    SysBulkCopyColumnMapping.DestinationColumn = value;
                }
                else
                {
                    _destinationColumnOrdinal = _internalDestinationColumnOrdinal = -1;
                    _destinationColumnName = value;
                }
            }
        }

        public int DestinationOrdinal {
            get
            {
                return SysBulkCopyColumnMapping?.DestinationOrdinal ?? _destinationColumnOrdinal;
            }
            set {
                if (SysBulkCopyColumnMapping != null)
                {
                    SysBulkCopyColumnMapping.DestinationOrdinal = value;
                }
                else
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
        }

        public string SourceColumn {
            get {
                if (SysBulkCopyColumnMapping != null)
                {
                    return SysBulkCopyColumnMapping.SourceColumn;
                }
                if (_sourceColumnName != null) {
                    return _sourceColumnName;
                }
                return string.Empty;
            }
            set
            {
                if (SysBulkCopyColumnMapping != null)
                {
                    SysBulkCopyColumnMapping.SourceColumn = value;
                }
                else
                {
                    _sourceColumnOrdinal = _internalSourceColumnOrdinal = -1;
                    _sourceColumnName = value;
                }
            }
        }

        public int SourceOrdinal {
            get {
                return SysBulkCopyColumnMapping?.SourceOrdinal ?? _sourceColumnOrdinal;
            }
            set {
                if (SysBulkCopyColumnMapping != null)
                {
                    SysBulkCopyColumnMapping.SourceOrdinal = value;
                }
                else
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
        }

        public SqlBulkCopyColumnMapping () {
            _internalSourceColumnOrdinal = -1;
        }

        public SqlBulkCopyColumnMapping (string sourceColumn, string destinationColumn) {
            SourceColumn = sourceColumn;
            DestinationColumn = destinationColumn;
        }

        public SqlBulkCopyColumnMapping (int sourceColumnOrdinal, string destinationColumn) {
            SourceOrdinal = sourceColumnOrdinal;
            DestinationColumn = destinationColumn;
        }

        public SqlBulkCopyColumnMapping (string sourceColumn, int destinationOrdinal) {
            SourceColumn = sourceColumn;
            DestinationOrdinal = destinationOrdinal;
        }

        public SqlBulkCopyColumnMapping (int sourceColumnOrdinal, int destinationOrdinal) {
            SourceOrdinal = sourceColumnOrdinal;
            DestinationOrdinal = destinationOrdinal;
        }
    }
}
