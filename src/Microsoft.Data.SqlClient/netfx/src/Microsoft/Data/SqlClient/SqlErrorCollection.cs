// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.ComponentModel;

namespace Microsoft.Data.SqlClient
{
    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/SqlErrorCollection/*' />
    [Serializable, ListBindable(false)]
    public sealed class SqlErrorCollection : ICollection
    {

        private ArrayList errors = new ArrayList();

        internal SqlErrorCollection()
        {
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/CopyToArrayIndex1/*' />
        public void CopyTo(Array array, int index)
        {
            this.errors.CopyTo(array, index);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/CopyToArrayIndex2/*' />
        public void CopyTo(SqlError[] array, int index)
        {
            this.errors.CopyTo(array, index);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/Count/*' />
        public int Count
        {
            get { return this.errors.Count; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/System.Collections.ICollection.SyncRoot/*' />
        object System.Collections.ICollection.SyncRoot
        { // MDAC 68481
            get { return this; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/System.Collections.ICollection.IsSynchronized/*' />
        bool System.Collections.ICollection.IsSynchronized
        { // MDAC 68481
            get { return false; }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/Item/*' />
        public SqlError this[int index]
        {
            get
            {
                return (SqlError)this.errors[index];
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlErrorCollection.xml' path='docs/members[@name="SqlErrorCollection"]/GetEnumerator/*' />
        public IEnumerator GetEnumerator()
        {
            return errors.GetEnumerator();
        }

        internal void Add(SqlError error)
        {
            this.errors.Add(error);
        }
    }
}
