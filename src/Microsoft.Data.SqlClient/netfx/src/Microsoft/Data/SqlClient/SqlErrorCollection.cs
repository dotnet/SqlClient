//------------------------------------------------------------------------------
// <copyright file="SqlErrorCollection.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.SqlClient {

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;

    [Serializable, ListBindable(false)]
    public sealed class SqlErrorCollection : ICollection {

        private ArrayList errors = new ArrayList();
        private System.Data.SqlClient.SqlErrorCollection SysSqlErrorCollection { get; set; }

        internal SqlErrorCollection() {}

        // Constructor for backward compatibility.
        internal SqlErrorCollection(System.Data.SqlClient.SqlErrorCollection sqlErrorCollection)
        {
            SysSqlErrorCollection = sqlErrorCollection;
        }

        public void CopyTo (Array array, int index) {
            if (null != SysSqlErrorCollection)
            {
                SysSqlErrorCollection.CopyTo(array, index);
            }
            else
            {
                this.errors.CopyTo(array, index);
            }
        }

        public void CopyTo (SqlError[] array, int index) {
            if (null != SysSqlErrorCollection)
            {
                List<SqlError> retList = new List<SqlError>();
                System.Data.SqlClient.SqlError[] sysArray = new System.Data.SqlClient.SqlError[SysSqlErrorCollection.Count];
                SysSqlErrorCollection.CopyTo(sysArray, index);
                foreach (System.Data.SqlClient.SqlError item in sysArray)
                {
                    retList.Add(new SqlError(item));
                }

                retList.CopyTo(array, index);
            }
            else
            {
                this.errors.CopyTo(array, index);
            }
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
                if (null != SysSqlErrorCollection) {
                    return new SqlError(SysSqlErrorCollection[index]);
                }
                return (SqlError)this.errors[index];
            }
        }

        public IEnumerator GetEnumerator() {
            if (null != SysSqlErrorCollection) {
                return SysSqlErrorCollection.GetEnumerator();
            }
            else {
                return errors.GetEnumerator();
            }
        }

        internal void Add(SqlError error) {
            this.errors.Add(error);
        }
    }
}
