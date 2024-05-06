using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.SqlClient;
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
            bool specialToken = false;
            int length = 0;
            switch (tokenType)
            {
                // Handle special tokens.
                case TdsTokens.SQLFEATUREEXTACK:
                    length = -1;
                    specialToken = true;
                    break;
                case TdsTokens.SQLSESSIONSTATE:
                    length = stream.ReadInt32();
                    specialToken = true;
                    break;
                case TdsTokens.SQLFEDAUTHINFO:
                    length = stream.ReadInt32();
                    specialToken = true;
                    break;
                case TdsTokens.SQLUDT:
                case TdsTokens.SQLRETURNVALUE:
                    length = -1;
                    specialToken = true;
                    break;
                case TdsTokens.SQLXMLTYPE:
                    length = stream.ReadUInt16();
                    specialToken = true;
                    break;

                default:
                    specialToken = false;
                    break;
            }

            int tokenLength = 0;
            if (!specialToken)
            {
                switch (tokenType & TdsEnums.SQLLenMask)
                {
                    case TdsEnums.SQLFixedLen:
                        tokenLength = (0x01 << ((tokenType & 0x0c) >> 2)) & 0xff;
                        break;
                    case TdsEnums.SQLZeroLen:
                        tokenLength = 0;
                        break;
                    case TdsEnums.SQLVarLen:
                    case TdsEnums.SQLVarCnt:
                        if (0 != (tokenType & 0x80))
                        {
                            tokenLength = stream.ReadUInt16();
                            break;
                        }
                        else if (0 == (tokenType & 0x0c))
                        {
                            tokenLength = stream.ReadInt32();
                            break;
                        }
                        else
                        {
                            byte value = stream.ReadByteCast();
                            break;
                        }
                    default:
                        Debug.Fail("Unknown token length!");
                        tokenLength = 0;
                        break;
                }
                length = tokenLength;
            }
            // Read the length

            // Read the data

            return length;
        }

        internal static bool IsNull(MetaType mt, int length)
        {
            // null bin and char types have a length of -1 to represent null
            if (mt.IsPlp)
            {
                throw new NotImplementedException("PLP not implemented");
            }

            // HOTFIX #50000415: for image/text, 0xFFFF is the length, not representing null
            if ((TdsEnums.VARNULL == length) && !mt.IsLong)
            {
                return true;
            }

            // other types have a length of 0 to represent null
            // long and non-PLP types will always return false because these types are either char or binary
            // this is expected since for long and non-plp types isnull is checked based on textptr field and not the length
            return ((TdsEnums.FIXEDNULL == length) && !mt.IsCharType && !mt.IsBinType);
        }
    }


}
