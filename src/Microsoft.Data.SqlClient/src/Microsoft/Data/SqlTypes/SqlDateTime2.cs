using System;
using System.Data.SqlTypes;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/SqlDateTime2/*' />
    [XmlSchemaProvider("GetXsdType")]
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SqlDateTime2 : INullable, IComparable<SqlDateTime2>, IEquatable<SqlDateTime2>, IComparable, IXmlSerializable
    {
        private const long _minTicks = 0; // DateTime.MinValue.Ticks
        private const long _maxTicks = 3155378975999999999; // DateTime.MaxValue.Ticks

        private bool _notNull;    // false if null - has to be _notNull and not _isNull - otherwise default(SqlDateTime2) will return a SqlDateTime2 with _isNull=false and that would make XmlSerialization fail
        private long _ticks; // Ticks representation similar to DateTime.Tics
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/ctor1/*' />
        private SqlDateTime2(bool isNull)
        {
            _notNull = !isNull;
            _ticks = 0;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/ctor2/*' />
        public SqlDateTime2(long ticks)
        {
            if (ticks < _minTicks || ticks > _maxTicks)
            {
                throw ADP.ArgumentOutOfRange(nameof(ticks));
            }
            _notNull = true;
            _ticks = ticks;
        }

        /// <inheritdoc />
        public bool IsNull => !_notNull;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/Value/*' />
        public DateTime Value => _notNull ? new DateTime(_ticks) : throw new SqlNullValueException();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/Null/*' />
        public static readonly SqlDateTime2 Null = new SqlDateTime2(true);

        /// <inheritdoc />
        public bool Equals(SqlDateTime2 other)
        {
            return _notNull == other._notNull && _ticks == other._ticks;
        }

        /// <inheritdoc />
        public int CompareTo(SqlDateTime2 other)
        {
            var result = _notNull.CompareTo(other._notNull);
            
            if (result != 0) 
            {
                return result;
            }

            return _ticks.CompareTo(other._ticks);
        }

        /// <inheritdoc />
        public int CompareTo(object obj)
        {
            if (obj is SqlDateTime2 sqlDateTime2)
            {
                return CompareTo(sqlDateTime2);
            }

            throw ADP.WrongType(obj.GetType(), typeof(SqlDateTime2));
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/OperatorDateTime/*' />
        public static explicit operator SqlDateTime2(DateTime dateTime)
        {
            return new SqlDateTime2(dateTime.Ticks);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/OperatorDBNull/*' />
        public static explicit operator SqlDateTime2(DBNull _)
        {
            return Null;
        }
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/OperatorSqlDateTime/*' />
        public static explicit operator DateTime(SqlDateTime2 sqlDateTime2)
        {
            return sqlDateTime2.Value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlDateTime2.xml' path='docs/members[@name="SqlDateTime2"]/GetXsdType/*' />
        public static XmlQualifiedName GetXsdType(XmlSchemaSet schemaSet)
        {
            return new XmlQualifiedName("dateTime2", "http://www.w3.org/2001/XMLSchema");
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            string attribute = reader.GetAttribute("nil", "http://www.w3.org/2001/XMLSchema-instance");
            if (attribute != null && XmlConvert.ToBoolean(attribute))
            {
                reader.ReadElementString();
                _notNull = false;
            }
            else
            {
                DateTime dateTime = XmlConvert.ToDateTime(reader.ReadElementString(), XmlDateTimeSerializationMode.RoundtripKind);
                if (dateTime.Kind != DateTimeKind.Unspecified)
                    throw new SqlTypeException(SQLResource.TimeZoneSpecifiedMessage);
                _ticks = dateTime.Ticks;
                _notNull = true;
            }
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            if (IsNull)
                writer.WriteAttributeString("xsi", "nil", "http://www.w3.org/2001/XMLSchema-instance", "true");
            else
                writer.WriteString(XmlConvert.ToString(Value, "yyyy-MM-ddTHH:mm:ss.fffffff"));
        }
    }
}
