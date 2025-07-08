namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Internal interface for types that represent a vector of SQL values.
    /// </summary>
    internal interface ISqlVector
    {
        /// <summary>
        /// Gets the number of elements in the vector.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Gets the type of the elements in vector as a 
        /// TDS Vector Header DimensionType value.
        /// Refer TDS section 2.2.5.5.7.4
        /// </summary>
        byte ElementType { get; }

        /// <summary>
        /// Gets the size (in bytes) of a single element.
        /// </summary>
        byte ElementSize { get; }

        /// <summary>
        /// Gets the raw vector data formatted for TDS payload.
        /// </summary>
        byte[] VectorPayload { get; }

        /// <summary>
        /// Returns the total size in bytes for sending SqlVector value on TDS.
        /// </summary>
        int Size { get; }
    }
}
