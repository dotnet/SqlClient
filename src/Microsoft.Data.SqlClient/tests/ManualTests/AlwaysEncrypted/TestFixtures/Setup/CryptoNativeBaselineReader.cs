using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    /// <summary>
    /// Class which reads the crypto test value vectors from resource text file, which was generated with native code.
    /// </summary>
    internal class CryptoNativeBaselineReader
    {
        /// <summary>
        /// Root Key Identifier, to refer inside resource text file.
        /// </summary>
        private const string RootKeyIdentifier = @"ROOTKEY";

        /// <summary>
        /// Plain Text Identifier, to refer inside resource text file.
        /// </summary>
        private const string PlainTextIdentifier = @"PLAINTEXT";

        /// <summary>
        /// Encryption Type Identifier, to refer inside resource text file.
        /// </summary>
        private const string EncryptionTypeIdentifier = @"ENC_TYPE";

        /// <summary>
        /// Final Cell Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string FinalCellIdentifier = @"FINAL_CELL";

        /// <summary>
        /// Key pair (RSA blob) Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string KeyPairIdentifier = @"KEYPAIR";

        /// <summary>
        /// Key pair (RSA blob) Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string PfxIdentifier = @"PFX";

        /// <summary>
        /// CEK plaintext Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string PlaintextCekIdentifier = @"PLAINTEXT_CEK";

        /// <summary>
        /// CEK ciphertext Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string CiphertextCekIdentifier = @"CIPHERTEXT_CEK";

        /// <summary>
        /// CEK plaintext Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string HashedCekIdentifier = @"HASHED_CEK";

        /// <summary>
        /// CEK ciphertext Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string SignedCekIdentifier = @"SIGNED_CEK";

        /// <summary>
        /// CEK ciphertext Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string PathCekIdentifier = @"PATH_CEK";

        /// <summary>
        /// CEK ciphertext Value Identifier, to refer inside resource text file.
        /// </summary>
        private const string FinalcellCekIdentifier = @"FINALCELL_CEK";


        /// <summary>
        /// Resource Data.
        /// </summary>
        private static readonly string m_resource = "TCECryptoNativeBaseline.txt";

        /// <summary>
        /// Resource Data for RSA test vectors.
        /// </summary>
        private static readonly string m_resource_rsa = "TCECryptoNativeBaselineRsa.txt";


        /// <summary>
        /// Resource Data.
        /// </summary>
        private static readonly string m_resource_data = File.ReadAllText(m_resource, Encoding.UTF8);

        /// <summary>
        /// Resource Data for RSA test vectors.
        /// </summary>
        private static readonly string m_resource_data_rsa = File.ReadAllText(m_resource_rsa, Encoding.UTF8);

        /// <summary>
        /// Byte Data identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexdataIdentifier = new Regex(@"0x");

        /// <summary>
        /// Root Key Data identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexRootKeyIdentifier = new Regex(RootKeyIdentifier);

        /// <summary>
        /// Plain text data identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexPlainTextIdentifier = new Regex(PlainTextIdentifier);

        /// <summary>
        /// Encryption Type identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexEncryptionTypeIdentifier = new Regex(EncryptionTypeIdentifier);

        /// <summary>
        /// Encryption Type Data identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexEncryptionTypeDataIdentifier = new Regex("[0-9]");

        /// <summary>
        /// Encryption Type Data identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexEncryptionTypeDataPath = new Regex("= [0-9a-zA-Z/]");

        /// <summary>
        /// Final Cell identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexFinalCellIdentifier = new Regex(FinalCellIdentifier);

        /// <summary>
        /// Key pair (RSA blob) identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexKeyPairIdentifier = new Regex(KeyPairIdentifier);

        /// <summary>
        /// Key pair (RSA blob) identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexPfxIdentifier = new Regex(PfxIdentifier);

        /// <summary>
        /// Plaintext CEK identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexPlaintextCekIdentifier = new Regex(PlaintextCekIdentifier);

        /// <summary>
        /// Ciphertext CEK identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexCiphertextCekIdentifier = new Regex(CiphertextCekIdentifier);

        /// <summary>
        /// Ciphertext CEK identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexHashedCekIdentifier = new Regex(HashedCekIdentifier);

        /// <summary>
        /// Ciphertext CEK identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexSignedCekIdentifier = new Regex(SignedCekIdentifier);

        /// <summary>
        /// Ciphertext CEK identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexPathCekIdentifier = new Regex(PathCekIdentifier);

        /// <summary>
        /// Ciphertext CEK identifier regex, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexFinalcellCekCekIdentifier = new Regex(FinalcellCekIdentifier);

        /// <summary>
        /// Parameter Data End identifier, to search inside resource text file.
        /// </summary>
        private static readonly Regex regexEndParameterIdentifier = new Regex(@"\r");

        private readonly IList<CryptoVector> m_CryptoVectors;

        public IList<CryptoVector> CryptoVectors
        {
            get
            {
                return m_CryptoVectors;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public CryptoNativeBaselineReader()
        {
            Debug.Assert(m_resource_data != null);
            m_CryptoVectors = new List<CryptoVector>();
        }

        /// <summary>
        /// Initialize crypto vectors.
        /// </summary>
        public void InitializeCryptoVectors()
        {
            InitializeCryptoVectors(CryptNativeTestVectorType.Aead);
        }

        /// <summary>
        /// Initialize crypto vectors.
        /// </summary>
        public void InitializeCryptoVectors(CryptNativeTestVectorType testVectorType)
        {
            switch (testVectorType)
            {
                case CryptNativeTestVectorType.Aead:
                    InitializeCryptoVectorsAead();
                    break;
                case CryptNativeTestVectorType.Rsa:
                    InitializeCryptoVectorsRsa();
                    break;
            };
        }

        /// <summary>
        /// Initialize crypto test vectors for AEAD tests
        /// </summary>
        private void InitializeCryptoVectorsAead()
        {
            int startIndex, resourceIndex = 0;
            CryptoVector CryptoVector;
            string rootKey, plainText, encryptionType, finalCellBlob;
            bool extractResult = false;

            while (resourceIndex < m_resource_data.Length)
            {
                // 1 - Extract RootKey from the resource text file.
                extractResult = ExtractCryptoParameter(m_resource_data, regexRootKeyIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(!extractResult || resourceIndex >= startIndex);

                // If input data is over, break.
                if (!extractResult)
                {
                    break;
                }

                rootKey = m_resource_data.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(rootKey));

                // 2 - Extract Plain Text from the resource text file.
                extractResult = ExtractCryptoParameter(m_resource_data, regexPlainTextIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                plainText = m_resource_data.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(plainText));

                // 3 - Extract Test Encryption Type from the resource text file.
                extractResult = ExtractCryptoParameter(m_resource_data, regexEncryptionTypeIdentifier, regexEncryptionTypeDataIdentifier, regexEndParameterIdentifier, 0, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                encryptionType = m_resource_data.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(plainText));

                // 4 - Extract Final Cell Value from the resource text file.
                extractResult = ExtractCryptoParameter(m_resource_data, regexFinalCellIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                finalCellBlob = m_resource_data.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(finalCellBlob));

                // 5 - Instantiate a new crypto vector with these parameters and add it to the vector list.
                CryptoVector = new CryptoVector(StringToByteArray(rootKey), StringToByteArray(plainText), StringToByteArray(finalCellBlob), (CryptoVectorEncryptionType)Enum.Parse(typeof(CryptoVectorEncryptionType), encryptionType, ignoreCase: true));

                m_CryptoVectors.Add(CryptoVector);
            }
        }

        /// <summary>
        /// Initialize RSA test vector data
        /// </summary>
        private void InitializeCryptoVectorsRsa()
        {
            int startIndex, resourceIndex = 0;
            CryptoVector CryptoVector;
            string keyPair, plaintextCek, ciphertextCek, hashedCek, signedCek, pathCek, finalcellCek;
            bool extractResult = false;

            // 1 - Extract RSA Key pair from the resource text file.
            extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexKeyPairIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
            Debug.Assert(!extractResult || resourceIndex >= startIndex);

            // If input data is over, break.
            if (!extractResult)
            {
                return;
            }

            keyPair = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
            Debug.Assert(!string.IsNullOrWhiteSpace(keyPair));

            CryptoVector = new CryptoVector(StringToByteArray(keyPair), CryptNativeTestVectorType.RsaKeyPair);
            m_CryptoVectors.Add(CryptoVector);

            // 1a - Extract a matching PFX from the resource text file.
            extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexPfxIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
            Debug.Assert(!extractResult || resourceIndex >= startIndex);

            // If input data is over, break.
            if (!extractResult)
            {
                return;
            }

            string pfx = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
            Debug.Assert(!string.IsNullOrWhiteSpace(keyPair));

            CryptoVector = new CryptoVector(StringToByteArray(pfx), CryptNativeTestVectorType.RsaPfx);
            m_CryptoVectors.Add(CryptoVector);

            while (resourceIndex < m_resource_data.Length)
            {
                // 2 - Extract Plain Text & ciphertext from the resource text file.
                extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexPlaintextCekIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                if (!extractResult)
                {
                    break;
                }

                Debug.Assert(extractResult && resourceIndex >= startIndex);

                plaintextCek = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(plaintextCek));

                extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexCiphertextCekIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                ciphertextCek = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(ciphertextCek));

                extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexPathCekIdentifier, regexEncryptionTypeDataPath, regexEndParameterIdentifier, 0, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                pathCek = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(pathCek));

                extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexHashedCekIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                hashedCek = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(hashedCek));

                extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexSignedCekIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                signedCek = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(signedCek));

                extractResult = ExtractCryptoParameter(m_resource_data_rsa, regexFinalcellCekCekIdentifier, regexdataIdentifier, regexEndParameterIdentifier, 2, resourceIndex, out startIndex, out resourceIndex);
                Debug.Assert(extractResult && resourceIndex >= startIndex);

                finalcellCek = m_resource_data_rsa.Substring(startIndex, resourceIndex - startIndex);
                Debug.Assert(!string.IsNullOrWhiteSpace(signedCek));

                // 3 - Instantiate a new crypto vector with these parameters and add it to the vector list.
                CryptoVector = new CryptoVector(
                    StringToByteArray(plaintextCek)
                    , StringToByteArray(ciphertextCek)
                    , StringToByteArray(hashedCek)
                    , StringToByteArray(signedCek)
                    , pathCek.Substring(2)
                    , StringToByteArray(finalcellCek));

                m_CryptoVectors.Add(CryptoVector);
            }
        }

        /// <summary>
        /// Extract the crypto properties.Tries to match regex1 followed by regex2 followed by regex3.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="regex1"></param>
        /// <param name="regex2"></param>
        /// <param name="regex3"></param>
        /// <param name="dataPadding"></param>
        /// <param name="startIndex"></param>
        /// <param name="outputStartIndex"></param>
        /// <param name="runningIndex"></param>
        private bool ExtractCryptoParameter(string input, Regex regex1, Regex regex2, Regex regex3, int dataPadding, int startIndex, out int outputStartIndex, out int runningIndex)
        {
            outputStartIndex = 0;
            runningIndex = 0;

            // Match regex1, regex2, regex3 in that order.
            // If regex1 match was not successful, then return false to indicate completion of parsing most likely.
            Match regexMatch = regex1.Match(input, startIndex);
            if (!regexMatch.Success)
            {
                return false;
            }

            runningIndex = regexMatch.Index;

            regexMatch = regex2.Match(input, runningIndex);
            Debug.Assert(regexMatch.Success);

            outputStartIndex = regexMatch.Index + dataPadding;
            runningIndex = regexMatch.Index + dataPadding;

            regexMatch = regex3.Match(input, runningIndex);
            Debug.Assert(regexMatch.Success);

            // The final index is passed back to caller so it know where to start next.
            runningIndex = regexMatch.Index;

            return true;
        }

        internal static byte[] StringToByteArray(string hex)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(hex));
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
