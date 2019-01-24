// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.DataClassification {

    public class Label {
        public string Name { get; private set; }
        public string Id { get; private set; }

        public Label(string name, string id) {
            Name = name;
            Id = id;
        }
    }

    public class InformationType {
        public string Name { get; private set; }
        public string Id { get; private set; }

        public InformationType(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    public class SensitivityProperty {
        public Label Label { get; private set; }
        public InformationType InformationType { get; private set; }

        public SensitivityProperty(Label label, InformationType informationType) {
            Label = label;
            InformationType = informationType;
        }
    }

    public class ColumnSensitivity {
        public ReadOnlyCollection<SensitivityProperty> SensitivityProperties { get; private set; }

        public ColumnSensitivity(IList<SensitivityProperty> sensitivityProperties) {
            SensitivityProperties = new ReadOnlyCollection<SensitivityProperty>(sensitivityProperties);
        }
    }

    public class SensitivityClassification {
        public ReadOnlyCollection<Label> Labels { get; private set; }
        public ReadOnlyCollection<InformationType> InformationTypes { get; private set; }
        public ReadOnlyCollection<ColumnSensitivity> ColumnSensitivities { get; private set; }

        public SensitivityClassification(IList<Label> labels, IList<InformationType> informationTypes, IList<ColumnSensitivity> columnSensitivity) {
            Labels = new ReadOnlyCollection<Label>(labels);
            InformationTypes = new ReadOnlyCollection<InformationType>(informationTypes);
            ColumnSensitivities = new ReadOnlyCollection<ColumnSensitivity>(columnSensitivity);
        }
    }
}
