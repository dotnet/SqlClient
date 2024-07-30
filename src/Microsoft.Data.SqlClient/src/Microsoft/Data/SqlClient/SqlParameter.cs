// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{
    internal abstract class DataFeed
    {
    }

    internal class StreamDataFeed : DataFeed
    {
        internal Stream _source;

        internal StreamDataFeed(Stream source)
        {
            _source = source;
        }
    }

    internal class TextDataFeed : DataFeed
    {
        private static UnicodeEncoding s_defaultEncoding;
        internal TextReader _source;

        internal TextDataFeed(TextReader source)
        {
            _source = source;
        }
        internal static UnicodeEncoding DefaultEncoding
        {
            get
            {
                UnicodeEncoding encoding = s_defaultEncoding;
                if (encoding is null)
                {
                    encoding = new UnicodeEncoding(false, false);
                    encoding = Interlocked.CompareExchange(ref s_defaultEncoding, encoding, null) ?? encoding;
                }
                return encoding;
            }
        }
    }

    internal class XmlDataFeed : DataFeed
    {
        internal XmlReader _source;

        internal XmlDataFeed(XmlReader source) => _source = source;
    }

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SqlParameter/*' />
    [TypeConverter(typeof(SqlParameter.SqlParameterConverter))]
    public sealed partial class SqlParameter : DbParameter, IDbDataParameter, ICloneable
    {
        internal sealed class SqlParameterConverter : ExpandableObjectConverter
        {

            // converter classes should have public ctor
            public SqlParameterConverter()
            {
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                if (typeof(InstanceDescriptor) == destinationType)
                {
                    return true;
                }
                return base.CanConvertTo(context, destinationType);
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType == null)
                {
                    throw ADP.ArgumentNull(nameof(destinationType));
                }
                if ((typeof(InstanceDescriptor) == destinationType) && (value is SqlParameter))
                {
                    return ConvertToInstanceDescriptor(value as SqlParameter);
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }

            private InstanceDescriptor ConvertToInstanceDescriptor(SqlParameter p)
            {
                int flags = 0; // if part of the collection - the parametername can't be empty

                if (p.ShouldSerializeSqlDbType())
                {
                    flags |= 1;
                }
                if (p.ShouldSerializeSize())
                {
                    flags |= 2;
                }
                if (!string.IsNullOrEmpty(p.SourceColumn))
                {
                    flags |= 4;
                }
                if (null != p.Value)
                {
                    flags |= 8;
                }
                if (
                    (ParameterDirection.Input != p.Direction) ||
                    p.IsNullable ||
                    p.ShouldSerializePrecision() ||
                    p.ShouldSerializeScale() ||
                    (DataRowVersion.Current != p.SourceVersion)
                )
                {
                    flags |= 16; // v1.0 everything
                }

                if (
                    p.SourceColumnNullMapping ||
                    !string.IsNullOrEmpty(p.XmlSchemaCollectionDatabase) ||
                    !string.IsNullOrEmpty(p.XmlSchemaCollectionOwningSchema) ||
                    !string.IsNullOrEmpty(p.XmlSchemaCollectionName)
                )
                {
                    flags |= 32; // v2.0 everything
                }

                Type[] ctorParams;
                object[] ctorValues;
                switch (flags)
                {
                    case 0: // ParameterName
                    case 1: // SqlDbType
                        ctorParams = new Type[] { typeof(string), typeof(SqlDbType) };
                        ctorValues = new object[] { p.ParameterName, p.SqlDbType };
                        break;
                    case 2: // Size
                    case 3: // Size, SqlDbType
                        ctorParams = new Type[] { typeof(string), typeof(SqlDbType), typeof(int) };
                        ctorValues = new object[] { p.ParameterName, p.SqlDbType, p.Size };
                        break;
                    case 4: // SourceColumn
                    case 5: // SourceColumn, SqlDbType
                    case 6: // SourceColumn, Size
                    case 7: // SourceColumn, Size, SqlDbType
                        ctorParams = new Type[] { typeof(string), typeof(SqlDbType), typeof(int), typeof(string) };
                        ctorValues = new object[] { p.ParameterName, p.SqlDbType, p.Size, p.SourceColumn };
                        break;
                    case 8: // Value
                        ctorParams = new Type[] { typeof(string), typeof(object) };
                        ctorValues = new object[] { p.ParameterName, p.Value };
                        break;
                    default:
                        if (0 == (32 & flags))
                        { // v1.0 everything
                            ctorParams = new Type[] {
                                                    typeof(string), typeof(SqlDbType), typeof(int), typeof(ParameterDirection),
                                                    typeof(bool), typeof(byte), typeof(byte),
                                                    typeof(string), typeof(DataRowVersion),
                                                    typeof(object) };
                            ctorValues = new object[] {
                                                      p.ParameterName, p.SqlDbType,  p.Size, p.Direction,
                                                      p.IsNullable, p.PrecisionInternal, p.ScaleInternal,
                                                      p.SourceColumn, p.SourceVersion,
                                                      p.Value };
                        }
                        else
                        { // v2.0 everything - round trip all browsable properties + precision/scale
                            ctorParams = new Type[] {
                                                    typeof(string), typeof(SqlDbType), typeof(int), typeof(ParameterDirection),
                                                    typeof(byte), typeof(byte),
                                                    typeof(string), typeof(DataRowVersion), typeof(bool),
                                                    typeof(object),
                                                    typeof(string), typeof(string),
                                                    typeof(string) };
                            ctorValues = new object[] {
                                                      p.ParameterName, p.SqlDbType,  p.Size, p.Direction,
                                                      p.PrecisionInternal, p.ScaleInternal,
                                                      p.SourceColumn, p.SourceVersion, p.SourceColumnNullMapping,
                                                      p.Value,
                                                      p.XmlSchemaCollectionDatabase, p.XmlSchemaCollectionOwningSchema,
                                                      p.XmlSchemaCollectionName};
                        }
                        break;
                }
                ConstructorInfo ctor = typeof(SqlParameter).GetConstructor(ctorParams);
                return new InstanceDescriptor(ctor, ctorValues);
            }
        }

        [Flags]
        private enum SqlParameterFlags : ushort
        {
            None = 0,
            IsNull = 1,
            IsNullable = 2,
            IsSqlParameterSqlType = 4,
            SourceColumnNullMapping = 8,
            CoercedValueIsSqlType = 16,
            CoercedValueIsDataFeed = 32,
            HasReceivedMetadata = 64,
            ForceColumnEncryption = 128,
            IsDerivedParameterTypeName = 256,
            HasScale = 512,
        }

        private MetaType _metaType;
        private SqlCollation _collation;
        private SqlMetaDataXmlSchemaCollection _xmlSchemaCollection;
        private string _udtTypeName;
        private string _typeName;
        private Exception _udtLoadError;
        private string _parameterName;
        private byte _precision;
        private byte _scale;
        private MetaType _internalMetaType;
        private SqlBuffer _sqlBufferReturnValue;
        private INullable _valueAsINullable;
        private int _actualSize;
        private object _value;
        private object _coercedValue;
        private object _parent;
        private ParameterDirection _direction;
        private int _size;
        private int _offset;
        private string _sourceColumn;
        private DataRowVersion _sourceVersion;
        private SqlParameterFlags _flags;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctor2/*' />
        public SqlParameter() : base()
        {
            _flags = SqlParameterFlags.IsNull;
            _actualSize = -1;
            _direction = ParameterDirection.Input;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbType/*' />
        public SqlParameter(string parameterName, SqlDbType dbType) : this()
        {
            ParameterName = parameterName;
            SqlDbType = dbType;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameValue/*' />
        public SqlParameter(string parameterName, object value) : this()
        {
            Debug.Assert(!(value is SqlDbType), "use SqlParameter(string, SqlDbType)");

            ParameterName = parameterName;
            Value = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSize/*' />
        public SqlParameter(string parameterName, SqlDbType dbType, int size) : this()
        {
            ParameterName = parameterName;
            SqlDbType = dbType;
            Size = size;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSizeSourceColumn/*' />
        public SqlParameter(string parameterName, SqlDbType dbType, int size, string sourceColumn) : this()
        {
            ParameterName = parameterName;
            SqlDbType = dbType;
            Size = size;
            SourceColumn = sourceColumn;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSizeDirectionIsNullablePrecisionScaleSourceColumnSourceVersionValue/*' />
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public SqlParameter(
            string parameterName,
            SqlDbType dbType,
            int size,
            ParameterDirection direction,
            bool isNullable,
            byte precision,
            byte scale,
            string sourceColumn,
            DataRowVersion sourceVersion,
            object value
        )
            : this(parameterName, dbType, size, sourceColumn)
        {
            Direction = direction;
            IsNullable = isNullable;
#if NETFRAMEWORK
            PrecisionInternal = precision;
            ScaleInternal = scale;
#else
            Precision = precision;
            Scale = scale;
#endif
            SourceVersion = sourceVersion;
            Value = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ctorParameterNameDbTypeSizeDirectionPrecisionScaleSourceColumnSourceVersionSourceColumnNullMappingValue/*' />
        public SqlParameter(
            string parameterName,
            SqlDbType dbType,
            int size,
            ParameterDirection direction,
            byte precision,
            byte scale,
            string sourceColumn,
            DataRowVersion sourceVersion,
            bool sourceColumnNullMapping,
            object value,
            string xmlSchemaCollectionDatabase,
            string xmlSchemaCollectionOwningSchema,
            string xmlSchemaCollectionName
        )
            : this()
        {
            ParameterName = parameterName;
            SqlDbType = dbType;
            Size = size;
            Direction = direction;
#if NETFRAMEWORK
            PrecisionInternal = precision;
            ScaleInternal = scale;
#else
            Precision = precision;
            Scale = scale;
#endif
            SourceColumn = sourceColumn;
            SourceVersion = sourceVersion;
            SourceColumnNullMapping = sourceColumnNullMapping;
            Value = value;
            if (!string.IsNullOrEmpty(xmlSchemaCollectionDatabase) || !string.IsNullOrEmpty(xmlSchemaCollectionOwningSchema) || !string.IsNullOrEmpty(xmlSchemaCollectionName))
            {
                EnsureXmlSchemaCollection();
                _xmlSchemaCollection.Database = xmlSchemaCollectionDatabase;
                _xmlSchemaCollection.OwningSchema = xmlSchemaCollectionOwningSchema;
                _xmlSchemaCollection.Name = xmlSchemaCollectionName;
            }
        }

        private SqlParameter(SqlParameter source) : this()
        {
            ADP.CheckArgumentNull(source, nameof(source));
            source.CloneHelper(this);
            if (_value is ICloneable cloneable)
            {
                _value = cloneable.Clone();
            }
        }

        /// <summary>
        /// Get or set the encryption related metadata of this SqlParameter.
        /// Should be set to a non-null value only once.
        /// </summary>
        internal SqlCipherMetadata CipherMetadata { get; set; }

        /// <summary>
        /// Indicates if the parameter encryption metadata received by sp_describe_parameter_encryption.
        /// For unencrypted parameters, the encryption metadata should still be sent (and will indicate 
        /// that no encryption is needed).
        /// </summary>
        internal bool HasReceivedMetadata
        {
            get => HasFlag(SqlParameterFlags.HasReceivedMetadata);
            set => SetFlag(SqlParameterFlags.HasReceivedMetadata, value);
        }

        /// <summary>
        /// Returns the normalization rule version number as a byte
        /// </summary>
        internal byte NormalizationRuleVersion => CipherMetadata?.NormalizationRuleVersion ?? 0x00;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/CompareInfo/*' />
        [Browsable(false)]
        public SqlCompareOptions CompareInfo
        {
            // Bits 21 through 25 represent the CompareInfo
            get
            {
                SqlCollation collation = _collation;
                if (null != collation)
                {
                    return collation.SqlCompareOptions;
                }
                return SqlCompareOptions.None;
            }
            set
            {
                SqlCollation collation = _collation;

                // Copied from SQLString.x_iValidSqlCompareOptionMask
                SqlCompareOptions validSqlCompareOptionMask =
                    SqlCompareOptions.IgnoreCase | SqlCompareOptions.IgnoreWidth |
                    SqlCompareOptions.IgnoreNonSpace | SqlCompareOptions.IgnoreKanaType |
                    SqlCompareOptions.BinarySort | SqlCompareOptions.BinarySort2;

                if ((value & validSqlCompareOptionMask) != value)
                {
                    throw ADP.ArgumentOutOfRange(nameof(CompareInfo));
                }

                if (collation == null || collation.SqlCompareOptions != value)
                {
                    _collation = SqlCollation.FromLCIDAndSort(collation?.LCID ?? 0, value);
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/XmlSchemaCollectionDatabase/*' />
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Xml)]
        public string XmlSchemaCollectionDatabase
        {
            get => _xmlSchemaCollection?.Database ?? string.Empty;
            set => EnsureXmlSchemaCollection().Database = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/XmlSchemaCollectionOwningSchema/*' />
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Xml)]
        public string XmlSchemaCollectionOwningSchema
        {
            get => _xmlSchemaCollection?.OwningSchema ?? string.Empty;
            set => EnsureXmlSchemaCollection().OwningSchema = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/XmlSchemaCollectionName/*' />
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Xml)]
        public string XmlSchemaCollectionName
        {
            get => _xmlSchemaCollection?.Name ?? string.Empty;
            set => EnsureXmlSchemaCollection().Name = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ForceColumnEncryption/*' />
        [
        DefaultValue(false),
        ResCategory(StringsHelper.ResourceNames.DataCategory_Data)
        ]
        public bool ForceColumnEncryption
        {
            get => HasFlag(SqlParameterFlags.ForceColumnEncryption);
            set => SetFlag(SqlParameterFlags.ForceColumnEncryption, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/DbType/*' />
        public override DbType DbType
        {
            get => GetMetaTypeOnly().DbType;
            set
            {
                MetaType metatype = _metaType;
                if ((null == metatype) || (metatype.DbType != value))
                {
                    PropertyTypeChanging();
                    _metaType = MetaType.GetMetaTypeFromDbType(value);
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ResetDbType/*' />
        public override void ResetDbType() => ResetSqlDbType();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ParameterName/*' />
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        public override string ParameterName
        {
            get => _parameterName ?? string.Empty;
            set
            {
                if (
                    string.IsNullOrEmpty(value) ||
                    (value.Length < TdsEnums.MAX_PARAMETER_NAME_LENGTH) ||
                    (
                        (value[0] == '@') &&
                        (value.Length <= TdsEnums.MAX_PARAMETER_NAME_LENGTH)
                    )
                )
                {
                    if (_parameterName != value)
                    {
                        PropertyChanging();
                        _parameterName = value;
                    }
                }
                else
                {
                    throw SQL.InvalidParameterNameLength(value);
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/LocaleId/*' />
        [Browsable(false)]
        public int LocaleId
        {
            // Lowest 20 bits represent LocaleId
            get
            {
                SqlCollation collation = _collation;
                if (null != collation)
                {
                    return collation.LCID;
                }
                return 0;
            }
            set
            {
                SqlCollation collation = _collation;

                if (value != (SqlCollation.MaskLcid & value))
                {
                    throw ADP.ArgumentOutOfRange(nameof(LocaleId));
                }

                if (collation == null || collation.LCID != value)
                {
                    _collation = SqlCollation.FromLCIDAndSort(value, collation?.SqlCompareOptions ?? SqlCompareOptions.None);
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Precision/*' />
        [
        DefaultValue((byte)0),
        ResCategory(StringsHelper.ResourceNames.DataCategory_Data)
        ]
        public new byte Precision
        {
            get => PrecisionInternal;
            set => PrecisionInternal = value;
        }

        private bool ShouldSerializePrecision() => _precision != 0;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Scale/*' />
        [
        DefaultValue((byte)0),
        ResCategory(StringsHelper.ResourceNames.DataCategory_Data)
        ]
        public new byte Scale
        {
            get => ScaleInternal;
            set => ScaleInternal = value;
        }

        internal byte ScaleInternal
        {
            get
            {
                byte scale = _scale;
                SqlDbType dbtype = GetMetaSqlDbTypeOnly();
                if ((scale == 0) && (dbtype == SqlDbType.Decimal))
                {
                    scale = ValueScale(SqlValue);
                }
                return scale;
            }
            set
            {
                if (_scale != value || !HasFlag(SqlParameterFlags.HasScale))
                {
                    PropertyChanging();
                    _scale = value;
                    SetFlag(SqlParameterFlags.HasScale, true);
                    _actualSize = -1;   // Invalidate actual size such that it is re-calculated
                }
            }
        }

        private bool ShouldSerializeScale() => _scale != 0; // V1.0 compat, ignore _hasScale

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SqlDbType/*' />
        [
        RefreshProperties(RefreshProperties.All),
        ResCategory(StringsHelper.ResourceNames.DataCategory_Data),
        DbProviderSpecificTypeProperty(true)
        ]
        public SqlDbType SqlDbType
        {
            get => GetMetaTypeOnly().SqlDbType;
            set
            {
                MetaType metatype = _metaType;
                // HACK!!!
                // We didn't want to expose SmallVarBinary on SqlDbType so we 
                // stuck it at the end of SqlDbType in v1.0, except that now 
                // we have new data types after that and it's smack dab in the
                // middle of the valid range.  To prevent folks from setting 
                // this invalid value we have to have this code here until we
                // can take the time to fix it later.
                if (TdsEnums.SmallVarBinary == value)
                {
                    throw SQL.InvalidSqlDbType(value);
                }
                if ((null == metatype) || (metatype.SqlDbType != value))
                {
                    PropertyTypeChanging();
                    _metaType = MetaType.GetMetaTypeFromSqlDbType(value, value == SqlDbType.Structured);
                }
            }
        }

        private bool ShouldSerializeSqlDbType() => _metaType != null;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ResetSqlDbType/*' />
        public void ResetSqlDbType()
        {
            if (_metaType != null)
            {
                PropertyTypeChanging();
                _metaType = null;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SqlValue/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ]
        public object SqlValue
        {
            get
            {
                if (_udtLoadError != null)
                {
                    throw _udtLoadError;
                }

                if (_value != null)
                {
                    if (_value == DBNull.Value)
                    {
                        return MetaType.GetNullSqlValue(GetMetaTypeOnly().SqlType);
                    }
                    if (_value is INullable)
                    {
                        return _value;
                    }

                    // For Date and DateTime2, return the CLR object directly without converting it to a SqlValue
                    // GetMetaTypeOnly() will convert _value to a string in the case of char or char[], so only check
                    // the SqlDbType for DateTime. This is the only case when we might return the CLR value directly.
                    if (_value is DateTime)
                    {
                        SqlDbType sqlDbType = GetMetaTypeOnly().SqlDbType;
                        if (sqlDbType == SqlDbType.Date || sqlDbType == SqlDbType.DateTime2)
                        {
                            return _value;
                        }
                    }

                    return (MetaType.GetSqlValueFromComVariant(_value));
                }
                else if (_sqlBufferReturnValue != null)
                {
                    return _sqlBufferReturnValue.SqlValue;
                }
                return null;
            }
            set
            {
                Value = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/UdtTypeName/*' />
        [
        Browsable(false),
        EditorBrowsable(EditorBrowsableState.Advanced)
        ]
        public string UdtTypeName
        {
            get => _udtTypeName ?? string.Empty;
            set => _udtTypeName = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/TypeName/*' />
        [
        Browsable(false),
        EditorBrowsable(EditorBrowsableState.Advanced)
        ]
        public string TypeName
        {
            get => _typeName ?? string.Empty;
            set
            {
                _typeName = value;
                IsDerivedParameterTypeName = false;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Value/*' />
        [
        RefreshProperties(RefreshProperties.All),
        ResCategory(StringsHelper.ResourceNames.DataCategory_Data),
        TypeConverter(typeof(StringConverter)),
        ]
        public override object Value
        {
            get
            {
                if (_udtLoadError != null)
                {
                    throw _udtLoadError;
                }

                if (_value != null)
                {
                    return _value;
                }
                else if (_sqlBufferReturnValue != null)
                {
                    if (ParameterIsSqlType)
                    {
                        return _sqlBufferReturnValue.SqlValue;
                    }
                    return _sqlBufferReturnValue.Value;
                }
                return null;
            }
            set
            {
                _value = value;
                _sqlBufferReturnValue = null;
                _coercedValue = null;
                _valueAsINullable = _value as INullable;
                SetFlag(SqlParameterFlags.IsSqlParameterSqlType, _valueAsINullable != null);
                SetFlag(SqlParameterFlags.IsNull, (null == _value) || (_value == DBNull.Value) || (HasFlag(SqlParameterFlags.IsSqlParameterSqlType) && _valueAsINullable.IsNull));
                _udtLoadError = null;
                _actualSize = -1;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Direction/*' />
        [
        RefreshProperties(RefreshProperties.All),
        ResCategory(StringsHelper.ResourceNames.DataCategory_Data),
        ]
        public override ParameterDirection Direction
        {
            get => _direction;
            set
            {
                if (_direction != value)
                {
                    switch (value)
                    {
                        case ParameterDirection.Input:
                        case ParameterDirection.Output:
                        case ParameterDirection.InputOutput:
                        case ParameterDirection.ReturnValue:
                            PropertyChanging();
                            _direction = value;
                            break;
                        default:
                            throw ADP.InvalidParameterDirection(value);
                    }
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/IsNullable/*' />
        public override bool IsNullable
        {
            get => HasFlag(SqlParameterFlags.IsNullable);
            set => SetFlag(SqlParameterFlags.IsNullable, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Offset/*' />
        public int Offset
        {
            get => _offset;
            set
            {
                if (value < 0)
                {
                    throw ADP.InvalidOffsetValue(value);
                }
                _offset = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/Size/*' />
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Data)]
        public override int Size
        {
            get
            {
                int size = _size;
                if (size == 0)
                {
                    size = ValueSize(Value);
                }
                return size;
            }
            set
            {
                if (value != _size)
                {
                    if (value < -1)
                    {
                        throw ADP.InvalidSizeValue(value);
                    }
                    PropertyChanging();
                    _size = value;
                }
            }
        }

        private void ResetSize()
        {
            if (_size != 0)
            {
                PropertyChanging();
                _size = 0;
            }
        }

        private bool ShouldSerializeSize() => _size != 0;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SourceColumn/*' />
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Update)]
        public override string SourceColumn
        {
            get => _sourceColumn ?? string.Empty;
            set => _sourceColumn = value;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SourceColumnNullMapping/*' />   
        [ResCategory("DataCategory_Update")]
#if NET6_0_OR_GREATER
        [ResDescription(StringsHelper.ResourceNames.SqlParameter_SourceColumnNullMapping)]
#endif
        public override bool SourceColumnNullMapping
        {
            get => HasFlag(SqlParameterFlags.SourceColumnNullMapping);
            set => SetFlag(SqlParameterFlags.SourceColumnNullMapping, value);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/ToString/*' />
        [ResCategory("Data")]
        public override string ToString() => ParameterName;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/SourceVersion/*' />
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Update)]
        public override DataRowVersion SourceVersion
        {
            get
            {
                DataRowVersion sourceVersion = _sourceVersion;
                return (sourceVersion != 0) ? sourceVersion : DataRowVersion.Current;
            }
            set
            {
                switch (value)
                {
                    case DataRowVersion.Original:
                    case DataRowVersion.Current:
                    case DataRowVersion.Proposed:
                    case DataRowVersion.Default:
                        _sourceVersion = value;
                        break;
                    default:
                        throw ADP.InvalidDataRowVersion(value);
                }
            }
        }


        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlParameter.xml' path='docs/members[@name="SqlParameter"]/System.ICloneable.Clone/*' />
        object ICloneable.Clone() => new SqlParameter(this);


        private object CoercedValue
        {
            get => _coercedValue;
            set => _coercedValue = value;
        }

        internal bool CoercedValueIsDataFeed
        {
            get
            {
                if (null == _coercedValue)
                {
                    GetCoercedValue();
                }
                AssertCachedPropertiesAreValid();
                return HasFlag(SqlParameterFlags.CoercedValueIsDataFeed);
            }
        }

        internal bool CoercedValueIsSqlType
        {
            get
            {
                if (_coercedValue == null)
                {
                    GetCoercedValue();
                }
                AssertCachedPropertiesAreValid();
                return HasFlag(SqlParameterFlags.CoercedValueIsSqlType);
            }
        }

        //
        // currently the user can't set this value.  it gets set by the returnvalue from tds
        //
        internal SqlCollation Collation
        {
            get => _collation;
            set => _collation = value;
        }

        private bool HasFlag(SqlParameterFlags flag)
        {
            return (_flags & flag) != 0;
        }

        internal bool IsNull
        {
            get
            {
                // NOTE: Udts can change their value any time
                if (_internalMetaType.SqlDbType == SqlDbType.Udt)
                {
                    SetFlag(SqlParameterFlags.IsNull, (_value == null) || (_value == DBNull.Value) || (HasFlag(SqlParameterFlags.IsSqlParameterSqlType) && _valueAsINullable.IsNull));
                }
                return HasFlag(SqlParameterFlags.IsNull);
            }
        }

        internal MetaType InternalMetaType
        {
            get
            {
                Debug.Assert(null != _internalMetaType, "null InternalMetaType");
                return _internalMetaType;
            }
            set => _internalMetaType = value;
        }

        internal byte PrecisionInternal
        {
            get
            {
                byte precision = _precision;
                SqlDbType dbtype = GetMetaSqlDbTypeOnly();
                if ((0 == precision) && (SqlDbType.Decimal == dbtype))
                {
                    precision = ValuePrecision(SqlValue);
                }
                return precision;
            }
            set
            {
                SqlDbType sqlDbType = SqlDbType;
                if (sqlDbType == SqlDbType.Decimal && value > TdsEnums.MAX_NUMERIC_PRECISION)
                {
                    throw SQL.PrecisionValueOutOfRange(value);
                }
                if (_precision != value)
                {
                    PropertyChanging();
                    _precision = value;
                }
            }
        }

        internal bool ParameterIsSqlType
        {
            get => HasFlag(SqlParameterFlags.IsSqlParameterSqlType);
            set => SetFlag(SqlParameterFlags.IsSqlParameterSqlType, value);
        }

        internal string GetPrefixedParameterName()
        {
            string parameterName = ParameterName;
            if ((parameterName.Length > 0) && (parameterName[0] != '@'))
            {
                parameterName = "@" + parameterName;
            }
            Debug.Assert(parameterName.Length <= TdsEnums.MAX_PARAMETER_NAME_LENGTH, "parameter name too long");
            return parameterName;
        }

        /// <summary>
        /// Checks the parameter name for the @ prefix and appends it if it is missing, then apends the parameter name
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="rawParameterName"></param>
        internal static void AppendPrefixedParameterName(StringBuilder builder, string rawParameterName)
        {
            if (!string.IsNullOrEmpty(rawParameterName))
            {
                if (rawParameterName[0] != '@')
                {
                    builder.Append('@');
                }
                builder.Append(rawParameterName);
            }
        }

        /// <summary>
        /// Compares the two input names for equality discounting the @ prefix on either or both arguments
        /// </summary>
        /// <returns></returns>
        internal static bool ParameterNamesEqual(string lhs, string rhs, StringComparison comparison = StringComparison.Ordinal)
        {
            if (!string.IsNullOrEmpty(lhs))
            {
                if (string.IsNullOrEmpty(rhs))
                {
                    return false;
                }
                else
                {
                    ReadOnlySpan<char> lhsSpan = lhs.AsSpan();
                    if (lhs[0] == '@')
                    {
                        lhsSpan = lhsSpan.Slice(1);
                    }
                    ReadOnlySpan<char> rhsSpan = rhs.AsSpan();
                    if (rhsSpan[0] == '@')
                    {
                        rhsSpan = rhsSpan.Slice(1);
                    }
                    return MemoryExtensions.Equals(lhsSpan, rhsSpan, comparison);
                }
            }
            else
            {
                // lhs is null or empty so equality is only possible if the rhs is the same
                return string.IsNullOrEmpty(rhs);
            }
        }

        internal bool SizeInferred => 0 == _size;

        internal INullable ValueAsINullable => _valueAsINullable;

        internal bool IsDerivedParameterTypeName
        {
            get => HasFlag(SqlParameterFlags.IsDerivedParameterTypeName);
            set => SetFlag(SqlParameterFlags.IsDerivedParameterTypeName, value);
        }

        private void CloneHelper(SqlParameter destination)
        {
            // NOTE: _parent is not cloned
            destination._value = _value;
            destination._direction = _direction;
            destination._size = _size;
            destination._offset = _offset;
            destination._sourceColumn = _sourceColumn;
            destination._sourceVersion = _sourceVersion;
            destination._flags = _flags & (
                SqlParameterFlags.SourceColumnNullMapping |
                SqlParameterFlags.IsNull |
                SqlParameterFlags.IsNullable |
                SqlParameterFlags.IsSqlParameterSqlType |
                SqlParameterFlags.CoercedValueIsDataFeed |
                SqlParameterFlags.CoercedValueIsSqlType |
                SqlParameterFlags.ForceColumnEncryption |
                SqlParameterFlags.IsDerivedParameterTypeName
            // HasScale and HasReceivedMetadata deliberately omitted
            );
            destination._metaType = _metaType;
            destination._collation = _collation;
            if (_xmlSchemaCollection != null)
            {
                destination.EnsureXmlSchemaCollection().CopyFrom(_xmlSchemaCollection);
            }
            destination._udtTypeName = _udtTypeName;
            destination._typeName = _typeName;
            destination._udtLoadError = _udtLoadError;
            destination._parameterName = _parameterName;
            destination._precision = _precision;
            destination._scale = _scale;
            destination._sqlBufferReturnValue = _sqlBufferReturnValue;
            destination._internalMetaType = _internalMetaType;
            destination.CoercedValue = CoercedValue; // copy cached value reference because of XmlReader problem
            destination._valueAsINullable = _valueAsINullable;
            destination._actualSize = _actualSize;
        }

        internal void CopyTo(SqlParameter destination)
        {
            ADP.CheckArgumentNull(destination, nameof(destination));
            CloneHelper(destination);
        }

        internal object CompareExchangeParent(object value, object comparand)
        {
            // the interlock guarantees same parameter won't belong to multiple collections
            // at the same time, but to actually occur the user must really try
            // since we never declared thread safety, we don't care at this time
            //return System.Threading.Interlocked.CompareExchange(ref _parent, value, comparand);
            object parent = _parent;
            if (comparand == parent)
            {
                _parent = value;
            }
            return parent;
        }

        private SqlMetaDataXmlSchemaCollection EnsureXmlSchemaCollection()
        {
            if (_xmlSchemaCollection is null)
            {
                _xmlSchemaCollection = new SqlMetaDataXmlSchemaCollection();
            }
            return _xmlSchemaCollection;
        }

        internal void FixStreamDataForNonPLP()
        {
            object value = GetCoercedValue();
            AssertCachedPropertiesAreValid();
            if (!HasFlag(SqlParameterFlags.CoercedValueIsDataFeed))
            {
                return;
            }

            SetFlag(SqlParameterFlags.CoercedValueIsDataFeed, false);

            if (value is TextDataFeed textFeed)
            {
                if (Size > 0)
                {
                    char[] buffer = new char[Size];
                    int nRead = textFeed._source.ReadBlock(buffer, 0, Size);
                    CoercedValue = new string(buffer, 0, nRead);
                }
                else
                {
                    CoercedValue = textFeed._source.ReadToEnd();
                }
                return;
            }

            if (value is StreamDataFeed streamFeed)
            {
                if (Size > 0)
                {
                    byte[] buffer = new byte[Size];
                    int totalRead = 0;
                    Stream sourceStream = streamFeed._source;
                    while (totalRead < Size)
                    {
                        int nRead = sourceStream.Read(buffer, totalRead, Size - totalRead);
                        if (nRead == 0)
                        {
                            break;
                        }
                        totalRead += nRead;
                    }
                    if (totalRead < Size)
                    {
                        Array.Resize(ref buffer, totalRead);
                    }
                    CoercedValue = buffer;
                }
                else
                {
                    MemoryStream ms = new MemoryStream();
                    streamFeed._source.CopyTo(ms);
                    CoercedValue = ms.ToArray();
                }
                return;
            }

            if (value is XmlDataFeed xmlFeed)
            {
                CoercedValue = MetaType.GetStringFromXml(xmlFeed._source);
                return;
            }

            // We should have returned before reaching here
            Debug.Fail("_coercedValueIsDataFeed was true, but the value was not a known DataFeed type");
        }

        private void GetActualFieldsAndProperties(out List<SmiExtendedMetaData> fields, out SmiMetaDataPropertyCollection props, out ParameterPeekAheadValue peekAhead)
        {
            fields = null;
            props = null;
            peekAhead = null;

            object value = GetCoercedValue();
            if (value is DataTable dt)
            {
                if (dt.Columns.Count <= 0)
                {
                    throw SQL.NotEnoughColumnsInStructuredType();
                }
                fields = new List<SmiExtendedMetaData>(dt.Columns.Count);
                bool[] keyCols = new bool[dt.Columns.Count];
                bool hasKey = false;

                // set up primary key as unique key list
                //  do this prior to general metadata loop to favor the primary key
                if (null != dt.PrimaryKey && 0 < dt.PrimaryKey.Length)
                {
                    foreach (DataColumn col in dt.PrimaryKey)
                    {
                        keyCols[col.Ordinal] = true;
                        hasKey = true;
                    }
                }

                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    fields.Add(MetaDataUtilsSmi.SmiMetaDataFromDataColumn(dt.Columns[i], dt));

                    // DataColumn uniqueness is only for a single column, so don't add
                    //  more than one.  (keyCols.Count first for assumed minimal perf benefit)
                    if (!hasKey && dt.Columns[i].Unique)
                    {
                        keyCols[i] = true;
                        hasKey = true;
                    }
                }

                // Add unique key property, if any found.
                if (hasKey)
                {
                    props = new SmiMetaDataPropertyCollection();
                    props[SmiPropertySelector.UniqueKey] = new SmiUniqueKeyProperty(new List<bool>(keyCols));
                }
            }
            else if (value is SqlDataReader sqlReader)
            {
                fields = new List<SmiExtendedMetaData>(sqlReader.GetInternalSmiMetaData());
                if (fields.Count <= 0)
                {
                    throw SQL.NotEnoughColumnsInStructuredType();
                }

                bool[] keyCols = new bool[fields.Count];
                bool hasKey = false;
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i] is SmiQueryMetaData qmd && !qmd.IsKey.IsNull && qmd.IsKey.Value)
                    {
                        keyCols[i] = true;
                        hasKey = true;
                    }
                }

                // Add unique key property, if any found.
                if (hasKey)
                {
                    props = new SmiMetaDataPropertyCollection();
                    props[SmiPropertySelector.UniqueKey] = new SmiUniqueKeyProperty(new List<bool>(keyCols));
                }
            }
            else if (value is IEnumerable<SqlDataRecord> enumerable)
            {
                // must grab the first record of the enumerator to get the metadata
                IEnumerator<SqlDataRecord> enumerator = enumerable.GetEnumerator();
                SqlDataRecord firstRecord = null;
                try
                {
                    // no need for fields if there's no rows or no columns -- we'll be sending a null instance anyway.
                    if (enumerator.MoveNext())
                    {
                        firstRecord = enumerator.Current;
                        int fieldCount = firstRecord.FieldCount;
                        if (0 < fieldCount)
                        {
                            // It's valid!  Grab those fields.
                            bool[] keyCols = new bool[fieldCount];
                            bool[] defaultFields = new bool[fieldCount];
                            bool[] sortOrdinalSpecified = new bool[fieldCount];
                            int maxSortOrdinal = -1;  // largest sort ordinal seen, used to optimize locating holes in the list
                            bool hasKey = false;
                            bool hasDefault = false;
                            int sortCount = 0;
                            SmiOrderProperty.SmiColumnOrder[] sort = new SmiOrderProperty.SmiColumnOrder[fieldCount];
                            fields = new List<SmiExtendedMetaData>(fieldCount);
                            for (int i = 0; i < fieldCount; i++)
                            {
                                SqlMetaData colMeta = firstRecord.GetSqlMetaData(i);
                                fields.Add(MetaDataUtilsSmi.SqlMetaDataToSmiExtendedMetaData(colMeta));
                                if (colMeta.IsUniqueKey)
                                {
                                    keyCols[i] = true;
                                    hasKey = true;
                                }

                                if (colMeta.UseServerDefault)
                                {
                                    defaultFields[i] = true;
                                    hasDefault = true;
                                }

                                sort[i]._order = colMeta.SortOrder;
                                if (SortOrder.Unspecified != colMeta.SortOrder)
                                {
                                    // SqlMetaData takes care of checking for negative sort ordinals with specified sort order

                                    // bail early if there's no way sort order could be monotonically increasing
                                    if (fieldCount <= colMeta.SortOrdinal)
                                    {
                                        throw SQL.SortOrdinalGreaterThanFieldCount(i, colMeta.SortOrdinal);
                                    }

                                    // Check to make sure we haven't seen this ordinal before
                                    if (sortOrdinalSpecified[colMeta.SortOrdinal])
                                    {
                                        throw SQL.DuplicateSortOrdinal(colMeta.SortOrdinal);
                                    }

                                    sort[i]._sortOrdinal = colMeta.SortOrdinal;
                                    sortOrdinalSpecified[colMeta.SortOrdinal] = true;
                                    if (colMeta.SortOrdinal > maxSortOrdinal)
                                    {
                                        maxSortOrdinal = colMeta.SortOrdinal;
                                    }
                                    sortCount++;
                                }
                            }

                            if (hasKey)
                            {
                                props = new SmiMetaDataPropertyCollection();
                                props[SmiPropertySelector.UniqueKey] = new SmiUniqueKeyProperty(new List<bool>(keyCols));
                            }

                            if (hasDefault)
                            {
                                // May have already created props list in unique key handling
                                if (null == props)
                                {
                                    props = new SmiMetaDataPropertyCollection();
                                }

                                props[SmiPropertySelector.DefaultFields] = new SmiDefaultFieldsProperty(new List<bool>(defaultFields));
                            }

                            if (0 < sortCount)
                            {
                                // validate monotonically increasing sort order.
                                //  Since we already checked for duplicates, we just need
                                //  to watch for values outside of the sortCount range.
                                if (maxSortOrdinal >= sortCount)
                                {
                                    // there is at least one hole, find the first one
                                    int i;
                                    for (i = 0; i < sortCount; i++)
                                    {
                                        if (!sortOrdinalSpecified[i])
                                        {
                                            break;
                                        }
                                    }
                                    Debug.Assert(i < sortCount, "SqlParameter.GetActualFieldsAndProperties: SortOrdinal hole-finding algorithm failed!");
                                    throw SQL.MissingSortOrdinal(i);
                                }

                                // May have already created props list
                                if (null == props)
                                {
                                    props = new SmiMetaDataPropertyCollection();
                                }

                                props[SmiPropertySelector.SortOrder] = new SmiOrderProperty(
                                        new List<SmiOrderProperty.SmiColumnOrder>(sort));
                            }

                            // pack it up so we don't have to rewind to send the first value
                            peekAhead = new ParameterPeekAheadValue()
                            {
                                Enumerator = enumerator,
                                FirstRecord = firstRecord
                            };

                            // now that it's all packaged, make sure we don't dispose it.
                            enumerator = null;
                        }
                        else
                        {
                            throw SQL.NotEnoughColumnsInStructuredType();
                        }
                    }
                    else
                    {
                        throw SQL.IEnumerableOfSqlDataRecordHasNoRows();
                    }
                }
                finally
                {
                    if (enumerator != null)
                    {
                        enumerator.Dispose();
                    }
                }
            }
            else if (value is DbDataReader dbReader)
            {
                DataTable schema = dbReader.GetSchemaTable();
                if (schema.Rows.Count <= 0)
                {
                    throw SQL.NotEnoughColumnsInStructuredType();
                }

                int fieldCount = schema.Rows.Count;
                fields = new List<SmiExtendedMetaData>(fieldCount);
                bool[] keyCols = new bool[fieldCount];
                bool hasKey = false;
                int ordinalForIsKey = schema.Columns[SchemaTableColumn.IsKey].Ordinal;
                int ordinalForColumnOrdinal = schema.Columns[SchemaTableColumn.ColumnOrdinal].Ordinal;
                // Extract column metadata
                for (int rowOrdinal = 0; rowOrdinal < fieldCount; rowOrdinal++)
                {
                    DataRow row = schema.Rows[rowOrdinal];
                    SmiExtendedMetaData candidateMd = MetaDataUtilsSmi.SmiMetaDataFromSchemaTableRow(row);

                    // Determine destination ordinal.  Allow for ordinal not specified by assuming rowOrdinal *is* columnOrdinal
                    // in that case, but don't worry about mix-and-match of the two techniques
                    int columnOrdinal = rowOrdinal;
                    if (!row.IsNull(ordinalForColumnOrdinal))
                    {
                        columnOrdinal = (int)row[ordinalForColumnOrdinal];
                    }

                    // After this point, things we are creating (keyCols, fields) should be accessed by columnOrdinal
                    // while the source should just be accessed via "row".

                    // Watch for out-of-range ordinals
                    if (columnOrdinal >= fieldCount || columnOrdinal < 0)
                    {
                        throw SQL.InvalidSchemaTableOrdinals();
                    }

                    // extend empty space if out-of-order ordinal
                    while (columnOrdinal > fields.Count)
                    {
                        fields.Add(null);
                    }

                    // Now add the candidate to the list
                    if (fields.Count == columnOrdinal)
                    {
                        fields.Add(candidateMd);
                    }
                    else
                    {
                        // Disallow two columns using the same ordinal (even if due to mixing null and non-null columnOrdinals)
                        if (fields[columnOrdinal] != null)
                        {
                            throw SQL.InvalidSchemaTableOrdinals();
                        }

                        // Don't use insert, since it shifts all later columns down a notch
                        fields[columnOrdinal] = candidateMd;
                    }

                    // Propagate key information
                    if (!row.IsNull(ordinalForIsKey) && (bool)row[ordinalForIsKey])
                    {
                        keyCols[columnOrdinal] = true;
                        hasKey = true;
                    }
                }

#if DEBUG
                // Check for holes
                //  Above loop logic prevents holes since:
                //      1) loop processes fieldcount # of columns
                //      2) no ordinals outside continuous range from 0 to fieldcount - 1 are allowed
                //      3) no duplicate ordinals are allowed
                // But assert no holes to be sure.
                foreach (SmiExtendedMetaData md in fields)
                {
                    Debug.Assert(null != md, "Shouldn't be able to have holes, since original loop algorithm prevents such.");
                }
#endif

                // Add unique key property, if any defined.
                if (hasKey)
                {
                    props = new SmiMetaDataPropertyCollection();
                    props[SmiPropertySelector.UniqueKey] = new SmiUniqueKeyProperty(new List<bool>(keyCols));
                }
            }
        }

        internal byte GetActualScale()
        {
            if (ShouldSerializeScale())
            {
                return ScaleInternal;
            }

            // issue: how could a user specify 0 as the actual scale?
            if (GetMetaTypeOnly().IsVarTime)
            {
                return TdsEnums.DEFAULT_VARTIME_SCALE;
            }
            return ValueScale(CoercedValue);
        }

        //
        // always returns data in bytes - except for non-unicode chars, which will be in number of chars
        //
        internal int GetActualSize()
        {
            MetaType mt = InternalMetaType;
            SqlDbType actualType = mt.SqlDbType;
            // NOTE: Users can change the Udt at any time, so we may need to recalculate
            if ((_actualSize == -1) || (actualType == SqlDbType.Udt))
            {
                _actualSize = 0;
                object val = GetCoercedValue();
                bool isSqlVariant = false;

                if (IsNull && !mt.IsVarTime)
                {
                    return 0;
                }

                // if this is a backend SQLVariant type, then infer the TDS type from the SQLVariant type
                if (actualType == SqlDbType.Variant)
                {
                    mt = MetaType.GetMetaTypeFromValue(val, streamAllowed: false);
                    actualType = MetaType.GetSqlDataType(mt.TDSType, 0 /*no user type*/, 0 /*non-nullable type*/).SqlDbType;
                    isSqlVariant = true;
                }

                if (mt.IsFixed)
                {
                    _actualSize = mt.FixedLength;
                }
                else
                {
                    // @hack: until we have ForceOffset behavior we have the following semantics:
                    // @hack: if the user supplies a Size through the Size property or constructor,
                    // @hack: we only send a MAX of Size bytes over.  If the actualSize is < Size, then
                    // @hack: we send over actualSize
                    int coercedSize = 0;

                    // get the actual length of the data, in bytes
                    switch (actualType)
                    {
                        case SqlDbType.NChar:
                        case SqlDbType.NVarChar:
                        case SqlDbType.NText:
                        case SqlDbType.Xml:
                        case SqlDbTypeExtensions.Json:
                            {
                                coercedSize = ((!HasFlag(SqlParameterFlags.IsNull)) && (!HasFlag(SqlParameterFlags.CoercedValueIsDataFeed))) ? StringSize(val, HasFlag(SqlParameterFlags.CoercedValueIsSqlType)) : 0;
                                _actualSize = (ShouldSerializeSize() ? Size : 0);
                                _actualSize = (ShouldSerializeSize() && (_actualSize <= coercedSize)) ? _actualSize : coercedSize;
                                if (_actualSize == -1)
                                {
                                    _actualSize = coercedSize;
                                }
                                _actualSize <<= 1;
                            }
                            break;
                        case SqlDbType.Char:
                        case SqlDbType.VarChar:
                        case SqlDbType.Text:
                            {
                                // for these types, ActualSize is the num of chars, not actual bytes - since non-unicode chars are not always uniform size
                                coercedSize = (!HasFlag(SqlParameterFlags.IsNull) && (!HasFlag(SqlParameterFlags.CoercedValueIsDataFeed))) ? (StringSize(val, HasFlag(SqlParameterFlags.CoercedValueIsSqlType))) : 0;
                                _actualSize = (ShouldSerializeSize() ? Size : 0);
                                _actualSize = (ShouldSerializeSize() && (_actualSize <= coercedSize)) ? _actualSize : coercedSize;
                                if (_actualSize == -1)
                                {
                                    _actualSize = coercedSize;
                                }
                            }
                            break;
                        case SqlDbType.Binary:
                        case SqlDbType.VarBinary:
                        case SqlDbType.Image:
                        case SqlDbType.Timestamp:
                            coercedSize = (!HasFlag(SqlParameterFlags.IsNull) && (!HasFlag(SqlParameterFlags.CoercedValueIsDataFeed))) ? (BinarySize(val, HasFlag(SqlParameterFlags.CoercedValueIsSqlType))) : 0;
                            _actualSize = (ShouldSerializeSize() ? Size : 0);
                            _actualSize = ((ShouldSerializeSize() && (_actualSize <= coercedSize)) ? _actualSize : coercedSize);
                            if (_actualSize == -1)
                            {
                                _actualSize = coercedSize;
                            }
                            break;
                        case SqlDbType.Udt:
                            if (!IsNull)
                            {
#if NETFRAMEWORK
                                //call the static function
                                coercedSize = AssemblyCache.GetLength(val);
#else
                                coercedSize = SerializationHelperSql9.SizeInBytes(val);
#endif
                            }
                            break;
                        case SqlDbType.Structured:
                            coercedSize = -1;
                            break;
                        case SqlDbType.Time:
                            _actualSize = isSqlVariant ? 5 : MetaType.GetTimeSizeFromScale(GetActualScale());
                            break;
                        case SqlDbType.DateTime2:
                            // Date in number of days (3 bytes) + time
                            _actualSize = 3 + (isSqlVariant ? 5 : MetaType.GetTimeSizeFromScale(GetActualScale()));
                            break;
                        case SqlDbType.DateTimeOffset:
                            // Date in days (3 bytes) + offset in minutes (2 bytes) + time
                            _actualSize = 5 + (isSqlVariant ? 5 : MetaType.GetTimeSizeFromScale(GetActualScale()));
                            break;
                        default:
                            Debug.Fail("Unknown variable length type!");
                            break;
                    }

                    // don't even send big values over to the variant
                    if (isSqlVariant && (coercedSize > TdsEnums.TYPE_SIZE_LIMIT))
                    {
                        throw SQL.ParameterInvalidVariant(ParameterName);
                    }
                }
            }

            return _actualSize;
        }

        internal byte GetActualPrecision()
        {
            return ShouldSerializePrecision() ? PrecisionInternal : ValuePrecision(CoercedValue);
        }

        internal object GetCoercedValue()
        {
            // NOTE: User can change the Udt at any time
            if ((_coercedValue == null) || (_internalMetaType.SqlDbType == SqlDbType.Udt))
            {  // will also be set during parameter Validation
                bool isDataFeed = Value is DataFeed;
                if (IsNull || isDataFeed)
                {
                    // No coercion is done for DataFeeds and Nulls
                    _coercedValue = Value;
                    SetFlag(SqlParameterFlags.CoercedValueIsSqlType, _coercedValue != null && HasFlag(SqlParameterFlags.IsSqlParameterSqlType)); // set to null for output parameters that keeps _isSqlParameterSqlType
                    SetFlag(SqlParameterFlags.CoercedValueIsDataFeed, isDataFeed);
                    _actualSize = IsNull ? 0 : -1;
                }
                else
                {
                    _coercedValue = CoerceValue(Value, _internalMetaType, out bool coercedValueIsDataFeed, out bool typeChanged);
                    SetFlag(SqlParameterFlags.CoercedValueIsDataFeed, coercedValueIsDataFeed);
                    SetFlag(SqlParameterFlags.CoercedValueIsSqlType, HasFlag(SqlParameterFlags.IsSqlParameterSqlType) && (!typeChanged));  // Type changed always results in a CLR type
                    _actualSize = -1;
                }
            }
            AssertCachedPropertiesAreValid();
            return _coercedValue;
        }

        internal int GetParameterSize()
        {
            return ShouldSerializeSize() ? Size : ValueSize(CoercedValue);
        }

        /// <summary>
        /// Get SMI Metadata to write out type_info stream.
        /// </summary>
        /// <returns></returns>
        internal SmiParameterMetaData GetMetadataForTypeInfo()
        {
            if (_internalMetaType == null)
            {
                _internalMetaType = GetMetaTypeOnly();
            }

            return MetaDataForSmi(out _);
        }

        // IMPORTANT DEVNOTE: This method is being used for parameter encryption functionality, to get the type_info TDS object from SqlParameter.
        // Please consider impact to that when changing this method. Refer to the callers of SqlParameter.GetMetadataForTypeInfo().
        internal SmiParameterMetaData MetaDataForSmi(out ParameterPeekAheadValue peekAhead)
        {
            peekAhead = null;
            MetaType mt = ValidateTypeLengths();
            long actualLen = GetActualSize();
            long maxLen = Size;

            // GetActualSize returns bytes length, but smi expects char length for 
            //  character types, so adjust
            if (!mt.IsLong)
            {
                if (mt.SqlDbType == SqlDbType.NChar || mt.SqlDbType == SqlDbType.NVarChar)
                {
                    actualLen /= sizeof(char);
                }

                if (actualLen > maxLen)
                {
                    maxLen = actualLen;
                }
            }

            // Determine maxLength for types that ValidateTypeLengths won't figure out
            if (maxLen == 0)
            {
                if (mt.SqlDbType == SqlDbType.Binary || mt.SqlDbType == SqlDbType.VarBinary)
                {
                    maxLen = SmiMetaData.MaxBinaryLength;
                }
                else if (mt.SqlDbType == SqlDbType.Char || mt.SqlDbType == SqlDbType.VarChar)
                {
                    maxLen = SmiMetaData.MaxANSICharacters;
                }
                else if (mt.SqlDbType == SqlDbType.NChar || mt.SqlDbType == SqlDbType.NVarChar)
                {
                    maxLen = SmiMetaData.MaxUnicodeCharacters;
                }
            }
            else if (
                (maxLen > SmiMetaData.MaxBinaryLength && (SqlDbType.Binary == mt.SqlDbType || SqlDbType.VarBinary == mt.SqlDbType)) ||
                (maxLen > SmiMetaData.MaxANSICharacters && (SqlDbType.Char == mt.SqlDbType || SqlDbType.VarChar == mt.SqlDbType)) ||
                (maxLen > SmiMetaData.MaxUnicodeCharacters && (SqlDbType.NChar == mt.SqlDbType || SqlDbType.NVarChar == mt.SqlDbType))
            )
            {
                maxLen = -1;
            }


            int localeId = LocaleId;
            if (localeId == 0 && mt.IsCharType)
            {
                if (GetCoercedValue() is SqlString sqlString && !sqlString.IsNull)
                {
                    localeId = sqlString.LCID;
                }
                else
                {
                    localeId = CultureInfo.CurrentCulture.LCID;
                }
            }

            SqlCompareOptions compareOpts = CompareInfo;
            if (compareOpts == 0 && mt.IsCharType)
            {
                if (GetCoercedValue() is SqlString sqlString && !sqlString.IsNull)
                {
                    compareOpts = sqlString.SqlCompareOptions;
                }
                else
                {
                    compareOpts = SmiMetaData.GetDefaultForType(mt.SqlDbType).CompareOptions;
                }
            }

            string typeSpecificNamePart1 = null;
            string typeSpecificNamePart2 = null;
            string typeSpecificNamePart3 = null;

            if (SqlDbType.Xml == mt.SqlDbType)
            {
                typeSpecificNamePart1 = XmlSchemaCollectionDatabase;
                typeSpecificNamePart2 = XmlSchemaCollectionOwningSchema;
                typeSpecificNamePart3 = XmlSchemaCollectionName;
            }
            else if (SqlDbType.Udt == mt.SqlDbType || (SqlDbType.Structured == mt.SqlDbType && !string.IsNullOrEmpty(TypeName)))
            {
                // Split the input name. The type name is specified as single 3 part name.
                // NOTE: ParseTypeName throws if format is incorrect
                string[] names;
                if (mt.SqlDbType == SqlDbType.Udt)
                {
                    names = ParseTypeName(UdtTypeName, true /* is UdtTypeName */);
                }
                else
                {
                    names = ParseTypeName(TypeName, false /* not UdtTypeName */);
                }

                if (names.Length == 1)
                {
                    typeSpecificNamePart3 = names[0];
                }
                else if (names.Length == 2)
                {
                    typeSpecificNamePart2 = names[0];
                    typeSpecificNamePart3 = names[1];
                }
                else if (names.Length == 3)
                {
                    typeSpecificNamePart1 = names[0];
                    typeSpecificNamePart2 = names[1];
                    typeSpecificNamePart3 = names[2];
                }
                else
                {
                    throw ADP.ArgumentOutOfRange(nameof(names));
                }

                if (
                    (!string.IsNullOrEmpty(typeSpecificNamePart1) && TdsEnums.MAX_SERVERNAME < typeSpecificNamePart1.Length) ||
                    (!string.IsNullOrEmpty(typeSpecificNamePart2) && TdsEnums.MAX_SERVERNAME < typeSpecificNamePart2.Length) ||
                    (!string.IsNullOrEmpty(typeSpecificNamePart3) && TdsEnums.MAX_SERVERNAME < typeSpecificNamePart3.Length)
                )
                {
                    throw ADP.ArgumentOutOfRange(nameof(names));
                }
            }

            byte precision = GetActualPrecision();
            byte scale = GetActualScale();

            // precision for decimal types may still need adjustment.
            if (mt.SqlDbType == SqlDbType.Decimal)
            {
                if (precision == 0)
                {
                    precision = TdsEnums.DEFAULT_NUMERIC_PRECISION;
                }
            }

            // Sub-field determination
            List<SmiExtendedMetaData> fields = null;
            SmiMetaDataPropertyCollection extendedProperties = null;
            if (mt.SqlDbType == SqlDbType.Structured)
            {
                GetActualFieldsAndProperties(out fields, out extendedProperties, out peekAhead);
            }

            return new SmiParameterMetaData(
                mt.SqlDbType,
                maxLen,
                precision,
                scale,
                localeId,
                compareOpts,
                null,           // Udt type not used for parameters
                SqlDbType.Structured == mt.SqlDbType,
                fields,
                extendedProperties,
                GetPrefixedParameterName(),
                typeSpecificNamePart1,
                typeSpecificNamePart2,
                typeSpecificNamePart3,
                Direction
            );
        }

        [Conditional("DEBUG")]
        internal void AssertCachedPropertiesAreValid()
        {
            AssertPropertiesAreValid(_coercedValue, HasFlag(SqlParameterFlags.CoercedValueIsSqlType), HasFlag(SqlParameterFlags.CoercedValueIsDataFeed), IsNull);
        }

        [Conditional("DEBUG")]
        internal void AssertPropertiesAreValid(object value, bool? isSqlType = null, bool? isDataFeed = null, bool? isNull = null)
        {
            Debug.Assert(!isSqlType.HasValue || (isSqlType.Value == (value is INullable)), "isSqlType is incorrect");
            Debug.Assert(!isDataFeed.HasValue || (isDataFeed.Value == (value is DataFeed)), "isDataFeed is incorrect");
            Debug.Assert(!isNull.HasValue || (isNull.Value == ADP.IsNull(value)), "isNull is incorrect");
        }

        private SqlDbType GetMetaSqlDbTypeOnly()
        {
            MetaType metaType = _metaType;
            if (null == metaType)
            { // infer the type from the value
                metaType = MetaType.GetDefaultMetaType();
            }
            return metaType.SqlDbType;
        }

        // This may not be a good thing to do in case someone overloads the parameter type but I
        // don't want to go from SqlDbType -> metaType -> TDSType
        private MetaType GetMetaTypeOnly()
        {
            if (_metaType != null)
            {
                return _metaType;
            }
            if (null != _value && DBNull.Value != _value)
            {
                // We have a value set by the user then just use that value
                // char and char[] are not directly supported so we convert those values to string
                Type valueType = _value.GetType();
                if (valueType == typeof(char))
                {
                    _value = _value.ToString();
                    valueType = typeof(string);
                }
                else if (valueType == typeof(char[]))
                {
                    _value = new string((char[])_value);
                    valueType = typeof(string);
                }
                return MetaType.GetMetaTypeFromType(valueType);
            }
            else if (_sqlBufferReturnValue != null)
            {  // value came back from the server
                Type valueType = _sqlBufferReturnValue.GetTypeFromStorageType(HasFlag(SqlParameterFlags.IsSqlParameterSqlType));
                if (valueType != null)
                {
                    return MetaType.GetMetaTypeFromType(valueType);
                }
            }
            return MetaType.GetDefaultMetaType();
        }

        internal void Prepare(SqlCommand cmd)
        {
            if (_metaType == null)
            {
                throw ADP.PrepareParameterType(cmd);
            }
            else if (!ShouldSerializeSize() && !_metaType.IsFixed)
            {
                throw ADP.PrepareParameterSize(cmd);
            }
            else if ((!ShouldSerializePrecision() && !ShouldSerializeScale()) && (_metaType.SqlDbType == SqlDbType.Decimal))
            {
                throw ADP.PrepareParameterScale(cmd, SqlDbType.ToString());
            }
        }

        private void PropertyChanging() => _internalMetaType = null;

        private void PropertyTypeChanging()
        {
            PropertyChanging();
            CoercedValue = null;
        }

        internal void ResetParent() => _parent = null;

        private void SetFlag(SqlParameterFlags flag, bool value)
        {
            _flags = value ? _flags | flag : _flags & ~flag;
        }

        internal void SetSqlBuffer(SqlBuffer buff)
        {
            _sqlBufferReturnValue = buff;
            _value = null;
            _coercedValue = null;
            SetFlag(SqlParameterFlags.IsNull, _sqlBufferReturnValue.IsNull);
            SetFlag(SqlParameterFlags.CoercedValueIsDataFeed, false);
            SetFlag(SqlParameterFlags.CoercedValueIsSqlType, false);
            _udtLoadError = null;
            _actualSize = -1;
        }

        internal void SetUdtLoadError(Exception e) => _udtLoadError = e;

        internal void Validate(int index, bool isCommandProc)
        {
            MetaType metaType = GetMetaTypeOnly();
            _internalMetaType = metaType;

            // SqlParameter does a Size Validation check and would fail if the size is 0.
            // This condition filters all scenarios where we view a valid size 0.
            if (
                ADP.IsDirection(this, ParameterDirection.Output) &&
                !ADP.IsDirection(this, ParameterDirection.ReturnValue) &&
                (!metaType.IsFixed) &&
                !ShouldSerializeSize() &&
                ((null == _value) || Convert.IsDBNull(_value)) &&
                (SqlDbType != SqlDbType.Timestamp) &&
                (SqlDbType != SqlDbType.Udt) &&
                // BUG: (VSTFDevDiv - 479609): Output parameter with size 0 throws for XML, TEXT, NTEXT, IMAGE.
                // NOTE: (VSTFDevDiv - 479609): Not Fixed for TEXT, NTEXT, IMAGE as these are deprecated LOB types.
                (SqlDbType != SqlDbType.Xml) &&
                !metaType.IsVarTime
            )
            {
                throw ADP.UninitializedParameterSize(index, metaType.ClassType);
            }

            if (metaType.SqlDbType != SqlDbType.Udt && Direction != ParameterDirection.Output)
            {
                GetCoercedValue();
            }

            //check if the UdtTypeName is specified for Udt params
            if (metaType.SqlDbType == SqlDbType.Udt)
            {
                if (string.IsNullOrEmpty(UdtTypeName))
                {
                    throw SQL.MustSetUdtTypeNameForUdtParams();
                }
            }
            else if (!string.IsNullOrEmpty(UdtTypeName))
            {
                throw SQL.UnexpectedUdtTypeNameForNonUdtParams();
            }

            // Validate structured-type-specific details.
            if (metaType.SqlDbType == SqlDbType.Structured)
            {
                if (!isCommandProc && string.IsNullOrEmpty(TypeName))
                {
                    throw SQL.MustSetTypeNameForParam(metaType.TypeName, ParameterName);
                }

                if (Direction != ParameterDirection.Input)
                {
                    throw SQL.UnsupportedTVPOutputParameter(Direction, ParameterName);
                }

                if (GetCoercedValue() == DBNull.Value)
                {
                    throw SQL.DBNullNotSupportedForTVPValues(ParameterName);
                }
            }
            else if (!string.IsNullOrEmpty(TypeName))
            {
                throw SQL.UnexpectedTypeNameForNonStructParams(ParameterName);
            }
        }

        // func will change type to that with a 4 byte length if the type has a two
        // byte length and a parameter length > than that expressible in 2 bytes
        internal MetaType ValidateTypeLengths()
        {
            MetaType mt = InternalMetaType;
            // Since the server will automatically reject any
            // char, varchar, binary, varbinary, nchar, or nvarchar parameter that has a
            // byte sizeInCharacters > 8000 bytes, we promote the parameter to image, text, or ntext.  This
            // allows the user to specify a parameter type using a COM+ datatype and be able to
            // use that parameter against a BLOB column.
            if ((mt.SqlDbType != SqlDbType.Udt) && !mt.IsFixed && !mt.IsLong)
            { // if type has 2 byte length
                long actualSizeInBytes = GetActualSize();
                long sizeInCharacters = Size;

                // Bug: VSTFDevDiv #636867
                // Notes:
                // 'actualSizeInBytes' is the size of value passed; 
                // 'sizeInCharacters' is the parameter size;
                // 'actualSizeInBytes' is in bytes; 
                // 'this.Size' is in charaters; 
                // 'sizeInCharacters' is in characters; 
                // 'TdsEnums.TYPE_SIZE_LIMIT' is in bytes;
                // For Non-NCharType and for non-2005 or greater variables, size should be maintained;
                // Reverting changes from bug VSTFDevDiv # 479739 as it caused an regression;
                // Modifed variable names from 'size' to 'sizeInCharacters', 'actualSize' to 'actualSizeInBytes', and 
                // 'maxSize' to 'maxSizeInBytes'
                // The idea is to
                //  1) revert the regression from bug 479739
                //  2) fix as many scenarios as possible including bug 636867
                //  3) cause no additional regression from 3.5 sp1
                // Keeping these goals in mind - the following are the changes we are making

                long maxSizeInBytes;
                if (mt.IsNCharType)
                {
                    maxSizeInBytes = ((sizeInCharacters * sizeof(char)) > actualSizeInBytes) ? sizeInCharacters * sizeof(char) : actualSizeInBytes;
                }
                else
                {
                    // Notes:
                    // Elevation from (n)(var)char (4001+) to (n)text succeeds without failure only with 2005 and greater.
                    // it fails in sql server 2000
                    maxSizeInBytes = (sizeInCharacters > actualSizeInBytes) ? sizeInCharacters : actualSizeInBytes;
                }

                if (
                    (maxSizeInBytes > TdsEnums.TYPE_SIZE_LIMIT) ||
                    HasFlag(SqlParameterFlags.CoercedValueIsDataFeed) ||
                    (sizeInCharacters == -1) ||
                    (actualSizeInBytes == -1)
                   )
                {
                    mt = MetaType.GetMaxMetaTypeFromMetaType(mt);
                    _metaType = mt;
                    InternalMetaType = mt;
                    if (!mt.IsPlp)
                    {
                        if (mt.SqlDbType == SqlDbType.Xml)
                        {
                            throw ADP.InvalidMetaDataValue();     //Xml should always have IsPartialLength = true
                        }
                        if (
                            mt.SqlDbType == SqlDbType.NVarChar ||
                            mt.SqlDbType == SqlDbType.VarChar ||
                            mt.SqlDbType == SqlDbType.VarBinary
                        )
                        {
                            Size = (int)SmiMetaData.UnlimitedMaxLengthIndicator;
                        }
                    }
                }
            }
            return mt;
        }

        private byte ValuePrecision(object value)
        {
            if (value is SqlDecimal sqlDecimal)
            {
                if (sqlDecimal.IsNull)
                {
                    return 0;
                }
                return sqlDecimal.Precision;
            }
            return ValuePrecisionCore(value);
        }

        private byte ValueScale(object value)
        {
            if (value is SqlDecimal sqlDecimal)
            {
                if (sqlDecimal.IsNull)
                {
                    return 0;
                }
                return sqlDecimal.Scale;
            }
            return ValueScaleCore(value);
        }

        private int ValueSize(object value)
        {
            if (value is SqlString sqlString)
            {
                if (sqlString.IsNull)
                {
                    return 0;
                }
                return sqlString.Value.Length;
            }
            if (value is SqlChars sqlChars)
            {
                if (sqlChars.IsNull)
                {
                    return 0;
                }
                return sqlChars.Value.Length;
            }

            if (value is SqlBinary sqlBinary)
            {
                if (sqlBinary.IsNull)
                {
                    return 0;
                }
                return sqlBinary.Length;
            }
            if (value is SqlBytes sqlBytes)
            {
                if (sqlBytes.IsNull)
                {
                    return 0;
                }
                return (int)(sqlBytes.Length);
            }
            if (value is DataFeed)
            {
                // Unknown length
                return 0;
            }
            return ValueSizeCore(value);
        }

        private byte ValuePrecisionCore(object value)
        {
            if (value is decimal decimalValue)
            {
                return ((SqlDecimal)decimalValue).Precision;
            }
            return 0;
        }

        private byte ValueScaleCore(object value)
        {
            if (value is decimal decimalValue)
            {
                return (byte)((decimal.GetBits(decimalValue)[3] & 0x00ff0000) >> 0x10);
            }
            return 0;
        }

        private int ValueSizeCore(object value)
        {
            if (!ADP.IsNull(value))
            {
                switch (value)
                {
                    case string svalue:
                        return svalue.Length;
                    case byte[] bvalue:
                        return bvalue.Length;
                    case char[] cvalue:
                        return cvalue.Length;
                    case byte _:
                    case char _:
                        return 1;
                }
            }
            return 0;
        }


        // Coerced Value is also used in SqlBulkCopy.ConvertValue(object value, _SqlMetaData metadata)
        internal static object CoerceValue(object value, MetaType destinationType, out bool coercedToDataFeed, out bool typeChanged, bool allowStreaming = true)
        {
            Debug.Assert(!(value is DataFeed), "Value provided should not already be a data feed");
            Debug.Assert(!ADP.IsNull(value), "Value provided should not be null");
            Debug.Assert(null != destinationType, "null destinationType");

            coercedToDataFeed = false;
            typeChanged = false;
            Type currentType = value.GetType();

            if (
                (destinationType.ClassType != typeof(object)) &&
                (destinationType.ClassType != currentType) &&
                (
                    (destinationType.SqlType != currentType) ||
                    (destinationType.SqlDbType == SqlDbType.Xml)
                )
            )
            {   // Special case for Xml types (since we need to convert SqlXml into a string)
                try
                {
                    // Assume that the type changed
                    typeChanged = true;
                    if (destinationType.ClassType == typeof(string))
                    {
                        // For Xml data, destination Type is always string
                        if (currentType == typeof(SqlXml))
                        {
                            value = MetaType.GetStringFromXml((XmlReader)(((SqlXml)value).CreateReader()));
                        }
                        else if (currentType == typeof(SqlString))
                        {
                            typeChanged = false;   // Do nothing
                        }
                        else if (typeof(XmlReader).IsAssignableFrom(currentType))
                        {
                            if (allowStreaming)
                            {
                                coercedToDataFeed = true;
                                value = new XmlDataFeed((XmlReader)value);
                            }
                            else
                            {
                                value = MetaType.GetStringFromXml((XmlReader)value);
                            }
                        }
                        else if (currentType == typeof(char[]))
                        {
                            value = new string((char[])value);
                        }
                        else if (currentType == typeof(SqlChars))
                        {
                            value = new string(((SqlChars)value).Value);
                        }
                        else if (value is TextReader textReader && allowStreaming)
                        {
                            coercedToDataFeed = true;
                            value = new TextDataFeed(textReader);
                        }
                        else
                        {
                            value = Convert.ChangeType(value, destinationType.ClassType, null);
                        }
                    }
                    else if ((destinationType.DbType == DbType.Currency) && (currentType == typeof(string)))
                    {
                        value = decimal.Parse((string)value, NumberStyles.Currency, null);
                    }
                    else if ((currentType == typeof(SqlBytes)) && (destinationType.ClassType == typeof(byte[])))
                    {
                        typeChanged = false;    // Do nothing
                    }
                    else if ((currentType == typeof(string)) && (destinationType.SqlDbType == SqlDbType.Time))
                    {
                        value = TimeSpan.Parse((string)value);
                    }
                    else if ((currentType == typeof(string)) && (destinationType.SqlDbType == SqlDbType.DateTimeOffset))
                    {
                        value = DateTimeOffset.Parse((string)value, (IFormatProvider)null);
                    }
                    else if ((currentType == typeof(DateTime)) && (destinationType.SqlDbType == SqlDbType.DateTimeOffset))
                    {
                        value = new DateTimeOffset((DateTime)value);
                    }
#if NET6_0_OR_GREATER
                    else if ((currentType == typeof(DateOnly)) && (destinationType.SqlDbType == SqlDbType.Date))
                    {
                        value = ((DateOnly)value).ToDateTime(new TimeOnly(0, 0));
                    }
                    else if ((currentType == typeof(TimeOnly)) && (destinationType.SqlDbType == SqlDbType.Time))
                    {
                        value = ((TimeOnly)value).ToTimeSpan();
                    }
#endif
                    else if (
                        TdsEnums.SQLTABLE == destinationType.TDSType &&
                        (
                            value is DataTable ||
                            value is DbDataReader ||
                            value is IEnumerable<SqlDataRecord>
                        )
                    )
                    {
                        // no conversion for TVPs.
                        typeChanged = false;
                    }
                    else if (destinationType.ClassType == typeof(byte[]) && allowStreaming && value is Stream stream)
                    {
                        coercedToDataFeed = true;
                        value = new StreamDataFeed(stream);
                    }
                    else
                    {
                        value = Convert.ChangeType(value, destinationType.ClassType, null);
                    }
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableExceptionType(e))
                    {
                        throw;
                    }

                    throw ADP.ParameterConversionFailed(value, destinationType.ClassType, e);
                }
            }

            Debug.Assert(allowStreaming || !coercedToDataFeed, "Streaming is not allowed, but type was coerced into a data feed");
            Debug.Assert(value.GetType() == currentType ^ typeChanged, "Incorrect value for typeChanged");
            return value;
        }

        private static int StringSize(object value, bool isSqlType)
        {
            if (isSqlType)
            {
                Debug.Assert(!((INullable)value).IsNull, "Should not call StringSize on null values");
                if (value is SqlString sqlString)
                {
                    return sqlString.Value.Length;
                }
                if (value is SqlChars sqlChars)
                {
                    return sqlChars.Value.Length;
                }
            }
            else
            {
                if (value is string svalue)
                {
                    return svalue.Length;
                }
                if (value is char[] cvalue)
                {
                    return cvalue.Length;
                }
                if (value is char)
                {
                    return 1;
                }
            }

            // Didn't match, unknown size
            return 0;
        }

        private static int BinarySize(object value, bool isSqlType)
        {
            if (isSqlType)
            {
                Debug.Assert(!((INullable)value).IsNull, "Should not call StringSize on null values");
                if (value is SqlBinary sqlBinary)
                {
                    return sqlBinary.Length;
                }
                if (value is SqlBytes sqlBytes)
                {
                    return sqlBytes.Value.Length;
                }
            }
            else
            {
                if (value is byte[] bvalue)
                {
                    return bvalue.Length;
                }
                if (value is byte)
                {
                    return 1;
                }
            }

            // Didn't match, unknown size
            return 0;
        }

        // parse an string of the form db.schema.name where any of the three components
        // might have "[" "]" and dots within it.
        // returns:
        //   [0] dbname (or null)
        //   [1] schema (or null)
        //   [2] name
        // NOTE: if perf/space implications of Regex is not a problem, we can get rid
        // of this and use a simple regex to do the parsing
        internal static string[] ParseTypeName(string typeName, bool isUdtTypeName)
        {
            Debug.Assert(null != typeName, "null typename passed to ParseTypeName");

            try
            {
                string errorMsg = isUdtTypeName ? Strings.SQL_UDTTypeName : Strings.SQL_TypeName;
                return MultipartIdentifier.ParseMultipartIdentifier(typeName, "[\"", "]\"", '.', 3, true, errorMsg, true);
            }
            catch (ArgumentException)
            {
                if (isUdtTypeName)
                {
                    throw SQL.InvalidUdt3PartNameFormat();
                }
                else
                {
                    throw SQL.InvalidParameterTypeNameFormat();
                }
            }
        }
    }
}
