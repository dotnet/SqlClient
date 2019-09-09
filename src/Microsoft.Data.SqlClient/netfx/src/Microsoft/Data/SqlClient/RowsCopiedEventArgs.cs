// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient {

    /// <summary>
    /// Represents the set of arguments passed to the <see cref="Microsoft.Data.SqlClient.SqlRowsCopiedEventHandler" />.
    /// </summary>
    /// <remarks>To be added.</remarks>
    public class SqlRowsCopiedEventArgs : System.EventArgs {
        private bool            _abort;
        private long             _rowsCopied;

        /// <summary>
        /// Creates a new instance of the <see cref="Microsoft.Data.SqlClient.SqlRowsCopiedEventArgs" /> object.
        /// </summary>
        /// <param name="rowsCopied"></param>
        /// <remarks>
        /// <![CDATA[
        /// ## Remarks 
        /// The value in the `rowsCopied` parameter is reset on each call to any one of the `SqlBulkCopy.WriteToServer` methods. 
        /// ]]>
        /// </remarks>
        public SqlRowsCopiedEventArgs (long rowsCopied) {
            _rowsCopied = rowsCopied;
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the bulk copy operation should be aborted.
        /// </summary>
        /// <value><see langword="true"/> if the bulk copy operation should be aborted; otherwise <see langword="false" />.</value>
        /// <remarks>
        /// <![CDATA[
        /// ## Remarks
        /// Use the <xref:Microsoft.Data.SqlClient.SqlRowsCopiedEventArgs.Abort%2A> property to cancel a bulk copy operation. Set <xref:Microsoft.Data.SqlClient.SqlRowsCopiedEventArgs.Abort%2A> to `true` to abort the bulk copy operation.
        /// If you call the **Close** method from <xref:Microsoft.Data.SqlClient.SqlBulkCopy.SqlRowsCopied>, an exception is generated, and the <xref:Microsoft.Data.SqlClient.SqlBulkCopy> object state does not change.
        ///  If an application specifically creates a <xref:Microsoft.Data.SqlClient.SqlTransaction> object in the <xref:Microsoft.Data.SqlClient.SqlCommand> constructor, the transaction is not rolled back. The application is responsible for determining whether it is required to rollback the operation, and if so, it must call the <xref:Microsoft.Data.SqlClient.SqlTransaction.Rollback%2A?displayProperty=nameWithType> method. If the application does not create a transaction, the internal transaction corresponding to the current batch is automatically rolled back. However, changes related to previous batches within the bulk copy operation are retained, because the transactions for them already have been committed.  
        /// ]]>
        /// </remarks>
        public bool Abort {
            get {
                return _abort;
            }
            set {
                _abort = value;
            }

        }

        /// <summary>
        /// Gets a value that returns the number of rows copied during the current bulk copy operation.
        /// </summary>
        /// <value>
        /// <see langword="int"/> that returns the number of rows copied.
        /// </value>
        /// <remarks>
        /// <![CDATA[
        /// ##Remarks
        /// The value in the <xref:Microsoft.Data.SqlClient.SqlRowsCopiedEventArgs.RowsCopied%2A> property is reset on each call to any of the `SqlBulkCopy.WriteToServer` methods.
        /// ]]>
        /// </remarks>
        public long RowsCopied {
            get {
                return _rowsCopied;
            }
        }
    }
}
