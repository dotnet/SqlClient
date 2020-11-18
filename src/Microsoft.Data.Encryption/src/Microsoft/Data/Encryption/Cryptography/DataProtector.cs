namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// The base class used for algorithms that do encryption and decryption of serialized object data.
    /// </summary>
    public abstract class DataProtector
    {
        /// <summary>
        /// Convert information or data into a code, especially to prevent unauthorized access.
        /// </summary>
        /// <param name="plaintext">The information or data to encrypt.</param>
        /// <returns>The ciphertext information.</returns>
        public abstract byte[] Encrypt(byte[] plaintext);

        /// <summary>
        /// Convert coded information or data into an intelligible message.
        /// </summary>
        /// <param name="ciphertext">The coded information or data.</param>
        /// <returns>An intelligible message.</returns>
        public abstract byte[] Decrypt(byte[] ciphertext);
    }
}
