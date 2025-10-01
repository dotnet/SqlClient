using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient.Utilities;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    
    internal partial class TdsParser
    {
        internal void ProcessSSPI(int receivedLength)
        {
            Debug.Assert(_authenticationProvider is not null);

            SniContext outerContext = _physicalStateObj.SniContext;
            _physicalStateObj.SniContext = SniContext.Snix_ProcessSspi;

            // allocate received buffer based on length from SSPI message
            byte[] receivedBuff = ArrayPool<byte>.Shared.Rent(receivedLength);

            try
            {
                // read SSPI data received from server
                Debug.Assert(_physicalStateObj._syncOverAsync, "Should not attempt pends in a synchronous call");
                TdsOperationStatus result = _physicalStateObj.TryReadByteArray(receivedBuff, receivedLength);
                if (result != TdsOperationStatus.Done)
                {
                    throw SQL.SynchronousCallMayNotPend();
                }

                // allocate send buffer and initialize length
                var writer = ObjectPools.BufferWriter.Rent();

                try
                {
                    // make call for SSPI data
                    _authenticationProvider!.WriteSSPIContext(receivedBuff.AsSpan(0, receivedLength), writer);

                    // DO NOT SEND LENGTH - TDS DOC INCORRECT!  JUST SEND SSPI DATA!
                    _physicalStateObj.WriteByteSpan(writer.WrittenSpan);

                }
                finally
                {
                    ObjectPools.BufferWriter.Return(writer);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(receivedBuff, clearArray: true);
            }

            // set message type so server knows its a SSPI response
            _physicalStateObj._outputMessageType = TdsEnums.MT_SSPI;

            // send to server
            _physicalStateObj.WritePacket(TdsEnums.HARDFLUSH);
            _physicalStateObj.SniContext = outerContext;
        }

#nullable disable
    }
}
