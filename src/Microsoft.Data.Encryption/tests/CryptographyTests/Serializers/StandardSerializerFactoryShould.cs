using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers
{
    public class StandardSerializerFactoryShould
    {
        [Fact]
        public void ThrowWhenGettingSerializerWithNulldentifier()
        {
            Assert.Throws<ArgumentNullException>(() => StandardSerializerFactory.Default.GetSerializer(null));
        }

        [Fact]
        public void ShouldReturnNullWhenGettingUnregisteredSerializer()
        {
            Assert.Null(StandardSerializerFactory.Default.GetSerializer(""));
        }

        [Theory]
        [GetSerializerByIdentifierTestData]
        public void ReturnRegisteredSerializerByIdentifier(string identifier, Type expectedType)
        {
            StandardSerializerFactory factory = new StandardSerializerFactory();
            ISerializer serialiser = factory.GetSerializer(identifier);
            Assert.Equal(expectedType, serialiser.GetType());
        }

        [Fact]
        public void ThrowWhenGettingUnregisteredDefault()
        {
            Assert.Throws<NotImplementedException>(() => StandardSerializerFactory.Default.GetDefaultSerializer<object>());
        }

        [Theory]
        [GetDefaultSerializerByTypeTestData]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Needed for generic test type inference")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Needed for generic test type inference")]
        public void ReturnDefaultSerializerByType<T>(Serializer<T> serializer, Type type)
        {
            StandardSerializerFactory factory = new StandardSerializerFactory();
            Serializer<T> actualSerializer = factory.GetDefaultSerializer<T>();
            Assert.Equal(actualSerializer.GetType(), type);
        }

        [Fact]
        public void ThrowWhenRegisterSerializerWithNullType()
        {
            Assert.Throws<ArgumentNullException>(() => StandardSerializerFactory.Default.RegisterSerializer(null, new TestSerializer()));
        }

        [Fact]
        public void ThrowWhenRegisterSerializerWithNullSerializer()
        {
            Assert.Throws<ArgumentNullException>(() => StandardSerializerFactory.Default.RegisterSerializer(typeof(bool), null));
        }

        [Fact]
        public void RegisterSerializerCorrectlyWithOveridingDefault()
        {
            StandardSerializerFactory factory = new StandardSerializerFactory();
            factory.RegisterSerializer(typeof(bool), new TestSerializer(), overrideDefault: true);

            ISerializer serializer = factory.GetSerializer("Test_Serializer");
            ISerializer defaultSerializer = factory.GetDefaultSerializer<bool>();

            Assert.Equal(serializer, defaultSerializer);
        }

        [Fact]
        public void RegisterSerializerCorrectlyWithoutOveridingDefault()
        {
            StandardSerializerFactory factory = new StandardSerializerFactory();
            factory.RegisterSerializer(typeof(bool), new TestSerializer(), overrideDefault: false);

            ISerializer serializer = factory.GetSerializer("Test_Serializer");
            ISerializer defaultSerializer = factory.GetDefaultSerializer<bool>();

            Assert.NotEqual(serializer, defaultSerializer);
            Assert.Equal(typeof(TestSerializer), serializer.GetType());
            Assert.NotEqual(typeof(TestSerializer), defaultSerializer.GetType());
        }

        private class TestSerializer : Serializer<bool>
        {
            public override string Identifier => "Test_Serializer";

            public override bool Deserialize(byte[] bytes)
            {
                throw new NotImplementedException();
            }

            public override byte[] Serialize(bool value)
            {
                throw new NotImplementedException();
            }
        }

        public class GetSerializerByIdentifierTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { new BooleanSerializer().Identifier, typeof(BooleanSerializer) };
                yield return new object[] { new ByteArraySerializer().Identifier, typeof(ByteArraySerializer) };
                yield return new object[] { new ByteSerializer().Identifier, typeof(ByteSerializer) };
                yield return new object[] { new CharSerializer().Identifier, typeof(CharSerializer) };
                yield return new object[] { new DateTimeOffsetSerializer().Identifier, typeof(DateTimeOffsetSerializer) };
                yield return new object[] { new DateTimeSerializer().Identifier, typeof(DateTimeSerializer) };
                yield return new object[] { new DecimalSerializer().Identifier, typeof(DecimalSerializer) };
                yield return new object[] { new DoubleSerializer().Identifier, typeof(DoubleSerializer) };
                yield return new object[] { new GuidSerializer().Identifier, typeof(GuidSerializer) };
                yield return new object[] { new Int16Serializer().Identifier, typeof(Int16Serializer) };
                yield return new object[] { new Int32Serializer().Identifier, typeof(Int32Serializer) };
                yield return new object[] { new Int64Serializer().Identifier, typeof(Int64Serializer) };
                yield return new object[] { new NullableBooleanSerializer().Identifier, typeof(NullableBooleanSerializer) };
                yield return new object[] { new NullableByteSerializer().Identifier, typeof(NullableByteSerializer) };
                yield return new object[] { new NullableCharSerializer().Identifier, typeof(NullableCharSerializer) };
                yield return new object[] { new NullableDateTimeOffsetSerializer().Identifier, typeof(NullableDateTimeOffsetSerializer) };
                yield return new object[] { new NullableDateTimeSerializer().Identifier, typeof(NullableDateTimeSerializer) };
                yield return new object[] { new NullableDecimalSerializer().Identifier, typeof(NullableDecimalSerializer) };
                yield return new object[] { new NullableDoubleSerializer().Identifier, typeof(NullableDoubleSerializer) };
                yield return new object[] { new NullableGuidSerializer().Identifier, typeof(NullableGuidSerializer) };
                yield return new object[] { new NullableInt16Serializer().Identifier, typeof(NullableInt16Serializer) };
                yield return new object[] { new NullableInt32Serializer().Identifier, typeof(NullableInt32Serializer) };
                yield return new object[] { new NullableInt64Serializer().Identifier, typeof(NullableInt64Serializer) };
                yield return new object[] { new NullableSByteSerializer().Identifier, typeof(NullableSByteSerializer) };
                yield return new object[] { new NullableSingleSerializer().Identifier, typeof(NullableSingleSerializer) };
                yield return new object[] { new NullableTimeSpanSerializer().Identifier, typeof(NullableTimeSpanSerializer) };
                yield return new object[] { new NullableUInt16Serializer().Identifier, typeof(NullableUInt16Serializer) };
                yield return new object[] { new NullableUInt32Serializer().Identifier, typeof(NullableUInt32Serializer) };
                yield return new object[] { new NullableUInt64Serializer().Identifier, typeof(NullableUInt64Serializer) };
                yield return new object[] { new SByteSerializer().Identifier, typeof(SByteSerializer) };
                yield return new object[] { new SingleSerializer().Identifier, typeof(SingleSerializer) };
                yield return new object[] { new StringSerializer().Identifier, typeof(StringSerializer) };
                yield return new object[] { new TimeSpanSerializer().Identifier, typeof(TimeSpanSerializer) };
                yield return new object[] { new UInt16Serializer().Identifier, typeof(UInt16Serializer) };
                yield return new object[] { new UInt32Serializer().Identifier, typeof(UInt32Serializer) };
                yield return new object[] { new UInt64Serializer().Identifier, typeof(UInt64Serializer) };
            }
        }

        public class GetDefaultSerializerByTypeTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { new BooleanSerializer(), typeof(BooleanSerializer) };
                yield return new object[] { new ByteArraySerializer(), typeof(ByteArraySerializer) };
                yield return new object[] { new ByteSerializer(), typeof(ByteSerializer) };
                yield return new object[] { new CharSerializer(), typeof(CharSerializer) };
                yield return new object[] { new DateTimeOffsetSerializer(), typeof(DateTimeOffsetSerializer) };
                yield return new object[] { new DateTimeSerializer(), typeof(DateTimeSerializer) };
                yield return new object[] { new DecimalSerializer(), typeof(DecimalSerializer) };
                yield return new object[] { new DoubleSerializer(), typeof(DoubleSerializer) };
                yield return new object[] { new GuidSerializer(), typeof(GuidSerializer) };
                yield return new object[] { new Int16Serializer(), typeof(Int16Serializer) };
                yield return new object[] { new Int32Serializer(), typeof(Int32Serializer) };
                yield return new object[] { new Int64Serializer(), typeof(Int64Serializer) };
                yield return new object[] { new NullableBooleanSerializer(), typeof(NullableBooleanSerializer) };
                yield return new object[] { new NullableByteSerializer(), typeof(NullableByteSerializer) };
                yield return new object[] { new NullableCharSerializer(), typeof(NullableCharSerializer) };
                yield return new object[] { new NullableDateTimeOffsetSerializer(), typeof(NullableDateTimeOffsetSerializer) };
                yield return new object[] { new NullableDateTimeSerializer(), typeof(NullableDateTimeSerializer) };
                yield return new object[] { new NullableDecimalSerializer(), typeof(NullableDecimalSerializer) };
                yield return new object[] { new NullableDoubleSerializer(), typeof(NullableDoubleSerializer) };
                yield return new object[] { new NullableGuidSerializer(), typeof(NullableGuidSerializer) };
                yield return new object[] { new NullableInt16Serializer(), typeof(NullableInt16Serializer) };
                yield return new object[] { new NullableInt32Serializer(), typeof(NullableInt32Serializer) };
                yield return new object[] { new NullableInt64Serializer(), typeof(NullableInt64Serializer) };
                yield return new object[] { new NullableSByteSerializer(), typeof(NullableSByteSerializer) };
                yield return new object[] { new NullableSingleSerializer(), typeof(NullableSingleSerializer) };
                yield return new object[] { new NullableTimeSpanSerializer(), typeof(NullableTimeSpanSerializer) };
                yield return new object[] { new NullableUInt16Serializer(), typeof(NullableUInt16Serializer) };
                yield return new object[] { new NullableUInt32Serializer(), typeof(NullableUInt32Serializer) };
                yield return new object[] { new NullableUInt64Serializer(), typeof(NullableUInt64Serializer) };
                yield return new object[] { new SByteSerializer(), typeof(SByteSerializer) };
                yield return new object[] { new SingleSerializer(), typeof(SingleSerializer) };
                yield return new object[] { new StringSerializer(), typeof(StringSerializer) };
                yield return new object[] { new TimeSpanSerializer(), typeof(TimeSpanSerializer) };
                yield return new object[] { new UInt16Serializer(), typeof(UInt16Serializer) };
                yield return new object[] { new UInt32Serializer(), typeof(UInt32Serializer) };
                yield return new object[] { new UInt64Serializer(), typeof(UInt64Serializer) };
            }
        }
    }
}
