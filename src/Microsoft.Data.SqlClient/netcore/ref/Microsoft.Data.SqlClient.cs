// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// NOTE: The current Microsoft.VSDesigner editor attributes are implemented for System.Data.SqlClient, and are not publicly available.
// New attributes that are designed to work with Microsoft.Data.SqlClient and are publicly documented should be included in future.

namespace Microsoft.Data.SqlClient.DataClassification
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ColumnSensitivity/*' />
    public partial class ColumnSensitivity
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ctor/*' />
        public ColumnSensitivity(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> sensitivityProperties) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/GetSensitivityProperties/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.SensitivityProperty> SensitivityProperties { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/InformationType/*' />
    public partial class InformationType
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/ctor/*' />
        public InformationType(string name, string id) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Id/*' />
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/InformationType.xml' path='docs/members[@name="InformationType"]/Name/*' />
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Label/*' />
    public partial class Label
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/ctor/*' />
        public Label(string name, string id) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Id/*' />
        public string Id { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/Label.xml' path='docs/members[@name="Label"]/Name/*' />
        public string Name { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/SensitivityRank/*' />
    public enum SensitivityRank
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/NotDefined/*' />
        NOT_DEFINED = -1,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/None/*' />
        NONE = 0,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Low/*' />
        LOW = 10,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Medium/*' />
        MEDIUM = 20,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/High/*' />
        HIGH = 30,
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityRank.xml' path='docs/members[@name="SensitivityRank"]/Critical/*' />
        CRITICAL = 40
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityClassification/*' />
    public partial class SensitivityClassification
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ctor/*' />
        public SensitivityClassification(System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.Label> labels, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.InformationType> informationTypes, System.Collections.Generic.IList<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> columnSensitivity, SensitivityRank sensitivityRank) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ColumnSensitivities/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.ColumnSensitivity> ColumnSensitivities { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/InformationTypes/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.InformationType> InformationTypes { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/Labels/*' />
        public System.Collections.ObjectModel.ReadOnlyCollection<Microsoft.Data.SqlClient.DataClassification.Label> Labels { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get { throw null; } }
    }
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityProperty/*' />
    public partial class SensitivityProperty
    {
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/ctor/*' />
        public SensitivityProperty(Microsoft.Data.SqlClient.DataClassification.Label label, Microsoft.Data.SqlClient.DataClassification.InformationType informationType, SensitivityRank sensitivityRank) { }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/InformationType/*' />
        public Microsoft.Data.SqlClient.DataClassification.InformationType InformationType { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/Label/*' />
        public Microsoft.Data.SqlClient.DataClassification.Label Label { [System.Runtime.CompilerServices.CompilerGeneratedAttribute] get { throw null; } }
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlClient.DataClassification/SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityRank/*' />
        public SensitivityRank SensitivityRank { get { throw null; } }
    }
}
