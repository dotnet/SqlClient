// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents a collection of alternative metadata sets within a SQLALTMETADATA or SQLALTROW token.
/// </summary>
internal sealed class TdsAltMetadataCollection
{
    private readonly List<TdsColumnMetadataToken> _altMetadataSetArray;

    internal TdsAltMetadataCollection()
    {
        _altMetadataSetArray = new List<TdsColumnMetadataToken>();
    }

    /// <summary>
    /// Gets or sets the primary metadata set associated with an alternative metadata collection.
    /// </summary>
    internal TdsColumnMetadataToken MetadataSet { get; set; }

    /// <summary>
    /// Creates a deep copy of the current TdsAltMetadataCollection instance, including all its elements.
    /// </summary>
    public object Clone()
    {
        TdsAltMetadataCollection result = new() { MetadataSet = MetadataSet?.Clone() };

        foreach (TdsColumnMetadataToken set in _altMetadataSetArray)
        {
            result._altMetadataSetArray.Add(set.Clone());
        }

        return result;
    }

    /// <summary>
    /// Retrieves the alternate metadata set that corresponds to the specified identifier.
    /// </summary>
    /// <param name="id">The identifier of the alternate metadata set to retrieve.</param>
    /// <returns>
    /// The <see cref="TdsColumnMetadataToken"/> instance that matches the specified identifier,
    /// or null if no matching metadata set is found.
    /// </returns>
    internal TdsColumnMetadataToken GetAltMetadata(int id)
    {
        foreach (TdsColumnMetadataToken altMetaDataSet in _altMetadataSetArray)
        {
            if (altMetaDataSet.id == id)
            {
                return altMetaDataSet;
            }
        }

        // @TODO: Debug fail usage here isn't ideal.
        Debug.Fail("Can't match up altMetaDataSet with given id");
        return null;
    }

    /// <summary>
    /// Updates or adds an alternative metadata set to the collection.
    /// </summary>
    /// <param name="altMetadataSet">
    /// Alternative metadata set to update or add to the collection.
    /// </param>
    internal void SetAltMetadata(TdsColumnMetadataToken altMetadataSet)
    {
        // If alt metadata with same id is found, override it rather than adding a new one
        int newId = altMetadataSet.id;
        for (int i = 0; i < _altMetadataSetArray.Count; i++)
        {
            if (_altMetadataSetArray[i].id == newId)
            {
                // override the existing metadata with the same id
                _altMetadataSetArray[i] = altMetadataSet;
                return;
            }
        }

        // if we did not find metadata to override, add as new
        _altMetadataSetArray.Add(altMetadataSet);
    }
}
