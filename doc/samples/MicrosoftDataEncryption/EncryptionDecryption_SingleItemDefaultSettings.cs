//<Snippet1>
using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.Encryption.AzureKeyVaultProvider;
using Microsoft.Data.Encryption.Cryptography;

namespace EncryptionDecryption_SingleItemDefaultSettings
{
    public class Program
    {
        // Data encryption key literal - for reproducable results.
        public const string EncryptedDataEncryptionKey = "0x01CE000001680074007400700073003A002F002F006A0065007400720069006D006D0065002D006B00650079002D007600610075006C0074002E007600610075006C0074002E0061007A007500720065002E006E00650074002F006B006500790073002F0061006C0077006100790073002D0065006E0063007200790070007400650064002D006100750074006F0031002F00610037006400350063003900380039006400350061003000340032003300660038006400330035003600660031003900350035003300360065003500390030003F744470CADF69743A5DCBA65D6C7AE084C3798B4FE7585E9DF26A7B0E129E6764D847EFF9E4D61982458FED89EBC86EE5EE8C2EAC074D694BDFC15ACD675D291F32BCC645D24B20C5990D8322525505E5FF7BB1C241485B0CC984438D60A92C1FC7961CF11229F776F32E38AE0792B3221F60BD44495F371D88BCD2CAA99AAA74EC8E5FA685A93EC45D4F1975105ADA34DC6BE337E6D89990BD601AD94F98E3F21BFCE445C511166EB2DDDB4DD4B87462599ED186CC760F66F48B1E5BADD98A95552C68FE1810CBDEF8A2C8772C699DC1E1FA6DB277CCBE2DED558A679741E2C268BA0356C236FD3AAB645C448B088F0DC70F53D01CC1DF487F2BB73512593A31D827D232855C68ECF4CCE7CD4C595786F85231CAD59033A42832E5C99D581B8D0B4E39B07E49407483A2CDE9A4789067E4B0152D3ADF9F33A5DDD4598300A1EB3D716DA4981CEECA551944F54591613754B5CE8059B6AEFA3B63A0E5EFCD7BE97E8BDB2152508A0614740DAE9B53E460CE8D96D3683FD7561A2E30B5E17D98860838294355BF879FDC66AE47362FE9B1DA1D70C8EF1BDB94DD5450B6A68EE31D18D8CB2F8CD2FF5E6A4E28F33A05C9EE67F2389A385D021710725AE13E9A4389BC80258DF5FA30B17C2B4EBC0DC8C2700E215293B90F0208290120BC2579B14584834C09809A9327616B258C0DDD967826B43F8A38DA2AE34F981AC1CBEFA9";

        // Azure Key Vault URI
        public static readonly string KeyEncryptionKeyPath = Environment.GetEnvironmentVariable("AzureKeyVaultKeyPath", EnvironmentVariableTarget.User);

        // New Token Credential to authenticate to Azure
        public static readonly TokenCredential TokenCredential = new ClientSecretCredential(
            tenantId: Environment.GetEnvironmentVariable("AzureTenantId", EnvironmentVariableTarget.User),
            clientId: Environment.GetEnvironmentVariable("AzureClientId", EnvironmentVariableTarget.User),
            clientSecret: Environment.GetEnvironmentVariable("AzureClientSecret", EnvironmentVariableTarget.User)
        );

        // AKV provider that allows client applications to access a column master key is stored in Microsoft Azure Key Vault.
        public static readonly EncryptionKeyStoreProvider azureKeyProvider = new AzureKeyVaultKeyStoreProvider(TokenCredential);

        // Represents the master key that encrypts and decrypts the encryption key
        public static readonly KeyEncryptionKey keyEncryptionKey = new KeyEncryptionKey("MK", KeyEncryptionKeyPath, azureKeyProvider);

        // Represents the encryption key that encrypts and decrypts the data items
        public static readonly ProtectedDataEncryptionKey encryptionKey = ProtectedDataEncryptionKey.GetOrCreate("EK", keyEncryptionKey, EncryptedDataEncryptionKey.FromHexString());

        public static void Main()
        {
            Console.WriteLine("\n**** Original Value ****");

            // Declare value to be encrypted
            DateTime original = DateTime.Now;
            Console.WriteLine(original);



            Console.WriteLine("\n**** Encrypted Value ****");

            // Encrypt value and convert to HEX string
            var encryptedBytes = original.Encrypt(encryptionKey);
            var encryptedHexString = encryptedBytes.ToHexString();
            Console.WriteLine(encryptedHexString);



            Console.WriteLine("\n**** Decrypted Value ****");

            // Decrypt value back to original
            var bytesToDecrypt = encryptedHexString.FromHexString();
            var decryptedBytes = bytesToDecrypt.Decrypt<DateTime>(encryptionKey);

            Console.WriteLine(decryptedBytes);

            Console.Clear();
        }
    }
}
//<Snippet1>
