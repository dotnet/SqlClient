// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.DataClassification;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.DataClassification
{
    /// <summary>
    /// Token indicating data classification information received for classified columns.
    /// </summary>
    internal class DataClassificationToken : Token
    {
        /// <summary>
        /// Token type.
        /// </summary>
        public override TokenType Type => TokenType.DataClassification;

        /// <summary>
        /// Sensitivity Classification data received from server.
        /// </summary>
        public SensitivityClassification SensitivityClassification;

        /// <summary>
        /// Create a new instance with sensitivity classification information received.
        /// </summary>
        /// <param name="sensitivityClassification"></param>
        public DataClassificationToken(SensitivityClassification sensitivityClassification)
        {
            SensitivityClassification = sensitivityClassification;
        }

        /// <summary>
        /// Gets a human readable string representation of this token.
        /// </summary>
        /// <returns>Human readable string representation.</returns>
        public override string ToString()
        {
            return $"DataClassificationToken[Labels Count={SensitivityClassification.Labels.Count})," +
                $"InformationTypes Count={SensitivityClassification.InformationTypes.Count}," +
                $"SensitivityRank={SensitivityClassification.SensitivityRank}," +
                $"ColumnSensitivities Count={SensitivityClassification.ColumnSensitivities.Count}]";
        }
    }
}
