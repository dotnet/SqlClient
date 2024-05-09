using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects.Packets
{
    internal class TdsLoginHandler : OutgoingPacketHandler
    {
        private enum States
        {
            None,
            LoginInformationWritten,

        }
        private TdsWriteStream _writeStream;

        /// <summary>
        /// These are the features requested by default. 
        /// </summary>
        internal LucidTdsEnums.FeatureExtension DefaultRequestedFeatures
        {
            get 
            {
                return LucidTdsEnums.FeatureExtension.None  
                    | LucidTdsEnums.FeatureExtension.GlobalTransactions
                    | LucidTdsEnums.FeatureExtension.DataClassification
                    | LucidTdsEnums.FeatureExtension.Tce
                    | LucidTdsEnums.FeatureExtension.UTF8Support
                    | LucidTdsEnums.FeatureExtension.SQLDNSCaching;
            }
        }

        internal LucidTdsEnums.FeatureExtension RequestedFeatures { get; private set; }

        protected override byte PacketHeaderType => TdsEnums.MT_LOGIN7;

        // We want to make sure that no one can write the data out of order.
        // We will use the state to make sure that each build function can be 
        // called in order that is expected.
        private States _currentState = States.None;

        /// <summary>
        /// Creates a login Handler to send out sending the login packet.
        /// </summary>
        /// <param name="writeStream"></param>
        /// <param name="additionalFeatures"></param>
        public TdsLoginHandler(TdsWriteStream writeStream,
            LucidTdsEnums.FeatureExtension additionalFeatures = LucidTdsEnums.FeatureExtension.None) 
        {
            _writeStream = writeStream;
            _writeStream.PacketHeaderType = PacketHeaderType;
            RequestedFeatures = DefaultRequestedFeatures | additionalFeatures;
        }
        
        public async ValueTask<TdsLoginHandler> SetLoginInformation(
            LoginPacket packet, 
            bool isAsync, 
            CancellationToken ct)
        {
            Debug.Assert(_currentState == States.None, "The Set Login information cannot be called if the state is already initialized");
            //TdsEnums.FeatureExtension requestedFeatures = TdsEnums.FeatureExtension.None;
            //requestedFeatures |= TdsEnums.FeatureExtension.GlobalTransactions
            //    | TdsEnums.FeatureExtension.DataClassification
            //    | TdsEnums.FeatureExtension.Tce
            //    | TdsEnums.FeatureExtension.UTF8Support
            //    | TdsEnums.FeatureExtension.SQLDNSCaching;

            packet.RequestedFeatures = RequestedFeatures;
            packet.FeatureExtensionData.requestedFeatures = RequestedFeatures;

            int length = packet.Length;
            await _writeStream.WriteIntAsync(length, isAsync, ct).ConfigureAwait(false);
            // Write TDS Version. We support 7.4
            await _writeStream.WriteIntAsync(packet.ProtocolVersion, isAsync, ct).ConfigureAwait(false);
            // Negotiate the packet size.
            await _writeStream.WriteIntAsync(packet.PacketSize, isAsync, ct).ConfigureAwait(false);
            // Client Prog Version
            await _writeStream.WriteIntAsync(packet.ClientProgramVersion, isAsync, ct).ConfigureAwait(false);
            // Current Process Id
            await _writeStream.WriteIntAsync(packet.ProcessIdForTdsLogin, isAsync, ct).ConfigureAwait(false);
            // Unused Connection Id 
            await _writeStream.WriteIntAsync(0, isAsync, ct).ConfigureAwait(false);

            _currentState = States.LoginInformationWritten;
            return this;
        }

        //public async ValueTask Send(bool isAsync, CancellationToken ct)
        //{
            
        //}
    }
}
