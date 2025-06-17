// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Data.SqlTypes;
using System.Text;
using System.Text.Json;

#nullable enable

namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/SqlJson/*' />
    public class SqlJson : INullable
    {
        // Our serialized JSON string, or null.
        private readonly string? _jsonString = null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor1/*' />
        public SqlJson()
        {
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor2/*' />
        public SqlJson(string? jsonString) 
        {
            if (jsonString == null)
            {
                return;
            }

            // Ask JsonDocument to parse it for validity, or throw.
            //
            // Note that we do not support trailing commas or comments in the
            // JSON.
            //
            JsonDocument.Parse(jsonString);

            _jsonString = jsonString;
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor3/*' />
        public SqlJson(JsonDocument? jsonDoc) 
        {
            if (jsonDoc == null)
            {
                return;
            }

            // Save the serialized JSON string from the document, or throw.
            _jsonString = jsonDoc.RootElement.GetRawText();
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/IsNull/*' />
        public bool IsNull => _jsonString is null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/Null/*' />
        public static SqlJson Null => new();

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/Value/*' />
        public string Value 
        { 
            get
            {
                if (IsNull)
                {
                    throw new SqlNullValueException();
                }

                return _jsonString!;
            }
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ToString/*' />
        public override string? ToString()
        {
            return _jsonString;
        }
    }
}
