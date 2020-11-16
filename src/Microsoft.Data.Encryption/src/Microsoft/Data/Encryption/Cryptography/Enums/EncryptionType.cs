namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// The type of encryption.
    /// </summary>
    /// <remarks>
    /// The three encryption types are Plaintext Deterministic and Randomized. Plaintext unencrypted data. 
    /// Deterministic encryption always generates the same encrypted value for any given plain text value. 
    /// Randomized encryption uses a method that encrypts data in a less predictable manner. Randomized encryption is more secure.
    /// </remarks>
    public enum EncryptionType
    {
        /// <summary>
        /// Plaintext unencrypted data.
        /// </summary>
        Plaintext,

        /// <summary>
        /// Deterministic encryption always generates the same encrypted value for any given plain text value.
        /// </summary>
        Deterministic,

        /// <summary>
        /// Randomized encryption uses a method that encrypts data in a less predictable manner. Randomized encryption is more secure.
        /// </summary>
        Randomized
    }
}
