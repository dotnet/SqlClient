using Microsoft.Data.Encryption.Cryptography;
using System.Collections.Generic;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Reads in data from a columnar data source.
    /// </summary>
    public interface IColumnarDataReader
    {
        /// <summary>
        /// Reads in the data on which to perform an encryption transformation.
        /// </summary>
        /// <returns>The data to transform.</returns>
        IEnumerable<IEnumerable<IColumn>> Read();

        /// <summary>
        /// Returns a <see cref="List{T}"/> of <see cref="FileEncryption.FileEncryptionSettings"/> 
        /// that are used to determine which transformation to perform on which column of data."/>
        /// </summary>
        IList<FileEncryptionSettings> FileEncryptionSettings { get; }
    }
}
