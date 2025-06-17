// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Server;


using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient
{
    // SqlServer provider's implementation of ISqlReader.
    //    Supports ISqlReader and ISqlResultSet objects.
    //
    //    User should never be able to create one of these themselves, nor subclass.
    //        This is accomplished by having no public override constructors.
    internal sealed class SqlDataReaderSmi : SqlDataReader
    {
        //
        // IDBRecord properties
        //
        public override int FieldCount
        {
            get
            {
                ThrowIfClosed();
                return InternalFieldCount;
            }
        }

        public override int VisibleFieldCount
        {
            get
            {
                ThrowIfClosed();

                if (FNotInResults())
                {
                    return 0;
                }

                return _visibleColumnCount;
            }
        }

        //
        // IDBRecord Metadata Methods
        //
        public override string GetName(int ordinal)
        {
            EnsureCanGetMetaData();
            return _currentMetaData[ordinal].Name;
        }

        public override string GetDataTypeName(int ordinal)
        {
            EnsureCanGetMetaData();
            SmiExtendedMetaData md = _currentMetaData[ordinal];
            if (SqlDbType.Udt == md.SqlDbType)
            {
                return md.TypeSpecificNamePart1 + "." + md.TypeSpecificNamePart2 + "." + md.TypeSpecificNamePart3;
            }
            else
            {
                return md.TypeName;
            }
        }

        public override Type GetFieldType(int ordinal)
        {
            EnsureCanGetMetaData();
            if (_currentMetaData[ordinal].SqlDbType == SqlDbType.Udt)
            {
                return _currentMetaData[ordinal].Type;
            }
            else
            {
                return MetaType.GetMetaTypeFromSqlDbType(_currentMetaData[ordinal].SqlDbType, _currentMetaData[ordinal].IsMultiValued).ClassType;
            }
        }

        override public Type GetProviderSpecificFieldType(int ordinal)
        {
            EnsureCanGetMetaData();

            if (SqlDbType.Udt == _currentMetaData[ordinal].SqlDbType)
            {
                return _currentMetaData[ordinal].Type;
            }
            else
            {
                return MetaType.GetMetaTypeFromSqlDbType(_currentMetaData[ordinal].SqlDbType, _currentMetaData[ordinal].IsMultiValued).SqlType;
            }
        }

        public override int Depth
        {
            get
            {
                ThrowIfClosed();
                return 0;
            }
        } // UNDONE: (alazela 10/14/2001) Multi-level reader not impl.

        public override object GetValue(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            SmiQueryMetaData metaData = _currentMetaData[ordinal];
            if (_currentConnection.Is2008OrNewer)
            {
                return ValueUtilsSmi.GetValue200(_readerEventSink, (SmiTypedGetterSetter)_currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
            }
            else
            {
                return ValueUtilsSmi.GetValue(_readerEventSink, _currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
            }
        }

        public override T GetFieldValue<T>(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            SmiQueryMetaData metaData = _currentMetaData[ordinal];

            if (typeof(INullable).IsAssignableFrom(typeof(T)))
            {
                // If its a SQL Type or Nullable UDT
                if (_currentConnection.Is2008OrNewer)
                {
                    return (T)ValueUtilsSmi.GetSqlValue200(_readerEventSink, (SmiTypedGetterSetter)_currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
                }
                else
                {
                    return (T)ValueUtilsSmi.GetSqlValue(_readerEventSink, _currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
                }
            }
            else
            {
                // Otherwise Its a CLR or non-Nullable UDT
                if (_currentConnection.Is2008OrNewer)
                {
                    return (T)ValueUtilsSmi.GetValue200(_readerEventSink, (SmiTypedGetterSetter)_currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
                }
                else
                {
                    return (T)ValueUtilsSmi.GetValue(_readerEventSink, _currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
                }
            }
        }

        public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        {
            // As per Async spec, Context Connections do not support async
            return ADP.CreatedTaskWithException<T>(ADP.ExceptionWithStackTrace(SQL.NotAvailableOnContextConnection()));
        }

        override internal SqlBuffer.StorageType GetVariantInternalStorageType(int ordinal)
        {
            Debug.Assert(_currentColumnValuesV3 != null, "Attempting to get variant internal storage type without calling GetValue first");
            if (IsDBNull(ordinal))
            {
                return SqlBuffer.StorageType.Empty;
            }

            SmiMetaData valueMetaData = _currentColumnValuesV3.GetVariantType(_readerEventSink, ordinal);
            if (valueMetaData == null)
            {
                return SqlBuffer.StorageType.Empty;
            }
            else
            {
                return ValueUtilsSmi.SqlDbTypeToStorageType(valueMetaData.SqlDbType);
            }
        }

        public override int GetValues(object[] values)
        {
            EnsureCanGetCol(0);
            if (values == null)
            {
                throw ADP.ArgumentNull(nameof(values));
            }

            int copyLength = (values.Length < _visibleColumnCount) ? values.Length : _visibleColumnCount;
            for (int i = 0; i < copyLength; i++)
            {
                values[_indexMap[i]] = GetValue(i);
            }
            return copyLength;
        }

        public override int GetOrdinal(string name)
        {
            EnsureCanGetMetaData();
            if (_fieldNameLookup == null)
            {
                _fieldNameLookup = new FieldNameLookup((IDataReader)this, -1); // TODO: Need to support DefaultLCID for name comparisons
            }
            return _fieldNameLookup.GetOrdinal(name); // MDAC 71470
        }

        // Generic array access by column index (accesses column value)
        public override object this[int ordinal] => GetValue(ordinal);

        // Generic array access by column name (accesses column value)
        public override object this[string strName] => GetValue(GetOrdinal(strName));

        //
        // IDataRecord Data Access methods
        //
        public override bool IsDBNull(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.IsDBNull(_readerEventSink, _currentColumnValuesV3, ordinal);
        }

        public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
        {
            // As per Async spec, Context Connections do not support async
            return ADP.CreatedTaskWithException<bool>(ADP.ExceptionWithStackTrace(SQL.NotAvailableOnContextConnection()));
        }

        public override bool GetBoolean(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetBoolean(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override byte GetByte(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetByte(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override long GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetBytes(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], fieldOffset, buffer, bufferOffset, length, true);
        }

        // XmlReader support code calls this method.
        internal override long GetBytesInternal(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetBytesInternal(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], fieldOffset, buffer, bufferOffset, length, false);
        }

        public override char GetChar(int ordinal) => throw ADP.NotSupported();

        public override long GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            EnsureCanGetCol(ordinal);
            SmiExtendedMetaData metaData = _currentMetaData[ordinal];
            if (IsCommandBehavior(CommandBehavior.SequentialAccess))
            {
                if (metaData.SqlDbType == SqlDbType.Xml)
                {
                    return GetStreamingXmlChars(ordinal, fieldOffset, buffer, bufferOffset, length);
                }
            }
            return ValueUtilsSmi.GetChars(_readerEventSink, _currentColumnValuesV3, ordinal, metaData, fieldOffset, buffer, bufferOffset, length);
        }

        public override Guid GetGuid(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetGuid(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override short GetInt16(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetInt16(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override int GetInt32(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetInt32(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override long GetInt64(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetInt64(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override float GetFloat(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSingle(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override double GetDouble(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetDouble(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override string GetString(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetString(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override decimal GetDecimal(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetDecimal(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override DateTime GetDateTime(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetDateTime(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        //
        // IDataReader properties
        //
        // Logically closed test. I.e. is this object closed as far as external access is concerned?
        public override bool IsClosed => IsReallyClosed();

        public override int RecordsAffected => Command.InternalRecordsAffected;

        //
        // IDataReader methods
        //
        internal override void CloseReaderFromConnection()
        {
            // Context Connections do not support async - so there is no threading issues with closing from the connection
            CloseInternal(closeConnection: false);
        }

        public override void Close()
        {
            // Connection should be open at this point, so we can do multiple checks of HasEvents, and we may need to close the connection afterwards
            CloseInternal(closeConnection: IsCommandBehavior(CommandBehavior.CloseConnection));
        }

        private void CloseInternal(bool closeConnection)
        {
            using (TryEventScope.Create("<sc.SqlDataReaderSmi.Close|API> {0}", ObjectID))
            {
                bool processFinallyBlock = true;
                try
                {
                    if (!IsClosed)
                    {
                        _hasRows = false;

                        // Process the remaining events. This makes sure that environment changes are applied and any errors are picked up.
                        while (_eventStream.HasEvents)
                        {
                            _eventStream.ProcessEvent(_readerEventSink);
                            _readerEventSink.ProcessMessagesAndThrow(true);
                        }

                        // Close the request executor
                        _requestExecutor.Close(_readerEventSink);
                        _readerEventSink.ProcessMessagesAndThrow(true);
                    }
                }
                catch (Exception e)
                {
                    processFinallyBlock = ADP.IsCatchableExceptionType(e);
                    throw;
                }
                finally
                {
                    if (processFinallyBlock)
                    {
                        _isOpen = false;

                        if ((closeConnection) && (Connection != null))
                        {
                            Connection.Close();
                        }
                    }
                }
            }
        }

        // Move to the next resultset
        public override unsafe bool NextResult()
        {
            ThrowIfClosed();

            bool hasAnotherResult = InternalNextResult(false);

            return hasAnotherResult;
        }

        public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        {
            // Async not supported on Context Connections
            return ADP.CreatedTaskWithException<bool>(ADP.ExceptionWithStackTrace(SQL.NotAvailableOnContextConnection()));
        }

        internal unsafe bool InternalNextResult(bool ignoreNonFatalMessages)
        {
            long scopeID = SqlClientEventSource.Log.TryAdvancedScopeEnterEvent("<sc.SqlDataReaderSmi.InternalNextResult|ADV> {0}", ObjectID);
            try
            {
                _hasRows = false;

                if (PositionState.AfterResults != _currentPosition)
                {
                    // Consume any remaning rows in the current result. 

                    while (InternalRead(ignoreNonFatalMessages))
                    {
                        // This space intentionally left blank
                    }

                    // reset resultset metadata - it will be created again if there is a pending resultset
                    ResetResultSet();

                    // Process the events until metadata is found or all of the
                    // available events have been consumed. If there is another
                    // result, the metadata for it will be available after the last
                    // read on the prior result.

                    while (_currentMetaData == null && _eventStream.HasEvents)
                    {
                        _eventStream.ProcessEvent(_readerEventSink);
                        _readerEventSink.ProcessMessagesAndThrow(ignoreNonFatalMessages);
                    }
                }

                return PositionState.AfterResults != _currentPosition;
            }
            finally
            {
                SqlClientEventSource.Log.TryAdvanceScopeLeave(scopeID);
            }
        }

        public override bool Read()
        {
            ThrowIfClosed();
            bool hasAnotherRow = InternalRead(false);

            return hasAnotherRow;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            // Async not supported on Context Connections
            return ADP.CreatedTaskWithException<bool>(ADP.ExceptionWithStackTrace(SQL.NotAvailableOnContextConnection()));
        }

        internal unsafe bool InternalRead(bool ignoreNonFatalErrors)
        {
            long scopeID = SqlClientEventSource.Log.TryAdvancedScopeEnterEvent("<sc.SqlDataReaderSmi.InternalRead|ADV> {0}", ObjectID);
            try
            {
                // Don't move unless currently in results.
                if (FInResults())
                {

                    // Set current row to null so we can see if we get a new one
                    _currentColumnValues = null;
                    _currentColumnValuesV3 = null;

                    // Reset blobs
                    if (_currentStream != null)
                    {
                        _currentStream.SetClosed();
                        _currentStream = null;
                    }
                    if (_currentTextReader != null)
                    {
                        _currentTextReader.SetClosed();
                        _currentTextReader = null;
                    }

                    // NOTE: SQLBUDT #386118 -- may indicate that we want to break this loop when we get a MessagePosted callback, but we can't prove that.
                    while (_currentColumnValues == null &&                         // Did we find a row?
                            _currentColumnValuesV3 == null &&                       // Did we find a V3 row?
                            FInResults() &&                         // Was the batch terminated due to a serious error?
                            PositionState.AfterRows != _currentPosition &&              // Have we seen a statement completed event?
                            _eventStream.HasEvents)
                    {              // Have we processed all events?
                        _eventStream.ProcessEvent(_readerEventSink);
                        _readerEventSink.ProcessMessagesAndThrow(ignoreNonFatalErrors);
                    }
                }

                return PositionState.OnRow == _currentPosition;
            }
            finally
            {
                SqlClientEventSource.Log.TryAdvanceScopeLeave(scopeID);
            }
        }

        public override DataTable GetSchemaTable()
        {
            ThrowIfClosed();

            if (_schemaTable == null && FInResults())
            {

                DataTable schemaTable = new DataTable("SchemaTable")
                {
                    Locale = System.Globalization.CultureInfo.InvariantCulture,
                    MinimumCapacity = InternalFieldCount
                };

                DataColumn ColumnName = new DataColumn(SchemaTableColumn.ColumnName, typeof(string));
                DataColumn Ordinal = new DataColumn(SchemaTableColumn.ColumnOrdinal, typeof(int));
                DataColumn Size = new DataColumn(SchemaTableColumn.ColumnSize, typeof(int));
                DataColumn Precision = new DataColumn(SchemaTableColumn.NumericPrecision, typeof(short));
                DataColumn Scale = new DataColumn(SchemaTableColumn.NumericScale, typeof(short));

                DataColumn DataType = new DataColumn(SchemaTableColumn.DataType, typeof(Type));
                DataColumn ProviderSpecificDataType = new DataColumn(SchemaTableOptionalColumn.ProviderSpecificDataType, typeof(Type));
                DataColumn ProviderType = new DataColumn(SchemaTableColumn.ProviderType, typeof(int));
                DataColumn NonVersionedProviderType = new DataColumn(SchemaTableColumn.NonVersionedProviderType, typeof(int));

                DataColumn IsLong = new DataColumn(SchemaTableColumn.IsLong, typeof(bool));
                DataColumn AllowDBNull = new DataColumn(SchemaTableColumn.AllowDBNull, typeof(bool));
                DataColumn IsReadOnly = new DataColumn(SchemaTableOptionalColumn.IsReadOnly, typeof(bool));
                DataColumn IsRowVersion = new DataColumn(SchemaTableOptionalColumn.IsRowVersion, typeof(bool));

                DataColumn IsUnique = new DataColumn(SchemaTableColumn.IsUnique, typeof(bool));
                DataColumn IsKey = new DataColumn(SchemaTableColumn.IsKey, typeof(bool));
                DataColumn IsAutoIncrement = new DataColumn(SchemaTableOptionalColumn.IsAutoIncrement, typeof(bool));
                DataColumn IsHidden = new DataColumn(SchemaTableOptionalColumn.IsHidden, typeof(bool));

                DataColumn BaseCatalogName = new DataColumn(SchemaTableOptionalColumn.BaseCatalogName, typeof(string));
                DataColumn BaseSchemaName = new DataColumn(SchemaTableColumn.BaseSchemaName, typeof(string));
                DataColumn BaseTableName = new DataColumn(SchemaTableColumn.BaseTableName, typeof(string));
                DataColumn BaseColumnName = new DataColumn(SchemaTableColumn.BaseColumnName, typeof(string));

                // unique to SqlClient
                DataColumn BaseServerName = new DataColumn(SchemaTableOptionalColumn.BaseServerName, typeof(string));
                DataColumn IsAliased = new DataColumn(SchemaTableColumn.IsAliased, typeof(bool));
                DataColumn IsExpression = new DataColumn(SchemaTableColumn.IsExpression, typeof(bool));
                DataColumn IsIdentity = new DataColumn("IsIdentity", typeof(bool));
                // UDT specific. Holds UDT typename ONLY if the type of the column is UDT, otherwise the data type
                DataColumn DataTypeName = new DataColumn("DataTypeName", typeof(string));
                DataColumn UdtAssemblyQualifiedName = new DataColumn("UdtAssemblyQualifiedName", typeof(string));
                // Xml metadata specific
                DataColumn XmlSchemaCollectionDatabase = new DataColumn("XmlSchemaCollectionDatabase", typeof(string));
                DataColumn XmlSchemaCollectionOwningSchema = new DataColumn("XmlSchemaCollectionOwningSchema", typeof(string));
                DataColumn XmlSchemaCollectionName = new DataColumn("XmlSchemaCollectionName", typeof(string));
                // SparseColumnSet
                DataColumn IsColumnSet = new DataColumn("IsColumnSet", typeof(bool));

                Ordinal.DefaultValue = 0;
                IsLong.DefaultValue = false;

                DataColumnCollection columns = schemaTable.Columns;

                // must maintain order for backward compatibility
                columns.Add(ColumnName);
                columns.Add(Ordinal);
                columns.Add(Size);
                columns.Add(Precision);
                columns.Add(Scale);
                columns.Add(IsUnique);
                columns.Add(IsKey);
                columns.Add(BaseServerName);
                columns.Add(BaseCatalogName);
                columns.Add(BaseColumnName);
                columns.Add(BaseSchemaName);
                columns.Add(BaseTableName);
                columns.Add(DataType);
                columns.Add(AllowDBNull);
                columns.Add(ProviderType);
                columns.Add(IsAliased);
                columns.Add(IsExpression);
                columns.Add(IsIdentity);
                columns.Add(IsAutoIncrement);
                columns.Add(IsRowVersion);
                columns.Add(IsHidden);
                columns.Add(IsLong);
                columns.Add(IsReadOnly);
                columns.Add(ProviderSpecificDataType);
                columns.Add(DataTypeName);
                columns.Add(XmlSchemaCollectionDatabase);
                columns.Add(XmlSchemaCollectionOwningSchema);
                columns.Add(XmlSchemaCollectionName);
                columns.Add(UdtAssemblyQualifiedName);
                columns.Add(NonVersionedProviderType);
                columns.Add(IsColumnSet);

                for (int i = 0; i < InternalFieldCount; i++)
                {
                    SmiQueryMetaData colMetaData = _currentMetaData[i];

                    long maxLength = colMetaData.MaxLength;

                    MetaType metaType = MetaType.GetMetaTypeFromSqlDbType(colMetaData.SqlDbType, colMetaData.IsMultiValued);
                    if (SmiMetaData.UnlimitedMaxLengthIndicator == maxLength)
                    {
                        metaType = MetaType.GetMaxMetaTypeFromMetaType(metaType);
                        maxLength = (metaType.IsSizeInCharacters && !metaType.IsPlp) ? (0x7fffffff / 2) : 0x7fffffff;
                    }

                    DataRow schemaRow = schemaTable.NewRow();

                    // NOTE: there is an impedence mismatch here - the server always 
                    // treats numeric data as variable length and sends a maxLength
                    // based upon the precision, whereas TDS always sends 17 for 
                    // the max length; rather than push this logic into the server,
                    // I've elected to make a fixup here instead.
                    if (SqlDbType.Decimal == colMetaData.SqlDbType)
                    {
                        // TODO: Consider moving this into SmiMetaData itself...
                        maxLength = TdsEnums.MAX_NUMERIC_LEN;   // SQLBUDT 339686
                    }
                    else if (SqlDbType.Variant == colMetaData.SqlDbType)
                    {
                        // TODO: Consider moving this into SmiMetaData itself...
                        maxLength = 8009;   // SQLBUDT 340726
                    }

                    schemaRow[ColumnName] = colMetaData.Name;
                    schemaRow[Ordinal] = i;
                    schemaRow[Size] = maxLength;

                    schemaRow[ProviderType] = (int)colMetaData.SqlDbType; // SqlDbType
                    schemaRow[NonVersionedProviderType] = (int)colMetaData.SqlDbType; // SqlDbType

                    if (colMetaData.SqlDbType != SqlDbType.Udt)
                    {
                        schemaRow[DataType] = metaType.ClassType; // com+ type
                        schemaRow[ProviderSpecificDataType] = metaType.SqlType;
                    }
                    else
                    {
                        schemaRow[UdtAssemblyQualifiedName] = colMetaData.Type.AssemblyQualifiedName;
                        schemaRow[DataType] = colMetaData.Type;
                        schemaRow[ProviderSpecificDataType] = colMetaData.Type;
                    }

                    // NOTE: there is also an impedence mismatch here - the server 
                    // has different ideas about what the precision value should be
                    // than does the client bits.  I tried fixing up the default
                    // meta data values in SmiMetaData, however, it caused the 
                    // server suites to fall over dead.  Rather than attempt to 
                    // bake it into the server, I'm fixing it up in the client.
                    byte precision;  // default for everything, except certain numeric types.

                    // TODO: Consider moving this into SmiMetaData itself...
                    switch (colMetaData.SqlDbType)
                    {
                        case SqlDbType.BigInt:
                        case SqlDbType.DateTime:
                        case SqlDbType.Decimal:
                        case SqlDbType.Int:
                        case SqlDbType.Money:
                        case SqlDbType.SmallDateTime:
                        case SqlDbType.SmallInt:
                        case SqlDbType.SmallMoney:
                        case SqlDbType.TinyInt:
                            precision = colMetaData.Precision;
                            break;
                        case SqlDbType.Float:
                            precision = 15;
                            break;
                        case SqlDbType.Real:
                            precision = 7;
                            break;
                        default:
                            precision = 0xff;   // everything else is unknown;
                            break;
                    }

                    schemaRow[Precision] = precision;

                    // TODO: Consider moving this to a utitlity class if we end up with a bunch more of this stuff...
                    if (SqlDbType.Decimal == colMetaData.SqlDbType ||
                        SqlDbType.Time == colMetaData.SqlDbType ||
                        SqlDbType.DateTime2 == colMetaData.SqlDbType ||
                        SqlDbType.DateTimeOffset == colMetaData.SqlDbType)
                    {
                        schemaRow[Scale] = colMetaData.Scale;
                    }
                    else
                    {
                        schemaRow[Scale] = MetaType.GetMetaTypeFromSqlDbType(colMetaData.SqlDbType, colMetaData.IsMultiValued).Scale;
                    }

                    schemaRow[AllowDBNull] = colMetaData.AllowsDBNull;
                    if (!(colMetaData.IsAliased.IsNull))
                    {
                        schemaRow[IsAliased] = colMetaData.IsAliased.Value;
                    }

                    if (!(colMetaData.IsKey.IsNull))
                    {
                        schemaRow[IsKey] = colMetaData.IsKey.Value;
                    }

                    if (!(colMetaData.IsHidden.IsNull))
                    {
                        schemaRow[IsHidden] = colMetaData.IsHidden.Value;
                    }

                    if (!(colMetaData.IsExpression.IsNull))
                    {
                        schemaRow[IsExpression] = colMetaData.IsExpression.Value;
                    }

                    schemaRow[IsReadOnly] = colMetaData.IsReadOnly;
                    schemaRow[IsIdentity] = colMetaData.IsIdentity;
                    schemaRow[IsColumnSet] = colMetaData.IsColumnSet;
                    schemaRow[IsAutoIncrement] = colMetaData.IsIdentity;
                    schemaRow[IsLong] = metaType.IsLong;

                    // mark unique for timestamp columns
                    if (SqlDbType.Timestamp == colMetaData.SqlDbType)
                    {
                        schemaRow[IsUnique] = true;
                        schemaRow[IsRowVersion] = true;
                    }
                    else
                    {
                        schemaRow[IsUnique] = false;
                        schemaRow[IsRowVersion] = false;
                    }

                    if (!string.IsNullOrEmpty(colMetaData.ColumnName))
                    {
                        schemaRow[BaseColumnName] = colMetaData.ColumnName;
                    }
                    else if (!string.IsNullOrEmpty(colMetaData.Name))
                    {
                        // Use projection name if base column name is not present
                        schemaRow[BaseColumnName] = colMetaData.Name;
                    }

                    if (!string.IsNullOrEmpty(colMetaData.TableName))
                    {
                        schemaRow[BaseTableName] = colMetaData.TableName;
                    }

                    if (!string.IsNullOrEmpty(colMetaData.SchemaName))
                    {
                        schemaRow[BaseSchemaName] = colMetaData.SchemaName;
                    }

                    if (!string.IsNullOrEmpty(colMetaData.CatalogName))
                    {
                        schemaRow[BaseCatalogName] = colMetaData.CatalogName;
                    }

                    if (!string.IsNullOrEmpty(colMetaData.ServerName))
                    {
                        schemaRow[BaseServerName] = colMetaData.ServerName;
                    }

                    if (SqlDbType.Udt == colMetaData.SqlDbType)
                    {
                        schemaRow[DataTypeName] = colMetaData.TypeSpecificNamePart1 + "." + colMetaData.TypeSpecificNamePart2 + "." + colMetaData.TypeSpecificNamePart3;
                    }
                    else
                    {
                        schemaRow[DataTypeName] = metaType.TypeName;
                    }

                    // Add Xml metadata
                    if (SqlDbType.Xml == colMetaData.SqlDbType)
                    {
                        schemaRow[XmlSchemaCollectionDatabase] = colMetaData.TypeSpecificNamePart1;
                        schemaRow[XmlSchemaCollectionOwningSchema] = colMetaData.TypeSpecificNamePart2;
                        schemaRow[XmlSchemaCollectionName] = colMetaData.TypeSpecificNamePart3;
                    }

                    schemaTable.Rows.Add(schemaRow);
                    schemaRow.AcceptChanges();
                }

                // mark all columns as readonly
                foreach (DataColumn column in columns)
                {
                    column.ReadOnly = true; // MDAC 70943
                }

                _schemaTable = schemaTable;
            }

            return _schemaTable;
        }

        //
        //    ISqlRecord methods
        //
        public override SqlBinary GetSqlBinary(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlBinary(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlBoolean GetSqlBoolean(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlBoolean(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlByte GetSqlByte(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlByte(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlInt16 GetSqlInt16(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlInt16(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlInt32 GetSqlInt32(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlInt32(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlInt64 GetSqlInt64(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlInt64(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlSingle GetSqlSingle(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlSingle(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlDouble GetSqlDouble(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlDouble(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlMoney GetSqlMoney(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlMoney(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlDateTime GetSqlDateTime(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlDateTime(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }


        public override SqlDecimal GetSqlDecimal(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlDecimal(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlString GetSqlString(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlString(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlGuid GetSqlGuid(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlGuid(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal]);
        }

        public override SqlChars GetSqlChars(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlChars(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], _currentConnection.InternalContext);
        }

        public override SqlBytes GetSqlBytes(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlBytes(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], _currentConnection.InternalContext);
        }

        public override SqlXml GetSqlXml(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetSqlXml(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], _currentConnection.InternalContext);
        }

        public override TimeSpan GetTimeSpan(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetTimeSpan(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], _currentConnection.Is2008OrNewer);
        }

        public override DateTimeOffset GetDateTimeOffset(int ordinal)
        {
            EnsureCanGetCol(ordinal);
            return ValueUtilsSmi.GetDateTimeOffset(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], _currentConnection.Is2008OrNewer);
        }

        public override object GetSqlValue(int ordinal)
        {
            EnsureCanGetCol(ordinal);

            SmiMetaData metaData = _currentMetaData[ordinal];
            if (_currentConnection.Is2008OrNewer)
            {
                return ValueUtilsSmi.GetSqlValue200(_readerEventSink, (SmiTypedGetterSetter)_currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
            }
            return ValueUtilsSmi.GetSqlValue(_readerEventSink, _currentColumnValuesV3, ordinal, metaData, _currentConnection.InternalContext);
            ;
        }

        public override int GetSqlValues(object[] values)
        {
            EnsureCanGetCol(0);

            if (values == null)
            {
                throw ADP.ArgumentNull(nameof(values));
            }

            int copyLength = (values.Length < _visibleColumnCount) ? values.Length : _visibleColumnCount;
            for (int i = 0; i < copyLength; i++)
            {
                values[_indexMap[i]] = GetSqlValue(i);
            }

            return copyLength;
        }

        //
        //    ISqlReader methods/properties
        //
        public override bool HasRows
        {
            get { return _hasRows; }
        }

        //
        //    SqlDataReader method/properties
        //
        public override Stream GetStream(int ordinal)
        {
            EnsureCanGetCol(ordinal);

            SmiQueryMetaData metaData = _currentMetaData[ordinal];

            // For non-null, non-variant types with sequential access, we support proper streaming
            if ((metaData.SqlDbType != SqlDbType.Variant) && (IsCommandBehavior(CommandBehavior.SequentialAccess)) && (!ValueUtilsSmi.IsDBNull(_readerEventSink, _currentColumnValuesV3, ordinal)))
            {
                if (HasActiveStreamOrTextReaderOnColumn(ordinal))
                {
                    throw ADP.NonSequentialColumnAccess(ordinal, ordinal + 1);
                }
                _currentStream = ValueUtilsSmi.GetSequentialStream(_readerEventSink, _currentColumnValuesV3, ordinal, metaData);
                return _currentStream;
            }
            else
            {
                return ValueUtilsSmi.GetStream(_readerEventSink, _currentColumnValuesV3, ordinal, metaData);
            }
        }

        public override TextReader GetTextReader(int ordinal)
        {
            EnsureCanGetCol(ordinal);

            SmiQueryMetaData metaData = _currentMetaData[ordinal];

            // For non-variant types with sequential access, we support proper streaming
            if ((metaData.SqlDbType != SqlDbType.Variant) && (IsCommandBehavior(CommandBehavior.SequentialAccess)) && (!ValueUtilsSmi.IsDBNull(_readerEventSink, _currentColumnValuesV3, ordinal)))
            {
                if (HasActiveStreamOrTextReaderOnColumn(ordinal))
                {
                    throw ADP.NonSequentialColumnAccess(ordinal, ordinal + 1);
                }
                _currentTextReader = ValueUtilsSmi.GetSequentialTextReader(_readerEventSink, _currentColumnValuesV3, ordinal, metaData);
                return _currentTextReader;
            }
            else
            {
                return ValueUtilsSmi.GetTextReader(_readerEventSink, _currentColumnValuesV3, ordinal, metaData);
            }
        }

        public override XmlReader GetXmlReader(int ordinal)
        {
            // NOTE: sql_variant can not contain a XML data type: http://msdn.microsoft.com/en-us/library/ms173829.aspx

            EnsureCanGetCol(ordinal);
            if (_currentMetaData[ordinal].SqlDbType != SqlDbType.Xml)
            {
                throw ADP.InvalidCast();
            }

            Stream stream;
            if ((IsCommandBehavior(CommandBehavior.SequentialAccess)) && (!ValueUtilsSmi.IsDBNull(_readerEventSink, _currentColumnValuesV3, ordinal)))
            {
                if (HasActiveStreamOrTextReaderOnColumn(ordinal))
                {
                    throw ADP.NonSequentialColumnAccess(ordinal, ordinal + 1);
                }
                // Need to bypass the type check since streams are not usually allowed on XML types
                _currentStream = ValueUtilsSmi.GetSequentialStream(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], bypassTypeCheck: true);
                stream = _currentStream;
            }
            else
            {
                stream = ValueUtilsSmi.GetStream(_readerEventSink, _currentColumnValuesV3, ordinal, _currentMetaData[ordinal], bypassTypeCheck: true);
            }

            return SqlTypeWorkarounds.SqlXmlCreateSqlXmlReader(stream, closeInput: false, async: false);
        }

        //
        //    Internal reader state
        //

        // Logical state of reader/resultset as viewed by the client
        //    Does not necessarily match up with server state.
        internal enum PositionState
        {
            BeforeResults,            // Before all resultset in request
            BeforeRows,                // Before all rows in current resultset
            OnRow,                    // On a valid row in the current resultset
            AfterRows,                // After all rows in current resultset
            AfterResults            // After all resultsets in request
        }

        private PositionState _currentPosition;        // Where is the reader relative to incoming results?
        private bool _isOpen;                    // Is the reader open?
        private SmiQueryMetaData[] _currentMetaData;           // Metadata for current resultset
        private int[] _indexMap;                  // map of indices for visible column
        private int _visibleColumnCount;        // number of visible columns
        private DataTable _schemaTable;               // Cache of user-visible extended metadata while in results.
        private ITypedGetters _currentColumnValues;       // Unmanaged-managed data marshalers/cache
        private ITypedGettersV3 _currentColumnValuesV3;     // Unmanaged-managed data marshalers/cache for SMI V3
        private bool _hasRows;                   // Are there any rows in the current resultset?  Must be able to say before moving to first row.
        private SmiEventStream _eventStream;               // The event buffer that receives the events from the execution engine.
        private SmiRequestExecutor _requestExecutor;           // The used to request actions from the execution engine.
        private SqlInternalConnectionSmi _currentConnection;
        private ReaderEventSink _readerEventSink;           // The event sink that will process events from the event buffer.
        private FieldNameLookup _fieldNameLookup;           // cached lookup object to improve access time based on field name
        private SqlSequentialStreamSmi _currentStream;             // The stream on the current column (if any)
        private SqlSequentialTextReaderSmi _currentTextReader;         // The text reader on the current column (if any)

        // @TODO: No longer used -- delete!
        //  Assumes that if there were any results, the first chunk of them are in the data stream
        //      (up to the first actual row or the end of the resultsets).
        unsafe internal SqlDataReaderSmi(
                SmiEventStream eventStream,        // the event stream that receives the events from the execution engine
                SqlCommand parent,             // command that owns reader
                CommandBehavior behavior,           // behavior specified for this execution
                SqlInternalConnectionSmi connection,         // connection that owns everybody
                SmiEventSink parentSink,         // Event sink of parent command
                SmiRequestExecutor requestExecutor
            ) : base(parent, behavior)
        {  // UNDONE: handle other command behaviors
            _eventStream = eventStream;
            _currentConnection = connection;
            _readerEventSink = new ReaderEventSink(this, parentSink);
            _currentPosition = PositionState.BeforeResults;
            _isOpen = true;
            _indexMap = null;
            _visibleColumnCount = 0;
            _currentStream = null;
            _currentTextReader = null;
            _requestExecutor = requestExecutor;
        }

        internal override SmiExtendedMetaData[] GetInternalSmiMetaData()
        {
            if (_currentMetaData == null || _visibleColumnCount == InternalFieldCount)
            {
                return _currentMetaData;
            }
            else
            {
#if DEBUG
                // DEVNOTE: Interpretation of returned array currently depends on hidden columns
                //  always appearing at the end, since there currently is no access to the index map
                //  outside of this class.  In Debug code, we check this assumption.
                bool sawHiddenColumn = false;
#endif
                SmiExtendedMetaData[] visibleMetaData = new SmiExtendedMetaData[_visibleColumnCount];
                for (int i = 0; i < _visibleColumnCount; i++)
                {
#if DEBUG
                    if (_currentMetaData[_indexMap[i]].IsHidden.IsTrue)
                    {
                        sawHiddenColumn = true;
                    }
                    else
                    {
                        Debug.Assert(!sawHiddenColumn);
                    }
#endif
                    visibleMetaData[i] = _currentMetaData[_indexMap[i]];
                }

                return visibleMetaData;
            }
        }

        internal override int GetLocaleId(int ordinal)
        {
            EnsureCanGetMetaData();
            return (int)_currentMetaData[ordinal].LocaleId;
        }
        private int InternalFieldCount
        {
            get
            {
                if (FNotInResults())
                {
                    return 0;
                }
                else
                {
                    return _currentMetaData.Length;
                }
            }
        }

        // Have we cleaned up internal resources?
        private bool IsReallyClosed() => !_isOpen;

        // Central checkpoint for closed recordset.
        //    Any code that requires an open recordset should call this method first!
        //    Especially any code that accesses unmanaged memory structures whose lifetime
        //      matches the lifetime of the unmanaged recordset.
        internal void ThrowIfClosed([CallerMemberName] string operationName = null)
        {
            if (IsClosed)
            {
                throw ADP.DataReaderClosed(operationName);
            }
        }

        // Central checkpoint to ensure the requested column can be accessed.
        //    Calling this function serves to notify that it has been accessed by the user.
        private void EnsureCanGetCol(int ordinal, [CallerMemberName] string operationName = null)
        {
            EnsureOnRow(operationName);
        }

        internal void EnsureOnRow(string operationName)
        {
            ThrowIfClosed(operationName);
            if (_currentPosition != PositionState.OnRow)
            {
                throw SQL.InvalidRead();
            }
        }

        internal void EnsureCanGetMetaData([CallerMemberName] string operationName = null)
        {
            ThrowIfClosed(operationName);
            if (FNotInResults())
            {
                throw SQL.InvalidRead(); // UNDONE: Shouldn't this be a bit more descriptive?
            }
        }

        private bool FInResults()
        {
            return !FNotInResults();
        }

        private bool FNotInResults()
        {
            return (PositionState.AfterResults == _currentPosition || PositionState.BeforeResults == _currentPosition);
        }

        private void MetaDataAvailable(SmiQueryMetaData[] md, bool nextEventIsRow)
        {
            Debug.Assert(_currentPosition != PositionState.AfterResults);

            _currentMetaData = md;
            _hasRows = nextEventIsRow;
            _fieldNameLookup = null;
            _schemaTable = null; // will be rebuilt based on new metadata
            _currentPosition = PositionState.BeforeRows;

            // calculate visible column indices
            _indexMap = new int[_currentMetaData.Length];
            int i;
            int visibleCount = 0;
            for (i = 0; i < _currentMetaData.Length; i++)
            {
                if (!_currentMetaData[i].IsHidden.IsTrue)
                {
                    _indexMap[visibleCount] = i;
                    visibleCount++;
                }
            }
            _visibleColumnCount = visibleCount;
        }

        private bool HasActiveStreamOrTextReaderOnColumn(int columnIndex)
        {
            bool active = false;

            active |= (_currentStream != null) && (_currentStream.ColumnIndex == columnIndex);
            active |= (_currentTextReader != null) && (_currentTextReader.ColumnIndex == columnIndex);

            return active;
        }

        // Obsolete V2- method
        private void RowAvailable(ITypedGetters row)
        {
            Debug.Assert(_currentPosition != PositionState.AfterResults);

            _currentColumnValues = row;
            _currentPosition = PositionState.OnRow;
        }

        private void RowAvailable(ITypedGettersV3 row)
        {
            Debug.Assert(_currentPosition != PositionState.AfterResults);

            _currentColumnValuesV3 = row;
            _currentPosition = PositionState.OnRow;
        }

        private void StatementCompleted()
        {
            Debug.Assert(_currentPosition != PositionState.AfterResults);

            _currentPosition = PositionState.AfterRows;
        }

        private void ResetResultSet()
        {
            _currentMetaData = null;
            _visibleColumnCount = 0;
            _schemaTable = null;
        }

        private void BatchCompleted()
        {
            Debug.Assert(_currentPosition != PositionState.AfterResults);

            ResetResultSet();

            _currentPosition = PositionState.AfterResults;
            _eventStream.Close(_readerEventSink);
        }

        // An implementation of the IEventSink interface that either performs
        // the required enviornment changes or forwards the events on to the
        // corresponding reader instance. Having the event sink be a separate
        // class keeps the IEventSink methods out of SqlDataReader's inteface.

        private sealed class ReaderEventSink : SmiEventSink_Default
        {
            private readonly SqlDataReaderSmi _reader;

            internal ReaderEventSink(SqlDataReaderSmi reader, SmiEventSink parent)
                : base(parent)
            {
                _reader = reader;
            }

            internal override void MetaDataAvailable(SmiQueryMetaData[] md, bool nextEventIsRow)
            {
                var mdLength = (md != null) ? md.Length : -1;
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlDataReaderSmi.ReaderEventSink.MetaDataAvailable|ADV> {0}, md.Length={1} nextEventIsRow={2}.", _reader.ObjectID, mdLength, nextEventIsRow);

                if (SqlClientEventSource.Log.IsAdvancedTraceOn())
                {
                    if (md != null)
                    {
                        for (int i = 0; i < md.Length; i++)
                        {
                            SqlClientEventSource.Log.TraceEvent("<sc.SqlDataReaderSmi.ReaderEventSink.MetaDataAvailable|ADV> {0}, metaData[{1}] is {2}{3}", _reader.ObjectID, i, md[i].GetType(), md[i].TraceString());
                        }
                    }
                }
                _reader.MetaDataAvailable(md, nextEventIsRow);
            }

            // Obsolete V2- method
            internal override void RowAvailable(ITypedGetters row)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlDataReaderSmi.ReaderEventSink.RowAvailable|ADV> {0} (v2).", _reader.ObjectID);
                _reader.RowAvailable(row);
            }

            internal override void RowAvailable(ITypedGettersV3 row)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlDataReaderSmi.ReaderEventSink.RowAvailable|ADV> {0} (ITypedGettersV3).", _reader.ObjectID);
                _reader.RowAvailable(row);
            }

            internal override void RowAvailable(SmiTypedGetterSetter rowData)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlDataReaderSmi.ReaderEventSink.RowAvailable|ADV> {0} (SmiTypedGetterSetter).", _reader.ObjectID);
                _reader.RowAvailable(rowData);
            }

            internal override void StatementCompleted(int recordsAffected)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlDataReaderSmi.ReaderEventSink.StatementCompleted|ADV> {0} recordsAffected= {1}.", _reader.ObjectID, recordsAffected);

                // devnote: relies on SmiEventSink_Default to pass event to parent
                // Both command and reader care about StatementCompleted, but for different reasons.
                base.StatementCompleted(recordsAffected);
                _reader.StatementCompleted();
            }

            internal override void BatchCompleted()
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlDataReaderSmi.ReaderEventSink.BatchCompleted|ADV> {0}.", _reader.ObjectID);

                // devnote: relies on SmiEventSink_Default to pass event to parent
                //  parent's callback *MUST* come before reader's BatchCompleted, since
                //  reader will close the event stream during this call, and parent wants
                //  to extract parameter values before that happens.
                base.BatchCompleted();
                _reader.BatchCompleted();
            }
        }

    }
}

