// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#if NET6_0_OR_GREATER

using System.Collections.Generic;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/SqlBatchCommandCollection/*'/>
    public class SqlBatchCommandCollection : DbBatchCommandCollection, IList<SqlBatchCommand>
    {
        readonly List<SqlBatchCommand> _list;

        internal SqlBatchCommandCollection(List<SqlBatchCommand> batchCommands)
        {
            _list = batchCommands;
        }
        /// <inheritdoc />
        public override int Count => _list.Count;
        /// <inheritdoc />
        public override bool IsReadOnly => false;
        IEnumerator<SqlBatchCommand> IEnumerable<SqlBatchCommand>.GetEnumerator() => _list.GetEnumerator();
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.GetEnumerator"/>
        public override IEnumerator<DbBatchCommand> GetEnumerator() => _list.GetEnumerator();
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Add1/*'/>
        public void Add(SqlBatchCommand item) => _list.Add(item);
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.Add(DbBatchCommand)"/>
        public override void Add(DbBatchCommand item) => Add((SqlBatchCommand)item);
        /// <inheritdoc />
        public override void Clear() => _list.Clear();
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Contains1/*'/>
        public bool Contains(SqlBatchCommand item) => _list.Contains(item);
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.Contains(DbBatchCommand)"/>
        public override bool Contains(DbBatchCommand item) => Contains((SqlBatchCommand)item);
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/CopyTo1/*'/>
        public void CopyTo(SqlBatchCommand[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.CopyTo(DbBatchCommand[], int)"/>
        public override void CopyTo(DbBatchCommand[] array, int arrayIndex)
        {
            SqlBatchCommand[] target = (SqlBatchCommand[])array;
            CopyTo(target, arrayIndex);
        }
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IndexOf1/*'/>
        public int IndexOf(SqlBatchCommand item) => _list.IndexOf(item);
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.IndexOf(DbBatchCommand)"/>
        public override int IndexOf(DbBatchCommand item) => IndexOf((SqlBatchCommand)item);
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Insert1/*'/>
        public void Insert(int index, SqlBatchCommand item) => _list.Insert(index, item);
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.Insert(int, DbBatchCommand)"/>
        public override void Insert(int index, DbBatchCommand item) => Insert(index, (SqlBatchCommand)item);
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Remove1/*'/>
        public bool Remove(SqlBatchCommand item) => _list.Remove(item);
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.Remove(DbBatchCommand)"/>
        public override bool Remove(DbBatchCommand item) => Remove((SqlBatchCommand)item);
        /// <inheritdoc />
        public override void RemoveAt(int index) => _list.RemoveAt(index);
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this1/*'/>
        SqlBatchCommand IList<SqlBatchCommand>.this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this2/*'/>
        public new SqlBatchCommand this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.GetBatchCommand(int)"/>
        protected override DbBatchCommand GetBatchCommand(int index) => _list[index];
        /// <inheritdoc cref="System.Data.Common.DbBatchCommandCollection.SetBatchCommand(int, DbBatchCommand)"/>
        protected override void SetBatchCommand(int index, DbBatchCommand batchCommand)
            => _list[index] = (SqlBatchCommand)batchCommand;
    }
}

#endif
