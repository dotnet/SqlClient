using System;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Represents a column of data that will undergo an encryption operation.
    /// </summary>
    public interface IColumn
    {
        /// <summary>
        /// Gets or sets the name of the <see cref="IColumn"/>.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets the type of the data in the <see cref="IColumn"/>.
        /// </summary>
        Type DataType { get; }

        /// <summary>
        /// Gets the data of the <see cref="IColumn"/>.
        /// </summary>
        Array Data { get; }
    }
}
