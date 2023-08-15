// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SqlBatchCommandCollection : DbBatchCommandCollection, IList<SqlBatchCommand>
    {
        readonly List<SqlBatchCommand> _list;

        internal SqlBatchCommandCollection(List<SqlBatchCommand> batchCommands)
        {
            _list = batchCommands;
        }

        public override int Count => _list.Count;

        public override bool IsReadOnly => false;

        IEnumerator<SqlBatchCommand> IEnumerable<SqlBatchCommand>.GetEnumerator() => _list.GetEnumerator();

        public override IEnumerator<DbBatchCommand> GetEnumerator() => _list.GetEnumerator();

        public void Add(SqlBatchCommand item) => _list.Add(item);

        public override void Add(DbBatchCommand item) => Add((SqlBatchCommand)item);

        public override void Clear() => _list.Clear();

        public bool Contains(SqlBatchCommand item) => _list.Contains(item);

        public override bool Contains(DbBatchCommand item) => Contains((SqlBatchCommand)item);

        public void CopyTo(SqlBatchCommand[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        public override void CopyTo(DbBatchCommand[] array, int arrayIndex)
        {
            SqlBatchCommand[] target = (SqlBatchCommand[])array;
            CopyTo(target, arrayIndex);
        }

        public int IndexOf(SqlBatchCommand item) => _list.IndexOf(item);

        public override int IndexOf(DbBatchCommand item) => IndexOf((SqlBatchCommand)item);

        public void Insert(int index, SqlBatchCommand item) => _list.Insert(index, item);

        public override void Insert(int index, DbBatchCommand item) => Insert(index, (SqlBatchCommand)item);

        public bool Remove(SqlBatchCommand item) => _list.Remove(item);

        public override bool Remove(DbBatchCommand item) => Remove((SqlBatchCommand)item);

        public override void RemoveAt(int index) => _list.RemoveAt(index);

        SqlBatchCommand IList<SqlBatchCommand>.this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        public new SqlBatchCommand this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        protected override DbBatchCommand GetBatchCommand(int index) => _list[index];

        protected override void SetBatchCommand(int index, DbBatchCommand batchCommand)
            => _list[index] = (SqlBatchCommand)batchCommand;

    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
