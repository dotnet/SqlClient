// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Data.SqlClient.DataClassification
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Label/*' />
    public sealed class Label
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Name/*' />
        public string Name { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Id/*' />
        public string Id { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/ctor/*' />
        public Label(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/InformationType/*' />
    public sealed class InformationType
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Name/*' />
        public string Name { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Id/*' />
        public string Id { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/ctor/*' />
        public InformationType(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/SensitivityRank/*' />
    public enum SensitivityRank
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/NotDefined/*' />
        NOT_DEFINED = -1,
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/None/*' />
        NONE = 0,
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Low/*' />
        LOW = 10,
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Medium/*' />
        MEDIUM = 20,
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/High/*' />
        HIGH = 30,
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Critical/*' />
        CRITICAL = 40
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityProperty/*' />
    public sealed class SensitivityProperty
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/Label/*' />
        public Label Label { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/InformationType/*' />
        public InformationType InformationType { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get; private set; }
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/ctor/*' />
        public SensitivityProperty(Label label, InformationType informationType, SensitivityRank sensitivityRank = SensitivityRank.NOT_DEFINED) // Default to NOT_DEFINED for backwards compatibility
        {
            Label = label;
            InformationType = informationType;
            SensitivityRank = sensitivityRank;
        }
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ColumnSensitivity/*' />
    public sealed class ColumnSensitivity
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/GetSensitivityProperties/*' />
        public ReadOnlyCollection<SensitivityProperty> SensitivityProperties { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ctor/*' />
        public ColumnSensitivity(IList<SensitivityProperty> sensitivityProperties)
        {
            SensitivityProperties = new ReadOnlyCollection<SensitivityProperty>(sensitivityProperties);
        }
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityClassification/*' />
    public sealed class SensitivityClassification
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/Labels/*' />
        public ReadOnlyCollection<Label> Labels { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/InformationTypes/*' />
        public ReadOnlyCollection<InformationType> InformationTypes { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ColumnSensitivities/*' />
        public ReadOnlyCollection<ColumnSensitivity> ColumnSensitivities { get; private set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ctor/*' />
        public SensitivityClassification(IList<Label> labels, IList<InformationType> informationTypes, IList<ColumnSensitivity> columnSensitivity, SensitivityRank sensitivityRank = SensitivityRank.NOT_DEFINED) // Default to NOT_DEFINED for backwards compatibility
        {
            Labels = new ReadOnlyCollection<Label>(labels);
            InformationTypes = new ReadOnlyCollection<InformationType>(informationTypes);
            ColumnSensitivities = new ReadOnlyCollection<ColumnSensitivity>(columnSensitivity);
            SensitivityRank = sensitivityRank;
        }
    }
}
