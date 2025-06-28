// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Tests that null and non-null values assigned to the SqlBuffer round-trip correctly to their CLR and their
    /// their SqlTypes representations.
    /// </summary>
    /// <remarks>
    /// Several methods in this class are internal. This is because their parameters are of SqlBuffer.StorageType,
    /// which is non-public.
    /// </remarks>
    public sealed class SqlBufferTest
    {
        private readonly SqlBuffer _target = new();

        /// <summary>
        /// Verifies that if a SqlBuffer is directly assigned the value of SqlGuid.Null, accessing its Guid property
        /// throws a SqlNullValueException.
        /// </summary>
        [Fact]
        public void GuidShouldThrowWhenSqlGuidNullIsSet()
        {
            _target.SqlGuid = SqlGuid.Null;

            Assert.Throws<SqlNullValueException>(() => _target.Guid);
        }

        /// <summary>
        /// Verifies that if a SqlBuffer is set to null of type Guid or SqlGuid, accessing its Guid property throws
        /// a SqlNullValueException.
        /// </summary>
        [Theory]
        [InlineData(SqlBuffer.StorageType.Guid)]
        [InlineData(SqlBuffer.StorageType.SqlGuid)]
        internal void GuidShouldThrowWhenSetToNullOfTypeIsCalled(SqlBuffer.StorageType storageType)
        {
            _target.SetToNullOfType(storageType);

            Assert.Throws<SqlNullValueException>(() => _target.Guid);
        }

        /// <summary>
        /// Verifies that the Guid property round-trips correctly.
        /// </summary>
        [Fact]
        public void GuidShouldReturnWhenGuidIsSet()
        {
            var expected = Guid.NewGuid();
            _target.Guid = expected;

            Assert.Equal(expected, _target.Guid);
        }

        /// <summary>
        /// Verifies that the SqlGuid property round-trips to the Guid property correctly.
        /// </summary>
        [Fact]
        public void GuidShouldReturnExpectedWhenSqlGuidIsSet()
        {
            var expected = Guid.NewGuid();
            _target.SqlGuid = expected;

            Assert.Equal(expected, _target.Guid);
        }

        /// <summary>
        /// Verifies that if a SqlBuffer is set to null of type Guid or SqlGuid, accessing its SqlGuid property returns
        /// SqlGuid.Null.
        /// </summary>
        [Theory]
        [InlineData(SqlBuffer.StorageType.Guid)]
        [InlineData(SqlBuffer.StorageType.SqlGuid)]
        internal void SqlGuidShouldReturnSqlNullWhenSetToNullOfTypeIsCalled(SqlBuffer.StorageType storageType)
        {
            _target.SetToNullOfType(storageType);

            Assert.Equal(SqlGuid.Null, _target.SqlGuid);
        }

        /// <summary>
        /// Verifies that if a SqlBuffer is directly assigned the value of SqlGuid.Null, accessing its SqlGuid property
        /// returns SqlGuid.Null.
        /// </summary>
        [Fact]
        public void SqlGuidShouldReturnSqlGuidNullWhenSqlGuidNullIsSet()
        {
            _target.SqlGuid = SqlGuid.Null;

            Assert.Equal(SqlGuid.Null, _target.SqlGuid);
        }
        
        /// <summary>
        /// Verifies that the Guid property round-trips to the SqlGuid property correctly.
        /// </summary>
        [Fact]
        public void SqlGuidShouldReturnExpectedWhenGuidIsSet()
        {
            var guid = Guid.NewGuid();
            SqlGuid expected = guid;
            _target.Guid = guid;

            Assert.Equal(expected, _target.SqlGuid);
        }

        /// <summary>
        /// Verifies that the SqlGuid property round-trips correctly.
        /// </summary>
        [Fact]
        public void SqlGuidShouldReturnExpectedWhenSqlGuidIsSet()
        {
            SqlGuid expected = Guid.NewGuid();
            _target.SqlGuid = expected;

            Assert.Equal(expected, _target.SqlGuid);
        }

        /// <summary>
        /// Verifies that the Guid property round-trips to the SqlValue property correctly.
        /// </summary>
        [Fact]
        public void SqlValueShouldReturnExpectedWhenGuidIsSet()
        {
            var guid = Guid.NewGuid();
            SqlGuid expected = guid;
            _target.Guid = guid;

            Assert.Equal(expected, _target.SqlValue);
        }

        /// <summary>
        /// Verifies that the SqlGuid property round-trips to the SqlValue property correctly.
        /// </summary>
        [Fact]
        public void SqlValueShouldReturnExpectedWhenSqlGuidIsSet()
        {
            SqlGuid expected = Guid.NewGuid();
            _target.SqlGuid = expected;

            Assert.Equal(expected, _target.SqlValue);
        }
    }
}
