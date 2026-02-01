// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/SqlBatchCommandCollection/*'/>
    public class SqlBatchCommandCollection :
        #if NET
        DbBatchCommandCollection,
        #endif
        IList<SqlBatchCommand>
    {
        readonly List<SqlBatchCommand> _list;

        internal SqlBatchCommandCollection(List<SqlBatchCommand> batchCommands)
        {
            _list = batchCommands;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Count/*'/>
        public
        #if NET
        override
        #endif
        int Count => _list.Count;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IsReadOnly/*'/>
        public
        #if NET
        override
        #endif
        bool IsReadOnly => false;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Clear/*'/>
        public
        #if NET
        override
        #endif
        void Clear() => _list.Clear();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/RemoveAt/*'/>
        public
        #if NET
        override
        #endif
        void RemoveAt(int index) => _list.RemoveAt(index);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this2/*'/>
        public
        #if NET
        new
        #endif
        SqlBatchCommand this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        #if NET
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/GetEnumerator/*'/>
        public override IEnumerator<DbBatchCommand> GetEnumerator() => _list.GetEnumerator();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Add2/*'/>
        public override void Add(DbBatchCommand item) => Add((SqlBatchCommand)item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Contains2/*'/>
        public override bool Contains(DbBatchCommand item) => Contains((SqlBatchCommand)item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/CopyTo2/*'/>
        public override void CopyTo(DbBatchCommand[] array, int arrayIndex)
        {
            SqlBatchCommand[] target = (SqlBatchCommand[])array;
            CopyTo(target, arrayIndex);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IndexOf2/*'/>
        public override int IndexOf(DbBatchCommand item) => IndexOf((SqlBatchCommand)item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Insert2/*'/>
        public override void Insert(int index, DbBatchCommand item) => Insert(index, (SqlBatchCommand)item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Remove2/*'/>
        public override bool Remove(DbBatchCommand item) => Remove((SqlBatchCommand)item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/GetBatchCommand/*'/>
        protected override DbBatchCommand GetBatchCommand(int index) => _list[index];

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/SetBatchCommand/*'/>
        protected override void SetBatchCommand(int index, DbBatchCommand batchCommand)
            => _list[index] = (SqlBatchCommand)batchCommand;
        #else
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
        #endif

        IEnumerator<SqlBatchCommand> IEnumerable<SqlBatchCommand>.GetEnumerator() => _list.GetEnumerator();

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Add1/*'/>
        public void Add(SqlBatchCommand item) => _list.Add(item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Contains1/*'/>
        public bool Contains(SqlBatchCommand item) => _list.Contains(item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/CopyTo1/*'/>
        public void CopyTo(SqlBatchCommand[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/IndexOf1/*'/>
        public int IndexOf(SqlBatchCommand item) => _list.IndexOf(item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Insert1/*'/>
        public void Insert(int index, SqlBatchCommand item) => _list.Insert(index, item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/Remove1/*'/>
        public bool Remove(SqlBatchCommand item) => _list.Remove(item);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBatchCommandCollection.xml' path='docs/members[@name="SqlBatchCommandCollection"]/this1/*'/>
        SqlBatchCommand IList<SqlBatchCommand>.this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }
    }
}
