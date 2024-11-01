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

        /// <summary>
        /// True if null.
        /// </summary>
        private bool _isNull;         
        
        private readonly string? _jsonString;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor1/*' />
        public SqlJson()
        {
            SetNull();
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor2/*' />
        public SqlJson(string? jsonString) 
        {
            if (jsonString == null)
            {
                SetNull();
            }
            else
            {
                // TODO: We need to validate the Json before storing it.
                ValidateJson(jsonString);
                _jsonString = jsonString;
            }
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/ctor3/*' />
        public SqlJson(JsonDocument? jsonDoc) 
        {
            if (jsonDoc == null)
            {
                SetNull();
            }
            else
            {
                _jsonString = jsonDoc.RootElement.GetRawText();
            }
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlJson.xml' path='docs/members[@name="SqlJson"]/IsNull/*' />
        public bool IsNull => _isNull;

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
                else
                {
                    return _jsonString!;
                }
            }
        }

        private void SetNull()
        {
            _isNull = true;
        }

        private static void ValidateJson(string jsonString)
        {
            // Convert the JSON string to a UTF-8 byte array
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);

            // Create a Utf8JsonReader instance
            var reader = new Utf8JsonReader(jsonBytes, isFinalBlock: true, state: default);

            // Read through the JSON data
            while (reader.Read())
            {
                // The Read method advances the reader to the next token
                // If the JSON is invalid, an exception will be thrown
            }
            // If we reach here, the JSON is valid
        }
    }
}
