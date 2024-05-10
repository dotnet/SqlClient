using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;

namespace simplesqlclient
{
    internal class ProtocolMetadata
    {
        public ProtocolMetadata()
        {
        }

        public SqlCollation Collation { get; internal set; }

        public int DefaultCodePage { get; internal set; }

        public Encoding DefaultEncoding { get; internal set; }
        public IServerFeature ColumnEncryptionFeature { get; internal set; }

        public IServerFeature FedAuthFeature { get; internal set;}

        public IServerFeature UTF8SupportFeature { get; internal set; }
        public IServerFeature DataClassificationFeature { get; internal set; }
        public HashSet<byte> FeatureIdList { get; internal set; } = new HashSet<byte>();
        public SQLDNSCachingFeature SQLDNSCachingFeature { get; private set; }
        

        public bool IsFeatureSupported ( byte featureId ) => FeatureIdList.Contains(featureId);

        public void AddFeature(byte featureId, Span<byte> featureData)
        {
            if (IsFeatureSupported(featureId))
            {
                throw new InvalidOperationException($"FeatureId {featureId} is already added.");
            }
            
            FeatureIdList.Add(featureId);
            switch (featureId)
            {
                case LucidTdsEnums.FEATUREEXT_TCE:
                    ColumnEncryptionFeature = new ColumnEncryptionData();
                    ColumnEncryptionFeature.SetAcknowledgedData(featureData);
                    break;
                case LucidTdsEnums.FEATUREEXT_DATACLASSIFICATION:
                    DataClassificationFeature = new DataClassificationFeature();
                    DataClassificationFeature.SetAcknowledgedData(featureData);
                    break;
                case LucidTdsEnums.FEATUREEXT_UTF8SUPPORT:
                    UTF8SupportFeature = new UTF8SupportFeature();
                    UTF8SupportFeature.SetAcknowledgedData(featureData);
                    break;
                case LucidTdsEnums.FEATUREEXT_FEDAUTH:
                    FedAuthFeature = new FedAuthFeature();
                    FedAuthFeature.SetAcknowledgedData(featureData);
                    break;
                case LucidTdsEnums.FEATUREEXT_SQLDNSCACHING:
                    SQLDNSCachingFeature = new SQLDNSCachingFeature();
                    SQLDNSCachingFeature.SetAcknowledgedData(featureData);
                    break;
                default:
                    throw new NotImplementedException($"FeatureId {featureId} is not implemented.");
            }
        }

    }
}
