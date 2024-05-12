using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using simplesqlclient;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Microsoft.Data.SqlClient.SqlClientX
{
    /// <summary>
    /// Data Reader X 
    /// </summary>
    public class SqlDataReaderX : DbDataReader, IDataReader, IDbColumnSchemaGenerator
    {
        private class ReaderState
        {
            internal bool _RowDataIsReady = false;
        }

        private ReaderState _readerState = new ReaderState();
        private SqlCommandX _sqlCommandX;
        private _SqlMetaDataSet _metadata;
        private SqlBuffer[] _sqlBuffers;
        private SqlPhysicalConnection _PhysicalConnection;

        internal SqlDataReaderX(SqlCommandX sqlCommandX) : this((sqlCommandX.Connection as SqlConnectionX)?.PhysicalConnection)
            => _sqlCommandX = sqlCommandX;

        internal SqlDataReaderX(SqlPhysicalConnection physicalConnection)
            => _PhysicalConnection = physicalConnection;

        /// <summary>
        /// Indexer
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override object this[int ordinal] => throw new NotImplementedException();

        /// <summary>
        /// Indexer by column name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override object this[string name] => throw new NotImplementedException();

        /// <summary>
        /// Get the depth
        /// </summary>
        public override int Depth => throw new NotImplementedException();

        /// <summary>
        /// The field count.
        /// </summary>
        public override int FieldCount => _metadata?.Length ?? 0;

        /// <summary>
        /// Are there more rows?
        /// </summary>
        public override bool HasRows => throw new NotImplementedException();

        /// <summary>
        /// If the reader is closed ? 
        /// </summary>
        public override bool IsClosed => throw new NotImplementedException();

        /// <summary>
        /// Number of records affected.
        /// </summary>
        public override int RecordsAffected => throw new NotImplementedException();

        /// <summary>
        /// Get the value of the column as boolean.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override bool GetBoolean(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value of the column as byte.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override byte GetByte(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value of column as byte[].
        /// </summary>
        /// <param name="ordinal"></param>
        /// <param name="dataOffset"></param>
        /// <param name="buffer"></param>
        /// <param name="bufferOffset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value of col as character.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override char GetChar(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the chars for the reader.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <param name="dataOffset"></param>
        /// <param name="buffer"></param>
        /// <param name="bufferOffset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the DB column schema.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ReadOnlyCollection<DbColumn> GetColumnSchema()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the name of the data type.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override string GetDataTypeName(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value of the column as DateTime.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override DateTime GetDateTime(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value of the column as decimal.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override decimal GetDecimal(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value as a double.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override double GetDouble(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the enumerator for this reader.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override IEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the field type.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public override Type GetFieldType(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the float value of the column.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override float GetFloat(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the Guid .
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override Guid GetGuid(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value as int16.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override short GetInt16(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value as int 32
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override int GetInt32(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value as int 64
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override long GetInt64(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Get the name of the column.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override string GetName(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the ordinal of a column by name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override int GetOrdinal(string name)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the col value as string.
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override string GetString(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the value as object. 
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override object GetValue(int ordinal)
        {
            var columns = _metadata;
            if (!_readerState._RowDataIsReady)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    //SqlBuffer data = new SqlBuffer();
                    _SqlMetaData column = columns[i];

                    Tuple<bool, ulong> tuple = _PhysicalConnection.ProcessColumnHeaderAsync(column,
                        i,
                        isAsync: false,
                        ct: CancellationToken.None).AsTask().GetAwaiter().GetResult();
                    bool isNull = tuple.Item1;
                    ulong length = tuple.Item2;
                    if (tuple.Item1)
                    {
                        throw new NotImplementedException("Null values are not implemented");
                    }
                    else
                    {
                        _PhysicalConnection.ReadSqlValueAsync(_sqlBuffers[i],
                            column,
                            column.metaType.IsPlp ? (Int32.MaxValue) : (int)length,
                            simplesqlclient.SqlCommandColumnEncryptionSetting.Disabled /*Column Encryption Disabled for Bulk Copy*/,
                            column.column,
                            isAsync: false,
                            ct: CancellationToken.None).AsTask().GetAwaiter().GetResult();
                    }
                    //data.Clear();
                }
                _readerState._RowDataIsReady = true;
            }
            return _sqlBuffers[ordinal].Value; //
        }

        /// <summary>
        /// Get an array of all the values of all the cols in the row.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Is this a DBNull ? 
        /// </summary>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override bool IsDBNull(int ordinal)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Go to the next result and return true if we could do this.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override bool NextResult()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read the next row.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override bool Read()
        {
            byte nextUnreadToken = _PhysicalConnection.PeekToken(false, CancellationToken.None).GetAwaiter().GetResult();
            CleanDataBuffer();
            while (nextUnreadToken == TdsTokens.SQLROW
                || nextUnreadToken == TdsTokens.SQLNBCROW)
            {
                _PhysicalConnection.AdvancePastRowAsync(false, CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
                _readerState._RowDataIsReady = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            byte nextUnreadToken = await _PhysicalConnection.PeekToken(true, CancellationToken.None).ConfigureAwait(false);
            CleanDataBuffer();
            while (nextUnreadToken == TdsTokens.SQLROW
                || nextUnreadToken == TdsTokens.SQLNBCROW)
            {
                await _PhysicalConnection.AdvancePastRowAsync(true, CancellationToken.None).ConfigureAwait(false);
                _readerState._RowDataIsReady = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ordinal"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        override async public Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        {
            var columns = _metadata;
            if (!_readerState._RowDataIsReady)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    //SqlBuffer data = new SqlBuffer();
                    _SqlMetaData column = columns[i];

                    Tuple<bool, ulong> tuple = await _PhysicalConnection.ProcessColumnHeaderAsync(column,
                        i,
                        isAsync: true,
                        ct: cancellationToken).ConfigureAwait(false);
                    bool isNull = tuple.Item1;
                    ulong length = tuple.Item2;
                    if (tuple.Item1)
                    {
                        throw new NotImplementedException("Null values are not implemented");
                    }
                    else
                    {
                        await _PhysicalConnection.ReadSqlValueAsync(_sqlBuffers[i],
                            column,
                            column.metaType.IsPlp ? (Int32.MaxValue) : (int)length,
                            simplesqlclient.SqlCommandColumnEncryptionSetting.Disabled /*Column Encryption Disabled for Bulk Copy*/,
                            column.column,
                            isAsync: true,
                            ct: cancellationToken).ConfigureAwait(false);
                    }
                    //data.Clear();
                }
                _readerState._RowDataIsReady = true;
            }
            //Type dataType = _sqlBuffers[ordinal].GetTypeFromStorageType(false);
            var value = _sqlBuffers[ordinal].Value;
            return (T)value;
            //return (T)GetValueFromSqlBufferInternal(data, metaData);
            //return _sqlBuffers[ordinal].Value; //
        }

        /// <summary>
        /// Clears up the buffers.
        /// </summary>

        private void CleanDataBuffer()
        {
            SqlBuffer.Clear(_sqlBuffers);
        }

        internal void SetMetadata(_SqlMetaDataSet mdSet, bool hasMoreInformation)
        {
            _metadata = mdSet;

            if(_metadata != null)
            {
                _sqlBuffers = SqlBuffer.CreateBufferArray(_metadata.Length);
            }
            if (hasMoreInformation && _metadata != null)
            {
                throw new NotImplementedException("More information is not implemented.");
            }
        }
    }
}
