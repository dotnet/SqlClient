using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient.SqlClientX.DevAPIs
{
    /// <summary>
    /// Data Reader X 
    /// </summary>
    public class SqlDataReaderX : DbDataReader, IDataReader, IDbColumnSchemaGenerator
    {
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
        public override int FieldCount => throw new NotImplementedException();

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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
