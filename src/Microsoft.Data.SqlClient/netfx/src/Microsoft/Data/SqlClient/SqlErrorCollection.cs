// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections;
using System.ComponentModel;

namespace Microsoft.Data.SqlClient {
    [Serializable, ListBindable(false)]
    public sealed class SqlErrorCollection : ICollection {

        private ArrayList errors = new ArrayList();

        internal SqlErrorCollection() {
        }

        public void CopyTo (Array array, int index) {
            this.errors.CopyTo(array, index);
        }

        public void CopyTo (SqlError[] array, int index) {
            this.errors.CopyTo(array, index);
        }

        public int Count {
            get { return this.errors.Count;}
        }

        object System.Collections.ICollection.SyncRoot { // MDAC 68481
            get { return this;}
        }

        bool System.Collections.ICollection.IsSynchronized { // MDAC 68481
            get { return false;}
        }

        public SqlError this[int index] {
            get {
                return (SqlError) this.errors[index];
            }
        }

        public IEnumerator GetEnumerator() {
            return errors.GetEnumerator();
        }

        internal void Add(SqlError error) {
            this.errors.Add(error);
        }
    }
}
