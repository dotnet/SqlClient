using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    /// <summary>
    /// Encryption Type as per the test code. Different than product code's enumeration.
    /// </summary>
    internal enum CryptoVectorEncryptionType
    {
        /// <summary>
        /// Randomized.
        /// </summary>
        Randomized = 2,

        /// <summary>
        /// Deterministic
        /// </summary>
        Deterministic = 3,
    }

    /// <summary>
    /// Native test vector type
    /// </summary>
    internal enum CryptNativeTestVectorType
    {
        /// <summary>
        /// An AEAD (symmetric) test vector value
        /// </summary>
        Aead,
        /// <summary>
        /// An RSA (CEK encrypted by CMK) test vector value 
        /// </summary>
        Rsa,
        /// <summary>
        /// Special case of RSA test vector, which holds a key pair
        /// </summary>
        RsaKeyPair,
        /// <summary>
        /// Special case of RSA test vector, which holds a PFX
        /// </summary>
        RsaPfx,
    }

    /// <summary>
    /// Represents crypto related parameters to use in testing.
    /// </summary>
    internal class CryptoVector
    {
        /// <summary>
        /// Root Key.
        /// </summary>
        private readonly byte[] m_blob0;

        /// <summary>
        /// Plain Text.
        /// </summary>
        private readonly byte[] m_blob1;

        /// <summary>
        /// Final Encrypted Cell Value.
        /// </summary>
        private readonly byte[] m_blob2;

        /// <summary>
        /// Generic Blob3
        /// </summary>
        private readonly byte[] m_blob3;

        /// <summary>
        /// Generic Blob4
        /// </summary>
        private readonly byte[] m_blob4;

        /// <summary>
        /// Generic String data 0
        /// </summary>
        private readonly string m_string0;

        /// <summary>
        /// Test Encryption Type - Is different from product mapped Encryption Type.
        /// </summary>
        private readonly CryptoVectorEncryptionType m_cryptoVectorEncryptionType;

        /// <summary>
        /// Defineds the test vector type.
        /// </summary>
        private readonly CryptNativeTestVectorType m_CryptNativeTestVectorType;

        /// <summary>
        /// Return the root key.
        /// </summary>
        public byte[] RootKey
        {
            get
            {
                return m_blob0;
            }
        }

        /// <summary>
        /// Return the plain text.
        /// </summary>
        public byte[] PlainText
        {
            get
            {
                return m_blob1;
            }
        }

        /// <summary>
        /// Return the final cell.
        /// </summary>
        public byte[] FinalCell
        {
            get
            {
                return m_blob2;
            }
        }

        /// <summary>
        /// Plaintext CEK
        /// </summary>
        public byte[] PlaintextCek
        {
            get
            {
                return m_blob0;
            }
        }

        /// <summary>
        /// Ciphertext CEK
        /// </summary>
        public byte[] CiphertextCek
        {
            get
            {
                return m_blob1;
            }
        }

        //Precalculated hashed CEK
        public byte[] HashedCek
        {
            get
            {
                return m_blob2;
            }
        }

        /// <summary>
        /// Precalculated Signed CEK blob
        /// </summary>
        public byte[] SignedCek
        {
            get
            {
                return m_blob3;
            }
        }

        /// <summary>
        /// Precalculated final cell CEK
        /// </summary>
        public byte[] FinalcellCek
        {
            get
            {
                return m_blob4;
            }
        }

        /// <summary>
        /// RSA key pair
        /// </summary>
        public byte[] RsaKeyPair
        {
            get
            {
                return m_blob0;
            }
        }

        /// <summary>
        /// Return the test crypto vector encryption type.
        /// </summary>
        public CryptoVectorEncryptionType CryptoVectorEncryptionTypeVal
        {
            get
            {
                return m_cryptoVectorEncryptionType;
            }
        }

        public CryptNativeTestVectorType CryptNativeTestVectorTypeVal
        {
            get
            {
                return m_CryptNativeTestVectorType;
            }
        }

        public string PathCek
        {
            get
            {
                return m_string0;
            }
        }

        /// <summary>
        /// Constructor for AEAD.
        /// </summary>
        /// <param name="rootKey"></param>
        /// <param name="plainText"></param>
        /// <param name="finalCell"></param>
        /// <param name="cryptoVectorEncryptionType"></param>
        public CryptoVector(byte[] rootKey, byte[] plainText, byte[] finalCell, CryptoVectorEncryptionType cryptoVectorEncryptionType)
        {
            Debug.Assert(rootKey != null);
            Debug.Assert(plainText != null);
            Debug.Assert(finalCell != null);

            m_blob0 = rootKey;
            m_blob1 = plainText;
            m_blob2 = finalCell;
            m_cryptoVectorEncryptionType = cryptoVectorEncryptionType;
            m_CryptNativeTestVectorType = CryptNativeTestVectorType.Aead;
            m_string0 = null;
            m_blob3 = null;
            m_blob4 = null;
        }

        /// <summary>
        /// Constructor for RSA test vector
        /// </summary>
        /// <param name="plaintextCek"></param>
        /// <param name="ciphertextCek"></param>
        public CryptoVector(byte[] plaintextCek, byte[] ciphertextCek, byte[] hashedCek, byte[] signedCek, string pathCek, byte[] finalcellCek)
        {
            Debug.Assert(plaintextCek != null);
            Debug.Assert(ciphertextCek != null);
            Debug.Assert(hashedCek != null);
            Debug.Assert(signedCek != null);

            m_blob0 = plaintextCek;
            m_blob1 = ciphertextCek;
            m_blob2 = hashedCek;
            m_blob3 = signedCek;
            m_blob4 = finalcellCek;
            m_string0 = pathCek;
            m_cryptoVectorEncryptionType = CryptoVectorEncryptionType.Randomized;
            m_CryptNativeTestVectorType = CryptNativeTestVectorType.Rsa;
        }

        /// <summary>
        /// Constructor for RSA Key pair (special case for RSA)
        /// </summary>
        /// <param name="rsaKeyPair"></param>
        public CryptoVector(byte[] rsaKeyPair, CryptNativeTestVectorType cryptNativeTestVectorType)
        {
            Debug.Assert(rsaKeyPair != null);

            m_blob0 = rsaKeyPair;
            m_blob1 = null;
            m_blob2 = null;
            m_blob3 = null;
            m_blob4 = null;
            m_cryptoVectorEncryptionType = CryptoVectorEncryptionType.Randomized;
            m_CryptNativeTestVectorType = cryptNativeTestVectorType;
            m_string0 = null;
        }
    }
}
