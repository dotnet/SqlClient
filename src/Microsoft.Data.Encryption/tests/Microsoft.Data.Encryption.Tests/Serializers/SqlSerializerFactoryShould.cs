using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers
{
    public class SqlSerializerFactoryShould
    {
        [Fact]
        public void ThrowWhenGettingSerializerWithNulldentifier()
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            Assert.Throws<ArgumentNullException>(() => factory.GetSerializer(null));
        }

        [Fact]
        public void ShouldReturnNullWhenGettingUnregisteredSerializer()
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            Assert.Null(factory.GetSerializer(""));
        }

        [Theory]
        [GetSqlSerializerByIdentifierTestData]
        public void ReturnRegisteredSerializerByIdentifier(string identifier, Type expectedType)
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            ISerializer serialiser = factory.GetSerializer(identifier);
            Assert.Equal(expectedType, serialiser.GetType());
        }

        [Fact]
        public void ThrowWhenGettingUnregisteredDefault()
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            Assert.Throws<NotImplementedException>(() => factory.GetDefaultSerializer<object>());
        }

        [Theory]
        [GetDefaultSqlSerializerByTypeTestData]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Needed for generic test type inference")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Needed for generic test type inference")]
        public void ReturnDefaultSerializerByType<T>(Serializer<T> serializer, Type type)
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            Serializer<T> actualSerializer = factory.GetDefaultSerializer<T>();
            Assert.Equal(actualSerializer.GetType(), type);
        }

        [Fact]
        public void ThrowWhenRegisterSerializerWithNullType()
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            Assert.Throws<ArgumentNullException>(() => factory.RegisterSerializer(null, new TestSerializer()));
        }

        [Fact]
        public void ThrowWhenRegisterSerializerWithNullSerializer()
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            Assert.Throws<ArgumentNullException>(() => factory.RegisterSerializer(typeof(bool), null));
        }

        [Fact]
        public void RegisterSerializerCorrectlyWithOveridingDefault()
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            factory.RegisterSerializer(typeof(bool), new TestSerializer(), overrideDefault: true);

            ISerializer serializer = factory.GetSerializer("Test_Serializer");
            ISerializer defaultSerializer = factory.GetDefaultSerializer<bool>();

            Assert.Equal(serializer, defaultSerializer);
        }

        [Fact]
        public void RegisterSerializerCorrectlyWithoutOveridingDefault()
        {
            SqlSerializerFactory factory = new SqlSerializerFactory();
            factory.RegisterSerializer(typeof(bool), new TestSerializer(), overrideDefault: false);

            ISerializer serializer = factory.GetSerializer("Test_Serializer");
            ISerializer defaultSerializer = factory.GetDefaultSerializer<bool>();

            Assert.NotEqual(serializer, defaultSerializer);
            Assert.Equal(typeof(TestSerializer), serializer.GetType());
            Assert.NotEqual(typeof(TestSerializer), defaultSerializer.GetType());
        }

        [Fact]
        public void CacheSerializerCorrectlyWhenCallingGetOrCreate()
        {
            ISerializer bigintSerializer1 = SqlSerializerFactory.GetOrCreate(typeof(SqlBigintSerializer));
            ISerializer bigintSerializer2 = SqlSerializerFactory.GetOrCreate(typeof(SqlBigintSerializer));
            ISerializer bigintSerializer3 = SqlSerializerFactory.GetOrCreate(typeof(SqlBigintSerializer), scale: 2);

            Assert.Same(bigintSerializer1, bigintSerializer2);
            Assert.NotSame(bigintSerializer1, bigintSerializer3);

            ISerializer decimalSerializer1 = SqlSerializerFactory.GetOrCreate(typeof(SqlDecimalSerializer), precision: 5, scale: 3);
            ISerializer decimalSerializer2 = SqlSerializerFactory.GetOrCreate(typeof(SqlDecimalSerializer), precision: 5, scale: 3);
            ISerializer decimalSerializer3 = SqlSerializerFactory.GetOrCreate(typeof(SqlDecimalSerializer), precision: 1, scale: 1);
            ISerializer decimalSerializer4 = SqlSerializerFactory.GetOrCreate(typeof(SqlDecimalSerializer));
            ISerializer decimalSerializer5 = SqlSerializerFactory.GetOrCreate(typeof(SqlDecimalSerializer), precision: 10, scale: 2);

            Assert.Same(decimalSerializer1, decimalSerializer2);
            Assert.Same(decimalSerializer3, decimalSerializer4);
            Assert.NotSame(decimalSerializer2, decimalSerializer3);
            Assert.NotSame(decimalSerializer4, decimalSerializer5);
            Assert.NotSame(decimalSerializer1, decimalSerializer5);
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

        public class GetSqlSerializerByIdentifierTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { new SqlBigintSerializer().Identifier, typeof(SqlBigintSerializer) };
                yield return new object[] { new SqlBinarySerializer().Identifier, typeof(SqlBinarySerializer) };
                yield return new object[] { new SqlBitSerializer().Identifier, typeof(SqlBitSerializer) };
                yield return new object[] { new SqlCharSerializer().Identifier, typeof(SqlCharSerializer) };
                yield return new object[] { new SqlDateSerializer().Identifier, typeof(SqlDateSerializer) };
                yield return new object[] { new SqlDatetime2Serializer().Identifier, typeof(SqlDatetime2Serializer) };
                yield return new object[] { new SqlDatetimeoffsetSerializer().Identifier, typeof(SqlDatetimeoffsetSerializer) };
                yield return new object[] { new SqlDatetimeSerializer().Identifier, typeof(SqlDatetimeSerializer) };
                yield return new object[] { new SqlDecimalSerializer().Identifier, typeof(SqlDecimalSerializer) };
                yield return new object[] { new SqlFloatSerializer().Identifier, typeof(SqlFloatSerializer) };
                yield return new object[] { new SqlIntSerializer().Identifier, typeof(SqlIntSerializer) };
                yield return new object[] { new SqlMoneySerializer().Identifier, typeof(SqlMoneySerializer) };
                yield return new object[] { new SqlNcharSerializer().Identifier, typeof(SqlNcharSerializer) };
                yield return new object[] { new SqlNullableBigintSerializer().Identifier, typeof(SqlNullableBigintSerializer) };
                yield return new object[] { new SqlNullableBitSerializer().Identifier, typeof(SqlNullableBitSerializer) };
                yield return new object[] { new SqlNullableDateSerializer().Identifier, typeof(SqlNullableDateSerializer) };
                yield return new object[] { new SqlNullableDatetime2Serializer().Identifier, typeof(SqlNullableDatetime2Serializer) };
                yield return new object[] { new SqlNullableDatetimeoffsetSerializer().Identifier, typeof(SqlNullableDatetimeoffsetSerializer) };
                yield return new object[] { new SqlNullableDatetimeSerializer().Identifier, typeof(SqlNullableDatetimeSerializer) };
                yield return new object[] { new SqlNullableDecimalSerializer().Identifier, typeof(SqlNullableDecimalSerializer) };
                yield return new object[] { new SqlNullableFloatSerializer().Identifier, typeof(SqlNullableFloatSerializer) };
                yield return new object[] { new SqlNullableIntSerializer().Identifier, typeof(SqlNullableIntSerializer) };
                yield return new object[] { new SqlNullableMoneySerializer().Identifier, typeof(SqlNullableMoneySerializer) };
                yield return new object[] { new SqlNullableNumericSerializer().Identifier, typeof(SqlNullableNumericSerializer) };
                yield return new object[] { new SqlNullableRealSerializer().Identifier, typeof(SqlNullableRealSerializer) };
                yield return new object[] { new SqlNullableSmalldatetimeSerializer().Identifier, typeof(SqlNullableSmalldatetimeSerializer) };
                yield return new object[] { new SqlNullableSmallintSerializer().Identifier, typeof(SqlNullableSmallintSerializer) };
                yield return new object[] { new SqlNullableSmallmoneySerializer().Identifier, typeof(SqlNullableSmallmoneySerializer) };
                yield return new object[] { new SqlNullableTimeSerializer().Identifier, typeof(SqlNullableTimeSerializer) };
                yield return new object[] { new SqlNullableTinyintSerializer().Identifier, typeof(SqlNullableTinyintSerializer) };
                yield return new object[] { new SqlNullableUniqueidentifierSerializer().Identifier, typeof(SqlNullableUniqueidentifierSerializer) };
                yield return new object[] { new SqlNumericSerializer().Identifier, typeof(SqlNumericSerializer) };
                yield return new object[] { new SqlNvarcharSerializer().Identifier, typeof(SqlNvarcharSerializer) };
                yield return new object[] { new SqlRealSerializer().Identifier, typeof(SqlRealSerializer) };
                yield return new object[] { new SqlSmalldatetimeSerializer().Identifier, typeof(SqlSmalldatetimeSerializer) };
                yield return new object[] { new SqlSmallintSerializer().Identifier, typeof(SqlSmallintSerializer) };
                yield return new object[] { new SqlSmallmoneySerializer().Identifier, typeof(SqlSmallmoneySerializer) };
                yield return new object[] { new SqlTimeSerializer().Identifier, typeof(SqlTimeSerializer) };
                yield return new object[] { new SqlTinyintSerializer().Identifier, typeof(SqlTinyintSerializer) };
                yield return new object[] { new SqlUniqueidentifierSerializer().Identifier, typeof(SqlUniqueidentifierSerializer) };
                yield return new object[] { new SqlVarbinarySerializer().Identifier, typeof(SqlVarbinarySerializer) };
                yield return new object[] { new SqlVarcharSerializer().Identifier, typeof(SqlVarcharSerializer) };
            }
        }

        public class GetDefaultSqlSerializerByTypeTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { new SqlBitSerializer(), typeof(SqlBitSerializer) };
                yield return new object[] { new SqlNullableBitSerializer(), typeof(SqlNullableBitSerializer) };
                yield return new object[] { new SqlTinyintSerializer(), typeof(SqlTinyintSerializer) };
                yield return new object[] { new SqlNullableTinyintSerializer(), typeof(SqlNullableTinyintSerializer) };
                yield return new object[] { new SqlVarbinarySerializer(), typeof(SqlVarbinarySerializer) };
                yield return new object[] { new SqlDatetime2Serializer(), typeof(SqlDatetime2Serializer) };
                yield return new object[] { new SqlNullableDatetime2Serializer(), typeof(SqlNullableDatetime2Serializer) };
                yield return new object[] { new SqlDatetimeoffsetSerializer(), typeof(SqlDatetimeoffsetSerializer) };
                yield return new object[] { new SqlNullableDatetimeoffsetSerializer(), typeof(SqlNullableDatetimeoffsetSerializer) };
                yield return new object[] { new SqlDecimalSerializer(), typeof(SqlDecimalSerializer) };
                yield return new object[] { new SqlNullableDecimalSerializer(), typeof(SqlNullableDecimalSerializer) };
                yield return new object[] { new SqlFloatSerializer(), typeof(SqlFloatSerializer) };
                yield return new object[] { new SqlNullableFloatSerializer(), typeof(SqlNullableFloatSerializer) };
                yield return new object[] { new SqlRealSerializer(), typeof(SqlRealSerializer) };
                yield return new object[] { new SqlNullableRealSerializer(), typeof(SqlNullableRealSerializer) };
                yield return new object[] { new SqlUniqueidentifierSerializer(), typeof(SqlUniqueidentifierSerializer) };
                yield return new object[] { new SqlNullableUniqueidentifierSerializer(), typeof(SqlNullableUniqueidentifierSerializer) };
                yield return new object[] { new SqlIntSerializer(), typeof(SqlIntSerializer) };
                yield return new object[] { new SqlNullableIntSerializer(), typeof(SqlNullableIntSerializer) };
                yield return new object[] { new SqlBigintSerializer(), typeof(SqlBigintSerializer) };
                yield return new object[] { new SqlNullableBigintSerializer(), typeof(SqlNullableBigintSerializer) };
                yield return new object[] { new SqlSmallintSerializer(), typeof(SqlSmallintSerializer) };
                yield return new object[] { new SqlNullableSmallintSerializer(), typeof(SqlNullableSmallintSerializer) };
                yield return new object[] { new SqlNvarcharSerializer(), typeof(SqlNvarcharSerializer) };
                yield return new object[] { new SqlTimeSerializer(), typeof(SqlTimeSerializer) };
                yield return new object[] { new SqlNullableTimeSerializer(), typeof(SqlNullableTimeSerializer) };
            }
        }
    }
}
