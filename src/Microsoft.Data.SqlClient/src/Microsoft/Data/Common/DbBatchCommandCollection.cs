// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Data.Common
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/DbBatchCommandCollection/*' />
    public abstract class DbBatchCommandCollection : IList<DbBatchCommand>
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/GetEnumerator/*'/>
        public abstract IEnumerator<DbBatchCommand> GetEnumerator();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/GetEnumerator/*'/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/Add/*'/>
        public abstract void Add(DbBatchCommand item);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/Clear/*'/>
        public abstract void Clear();
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/Contains/*'/>
        public abstract bool Contains(DbBatchCommand item);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/CopyTo/*'/>
        public abstract void CopyTo(DbBatchCommand[] array, int arrayIndex);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/Remove/*'/>
        public abstract bool Remove(DbBatchCommand item);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/Count/*'/>
        public abstract int Count { get; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/IsReadOnly/*'/>
        public abstract bool IsReadOnly { get; }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/IndexOf/*'/>
        public abstract int IndexOf(DbBatchCommand item);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/Insert/*'/>
        public abstract void Insert(int index, DbBatchCommand item);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/RemoveAt/*'/>
        public abstract void RemoveAt(int index);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/this/*'/>
        public DbBatchCommand this[int index]
        {
            get => GetBatchCommand(index);
            set => SetBatchCommand(index, value);
        }
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/GetBatchCommand/*'/>
        protected abstract DbBatchCommand GetBatchCommand(int index);
        /// <include file='../../../../doc/snippets/Microsoft.Data.Common/DbBatchCommandCollection.xml' path='docs/members[@name="DbBatchCommandCollection"]/SetBatchCommand/*'/>
        protected abstract void SetBatchCommand(int index, DbBatchCommand batchCommand);
    }
}
