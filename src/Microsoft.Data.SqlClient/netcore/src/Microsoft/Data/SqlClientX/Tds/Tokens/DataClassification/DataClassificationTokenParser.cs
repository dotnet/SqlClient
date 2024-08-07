// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;
using Microsoft.Data.SqlClient.DataClassification;

namespace Microsoft.Data.SqlClientX.Tds.Tokens.DataClassification
{
    internal class DataClassificationTokenParser : TokenParser
    {
        public override async ValueTask<Token> ParseAsync(TokenType tokenType, TdsStream tdsStream, bool isAsync, CancellationToken ct)
        {
            if (tdsStream.DataClassificationVersion == 0)
            {
                throw SQL.ParsingError(ParsingErrorState.DataClassificationNotExpected);
            }

            // get the labels
            ushort numLabels = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);

            List<Label> labels = new List<Label>(numLabels);
            for (ushort i = 0; i < numLabels; i++)
            {
                var labelLen = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                var label = await tdsStream.TdsReader.ReadStringAsync(labelLen, isAsync, ct).ConfigureAwait(false);
                var idLen = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                var id = await tdsStream.TdsReader.ReadStringAsync(idLen, isAsync, ct).ConfigureAwait(false);
                labels.Add(new Label(label, id));
            }

            // get the information types
            ushort numInformationTypes = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct);

            List<InformationType> informationTypes = new List<InformationType>(numInformationTypes);
            for (ushort i = 0; i < numInformationTypes; i++)
            {
                var informationTypeLen = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                var informationType = await tdsStream.TdsReader.ReadStringAsync(informationTypeLen, isAsync, ct).ConfigureAwait(false);
                var idLen = await tdsStream.TdsReader.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
                var id = await tdsStream.TdsReader.ReadStringAsync(idLen, isAsync, ct).ConfigureAwait(false);
                informationTypes.Add(new InformationType(informationType, id));
            }

            // get sensitivity rank
            int sensitivityRank = (int)SensitivityRank.NOT_DEFINED;
            if (tdsStream.DataClassificationVersion > TdsEnums.DATA_CLASSIFICATION_VERSION_WITHOUT_RANK_SUPPORT)
            {
                var rank = await tdsStream.TdsReader.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                if (!Enum.IsDefined(typeof(SensitivityRank), rank))
                {
                    return null;
                }
            }

            // get the per column classification data (corresponds to order of output columns for query)
            var numResultColumns = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
            List<ColumnSensitivity> columnSensitivities = new List<ColumnSensitivity>(numResultColumns);
            for (ushort columnNum = 0; columnNum < numResultColumns; columnNum++)
            {
                // get sensitivity properties for all the different sources which were used in generating the column output
                var numSources = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                List<SensitivityProperty> sensitivityProperties = new List<SensitivityProperty>(numSources);
                for (ushort sourceNum = 0; sourceNum < numSources; sourceNum++)
                {
                    // get the label index and then lookup label to use for source
                    var labelIndex = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                    Label label = null;
                    if (labelIndex != ushort.MaxValue)
                    {
                        if (labelIndex >= labels.Count)
                        {
                            throw SQL.ParsingError(ParsingErrorState.DataClassificationInvalidLabelIndex);
                        }
                        label = labels[labelIndex];
                    }

                    // get the information type index and then lookup information type to use for source
                    var informationTypeIndex = await tdsStream.TdsReader.ReadUInt16Async(isAsync, ct).ConfigureAwait(false);
                    InformationType informationType = null;
                    if (informationTypeIndex != ushort.MaxValue)
                    {
                        if (informationTypeIndex >= informationTypes.Count)
                        {
                            throw SQL.ParsingError(ParsingErrorState.DataClassificationInvalidInformationTypeIndex);
                        }
                        informationType = informationTypes[informationTypeIndex];
                    }

                    // get sensitivity rank
                    int sensitivityRankProperty = (int)SensitivityRank.NOT_DEFINED;
                    if (tdsStream.DataClassificationVersion > TdsEnums.DATA_CLASSIFICATION_VERSION_WITHOUT_RANK_SUPPORT)
                    {
                        var rankProperty = await tdsStream.TdsReader.ReadInt32Async(isAsync, ct).ConfigureAwait(false);
                        if (!Enum.IsDefined(typeof(SensitivityRank), rankProperty))
                        {
                            return null;
                        }
                    }

                    // add sensitivity properties for the source
                    sensitivityProperties.Add(new SensitivityProperty(label, informationType, (SensitivityRank)sensitivityRankProperty));
                }
                columnSensitivities.Add(new ColumnSensitivity(sensitivityProperties));
            }

            return new DataClassificationToken(new SensitivityClassification(labels, informationTypes, columnSensitivities, (SensitivityRank)sensitivityRank));
        }
    }
}
