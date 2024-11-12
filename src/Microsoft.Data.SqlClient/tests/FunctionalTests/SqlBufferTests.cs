// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public sealed class SqlBufferTests
    {
        static SqlBufferTests()
        {
            const string sqlBufferTypeFullName = "Microsoft.Data.SqlClient.SqlBuffer";
            const string storageTypeName = nameof(SqlBufferProxy.StorageType);

            var assembly = typeof(SqlClientFactory).Assembly;
            _sqlBufferType = assembly.GetType(sqlBufferTypeFullName)
                             ?? throw new Exception($"Type not found [{sqlBufferTypeFullName}]");
            _storageTypeType = _sqlBufferType.GetNestedTypes(BindingFlags.NonPublic)
                                   .FirstOrDefault(x => x.Name == storageTypeName)
                               ?? throw new Exception($"Type not found [{sqlBufferTypeFullName}+{storageTypeName}]");
        }

        private static readonly Type _sqlBufferType;
        private static readonly Type _storageTypeType;
        private readonly SqlBufferProxy _target = new();

        public static IEnumerable<object[]> GetStorageTypeValues()
        {
#if NET
            return Enum.GetValues<SqlBufferProxy.StorageType>()
                .Select(x => new object[] { x });
#else
            return Enum.GetValues(typeof(SqlBufferProxy.StorageType))
                .OfType<SqlBufferProxy.StorageType>()
                .Select(x => new object[] { x });
#endif
        }

        [Theory]
        [MemberData(nameof(GetStorageTypeValues))]
        public void StorageTypeInProxyShouldHaveTheSameValueAsOriginal(SqlBufferProxy.StorageType expected)
        {
            var originalEnumName = Enum.GetName(_storageTypeType, (int)expected);

            Assert.Equal(expected.ToString(), originalEnumName);
        }

        [Fact]
        public void GuidShouldThrowWhenSqlGuidNullIsSet()
        {
            _target.SqlGuid = SqlGuid.Null;

            Assert.Throws<SqlNullValueException>(() => _target.Guid);
        }

        [Theory]
        [InlineData(SqlBufferProxy.StorageType.Guid)]
        [InlineData(SqlBufferProxy.StorageType.SqlGuid)]
        public void GuidShouldThrowWhenSetToNullOfTypeIsCalled(SqlBufferProxy.StorageType storageType)
        {
            _target.SetToNullOfType(storageType);

            Assert.Throws<SqlNullValueException>(() => _target.Guid);
        }

        [Fact]
        public void GuidShouldReturnWhenGuidIsSet()
        {
            var expected = Guid.NewGuid();
            _target.Guid = expected;

            Assert.Equal(expected, _target.Guid);
        }

        [Fact]
        public void GuidShouldReturnExpectedWhenSqlGuidIsSet()
        {
            var expected = Guid.NewGuid();
            _target.SqlGuid = expected;

            Assert.Equal(expected, _target.Guid);
        }

        [Theory]
        [InlineData(SqlBufferProxy.StorageType.Guid)]
        [InlineData(SqlBufferProxy.StorageType.SqlGuid)]
        public void SqlGuidShouldReturnSqlNullWhenSetToNullOfTypeIsCalled(SqlBufferProxy.StorageType storageType)
        {
            _target.SetToNullOfType(storageType);

            Assert.Equal(SqlGuid.Null, _target.SqlGuid);
        }

        [Fact]
        public void SqlGuidShouldReturnSqlGuidNullWhenSqlGuidNullIsSet()
        {
            _target.SqlGuid = SqlGuid.Null;

            Assert.Equal(SqlGuid.Null, _target.SqlGuid);
        }
        
        [Fact]
        public void SqlGuidShouldReturnExpectedWhenGuidIsSet()
        {
            var guid = Guid.NewGuid();
            SqlGuid expected = guid;
            _target.Guid = guid;

            Assert.Equal(expected, _target.SqlGuid);
        }

        [Fact]
        public void SqlGuidShouldReturnExpectedWhenSqlGuidIsSet()
        {
            SqlGuid expected = Guid.NewGuid();
            _target.SqlGuid = expected;

            Assert.Equal(expected, _target.SqlGuid);
        }

        [Fact]
        public void SqlValueShouldReturnExpectedWhenGuidIsSet()
        {
            var guid = Guid.NewGuid();
            SqlGuid expected = guid;
            _target.Guid = guid;

            Assert.Equal(expected, _target.SqlValue);
        }

        [Fact]
        public void SqlValueShouldReturnExpectedWhenSqlGuidIsSet()
        {
            SqlGuid expected = Guid.NewGuid();
            _target.SqlGuid = expected;

            Assert.Equal(expected, _target.SqlValue);
        }

        public sealed class SqlBufferProxy
        {
            public enum StorageType
            {
                Empty = 0,
                Boolean,
                Byte,
                DateTime,
                Decimal,
                Double,
                Int16,
                Int32,
                Int64,
                Guid,
                Money,
                Single,
                String,
                SqlBinary,
                SqlCachedBuffer,
                SqlGuid,
                SqlXml,
                Date,
                DateTime2,
                DateTimeOffset,
                Time,
            }

            private static readonly PropertyInfo _guidProperty;
            private static readonly PropertyInfo _sqlGuidProperty;
            private static readonly PropertyInfo _sqlValueProperty;
            private static readonly MethodInfo _setToNullOfTypeMethod;
            private readonly object _instance;

            static SqlBufferProxy()
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                _guidProperty = _sqlBufferType.GetProperty(nameof(Guid), flags);
                _sqlGuidProperty = _sqlBufferType.GetProperty(nameof(SqlGuid), flags);
                _sqlValueProperty = _sqlBufferType.GetProperty(nameof(SqlValue), flags);
                _setToNullOfTypeMethod = _sqlBufferType.GetMethod(nameof(SetToNullOfType), flags);
            }

            public SqlBufferProxy()
            {
                _instance = Activator.CreateInstance(_sqlBufferType, true);
            }

            public Guid Guid
            {
                get => GetPropertyValue<Guid>(_guidProperty);
                set => SetPropertyValue(_guidProperty, value);
            }

            public SqlGuid SqlGuid
            {
                get => GetPropertyValue<SqlGuid>(_sqlGuidProperty);
                set => SetPropertyValue(_sqlGuidProperty, value);
            }

            public object SqlValue
            {
                get => GetPropertyValue<object>(_sqlValueProperty);
            }

            public void SetToNullOfType(StorageType storageType)
            {
#if NET
                _setToNullOfTypeMethod
                    .Invoke(_instance, BindingFlags.DoNotWrapExceptions, null, new object[] { (int)storageType }, null);
#else
                _setToNullOfTypeMethod.Invoke(_instance, new object[] { (int)storageType });
#endif
            }

            private T GetPropertyValue<T>(PropertyInfo property)
            {
#if NET
                return (T)property.GetValue(_instance, BindingFlags.DoNotWrapExceptions, null, null, null);
#else
                try
                {
                    return (T)property.GetValue(_instance);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException!;
                }
#endif
            }

            private void SetPropertyValue(PropertyInfo property, object value)
            {
#if NET
                property.SetValue(_instance, value, BindingFlags.DoNotWrapExceptions, null, null, null);
#else
                try
                {
                    property.SetValue(_instance, value);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException!;
                }
#endif
            }
        }
    }
}
