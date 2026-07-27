// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Server-independent tests for <see cref="AlwaysEncryptedCleanup"/>.
    /// </summary>
    public class AlwaysEncryptedCleanupTests
    {
        /// <summary>
        /// A <see cref="DbObject"/> that records drop attempts and can be told
        /// to throw, standing in for a real key/table without a server.
        /// </summary>
        private sealed class FakeDbObject : DbObject
        {
            private readonly bool _throwOnDrop;

            public FakeDbObject(string name, bool throwOnDrop = false) : base(name)
            {
                _throwOnDrop = throwOnDrop;
            }

            public int DropCount { get; private set; }

            public override void Create(SqlConnection sqlConnection)
            {
            }

            public override void Drop(SqlConnection sqlConnection)
            {
                DropCount++;
                if (_throwOnDrop)
                {
                    throw new InvalidOperationException($"Simulated drop failure for '{Name}'.");
                }
            }
        }

        [Fact]
        public void DropSafely_ContinuesPastFailures_AndAttemptsEveryObject()
        {
            FakeDbObject first = new FakeDbObject("first");
            FakeDbObject failing = new FakeDbObject("failing", throwOnDrop: true);
            FakeDbObject last = new FakeDbObject("last");

            List<DbObject> databaseObjects = new List<DbObject> { first, failing, last };

            // Must not throw even though one object's Drop throws.
            AlwaysEncryptedCleanup.DropSafely(sqlConnection: null, databaseObjects);

            // Every object is attempted exactly once, including the one after
            // the failure (the leak this change prevents).
            Assert.Equal(1, first.DropCount);
            Assert.Equal(1, failing.DropCount);
            Assert.Equal(1, last.DropCount);
        }

        [Fact]
        public void DropSafely_EmptySequence_DoesNotThrow()
        {
            AlwaysEncryptedCleanup.DropSafely(sqlConnection: null, new List<DbObject>());
        }
    }
}
