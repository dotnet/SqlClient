using Microsoft.Data.Encryption.Cryptography;
using System.Collections.Generic;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Writes data to a columnar data store.
    /// </summary>
    public interface IColumnarDataWriter
    {
        /// <summary>
        /// Returns a <see cref="List{T}"/> of <see cref="FileEncryption.FileEncryptionSettings"/> 
        /// that are used to determine which transformation to perform on which column of data."/>
        /// </summary>
        IList<FileEncryptionSettings> FileEncryptionSettings { get; }

        /// <summary>
        /// Writes out the data on which encryption transformations were performed.
        /// </summary>
        /// <param name="columns">The <see cref="List{T}"/> of <see cref="IColumn"/>s to write out.</param>
        void Write(IEnumerable<IColumn> columns);
    }
}
