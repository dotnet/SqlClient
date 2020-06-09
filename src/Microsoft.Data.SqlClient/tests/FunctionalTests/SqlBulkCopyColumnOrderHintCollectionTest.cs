// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlBulkCopyColumnOrderHintCollectionTest
    {
        private static SqlBulkCopyColumnOrderHintCollection CreateCollection() => new SqlBulkCopy(new SqlConnection()).ColumnOrderHints;

        private static SqlBulkCopyColumnOrderHintCollection CreateCollection(params SqlBulkCopyColumnOrderHint[] orderHints)
        {
            Assert.NotNull(orderHints);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();

            foreach (SqlBulkCopyColumnOrderHint orderHint in orderHints)
            {
                collection.Add(orderHint);
            }

            return collection;
        }

        [Fact]
        public void Properties_ReturnFalse()
        {
            IList list = CreateCollection();
            Assert.False(list.IsSynchronized);
            Assert.False(list.IsFixedSize);
            Assert.False(list.IsReadOnly);
        }

        [Fact]
        public void Methods_NullParameterPassed_ThrowsArgumentNullException()
        {
            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();
            collection.Add(new SqlBulkCopyColumnOrderHint("column", SortOrder.Ascending));

            Assert.Throws<ArgumentNullException>(() => collection.CopyTo(null, 0));
            Assert.Throws<ArgumentNullException>(() => collection.Add(null));

            Assert.Throws<ArgumentNullException>(() => collection.Insert(0, null));
            Assert.Throws<ArgumentNullException>(() => collection.Remove(null));

            IList list = collection;
            Assert.Throws<ArgumentNullException>(() => list[0] = null);
            Assert.Throws<ArgumentNullException>(() => list.Add(null));
            Assert.Throws<ArgumentNullException>(() => list.CopyTo(null, 0));
            Assert.Throws<ArgumentNullException>(() => list.Insert(0, null));
            Assert.Throws<ArgumentNullException>(() => list.Remove(null));
        }

        [Fact]
        public void Members_InvalidRange_ThrowsArgumentOutOfRangeException()
        {
            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();

            var item = new SqlBulkCopyColumnOrderHint("column", SortOrder.Ascending);

            Assert.Throws<ArgumentOutOfRangeException>(() => collection[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => collection[collection.Count]);
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.Insert(-1, item));
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.Insert(collection.Count + 1, item));
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.RemoveAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.RemoveAt(collection.Count));

            IList list = collection;
            Assert.Throws<ArgumentOutOfRangeException>(() => list[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => list[collection.Count]);
            Assert.Throws<ArgumentOutOfRangeException>(() => list[-1] = item);
            Assert.Throws<ArgumentOutOfRangeException>(() => list[collection.Count] = item);
            Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(-1, item));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(collection.Count + 1, item));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(collection.Count));
        }

        [Fact]
        public void Add_AddItems_ItemsAddedAsEpected()
        {
            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            Assert.Same(item1, collection.Add(item1));
            Assert.Same(item1, collection[0]);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            Assert.Same(item2, collection.Add(item2));
            Assert.Same(item2, collection[1]);

            IList list = CreateCollection();
            int index = list.Add(item1);
            Assert.Equal(0, index);
            Assert.Same(item1, list[0]);
            index = list.Add(item2);
            Assert.Equal(1, index);
            Assert.Same(item2, list[1]);
        }

        [Fact]
        public void Add_HelperOverloads_ItemsAddedAsExpected()
        {
            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();
            SqlBulkCopyColumnOrderHint item;

            item = collection.Add("column1", SortOrder.Ascending);
            Assert.NotNull(item);
            Assert.Equal("column1", item.Column);
            Assert.Equal(SortOrder.Ascending, item.SortOrder);
        }

        [Fact]
        public void Add_InvalidItems_ThrowsArgumentException()
        {
            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();
            Assert.Throws<ArgumentException>(() => collection.Add(new SqlBulkCopyColumnOrderHint(null, SortOrder.Ascending)));
            Assert.Throws<ArgumentException>(() => collection.Add(new SqlBulkCopyColumnOrderHint(null, SortOrder.Unspecified)));
            Assert.Throws<ArgumentException>(() => collection.Add(new SqlBulkCopyColumnOrderHint("", SortOrder.Descending)));
            Assert.Throws<ArgumentException>(() => collection.Add(new SqlBulkCopyColumnOrderHint("", SortOrder.Unspecified)));
            Assert.Throws<ArgumentException>(() => collection.Add(new SqlBulkCopyColumnOrderHint("column", SortOrder.Unspecified)));

            IList list = CreateCollection();
            Assert.Throws<ArgumentException>(() => list.Add(new SqlBulkCopyColumnOrderHint(null, SortOrder.Ascending)));
            Assert.Throws<ArgumentException>(() => list.Add(new SqlBulkCopyColumnOrderHint(null, SortOrder.Unspecified)));
            Assert.Throws<ArgumentException>(() => list.Add(new SqlBulkCopyColumnOrderHint("", SortOrder.Descending)));
            Assert.Throws<ArgumentException>(() => list.Add(new SqlBulkCopyColumnOrderHint("", SortOrder.Unspecified)));
            Assert.Throws<ArgumentException>(() => list.Add(new SqlBulkCopyColumnOrderHint("column", SortOrder.Unspecified)));
        }

        [Fact]
        public void IListAddInsert_InsertNonSqlBulkCopyColumnOrderHintItems_DoNotThrow()
        {
            IList list = CreateCollection();
            list.Add(new SqlBulkCopyColumnOrderHint("column", SortOrder.Descending));

            // The following operations should really throw ArgumentException due to the
            // mismatched types, but do not throw in the full framework.
            string foo = "foo";
            list[0] = foo;
            list.Add(foo);
            list.Insert(0, foo);
        }

        [Fact]
        public void GetEnumerator_NoItems_EmptyEnumerator()
        {
            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();
            IEnumerator e = collection.GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => e.Current);
            Assert.False(e.MoveNext());
            Assert.Throws<InvalidOperationException>(() => e.Current);
        }

        [Fact]
        public void GetEnumerator_ItemsAdded_AllItemsReturnedAndEnumeratorBehavesAsExpected()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Descending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2, item3);

            IEnumerator e = collection.GetEnumerator();

            const int Iterations = 2;
            for (int i = 0; i < Iterations; i++)
            {
                // Not started
                Assert.Throws<InvalidOperationException>(() => e.Current);

                Assert.True(e.MoveNext());
                Assert.Same(item1, e.Current);

                Assert.True(e.MoveNext());
                Assert.Same(item2, e.Current);

                Assert.True(e.MoveNext());
                Assert.Same(item3, e.Current);

                Assert.False(e.MoveNext());
                Assert.False(e.MoveNext());
                Assert.False(e.MoveNext());
                Assert.False(e.MoveNext());
                Assert.False(e.MoveNext());

                // Ended
                Assert.Throws<InvalidOperationException>(() => e.Current);

                e.Reset();
            }
        }

        [Fact]
        public void GetEnumerator_ItemsAdded_ItemsFromEnumeratorMatchesItemsFromIndexer()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Descending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2, item3);
            int index = 0;
            foreach (SqlBulkCopyColumnOrderHint enumeratorItem in collection)
            {
                SqlBulkCopyColumnOrderHint indexerItem = collection[index];

                Assert.NotNull(enumeratorItem);
                Assert.NotNull(indexerItem);

                Assert.Same(indexerItem, enumeratorItem);
                index++;
            }
        }

        [Fact]
        public void GetEnumerator_ModifiedCollectionDuringEnumeration_ThrowsInvalidOperationException()
        {
            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();

            IEnumerator e = collection.GetEnumerator();

            collection.Add("column", SortOrder.Ascending);

            // Collection changed.
            Assert.Throws<InvalidOperationException>(() => e.MoveNext());
            Assert.Throws<InvalidOperationException>(() => e.Reset());
        }

        [Fact]
        public void Contains_ItemsAdded_MatchesExpectation()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2, item3);

            Assert.True(collection.Contains(item1));
            Assert.True(collection.Contains(item2));
            Assert.True(collection.Contains(item3));
            Assert.False(collection.Contains(null));

            IList list = collection;
            Assert.True(list.Contains(item1));
            Assert.True(list.Contains(item2));
            Assert.True(list.Contains(item3));
            Assert.False(list.Contains(null));
            Assert.False(list.Contains("Bogus"));
        }

        [Fact]
        public void CopyTo_ItemsAdded_ItemsCopiedToArray()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2, item3);

            var array1 = new SqlBulkCopyColumnOrderHint[collection.Count];
            collection.CopyTo(array1, 0);

            Assert.Same(item1, array1[0]);
            Assert.Same(item2, array1[1]);
            Assert.Same(item3, array1[2]);

            var array2 = new SqlBulkCopyColumnOrderHint[collection.Count];
            ((ICollection)collection).CopyTo(array2, 0);

            Assert.Same(item1, array2[0]);
            Assert.Same(item2, array2[1]);
            Assert.Same(item3, array2[2]);
        }

        [Fact]
        public void CopyTo_InvalidArrayType_Throws()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            ICollection collection = CreateCollection(item1, item2, item3);

            Assert.Throws<InvalidCastException>(() => collection.CopyTo(new int[collection.Count], 0));
            Assert.Throws<InvalidCastException>(() => collection.CopyTo(new string[collection.Count], 0));
        }

        [Fact]
        public void Indexer_BehavesAsExpected()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2, item3);

            Assert.Same(item1, collection[0]);
            Assert.Same(item2, collection[1]);
            Assert.Same(item3, collection[2]);

            IList list = collection;
            list[0] = item2;
            list[1] = item3;
            list[2] = item1;
            Assert.Same(item2, list[0]);
            Assert.Same(item3, list[1]);
            Assert.Same(item1, list[2]);
        }

        [Fact]
        public void IndexOf_BehavesAsExpected()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2);

            Assert.Equal(0, collection.IndexOf(item1));
            Assert.Equal(1, collection.IndexOf(item2));
            Assert.Equal(-1, collection.IndexOf(item3));

            IList list = collection;
            Assert.Equal(0, list.IndexOf(item1));
            Assert.Equal(1, list.IndexOf(item2));
            Assert.Equal(-1, list.IndexOf(item3));
            Assert.Equal(-1, list.IndexOf("bogus"));
        }

        [Fact]
        public void Insert_BehavesAsExpected()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();

            collection.Insert(0, item3);
            collection.Insert(0, item2);
            collection.Insert(0, item1);

            Assert.Equal(3, collection.Count);
            Assert.Same(item1, collection[0]);
            Assert.Same(item2, collection[1]);
            Assert.Same(item3, collection[2]);
        }

        [Fact]
        public void InsertAndClear_BehavesAsExpected()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection();

            collection.Insert(0, item1);
            collection.Insert(1, item2);
            collection.Insert(2, item3);
            Assert.Equal(3, collection.Count);
            Assert.Same(item1, collection[0]);
            Assert.Same(item2, collection[1]);
            Assert.Same(item3, collection[2]);

            collection.Clear();
            Assert.Empty(collection);

            collection.Add(item1);
            collection.Add(item3);
            Assert.Equal(2, collection.Count);
            Assert.Same(item1, collection[0]);
            Assert.Same(item3, collection[1]);

            collection.Insert(1, item2);
            Assert.Equal(3, collection.Count);
            Assert.Same(item1, collection[0]);
            Assert.Same(item2, collection[1]);
            Assert.Same(item3, collection[2]);

            collection.Clear();
            Assert.Empty(collection);

            IList list = collection;
            list.Insert(0, item1);
            list.Insert(1, item2);
            list.Insert(2, item3);
            Assert.Equal(3, list.Count);
            Assert.Same(item1, list[0]);
            Assert.Same(item2, list[1]);
            Assert.Same(item3, list[2]);

            list.Clear();
            Assert.Equal(0, list.Count);

            list.Add(item1);
            list.Add(item3);
            Assert.Equal(2, list.Count);
            Assert.Same(item1, list[0]);
            Assert.Same(item3, list[1]);

            list.Insert(1, item2);
            Assert.Equal(3, list.Count);
            Assert.Same(item1, list[0]);
            Assert.Same(item2, list[1]);
            Assert.Same(item3, list[2]);

            list.Clear();
            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void Remove_BehavesAsExpected()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2);

            collection.Remove(item1);
            Assert.Single(collection);
            Assert.Same(item2, collection[0]);

            collection.Remove(item2);
            Assert.Empty(collection);

            AssertExtensions.Throws<ArgumentException>(() => collection.Remove(item2));
            AssertExtensions.Throws<ArgumentException>(() => collection.Remove(new SqlBulkCopyColumnOrderHint("column3", SortOrder.Descending)));

            IList list = CreateCollection(item1, item2);

            list.Remove(item1);
            Assert.Equal(1, list.Count);
            Assert.Same(item2, list[0]);

            list.Remove(item2);
            Assert.Equal(0, list.Count);

            AssertExtensions.Throws<ArgumentException>(null, () => list.Remove(item2));
            AssertExtensions.Throws<ArgumentException>(null, () => list.Remove(new SqlBulkCopyColumnOrderHint("column4", SortOrder.Ascending)));
            AssertExtensions.Throws<ArgumentException>(null, () => list.Remove("bogus"));
        }

        [Fact]
        public void RemoveAt_BehavesAsExpected()
        {
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            var item2 = new SqlBulkCopyColumnOrderHint("column2", SortOrder.Descending);
            var item3 = new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending);

            SqlBulkCopyColumnOrderHintCollection collection = CreateCollection(item1, item2, item3);

            collection.RemoveAt(0);
            Assert.Equal(2, collection.Count);
            Assert.Same(item2, collection[0]);
            Assert.Same(item3, collection[1]);

            collection.RemoveAt(1);
            Assert.Single(collection);
            Assert.Same(item2, collection[0]);

            collection.RemoveAt(0);
            Assert.Empty(collection);

            IList list = CreateCollection(item1, item2, item3);

            list.RemoveAt(0);
            Assert.Equal(2, list.Count);
            Assert.Same(item2, list[0]);
            Assert.Same(item3, list[1]);
            list.RemoveAt(1);
            Assert.Equal(1, list.Count);
            Assert.Same(item2, list[0]);

            list.RemoveAt(0);
            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void SyncRoot_NotNullAndSameObject()
        {
            ICollection collection = CreateCollection();
            Assert.NotNull(collection.SyncRoot);
            Assert.Same(collection.SyncRoot, collection.SyncRoot);
        }

        [Fact]
        public void Add_DuplicateColumnNames_NotAllowed()
        {
            SqlBulkCopyColumnOrderHintCollection collection1 = CreateCollection();
            SqlBulkCopyColumnOrderHintCollection collection2 = CreateCollection();
            var item1 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);

            collection1.Add(item1);
            item1.Column += "2";
            collection1[0].Column += "3";
            Assert.Equal("column123", item1.Column);
            Assert.Throws<InvalidOperationException>(() => collection1.Add(item1));

            collection2.Add(item1);
            item1.Column += "4";
            Assert.Same(collection1[0], collection2[0]);
            Assert.Equal("column1234", collection1[0].Column);

            item1.Column = "column1";
            collection1.Add("column2", SortOrder.Ascending);
            item1.Column = "column1";
            collection1[0].Column = "column1";
            collection1[1].Column = "column2";
            Assert.Throws<InvalidOperationException>(() => item1.Column = "column2");
            Assert.Throws<InvalidOperationException>(() => collection1[0].Column = "column2");
            Assert.Throws<InvalidOperationException>(() => collection1[1].Column = "column1");
            Assert.Throws<InvalidOperationException>(() => collection2[0].Column = "column2");
            Assert.Equal("column1", collection1[0].Column);
            Assert.Equal("column2", collection1[1].Column);
            ValidateCollection(collection1, expectedCount: 2);

            Assert.Throws<InvalidOperationException>(() => collection2.Add(collection1[0]));
            collection2.Add(collection1[1]);
            var item3 = new SqlBulkCopyColumnOrderHint("column1", SortOrder.Ascending);
            Assert.Throws<InvalidOperationException>(() => collection1.Add(item3));
            item3.Column = "column3";
            collection1.Add(item3);
            ValidateCollection(collection1, expectedCount: 3);
            ValidateCollection(collection2, expectedCount: 2);

            Assert.Throws<InvalidOperationException>(() => collection1.Insert(0, new SqlBulkCopyColumnOrderHint("column3", SortOrder.Ascending)));
            collection1.Insert(0, new SqlBulkCopyColumnOrderHint("column4", SortOrder.Ascending));
            collection1.Insert(collection1.Count, new SqlBulkCopyColumnOrderHint("column5", SortOrder.Ascending));
            Assert.Throws<InvalidOperationException>(() => collection1[collection1.IndexOf(item1)].Column = "column4");
            ValidateCollection(collection1, expectedCount: 5);

            collection2.Remove(item1);
            Assert.Throws<InvalidOperationException>(() => collection2[0].Column = item1.Column);
            collection2[0].Column = "column6";
            ValidateCollection(collection2, expectedCount: 1);

            collection1.Clear();
            Assert.Empty(collection1);
            collection1.Add("column1", SortOrder.Descending);
            collection1.Add("column2", SortOrder.Descending);
            collection1.Add("column3", SortOrder.Descending);
            collection2[0].Column = collection1[0].Column;
            Assert.Throws<InvalidOperationException>(() => collection1.Add(collection2[0]));
            ValidateCollection(collection1, expectedCount: 3);
            collection1.RemoveAt(0);
            collection1.Add(collection2[0]);
            collection1.Remove(collection1[collection1.Count - 1]);
            collection1.RemoveAt(1);
            collection1.Remove(collection1[collection1.Count - 1]);
            collection1.Add("column1", SortOrder.Descending);
            collection1.Add("column2", SortOrder.Descending);
            collection1.Add("column3", SortOrder.Descending);
            collection1.Add("column4", SortOrder.Descending);
            collection1.Add("column5", SortOrder.Descending);
            ValidateCollection(collection1, expectedCount: 5);
        }

        // verifies that the collection contains no duplicate column names
        private void ValidateCollection(SqlBulkCopyColumnOrderHintCollection collection, int expectedCount)
        {
            Assert.True(expectedCount == collection.Count, "Collection was not the expected size.");
            bool valid = true;
            HashSet<string> columnNames = new HashSet<string>();
            foreach (SqlBulkCopyColumnOrderHint orderHint in collection)
            {
                if (!columnNames.Contains(orderHint.Column))
                {
                    columnNames.Add(orderHint.Column);
                }
                else
                {
                    valid = false;
                }
            }
            Assert.True(valid, "Collection contained a duplicate column name.");
        }
    }
}
