// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal sealed class TdsParserStaticMethods
    {

        private TdsParserStaticMethods() { /* prevent utility class from being insantiated*/ }
        //
        // Static methods
        //

        // SxS: this method accesses registry to resolve the alias.
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        static internal void AliasRegistryLookup(ref string host, ref string protocol)
        {
            if (!string.IsNullOrEmpty(host))
            {
                const String folder = "SOFTWARE\\Microsoft\\MSSQLServer\\Client\\ConnectTo";
                // Put a try...catch... around this so we don't abort ANY connection if we can't read the registry.
                string aliasLookup = (string)ADP.LocalMachineRegistryValue(folder, host);
                if (!string.IsNullOrEmpty(aliasLookup))
                {
                    /* Result will be in the form of: "DBNMPNTW,\\server\pipe\sql\query". or
                         Result will be in the form of: "DBNETLIB, via:\\server\pipe\sql\query".

                        supported formats:
                            tcp	- DBMSSOCN,[server|server\instance][,port]
                            np - DBNMPNTW,[\\server\pipe\sql\query | \\server\pipe\MSSQL$instance\sql\query]
                                  where \sql\query is the pipename and can be replaced with any other pipe name
                            via - [DBMSGNET,server,port | DBNETLIB, via:server, port]
                            sm - DBMSLPCN,server

                        unsupported formats:
                            rpc - DBMSRPCN,server,[parameters] where parameters could be "username,password"
                            bv -  DBMSVINN,service@group@organization
                            appletalk - DBMSADSN,objectname@zone
                            spx - DBMSSPXN,[service | address,port,network]
                    */
                    // We must parse into the two component pieces, then map the first protocol piece to the
                    // appropriate value.
                    int index = aliasLookup.IndexOf(',');

                    // If we found the key, but there was no "," in the string, it is a bad Alias so return.
                    if (-1 != index)
                    {
                        string parsedProtocol = aliasLookup.Substring(0, index).ToLower(CultureInfo.InvariantCulture);

                        // If index+1 >= length, Alias consisted of "FOO," which is a bad alias so return.
                        if (index + 1 < aliasLookup.Length)
                        {
                            string parsedAliasName = aliasLookup.Substring(index + 1);

                            // Fix bug 298286
                            if ("dbnetlib" == parsedProtocol)
                            {
                                index = parsedAliasName.IndexOf(':');
                                if (-1 != index && index + 1 < parsedAliasName.Length)
                                {
                                    parsedProtocol = parsedAliasName.Substring(0, index);
                                    if (SqlConnectionString.ValidProtocol(parsedProtocol))
                                    {
                                        protocol = parsedProtocol;
                                        host = parsedAliasName.Substring(index + 1);
                                    }
                                }
                            }
                            else
                            {
                                protocol = (string)SqlConnectionString.NetlibMapping()[parsedProtocol];
                                if (protocol != null)
                                {
                                    host = parsedAliasName;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Obfuscate password to be sent to SQL Server
        // Blurb from the TDS spec at https://msdn.microsoft.com/en-us/library/dd304523.aspx
        // "Before submitting a password from the client to the server, for every byte in the password buffer 
        // starting with the position pointed to by IbPassword, the client SHOULD first swap the four high bits 
        // with the four low bits and then do a bit-XOR with 0xA5 (10100101). After reading a submitted password, 
        // for every byte in the password buffer starting with the position pointed to by IbPassword, the server SHOULD 
        // first do a bit-XOR with 0xA5 (10100101) and then swap the four high bits with the four low bits."
        // The password exchange during Login phase happens over a secure channel i.e. SSL/TLS 
        // Note: The same logic is used in SNIPacketSetData (SniManagedWrapper) to encrypt passwords stored in SecureString
        //       If this logic changed, SNIPacketSetData needs to be changed as well
        internal static byte[] ObfuscatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return Array.Empty<byte>();
            }
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

        internal static byte[] ObfuscatePassword(byte[] password)
        {
            if (password == null || password.Length == 0)
            {
                return Array.Empty<byte>();
            }
            byte bLo;
            byte bHi;

            for (int i = 0; i < password.Length; i++)
            {
                bLo = (byte)(password[i] & 0x0f);
                bHi = (byte)(password[i] & 0xf0);
                password[i] = (byte)(((bHi >> 4) | (bLo << 4)) ^ 0xa5);
            }
            return password;
        }

        private const int NoProcessId = -1;
        private static int s_currentProcessId = NoProcessId;
        internal static int GetCurrentProcessIdForTdsLoginOnly()
        {
            if (s_currentProcessId == NoProcessId)
            {
                // Pick up the process Id from the current process instead of randomly generating it.
                // This would be helpful while tracing application related issues.
                int processId;
#if NET
                processId = Environment.ProcessId;
#else
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess())
                {
                    processId = p.Id;
                }
#endif
                System.Threading.Volatile.Write(ref s_currentProcessId, processId);
            }
            return s_currentProcessId;
        }


        internal static int GetCurrentThreadIdForTdsLoginOnly()
        {
            return Environment.CurrentManagedThreadId;
        }

        private static byte[] s_nicAddress = null;

        [ResourceExposure(ResourceScope.None)] // SxS: we use MAC address for TDS login only
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        static internal byte[] GetNetworkPhysicalAddressForTdsLoginOnly()
        {
            if (s_nicAddress != null)
            {
                return s_nicAddress;
            }

            byte[] nicAddress = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // NIC address is stored in NetworkAddress key.  However, if NetworkAddressLocal key
                // has a value that is not zero, then we cannot use the NetworkAddress key and must
                // instead generate a random one.  I do not fully understand why, this is simply what
                // the native providers do.  As for generation, I use a random number generator, which
                // means that different processes on the same machine will have different NIC address
                // values on the server.  It is not ideal, but native does not have the same value for
                // different processes either.

                const string key = "NetworkAddress";
                const string localKey = "NetworkAddressLocal";
                const string folder = "SOFTWARE\\Description\\Microsoft\\Rpc\\UuidTemporaryData";

                int result = 0;

                object temp = ADP.LocalMachineRegistryValue(folder, localKey);
                if (temp is int)
                {
                    result = (int)temp;
                }

                if (result <= 0)
                {
                    temp = ADP.LocalMachineRegistryValue(folder, key);
                    if (temp is byte[])
                    {
                        nicAddress = (byte[])temp;
                    }
                }
            }

            if (nicAddress == null)
            {
                nicAddress = new byte[TdsEnums.MAX_NIC_SIZE];
                Random random = new Random();
                random.NextBytes(nicAddress);
            }

            System.Threading.Interlocked.CompareExchange(ref s_nicAddress, nicAddress, null);

            return s_nicAddress;
        }

        // translates remaining time in stateObj (from user specified timeout) to timeout value for SNI
        internal static int GetTimeoutMilliseconds(long timeoutTime)
        {
            // User provided timeout t | timeout value for SNI | meaning
            // ------------------------+-----------------------+------------------------------
            //      t == long.MaxValue |                    -1 | infinite timeout (no timeout)
            //   t>0 && t<int.MaxValue |                     t |
            //          t>int.MaxValue |          int.MaxValue | must not exceed int.MaxValue

            if (long.MaxValue == timeoutTime)
            {
                return -1;  // infinite timeout
            }

            long msecRemaining = ADP.TimerRemainingMilliseconds(timeoutTime);

            if (msecRemaining < 0)
            {
                return 0;
            }
            if (msecRemaining > (long)int.MaxValue)
            {
                return int.MaxValue;
            }
            return (int)msecRemaining;
        }

        internal static long GetTimeout(long timeoutMilliseconds)
        {
            long result;
            if (timeoutMilliseconds <= 0)
            {
                result = long.MaxValue; // no timeout...
            }
            else
            {
                try
                {
                    result = checked(ADP.TimerCurrent() + ADP.TimerFromMilliseconds(timeoutMilliseconds));
                }
                catch (OverflowException)
                {
                    // In case of overflow, set to 'infinite' timeout
                    result = long.MaxValue;
                }
            }
            return result;
        }

        internal static long GetTimeoutSeconds(int timeout) => GetTimeout((long)timeout * 1000L);

        internal static bool TimeoutHasExpired(long timeoutTime)
        {
            bool result = false;

            if (0 != timeoutTime && long.MaxValue != timeoutTime)
            {
                result = ADP.TimerHasExpired(timeoutTime);
            }
            return result;
        }

        internal static int NullAwareStringLength(string str)
        {
            if (str == null)
            {
                return 0;
            }
            else
            {
                return str.Length;
            }
        }

        internal static int GetRemainingTimeout(int timeout, long start)
        {
            if (timeout <= 0)
            {
                return timeout;
            }
            long remaining = ADP.TimerRemainingSeconds(start + ADP.TimerFromSeconds(timeout));
            if (remaining <= 0)
            {
                return 1;
            }
            else
            {
                return checked((int)remaining);
            }
        }
    }
}
