using System;
using System.Collections.Generic;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class StandardSerializerFactory : SerializerFactory
    {
        private readonly Dictionary<Type, ISerializer> serializerByType = new Dictionary<Type, ISerializer>();

        private readonly Dictionary<string, ISerializer> serializerByIdentifier = new Dictionary<string, ISerializer>();

        /// <summary>
        /// Initializes a new instance of the <see cref="StandardSerializerFactory"/> class.
        /// </summary>
        public StandardSerializerFactory()
        {
            RegisterDefaultStandardSerializers();
        }

        /// <summary>
        /// Returns a default instance of the <see cref="StandardSerializerFactory"/> class.
        /// </summary>
        public static StandardSerializerFactory Default { get; } = new StandardSerializerFactory();

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

            throw new NotImplementedException($"A default serializer cannot be found for type {typeof(T).Name}. A serializer can be registered for this type with the {nameof(RegisterSerializer)} method.");
        }

        /// <inheritdoc/>
        public override void RegisterSerializer(Type type, ISerializer serializer, bool overrideDefault = false)
        {
            type.ValidateNotNull(nameof(type));
            serializer.ValidateNotNull(nameof(serializer));

            // TODO: Test
            serializerByIdentifier[serializer.Identifier] = serializer;

            if (overrideDefault || !HasDefaultSerializer(type))
            {
                serializerByType[type] = serializer;
            }

        }

        private bool HasDefaultSerializer(Type type)
        {
            return serializerByType.ContainsKey(type);
        }

        private void RegisterDefaultStandardSerializers()
        {
            RegisterSerializer(typeof(bool), new BooleanSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(bool?), new NullableBooleanSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(byte), new ByteSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(byte?), new NullableByteSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(byte[]), new ByteArraySerializer(), overrideDefault: true);
            RegisterSerializer(typeof(char), new CharSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(char?), new NullableCharSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTime), new DateTimeSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTime?), new NullableDateTimeSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(DateTimeOffset?), new NullableDateTimeOffsetSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(decimal), new DecimalSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(decimal?), new NullableDecimalSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(double), new DoubleSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(double?), new NullableDoubleSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(float), new SingleSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(float?), new NullableSingleSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(Guid), new GuidSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(Guid?), new NullableGuidSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(int), new Int32Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(int?), new NullableInt32Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(long), new Int64Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(long?), new NullableInt64Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(sbyte), new SByteSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(sbyte?), new NullableSByteSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(short), new Int16Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(short?), new NullableInt16Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(string), new StringSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(TimeSpan), new TimeSpanSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(TimeSpan?), new NullableTimeSpanSerializer(), overrideDefault: true);
            RegisterSerializer(typeof(uint), new UInt32Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(uint?), new NullableUInt32Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(ulong), new UInt64Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(ulong?), new NullableUInt64Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(ushort), new UInt16Serializer(), overrideDefault: true);
            RegisterSerializer(typeof(ushort?), new NullableUInt16Serializer(), overrideDefault: true);
        }
    }
}
