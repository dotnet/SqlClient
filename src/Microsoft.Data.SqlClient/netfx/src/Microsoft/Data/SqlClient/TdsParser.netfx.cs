using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;
using System.Net;
using System.Threading;
using Interop.Windows.Sni;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class TdsParser
    {
        // This is called from a ThreadAbort - ensure that it can be run from a CER Catch
        internal void BestEffortCleanup()
        {
            _state = TdsParserState.Broken;

            var stateObj = _physicalStateObj;
            if (stateObj != null)
            {
                var stateObjHandle = stateObj.Handle;
                if (stateObjHandle != null)
                {
                    stateObjHandle.Dispose();
                }
            }

            if (_fMARS)
            {
                var sessionPool = _sessionPool;
                if (sessionPool != null)
                {
                    sessionPool.BestEffortCleanup();
                }

                var marsStateObj = _pMarsPhysicalConObj;
                if (marsStateObj != null)
                {
                    var marsStateObjHandle = marsStateObj.Handle;
                    if (marsStateObjHandle != null)
                    {
                        marsStateObjHandle.Dispose();
                    }
                }
            }
        }

        // Retrieve the IP and port number from native SNI for TCP protocol. The IP information is stored temporarily in the
        // pendingSQLDNSObject but not in the DNS Cache at this point. We only add items to the DNS Cache after we receive the
        // IsSupported flag as true in the feature ext ack from server.
        internal void AssignPendingDNSInfo(string userProtocol, string DNSCacheKey)
        {
            uint result;
            ushort portFromSNI = 0;
            string IPStringFromSNI = string.Empty;
            IPAddress IPFromSNI;
            isTcpProtocol = false;
            Provider providerNumber = Provider.INVALID_PROV;

            if (string.IsNullOrEmpty(userProtocol))
            {

                result = SniNativeWrapper.SniGetProviderNumber(_physicalStateObj.Handle, ref providerNumber);
                Debug.Assert(result == TdsEnums.SNI_SUCCESS, "Unexpected failure state upon calling SniGetProviderNumber");
                isTcpProtocol = (providerNumber == Provider.TCP_PROV);
            }
            else if (userProtocol == TdsEnums.TCP)
            {
                isTcpProtocol = true;
            }

            // serverInfo.UserProtocol could be empty
            if (isTcpProtocol)
            {
                result = SniNativeWrapper.SniGetConnectionPort(_physicalStateObj.Handle, ref portFromSNI);
                Debug.Assert(result == TdsEnums.SNI_SUCCESS, "Unexpected failure state upon calling SniGetConnectionPort");


                result = SniNativeWrapper.SniGetConnectionIpString(_physicalStateObj.Handle, ref IPStringFromSNI);
                Debug.Assert(result == TdsEnums.SNI_SUCCESS, "Unexpected failure state upon calling SniGetConnectionIPString");

                _connHandler.pendingSQLDNSObject = new SQLDNSInfo(DNSCacheKey, null, null, portFromSNI.ToString());

                if (IPAddress.TryParse(IPStringFromSNI, out IPFromSNI))
                {
                    if (System.Net.Sockets.AddressFamily.InterNetwork == IPFromSNI.AddressFamily)
                    {
                        _connHandler.pendingSQLDNSObject.AddrIPv4 = IPStringFromSNI;
                    }
                    else if (System.Net.Sockets.AddressFamily.InterNetworkV6 == IPFromSNI.AddressFamily)
                    {
                        _connHandler.pendingSQLDNSObject.AddrIPv6 = IPStringFromSNI;
                    }
                }
            }
            else
            {
                _connHandler.pendingSQLDNSObject = null;
            }
        }

        // @TODO: Consider adopting this pattern for all usages of Run and rename to Run.
        internal bool RunReliably(RunBehavior runBehavior, SqlCommand cmdHandler, SqlDataReader dataStream, BulkCopySimpleResultSet bulkCopyHandler, TdsParserStateObject stateObj)
        {
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                return Run(runBehavior, cmdHandler, dataStream, bulkCopyHandler, stateObj);
            }
            catch (OutOfMemoryException)
            {
                _connHandler.DoomThisConnection();
                throw;
            }
            catch (StackOverflowException)
            {
                _connHandler.DoomThisConnection();
                throw;
            }
            catch (ThreadAbortException)
            {
                _connHandler.DoomThisConnection();
                throw;
            }
        }
    }
}
