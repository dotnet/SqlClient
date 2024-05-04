using System.Diagnostics;
using System.IO;
using Microsoft.Data.SqlClient.SqlClientX.Streams;

namespace simplesqlclient
{
    internal class Utilities
    {
        private const int NoProcessId = -1;

        private static int s_currentProcessId = NoProcessId;

        internal static int GetCurrentProcessIdForTdsLoginOnly()
        {
            if (s_currentProcessId == NoProcessId)
            {
                // Pick up the process Id from the current process instead of randomly generating it.
                // This would be helpful while tracing application related issues.
                int processId;
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess())
                {
                    processId = p.Id;
                }
                System.Threading.Volatile.Write(ref s_currentProcessId, processId);
            }
            return s_currentProcessId;
        }

        internal static byte[] ObfuscatePassword(string password)
        {
            byte[] bObfuscated = new byte[password.Length << 1];
            int s;
            byte bLo;
            byte bHi;

            for (int i = 0; i < password.Length; i++)
            {
                s = (int)password[i];
                bLo = (byte)(s & 0xff);
                bHi = (byte)((s >> 8) & 0xff);
                bObfuscated[i << 1] = (byte)((((bLo & 0x0f) << 4) | (bLo >> 4)) ^ 0xa5);
                bObfuscated[(i << 1) + 1] = (byte)((((bHi & 0x0f) << 4) | (bHi >> 4)) ^ 0xa5);
            }
            return bObfuscated;
        }

        internal static bool IsVarTimeTds(byte tdsType) => tdsType == TdsEnums.SQLTIME || tdsType == TdsEnums.SQLDATETIME2 || tdsType == TdsEnums.SQLDATETIMEOFFSET;

        internal static int GetSpecialTokenLength(byte tokenType, TdsReadStream stream)
        {
            int length = 0;
            switch (tokenType)
            {
                // Handle special tokens.
                case TdsTokens.SQLFEATUREEXTACK:
                    length = -1;
                    
                    break;
                case TdsTokens.SQLSESSIONSTATE:
                    length = stream.ReadInt32();
                    break;
                case TdsTokens.SQLFEDAUTHINFO:
                    length = stream.ReadInt32();
                    break;
                case TdsTokens.SQLUDT:
                case TdsTokens.SQLRETURNVALUE:
                    length = -1;
                    break;
                case TdsTokens.SQLXMLTYPE:
                    length = stream.ReadUInt16();
                    break;
                default:
                    Debug.Assert(false, "Unknown token length!");
                    break;
            }
            return length;
        }
    }


}
