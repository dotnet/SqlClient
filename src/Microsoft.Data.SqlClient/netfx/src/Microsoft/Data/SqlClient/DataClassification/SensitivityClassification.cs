// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Data.SqlClient.DataClassification
{
    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\Label.xml' path='docs/members[@name="Label"]/Label/*' />
    public class Label
    {
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\Label.xml' path='docs/members[@name="Label"]/Name/*' />
        public string Name { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\Label.xml' path='docs/members[@name="Label"]/Id/*' />
        public string Id { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\Label.xml' path='docs/members[@name="Label"]/ctor/*' />
        public Label(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\InformationType.xml' path='docs/members[@name="InformationType"]/InformationType/*' />
    public class InformationType
    {
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\InformationType.xml' path='docs/members[@name="InformationType"]/Name/*' />
        public string Name { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\InformationType.xml' path='docs/members[@name="InformationType"]/Id/*' />
        public string Id { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\InformationType.xml' path='docs/members[@name="InformationType"]/ctor/*' />
        public InformationType(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/SensitivityProperty/*' />
    public class SensitivityProperty
    {
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/Label/*' />
        public Label Label { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/InformationType/*' />
        public InformationType InformationType { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityProperty.xml' path='docs/members[@name="SensitivityProperty"]/ctor/*' />
        public SensitivityProperty(Label label, InformationType informationType)
        {
            Label = label;
            InformationType = informationType;
        }
    }

    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ColumnSensitivity/*' />
    public class ColumnSensitivity
    {
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/GetSensitivityProperties/*' />
        public ReadOnlyCollection<SensitivityProperty> SensitivityProperties { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\ColumnSensitivity.xml' path='docs/members[@name="ColumnSensitivity"]/ctor/*' />
        public ColumnSensitivity(IList<SensitivityProperty> sensitivityProperties)
        {
            SensitivityProperties = new ReadOnlyCollection<SensitivityProperty>(sensitivityProperties);
        }
    }

    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/SensitivityClassification/*' />
    public class SensitivityClassification
    {
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/Labels/*' />
        public ReadOnlyCollection<Label> Labels { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/InformationTypes/*' />
        public ReadOnlyCollection<InformationType> InformationTypes { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ColumnSensitivities/*' />
        public ReadOnlyCollection<ColumnSensitivity> ColumnSensitivities { get; private set; }

        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.DataClassification\SensitivityClassification.xml' path='docs/members[@name="SensitivityClassification"]/ctor/*' />
        public SensitivityClassification(IList<Label> labels, IList<InformationType> informationTypes, IList<ColumnSensitivity> columnSensitivity)
        {
            Labels = new ReadOnlyCollection<Label>(labels);
            InformationTypes = new ReadOnlyCollection<InformationType>(informationTypes);
            ColumnSensitivities = new ReadOnlyCollection<ColumnSensitivity>(columnSensitivity);
        }
    }
}
