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
        public ColumnEncryptionData ColumnEncryptionFeature { get; internal set; }

        public FedAuthFeature FedAuthFeature { get; internal set;}

        public UTF8SupportFeature UTF8SupportFeature { get; internal set; }
        public DataClassificationFeature DataClassificationFeature { get; internal set; }
        public HashSet<byte> FeatureIdList { get; internal set; } = new HashSet<byte>();

        public bool IsFeatureSupported ( byte featureId ) => FeatureIdList.Contains(featureId);

        public void AddFeature(byte featureId, Span<byte> featureData)
        {
            if (IsFeatureSupported(featureId))
            {
                throw new InvalidOperationException($"FeatureId {featureId} is already added.");
            }
            switch (featureId)
            {
                case TdsEnums.FEATUREEXT_TCE:
                    ColumnEncryptionFeature = new ColumnEncryptionData();
                    ColumnEncryptionFeature.SetAcknowledgedData(featureData);
                    break;
                case TdsEnums.FEATUREEXT_DATACLASSIFICATION:
                    DataClassificationFeature = new DataClassificationFeature().SetAcknowledgedData(featureData);
                    break;
                case TdsEnums.FEATUREEXT_UTF8SUPPORT
                default:
                    throw new NotImplementedException($"FeatureId {featureId} is not implemented.");
            }
        }

    }
}
