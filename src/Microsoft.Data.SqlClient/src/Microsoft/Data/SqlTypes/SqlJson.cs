// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Data.SqlTypes;
using System.Text;
using System.Text.Json;

#nullable enable

namespace Microsoft.Data.SqlTypes
{
    /// <summary>
    /// Represents the Json Data type in SQL Server.
    /// </summary>
    public class SqlJson : INullable
    {

        /// <summary>
        /// True if null.
        /// </summary>
        private bool _isNull;         
        
        private readonly string? _jsonString;

        /// <summary>
        /// Parameterless constructor. Initializes a new instance of the SqlJson class which 
        /// represents a null JSON value.
        /// </summary>
        public SqlJson()
        {
            SetNull();
        }

        /// <summary>
        /// Takes a <see cref="string"/> as input and initializes a new instance of the SqlJson class.
        /// </summary>
        /// <param name="jsonString"></param>
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

        /// <summary>
        /// Takes a <see cref="JsonDocument"/> as input and initializes a new instance of the SqlJson class.
        /// </summary>
        /// <param name="jsonDoc"></param>
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

        /// <inheritdoc/>
        public bool IsNull => _isNull;

        /// <summary>
        /// Represents a null instance of the <see cref="SqlJson"/> type.
        /// </summary>
        public static SqlJson Null => new();

        /// <summary>
        /// Gets the string representation of the Json content of this <see cref="SqlJson" /> instance.
        /// </summary>
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

        static void ValidateJson(string jsonString)
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
