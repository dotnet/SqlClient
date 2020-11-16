using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlSerializerFactory : SerializerFactory
    {
        private readonly Dictionary<Type, ISerializer> serializerByType = new Dictionary<Type, ISerializer>();

        private readonly Dictionary<string, ISerializer> serializerByIdentifier = new Dictionary<string, ISerializer>();

        private static readonly Dictionary<Type, Func<int, byte, byte, int, ISerializer>> createSerializerByType = new Dictionary<Type, Func<int, byte, byte, int, ISerializer>>()
        {
            [typeof(SqlBitSerializer)] = (size, precision, scale, codepage) => new SqlBitSerializer(),
            [typeof(SqlNullableBitSerializer)] = (size, precision, scale, codepage) => new SqlNullableBitSerializer(),
            [typeof(SqlTinyintSerializer)] = (size, precision, scale, codepage) => new SqlTinyintSerializer(),
            [typeof(SqlNullableTinyintSerializer)] = (size, precision, scale, codepage) => new SqlNullableTinyintSerializer(),
            [typeof(SqlVarbinarySerializer)] = (size, precision, scale, codepage) => new SqlVarbinarySerializer(size),
            [typeof(SqlDatetime2Serializer)] = (size, precision, scale, codepage) => new SqlDatetime2Serializer(precision),
            [typeof(SqlNullableDatetime2Serializer)] = (size, precision, scale, codepage) => new SqlNullableDatetime2Serializer(precision),
            [typeof(SqlDatetimeoffsetSerializer)] = (size, precision, scale, codepage) => new SqlDatetimeoffsetSerializer(scale),
            [typeof(SqlNullableDatetimeoffsetSerializer)] = (size, precision, scale, codepage) => new SqlNullableDatetimeoffsetSerializer(scale),
            [typeof(SqlDecimalSerializer)] = (size, precision, scale, codepage) => new SqlDecimalSerializer(precision, scale),
            [typeof(SqlNullableDecimalSerializer)] = (size, precision, scale, codepage) => new SqlNullableDecimalSerializer(precision, scale),
            [typeof(SqlFloatSerializer)] = (size, precision, scale, codepage) => new SqlFloatSerializer(),
            [typeof(SqlNullableFloatSerializer)] = (size, precision, scale, codepage) => new SqlNullableFloatSerializer(),
            [typeof(SqlRealSerializer)] = (size, precision, scale, codepage) => new SqlRealSerializer(),
            [typeof(SqlNullableRealSerializer)] = (size, precision, scale, codepage) => new SqlNullableRealSerializer(),
            [typeof(SqlUniqueidentifierSerializer)] = (size, precision, scale, codepage) => new SqlUniqueidentifierSerializer(),
            [typeof(SqlNullableUniqueidentifierSerializer)] = (size, precision, scale, codepage) => new SqlNullableUniqueidentifierSerializer(),
            [typeof(SqlIntSerializer)] = (size, precision, scale, codepage) => new SqlIntSerializer(),
            [typeof(SqlNullableIntSerializer)] = (size, precision, scale, codepage) => new SqlNullableIntSerializer(),
            [typeof(SqlBigintSerializer)] = (size, precision, scale, codepage) => new SqlBigintSerializer(),
            [typeof(SqlNullableBigintSerializer)] = (size, precision, scale, codepage) => new SqlNullableBigintSerializer(),
            [typeof(SqlSmallintSerializer)] = (size, precision, scale, codepage) => new SqlSmallintSerializer(),
            [typeof(SqlNullableSmallintSerializer)] = (size, precision, scale, codepage) => new SqlNullableSmallintSerializer(),
            [typeof(SqlNvarcharSerializer)] = (size, precision, scale, codepage) => new SqlNvarcharSerializer(size),
            [typeof(SqlTimeSerializer)] = (size, precision, scale, codepage) => new SqlTimeSerializer(scale),
            [typeof(SqlNullableTimeSerializer)] = (size, precision, scale, codepage) => new SqlNullableTimeSerializer(scale),
            [typeof(SqlBinarySerializer)] = (size, precision, scale, codepage) => new SqlBinarySerializer(size),
            [typeof(SqlDateSerializer)] = (size, precision, scale, codepage) => new SqlDateSerializer(),
            [typeof(SqlNullableDateSerializer)] = (size, precision, scale, codepage) => new SqlNullableDateSerializer(),
            [typeof(SqlDatetimeSerializer)] = (size, precision, scale, codepage) => new SqlDatetimeSerializer(),
            [typeof(SqlNullableDatetimeSerializer)] = (size, precision, scale, codepage) => new SqlNullableDatetimeSerializer(),
            [typeof(SqlSmalldatetimeSerializer)] = (size, precision, scale, codepage) => new SqlSmalldatetimeSerializer(),
            [typeof(SqlNullableSmalldatetimeSerializer)] = (size, precision, scale, codepage) => new SqlNullableSmalldatetimeSerializer(),
            [typeof(SqlMoneySerializer)] = (size, precision, scale, codepage) => new SqlMoneySerializer(),
            [typeof(SqlNullableMoneySerializer)] = (size, precision, scale, codepage) => new SqlNullableMoneySerializer(),
            [typeof(SqlNumericSerializer)] = (size, precision, scale, codepage) => new SqlNumericSerializer(precision, scale),
            [typeof(SqlNullableNumericSerializer)] = (size, precision, scale, codepage) => new SqlNullableNumericSerializer(precision, scale),
            [typeof(SqlSmallmoneySerializer)] = (size, precision, scale, codepage) => new SqlSmallmoneySerializer(),
            [typeof(SqlNullableSmallmoneySerializer)] = (size, precision, scale, codepage) => new SqlNullableSmallmoneySerializer(),
            [typeof(SqlNcharSerializer)] = (size, precision, scale, codepage) => new SqlNcharSerializer(size),
            [typeof(SqlCharSerializer)] = (size, precision, scale, codepage) => new SqlCharSerializer(size, codepage),
            [typeof(SqlVarcharSerializer)] = (size, precision, scale, codepage) => new SqlVarcharSerializer(size, codepage),
        };

        private static readonly LocalCache<Tuple<Type, int, byte, byte, int>, ISerializer> serializerCache
            = new LocalCache<Tuple<Type, int, byte, byte, int>, ISerializer>();

        /// <summary>
        /// Returns a cached instance of the <see cref="ISerializer"/> or, if not present, creates a new one.
        /// </summary>
        /// <param name="serializerType">The type of serializer to get or create.</param>
        /// <param name="size">The maximum size of the data.</param>
        /// <param name="precision">The maximum number of digits.</param>
        /// <param name="scale">The number of decimal places.</param>
        /// <param name="codepage">The code page character encoding.</param>
        /// <returns>An  <see cref="ISerializer"/> object.</returns>
        public static ISerializer GetOrCreate(Type serializerType, int size = 1, byte precision = 1, byte scale = 1, int codepage = 1)
        {
            serializerType.ValidateNotNull(nameof(serializerType));

            return serializerCache.GetOrCreate(
                key: Tuple.Create(serializerType, size, precision, scale, codepage),
                createItem: () => createSerializerByType[serializerType].Invoke(size, precision, scale, codepage)
            );
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlSerializerFactory"/> class.
        /// </summary>
        public SqlSerializerFactory()
        {
            Initialize();
        }

        /// <summary>
        /// Returns a default instance of the <see cref="SqlSerializerFactory"/> class.
        /// </summary>
        public static SqlSerializerFactory Default { get; } = new SqlSerializerFactory();

        /// <inheritdoc/>
        public override ISerializer GetSerializer(string identifier)
        {
            identifier.ValidateNotNull(nameof(identifier));

            if (serializerByIdentifier.ContainsKey(identifier))
            {
                return serializerByIdentifier[identifier];
            }

            return null;
        }

        /// <inheritdoc/>
        public override Serializer<T> GetDefaultSerializer<T>()
        {
            if (serializerByType.ContainsKey(typeof(T)))
            {
                return (Serializer<T>)serializerByType[typeof(T)];
            }

            throw new NotImplementedException($"A default Always Encrypted compatible serializer cannot be found for type {typeof(T).Name}. A serializer can be registered for this type with the {nameof(RegisterSerializer)} method.");
        }

        /// <inheritdoc/>
        public override void RegisterSerializer(Type type, ISerializer serializer, bool overrideDefault = false)
        {
            type.ValidateNotNull(nameof(type));
            serializer.ValidateNotNull(nameof(serializer));

            serializerByIdentifier[serializer.Identifier] = serializer;

            if (overrideDefault || !HasDefaultSqlSerializer(type))
            {
                serializerByType[type] = serializer;
            }
        }

        private bool HasDefaultSerializer(Type type)
        {
            return serializerByType.ContainsKey(type);
        }

        private bool HasDefaultSqlSerializer(Type type)
        {
            return serializerByType.ContainsKey(type);
        }

        private void Initialize()
        {
            RegisterDefaultSqlSerializers();
            RegisterNonDefaultSqlSerializers();
        }

        private void RegisterDefaultSqlSerializers()
        {
            RegisterSerializer(typeof(bool), new SqlBitSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(bool?), new SqlNullableBitSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(byte), new SqlTinyintSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(byte?), new SqlNullableTinyintSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(byte[]), new SqlVarbinarySerializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTime), new SqlDatetime2Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTime?), new SqlNullableDatetime2Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTimeOffset), new SqlDatetimeoffsetSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTimeOffset?), new SqlNullableDatetimeoffsetSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(decimal), new SqlDecimalSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(decimal?), new SqlNullableDecimalSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(double), new SqlFloatSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(double?), new SqlNullableFloatSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(float), new SqlRealSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(float?), new SqlNullableRealSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(Guid), new SqlUniqueidentifierSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(Guid?), new SqlNullableUniqueidentifierSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(int), new SqlIntSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(int?), new SqlNullableIntSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(long), new SqlBigintSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(long?), new SqlNullableBigintSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(short), new SqlSmallintSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(short?), new SqlNullableSmallintSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(string), new SqlNvarcharSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(TimeSpan), new SqlTimeSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(TimeSpan?), new SqlNullableTimeSerializer(), overrideDefault: true);
        }

        private void RegisterNonDefaultSqlSerializers()
        {
            RegisterSerializer(typeof(byte[]), new SqlBinarySerializer(), overrideDefault: false);
            RegisterSerializer(typeof(DateTime), new SqlDateSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(DateTime?), new SqlNullableDateSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(DateTime), new SqlDatetimeSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(DateTime?), new SqlNullableDatetimeSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(DateTime), new SqlSmalldatetimeSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(DateTime), new SqlNullableSmalldatetimeSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(decimal), new SqlMoneySerializer(), overrideDefault: false);
            RegisterSerializer(typeof(decimal?), new SqlNullableMoneySerializer(), overrideDefault: false);
            RegisterSerializer(typeof(decimal), new SqlNumericSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(decimal?), new SqlNullableNumericSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(decimal), new SqlSmallmoneySerializer(), overrideDefault: false);
            RegisterSerializer(typeof(decimal?), new SqlNullableSmallmoneySerializer(), overrideDefault: false);
            RegisterSerializer(typeof(string), new SqlNcharSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(string), new SqlCharSerializer(), overrideDefault: false);
            RegisterSerializer(typeof(string), new SqlVarcharSerializer(), overrideDefault: false);
        }
    }
}
