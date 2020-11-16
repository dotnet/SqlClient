using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Represents a column of data that will undergo an encryption operation.
    /// </summary>
    /// <typeparam name="T">The type of data in the <see cref="Column{T}"/></typeparam>
    public class Column<T> : IColumn
    {
        /// <summary>
        /// Gets or sets the name of the <see cref="Column{T}"/>.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the type of the data in the <see cref="Column{T}"/>.
        /// </summary>
        public Type DataType => typeof(T);

        /// <summary>
        /// Gets the data of the <see cref="Column{T}"/>.
        /// </summary>
        public IList<T> Data { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Column{T}"/> class
        /// </summary>
        /// <param name="data">The column's data.</param>
        public Column(IList<T> data)
        {
            Data = data;
        }

        /// <summary>
        /// Gets the data of the <see cref="Column{T}"/>. Facilitates access from the interface.
        /// </summary>
        Array IColumn.Data => Data.ToArray();
    }
}
