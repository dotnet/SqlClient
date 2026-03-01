// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for EBCDIC930 collation support (SQL_Japanese_180_EBCDIC930_CS_AS_UTF8).
    /// This collation requires trace flag 16240 to be enabled on the SQL Server instance.
    /// </summary>
    public class EBCDIC930CollationTest
    {
        private const string CollationName = "SQL_Japanese_180_EBCDIC930_CS_AS_UTF8";
        
        // Test data
        private const string KatakanaChar = "ア"; // U+30A2
        private const string HiraganaChar = "あ"; // U+3042  
        private const string KanjiChar = "漢"; // U+6F22
        private const string LowercaseChar = "a";
        private const string UppercaseChar = "A";
        private const string DigitChar = "5";
        
        /// <summary>
        /// Tests that EBCDIC930 collation produces the expected sort order:
        /// Katakana → Hiragana → Kanji → ASCII lowercase → ASCII uppercase → ASCII digits
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), 
            nameof(DataTestUtility.AreConnStringsSetup), 
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ValidateEBCDIC930SortOrder()
        {
            string dbName = DataTestUtility.GetShortName("EBCDIC930SortTest", false);
            string tableName = "SortOrderTest";

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                // Try to enable trace flag - skip test if it fails
                if (!TryEnableTraceFlag(connection))
                {
                    return; // Test skipped - trace flag not available
                }

                // Create database with EBCDIC930 collation
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE {CollationName};";
                    cmd.ExecuteNonQuery();
                }

                // Switch to the new database
                SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = dbName
                };

                using (SqlConnection dbConnection = new(builder.ConnectionString))
                {
                    dbConnection.Open();
                    
                    using (SqlCommand cmd = dbConnection.CreateCommand())
                    {
                        // Create test table
                        cmd.CommandText = $@"
                            CREATE TABLE {tableName} (
                                id INT IDENTITY(1,1) PRIMARY KEY,
                                test_char NVARCHAR(10) COLLATE {CollationName}
                            );";
                        cmd.ExecuteNonQuery();

                        // Insert test data
                        string[] testChars = { DigitChar, UppercaseChar, LowercaseChar, KanjiChar, HiraganaChar, KatakanaChar };
                        foreach (string ch in testChars)
                        {
                            cmd.CommandText = $"INSERT INTO {tableName} (test_char) VALUES (@char);";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@char", ch);
                            cmd.ExecuteNonQuery();
                        }

                        // Query with ORDER BY
                        cmd.CommandText = $"SELECT test_char FROM {tableName} ORDER BY test_char;";
                        cmd.Parameters.Clear();

                        List<string> sortedChars = new();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sortedChars.Add(reader.GetString(0));
                            }
                        }

                        // Expected EBCDIC930 order
                        string[] expectedOrder = { KatakanaChar, HiraganaChar, KanjiChar, LowercaseChar, UppercaseChar, DigitChar };
                        
                        Assert.Equal(expectedOrder.Length, sortedChars.Count);
                        for (int i = 0; i < expectedOrder.Length; i++)
                        {
                            Assert.Equal(expectedOrder[i], sortedChars[i]);
                        }
                    }
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(connection, dbName);
            }
        }

        /// <summary>
        /// Tests SqlParameter binding with Japanese strings in EBCDIC930 collated columns.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), 
            nameof(DataTestUtility.AreConnStringsSetup), 
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestParameterBindingWithEBCDIC930()
        {
            string dbName = DataTestUtility.GetShortName("EBCDIC930Param", false);
            string tableName = "ParameterTest";

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                if (!TryEnableTraceFlag(connection))
                {
                    return; // Test skipped
                }

                // Create database
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE {CollationName};";
                    cmd.ExecuteNonQuery();
                }

                SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = dbName
                };

                using (SqlConnection dbConnection = new(builder.ConnectionString))
                {
                    dbConnection.Open();

                    using (SqlCommand cmd = dbConnection.CreateCommand())
                    {
                        // Create test table
                        cmd.CommandText = $@"
                            CREATE TABLE {tableName} (
                                id INT PRIMARY KEY,
                                text_data NVARCHAR(100) COLLATE {CollationName}
                            );";
                        cmd.ExecuteNonQuery();

                        // Test INSERT with parameters
                        string japaneseText = "江戸糸あやつり人形";
                        cmd.CommandText = $"INSERT INTO {tableName} (id, text_data) VALUES (@id, @text);";
                        cmd.Parameters.AddWithValue("@id", 1);
                        cmd.Parameters.AddWithValue("@text", japaneseText);
                        cmd.ExecuteNonQuery();

                        // Test SELECT
                        cmd.CommandText = $"SELECT text_data FROM {tableName} WHERE id = @id;";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@id", 1);
                        string retrievedText = cmd.ExecuteScalar()?.ToString();
                        Assert.Equal(japaneseText, retrievedText);
                    }
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(connection, dbName);
            }
        }

        /// <summary>
        /// Comprehensive test for EBCDIC930 encoding with full-width, half-width, and various character types.
        /// Tests: Latin (full/half), Katakana (full/half), Hiragana, Kanji, Numerals (full/half), Symbols.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), 
            nameof(DataTestUtility.AreConnStringsSetup), 
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestEBCDIC930EncodingRoundTrip()
        {
            string dbName = DataTestUtility.GetShortName("EBCDIC930Encoding", false);
            string tableName = "EncodingTest";

            // Test data covering various character types
            var testData = new Dictionary<string, string>
            {
                // Half-width Latin
                { "HalfWidthLatin", "ABCabc123" },
                // Full-width Latin (Zenkaku)
                { "FullWidthLatin", "ＡＢＣａｂｃ１２３" },
                // Half-width Katakana
                { "HalfWidthKatakana", "ｱｲｳｴｵｶｷｸｹｺ" },
                // Full-width Katakana
                { "FullWidthKatakana", "アイウエオカキクケコ" },
                // Hiragana (no half-width equivalent)
                { "Hiragana", "あいうえおかきくけこ" },
                // Kanji
                { "Kanji", "漢字日本語東京大阪" },
                // Half-width numerals
                { "HalfWidthNumerals", "0123456789" },
                // Full-width numerals
                { "FullWidthNumerals", "０１２３４５６７８９" },
                // Half-width symbols
                { "HalfWidthSymbols", "!@#$%^&*()" },
                // Full-width symbols
                { "FullWidthSymbols", "！＠＃＄％＾＆＊（）" },
                // Mixed content - real-world example
                { "MixedContent", "東京都渋谷区１−２−３ＡＢＣビル４階" },
                // Special Japanese punctuation
                { "JapanesePunctuation", "、。「」『』・ー" },
                // Dakuten and Handakuten
                { "DakutenHandakuten", "がぎぐげごぱぴぷぺぽ" },
                // Katakana with dakuten
                { "KatakanaDakuten", "ガギグゲゴパピプペポ" }
            };

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                if (!TryEnableTraceFlag(connection))
                {
                    return; // Test skipped
                }

                // Create database
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE {CollationName};";
                    cmd.ExecuteNonQuery();
                }

                SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = dbName
                };

                using (SqlConnection dbConnection = new(builder.ConnectionString))
                {
                    dbConnection.Open();

                    using (SqlCommand cmd = dbConnection.CreateCommand())
                    {
                        // Create test table
                        cmd.CommandText = $@"
                            CREATE TABLE {tableName} (
                                category NVARCHAR(50) COLLATE {CollationName} PRIMARY KEY,
                                test_data NVARCHAR(200) COLLATE {CollationName},
                                data_length INT
                            );";
                        cmd.ExecuteNonQuery();

                        // Insert all test data
                        foreach (var kvp in testData)
                        {
                            cmd.CommandText = $"INSERT INTO {tableName} (category, test_data, data_length) VALUES (@category, @data, @length);";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@category", kvp.Key);
                            cmd.Parameters.AddWithValue("@data", kvp.Value);
                            cmd.Parameters.AddWithValue("@length", kvp.Value.Length);
                            cmd.ExecuteNonQuery();
                        }

                        // Retrieve and validate all data
                        cmd.CommandText = $"SELECT category, test_data, data_length FROM {tableName} ORDER BY category;";
                        cmd.Parameters.Clear();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int rowCount = 0;
                            while (reader.Read())
                            {
                                string category = reader.GetString(0);
                                string retrievedData = reader.GetString(1);
                                int retrievedLength = reader.GetInt32(2);
                                
                                // Verify data exists in test set
                                Assert.True(testData.ContainsKey(category), $"Unexpected category: {category}");
                                
                                string expectedData = testData[category];
                                
                                // Validate exact match
                                Assert.Equal(expectedData, retrievedData);
                                
                                // Validate length is preserved
                                Assert.Equal(expectedData.Length, retrievedLength);
                                Assert.Equal(expectedData.Length, retrievedData.Length);
                                
                                // Validate character-by-character
                                for (int i = 0; i < expectedData.Length; i++)
                                {
                                    Assert.Equal(expectedData[i], retrievedData[i]);
                                }
                                
                                rowCount++;
                            }
                            
                            // Ensure all test cases were retrieved
                            Assert.Equal(testData.Count, rowCount);
                        }

                        // Test parameterized WHERE clause with various encodings
                        foreach (var kvp in testData)
                        {
                            cmd.CommandText = $"SELECT test_data FROM {tableName} WHERE test_data = @searchData;";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@searchData", kvp.Value);
                            
                            string result = cmd.ExecuteScalar()?.ToString();
                            Assert.Equal(kvp.Value, result);
                        }
                    }
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(connection, dbName);
            }
        }

        /// <summary>
        /// Tests EBCDIC930 collation across different SQL Server data types (CHAR, VARCHAR, NCHAR, NVARCHAR, and MAX variants).
        /// Validates that NVARCHAR properly handles Unicode data regardless of collation.
        /// Note: CHAR/VARCHAR with EBCDIC collations use code page conversion, NCHAR/NVARCHAR use Unicode.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), 
            nameof(DataTestUtility.AreConnStringsSetup), 
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestEBCDIC930DataTypes()
        {
            string dbName = DataTestUtility.GetShortName("EBCDIC930DataTypes", false);
            string tableName = "DataTypeTest";

            // Test data with various Japanese characters
            var testData = new Dictionary<string, string>
            {
                { "Simple", "ABC123" },
                { "Katakana", "アイウエオ" },
                { "Hiragana", "あいうえお" },
                { "Kanji", "漢字日本" },
                { "Mixed", "東京ABC123" },
                { "FullWidth", "ＡＢＣ１２３" },
                { "HalfKata", "ｱｲｳｴｵ" },
                { "Complex", "東京都渋谷区1-2-3" }
            };

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                if (!TryEnableTraceFlag(connection))
                {
                    return;
                }

                // Create database
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE {CollationName};";
                    cmd.ExecuteNonQuery();
                }

                SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = dbName
                };

                using (SqlConnection dbConnection = new(builder.ConnectionString))
                {
                    dbConnection.Open();

                    using (SqlCommand cmd = dbConnection.CreateCommand())
                    {
                        // Create table with various data types
                        cmd.CommandText = $@"
                            CREATE TABLE {tableName} (
                                id INT PRIMARY KEY,
                                test_name NVARCHAR(50),
                                -- Unicode types (N prefix) - recommended for Japanese
                                nchar_col NCHAR(50) COLLATE {CollationName},
                                nvarchar_col NVARCHAR(50) COLLATE {CollationName},
                                nvarchar_max_col NVARCHAR(MAX) COLLATE {CollationName},
                                -- Non-Unicode types - use code page conversion
                                varchar_col VARCHAR(200) COLLATE {CollationName},
                                varchar_max_col VARCHAR(MAX) COLLATE {CollationName},
                                -- Store lengths for validation
                                nvarchar_len AS LEN(nvarchar_col),
                                varchar_len AS LEN(varchar_col)
                            );";
                        cmd.ExecuteNonQuery();

                        // Insert test data into all column types
                        int id = 1;
                        foreach (var kvp in testData)
                        {
                            cmd.CommandText = $@"
                                INSERT INTO {tableName} 
                                (id, test_name, nchar_col, nvarchar_col, nvarchar_max_col, varchar_col, varchar_max_col)
                                VALUES (@id, @name, @nchar, @nvarchar, @nvarchar_max, @varchar, @varchar_max);";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@id", id++);
                            cmd.Parameters.AddWithValue("@name", kvp.Key);
                            cmd.Parameters.AddWithValue("@nchar", kvp.Value);
                            cmd.Parameters.AddWithValue("@nvarchar", kvp.Value);
                            cmd.Parameters.AddWithValue("@nvarchar_max", kvp.Value);
                            cmd.Parameters.AddWithValue("@varchar", kvp.Value);
                            cmd.Parameters.AddWithValue("@varchar_max", kvp.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // Retrieve and validate data across all types
                        cmd.CommandText = $@"
                            SELECT test_name, 
                                   nchar_col, nvarchar_col, nvarchar_max_col,
                                   varchar_col, varchar_max_col,
                                   CAST(nvarchar_len AS INT) AS nvarchar_len, 
                                   CAST(varchar_len AS INT) AS varchar_len,
                                   DATALENGTH(nvarchar_col) AS nvarchar_bytes,
                                   DATALENGTH(varchar_col) AS varchar_bytes,
                                   COLLATIONPROPERTY('{CollationName}', 'CodePage') AS collation_codepage
                            FROM {tableName} 
                            ORDER BY id;";
                        cmd.Parameters.Clear();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string testName = reader.GetString(0);
                                string expectedData = testData[testName];

                                // Read all column types
                                string ncharData = reader.GetString(1).TrimEnd();  // NCHAR is fixed-length, trim padding
                                string nvarcharData = reader.GetString(2);
                                string nvarcharMaxData = reader.GetString(3);
                                string varcharData = reader.GetString(4);
                                string varcharMaxData = reader.GetString(5);

                                int nvarcharLen = reader.GetInt32(6);
                                int varcharLen = reader.GetInt32(7);
                                int nvarcharBytes = Convert.ToInt32(reader.GetValue(8));
                                int varcharBytes = Convert.ToInt32(reader.GetValue(9));
                                int codePage = reader.GetInt32(10);
                                
                                // Validate UTF-8 code page for EBCDIC930_UTF8 collation
                                Assert.Equal(65001, codePage); // UTF-8 code page

                                // NVARCHAR types should preserve Unicode exactly
                                Assert.Equal(expectedData, ncharData);
                                Assert.Equal(expectedData, nvarcharData);
                                Assert.Equal(expectedData, nvarcharMaxData);

                                // VARCHAR types use UTF-8 encoding (Code Page 65001)
                                // Should also preserve data correctly
                                Assert.Equal(expectedData, varcharData);
                                Assert.Equal(expectedData, varcharMaxData);

                                // Validate lengths
                                Assert.Equal(expectedData.Length, nvarcharLen);
                                Assert.Equal(expectedData.Length, varcharLen);

                                // NVARCHAR uses 2 bytes per character (UTF-16LE)
                                Assert.Equal(expectedData.Length * 2, nvarcharBytes);

                                // VARCHAR with UTF-8 uses 1-4 bytes per character depending on Unicode range
                                // Validate UTF-8 byte length matches expectations
                                byte[] utf8Bytes = Encoding.UTF8.GetBytes(expectedData);
                                Assert.Equal(utf8Bytes.Length, varcharBytes);
                                
                                // For ASCII: 1 byte, Japanese: typically 3 bytes
                                Assert.True(varcharBytes >= expectedData.Length); // At least 1 byte per char
                            }
                        }

                        // Test sorting behavior across data types
                        cmd.CommandText = $@"
                            SELECT nvarchar_col FROM {tableName} 
                            WHERE test_name IN ('Simple', 'Katakana', 'Hiragana', 'Kanji')
                            ORDER BY nvarchar_col;";
                        cmd.Parameters.Clear();

                        List<string> sortedNVarchar = new();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sortedNVarchar.Add(reader.GetString(0));
                            }
                        }

                        // Test VARCHAR sorting
                        cmd.CommandText = $@"
                            SELECT varchar_col FROM {tableName} 
                            WHERE test_name IN ('Simple', 'Katakana', 'Hiragana', 'Kanji')
                            ORDER BY varchar_col;";

                        List<string> sortedVarchar = new();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                sortedVarchar.Add(reader.GetString(0));
                            }
                        }

                        // Both should produce same EBCDIC930 sort order
                        Assert.Equal(sortedNVarchar, sortedVarchar);

                        // Test MAX types with larger data
                        string largeText = string.Join("", System.Linq.Enumerable.Repeat("日本語テスト", 100));
                        cmd.CommandText = $@"
                            INSERT INTO {tableName} 
                            (id, test_name, nchar_col, nvarchar_col, nvarchar_max_col, varchar_col, varchar_max_col)
                            VALUES (999, 'LargeData', @small, @small, @large, @small, @large);";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@small", "Test");
                        cmd.Parameters.AddWithValue("@large", largeText);
                        cmd.ExecuteNonQuery();

                        // Validate MAX columns handle large data
                        cmd.CommandText = $@"
                            SELECT nvarchar_max_col, varchar_max_col, 
                                   CAST(LEN(nvarchar_max_col) AS INT) AS nvarchar_len,
                                   CAST(LEN(varchar_max_col) AS INT) AS varchar_len
                            FROM {tableName} WHERE id = 999;";
                        cmd.Parameters.Clear();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string nvarcharMaxRetrieved = reader.GetString(0);
                                string varcharMaxRetrieved = reader.GetString(1);
                                int nvarcharMaxLen = reader.GetInt32(2);
                                int varcharMaxLen = reader.GetInt32(3);

                                Assert.Equal(largeText, nvarcharMaxRetrieved);
                                Assert.Equal(largeText, varcharMaxRetrieved);
                                Assert.Equal(largeText.Length, nvarcharMaxLen);
                                Assert.Equal(largeText.Length, varcharMaxLen);
                            }
                        }
                    }
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(connection, dbName);
            }
        }

        /// <summary>
        /// Tests client-side encoding of data retrieved from EBCDIC930 collated columns.
        /// Validates that data is transmitted as Unicode (UTF-16LE) and verifies byte-level encoding.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), 
            nameof(DataTestUtility.AreConnStringsSetup), 
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestEBCDIC930ClientSideEncoding()
        {
            string dbName = DataTestUtility.GetShortName("EBCDIC930ClientEnc", false);
            string tableName = "EncodingInspection";

            // Test characters with known Unicode code points
            var testCharacters = new Dictionary<string, (string Char, int CodePoint, string Description)>
            {
                { "Katakana_A", ("ア", 0x30A2, "Full-width Katakana A") },
                { "Hiragana_A", ("あ", 0x3042, "Hiragana A") },
                { "Kanji_Han", ("漢", 0x6F22, "Kanji 'Han'") },
                { "FullWidth_A", ("Ａ", 0xFF21, "Full-width Latin A") },
                { "HalfWidth_A", ("A", 0x0041, "Half-width Latin A") },
                { "HalfKata_A", ("ｱ", 0xFF71, "Half-width Katakana A") },
                { "FullDigit_1", ("１", 0xFF11, "Full-width digit 1") },
                { "HalfDigit_1", ("1", 0x0031, "Half-width digit 1") },
                { "JapPunct_Comma", ("、", 0x3001, "Japanese comma") },
                { "Dakuten_Ga", ("が", 0x304C, "Hiragana GA with dakuten") }
            };

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                if (!TryEnableTraceFlag(connection))
                {
                    return;
                }

                // Create database
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE {CollationName};";
                    cmd.ExecuteNonQuery();
                }

                SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = dbName
                };

                using (SqlConnection dbConnection = new(builder.ConnectionString))
                {
                    dbConnection.Open();

                    using (SqlCommand cmd = dbConnection.CreateCommand())
                    {
                        // Create table
                        cmd.CommandText = $@"
                            CREATE TABLE {tableName} (
                                id NVARCHAR(50) COLLATE {CollationName} PRIMARY KEY,
                                test_char NVARCHAR(10) COLLATE {CollationName},
                                char_binary VARBINARY(20)
                            );";
                        cmd.ExecuteNonQuery();

                        // Insert test data with binary representation
                        foreach (var kvp in testCharacters)
                        {
                            cmd.CommandText = $@"
                                INSERT INTO {tableName} (id, test_char, char_binary) 
                                VALUES (@id, @char, CAST(@char AS VARBINARY(20)));";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@id", kvp.Key);
                            cmd.Parameters.AddWithValue("@char", kvp.Value.Char);
                            cmd.ExecuteNonQuery();
                        }

                        // Retrieve and validate encoding
                        cmd.CommandText = $"SELECT id, test_char, char_binary FROM {tableName} ORDER BY id;";
                        cmd.Parameters.Clear();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string id = reader.GetString(0);
                                string retrievedChar = reader.GetString(1);
                                byte[] sqlBinary = (byte[])reader.GetValue(2);

                                var expected = testCharacters[id];

                                // 1. Validate character roundtrip
                                Assert.Equal(expected.Char, retrievedChar);

                                // 2. Validate Unicode code point
                                char[] chars = retrievedChar.ToCharArray();
                                Assert.Single(chars); // Should be single character
                                int codePoint = char.ConvertToUtf32(retrievedChar, 0);
                                Assert.Equal(expected.CodePoint, codePoint);

                                // 3. Validate client-side encoding (UTF-16LE for .NET strings)
                                byte[] clientBytes = Encoding.Unicode.GetBytes(retrievedChar);
                                
                                // 4. Validate SQL Server stored it as UTF-16LE (NVARCHAR uses UCS-2/UTF-16LE)
                                // SQL Server NVARCHAR stores data as UTF-16LE
                                Assert.Equal(sqlBinary.Length, clientBytes.Length);
                                Assert.Equal(sqlBinary, clientBytes);

                                // 5. Verify UTF-16LE byte order (little-endian)
                                // For code points < 0x10000, UTF-16LE uses 2 bytes: low byte, high byte
                                if (expected.CodePoint < 0x10000)
                                {
                                    byte lowByte = (byte)(expected.CodePoint & 0xFF);
                                    byte highByte = (byte)((expected.CodePoint >> 8) & 0xFF);
                                    Assert.Equal(lowByte, clientBytes[0]);
                                    Assert.Equal(highByte, clientBytes[1]);
                                }
                            }
                        }

                        // Test encoding consistency across operations
                        foreach (var kvp in testCharacters)
                        {
                            // Test: Can we search using the character?
                            cmd.CommandText = $"SELECT test_char FROM {tableName} WHERE test_char = @search;";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@search", kvp.Value.Char);
                            string found = cmd.ExecuteScalar()?.ToString();
                            Assert.Equal(kvp.Value.Char, found);

                            // Test: Does collation affect comparison?
                            cmd.CommandText = $@"
                                SELECT COLLATIONPROPERTY(@collation, 'CodePage') AS CodePage,
                                       COLLATIONPROPERTY(@collation, 'LCID') AS LCID,
                                       UNICODE(@char) AS UnicodeValue;";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@collation", CollationName);
                            cmd.Parameters.AddWithValue("@char", kvp.Value.Char);
                            
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    // EBCDIC930 uses UTF-8 as the code page for storage
                                    int codePage = reader.GetInt32(0);
                                    int lcid = reader.GetInt32(1);
                                    int unicodeValue = reader.GetInt32(2);
                                    
                                    // Validate Unicode value matches expected code point
                                    Assert.Equal(kvp.Value.CodePoint, unicodeValue);
                                    
                                    // EBCDIC930_UTF8 collation should use UTF-8 code page (65001)
                                    Assert.Equal(65001, codePage);
                                }
                            }
                        }

                        // Test byte-level roundtrip through binary column
                        foreach (var kvp in testCharacters)
                        {
                            cmd.CommandText = $@"
                                SELECT CAST(char_binary AS NVARCHAR(10)) AS FromBinary,
                                       test_char AS Original
                                FROM {tableName} WHERE id = @id;";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@id", kvp.Key);
                            
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string fromBinary = reader.GetString(0);
                                    string original = reader.GetString(1);
                                    
                                    // Validate binary->string conversion matches original
                                    Assert.Equal(original, fromBinary);
                                    Assert.Equal(kvp.Value.Char, fromBinary);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(connection, dbName);
            }
        }

        /// <summary>
        /// Tests for EBCDIC Shift In (SI=0x0F) and Shift Out (SO=0x0E) control characters.
        /// In traditional EBCDIC encoding, these bytes switch between single-byte and double-byte modes.
        /// This test validates that SQL Server does NOT insert these control characters, even with EBCDIC930 collation.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), 
            nameof(DataTestUtility.AreConnStringsSetup), 
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestEBCDIC930NoShiftCharacters()
        {
            string dbName = DataTestUtility.GetShortName("EBCDIC930Shift", false);
            string tableName = "ShiftCharTest";

            // EBCDIC control characters
            const byte SHIFT_OUT = 0x0E;  // SO - Switch to double-byte mode
            const byte SHIFT_IN = 0x0F;   // SI - Switch to single-byte mode

            // Test data that would require SI/SO in traditional EBCDIC
            var testData = new Dictionary<string, string>
            {
                // ASCII → Japanese transitions (would need SO before Japanese)
                { "ASCII_to_Kana", "ABC" + "アイウ" },  // ABC<SO>アイウ in traditional EBCDIC
                { "Kana_to_ASCII", "アイウ" + "ABC" },  // アイウ<SI>ABC in traditional EBCDIC
                { "Mixed_Multiple", "Test" + "日本語" + "Data" + "漢字" },
                
                // Single-byte → Multi-byte → Single-byte
                { "Complex_Mix", "A" + "あ" + "B" + "い" + "C" },
                
                // Pure Japanese (would be SO...SI wrapped in traditional EBCDIC)
                { "Pure_Japanese", "これは日本語のテストです" },
                
                // Edge cases
                { "Start_Japanese", "日本語Test" },
                { "End_Japanese", "Test日本語" },
                { "Alternating", "A" + "あ" + "B" + "い" + "C" + "う" }
            };

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                if (!TryEnableTraceFlag(connection))
                {
                    return;
                }

                // Create database
                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE [{dbName}] COLLATE {CollationName};";
                    cmd.ExecuteNonQuery();
                }

                SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
                {
                    InitialCatalog = dbName
                };

                using (SqlConnection dbConnection = new(builder.ConnectionString))
                {
                    dbConnection.Open();

                    using (SqlCommand cmd = dbConnection.CreateCommand())
                    {
                        // Create table with various data types
                        cmd.CommandText = $@"
                            CREATE TABLE {tableName} (
                                id INT PRIMARY KEY,
                                test_name NVARCHAR(50),
                                nvarchar_data NVARCHAR(200) COLLATE {CollationName},
                                varchar_data VARCHAR(600) COLLATE {CollationName},
                                nvarchar_binary VARBINARY(400),
                                varchar_binary VARBINARY(1200)
                            );";
                        cmd.ExecuteNonQuery();

                        // Insert test data with binary representation
                        int id = 1;
                        foreach (var kvp in testData)
                        {
                            cmd.CommandText = $@"
                                INSERT INTO {tableName} 
                                (id, test_name, nvarchar_data, varchar_data, nvarchar_binary, varchar_binary)
                                VALUES (@id, @name, @nvarchar, @varchar, 
                                        CAST(@nvarchar AS VARBINARY(400)),
                                        CAST(@varchar AS VARBINARY(1200)));";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@id", id++);
                            cmd.Parameters.AddWithValue("@name", kvp.Key);
                            cmd.Parameters.AddWithValue("@nvarchar", kvp.Value);
                            cmd.Parameters.AddWithValue("@varchar", kvp.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // Retrieve and validate - check for SI/SO bytes
                        cmd.CommandText = $@"
                            SELECT test_name, nvarchar_data, varchar_data, 
                                   nvarchar_binary, varchar_binary
                            FROM {tableName} ORDER BY id;";
                        cmd.Parameters.Clear();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string testName = reader.GetString(0);
                                string nvarcharData = reader.GetString(1);
                                string varcharData = reader.GetString(2);
                                byte[] nvarcharBinary = (byte[])reader.GetValue(3);
                                byte[] varcharBinary = (byte[])reader.GetValue(4);

                                string expectedData = testData[testName];

                                // 1. Validate data roundtrip
                                Assert.Equal(expectedData, nvarcharData);
                                Assert.Equal(expectedData, varcharData);

                                // 2. Check NVARCHAR binary for SI/SO bytes
                                // UTF-16LE should NOT contain 0x0E or 0x0F as control characters
                                bool nvarcharHasShiftOut = Array.IndexOf(nvarcharBinary, SHIFT_OUT) >= 0;
                                bool nvarcharHasShiftIn = Array.IndexOf(nvarcharBinary, SHIFT_IN) >= 0;
                                
                                // Note: 0x0E and 0x0F might appear as part of UTF-16LE encoded characters,
                                // but NOT as standalone control bytes between character boundaries
                                // We validate they don't appear as standalone bytes in the expected positions

                                // 3. Check VARCHAR binary for SI/SO bytes  
                                // When CAST(VARCHAR AS VARBINARY), SQL Server converts to UTF-16LE
                                // So we check the actual VARCHAR bytes differently
                                bool varcharHasShiftOut = Array.IndexOf(varcharBinary, SHIFT_OUT) >= 0;
                                bool varcharHasShiftIn = Array.IndexOf(varcharBinary, SHIFT_IN) >= 0;

                                // 4. Decode the binary - SQL Server converts VARCHAR to UTF-16LE when casting to VARBINARY
                                string decodedVarcharBinary = Encoding.Unicode.GetString(varcharBinary);
                                Assert.Equal(expectedData, decodedVarcharBinary);
                                
                                // 5. Validate UTF-16LE encoding for NVARCHAR
                                string decodedUtf16 = Encoding.Unicode.GetString(nvarcharBinary);
                                Assert.Equal(expectedData, decodedUtf16);

                                // 6. Compare with client-side UTF-16LE encoding
                                byte[] clientUtf16 = Encoding.Unicode.GetBytes(expectedData);
                                
                                Assert.Equal(clientUtf16, nvarcharBinary);
                                Assert.Equal(clientUtf16, varcharBinary); // Both end up as UTF-16LE in VARBINARY

                                // 7. Validate: SQL Server does NOT insert SI/SO for mode switching
                                // Traditional EBCDIC would insert these, but neither UTF-8 nor UTF-16 do
                                Assert.False(nvarcharHasShiftOut, 
                                    $"NVARCHAR should not contain SHIFT OUT (0x0E) bytes. Test: {testName}");
                                Assert.False(nvarcharHasShiftIn, 
                                    $"NVARCHAR should not contain SHIFT IN (0x0F) bytes. Test: {testName}");
                                Assert.False(varcharHasShiftOut, 
                                    $"VARCHAR should not contain SHIFT OUT (0x0E) bytes. Test: {testName}");
                                Assert.False(varcharHasShiftIn, 
                                    $"VARCHAR should not contain SHIFT IN (0x0F) bytes. Test: {testName}");
                            }
                        }

                        // Additional test: Get raw UTF-8 bytes from VARCHAR using different method
                        cmd.CommandText = $@"
                            SELECT test_name, varchar_data,
                                   CONVERT(VARBINARY(MAX), varchar_data) AS varchar_hex
                            FROM {tableName} 
                            WHERE test_name = 'ASCII_to_Kana';";
                        cmd.Parameters.Clear();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string testName = reader.GetString(0);
                                string data = reader.GetString(1);
                                byte[] bytes = (byte[])reader.GetValue(2);
                                
                                // Expected: "ABCアイウ"  
                                // CONVERT(VARBINARY) converts VARCHAR to UTF-16LE
                                // UTF-16LE encoding:
                                // A = 0x41 0x00, B = 0x42 0x00, C = 0x43 0x00
                                // ア = 0xA2 0x30 (U+30A2)
                                
                                // Verify the transition from ASCII 'C' to Katakana 'ア'
                                // In traditional EBCDIC, there would be SO (0x0E) between them
                                // In UTF-16LE, they appear directly adjacent
                                
                                // Find where C ends and ア begins
                                // C = 0x43 0x00 should be at positions [4,5]
                                // ア = 0xA2 0x30 should be at positions [6,7]
                                
                                int posC = Array.IndexOf(bytes, (byte)0x43);
                                Assert.True(posC >= 0, "Expected to find 'C' (0x43) in byte array");
                                
                                // After the 0x43 byte, check what follows
                                if (posC + 3 < bytes.Length)
                                {
                                    byte afterC1 = bytes[posC + 1]; // Should be 0x00 (high byte of C)
                                    byte afterC2 = bytes[posC + 2]; // Should be 0xA2 or start of ア
                                    byte afterC3 = bytes[posC + 3]; // Should be part of ア
                                    
                                    // Key assertion: Verify NO shift out (0x0E) between C and ア
                                    Assert.NotEqual(SHIFT_OUT, afterC1);
                                    Assert.NotEqual(SHIFT_OUT, afterC2);
                                    
                                    // Also verify we don't have shift in (0x0F) anywhere nearby
                                    Assert.NotEqual(SHIFT_IN, afterC1);
                                    Assert.NotEqual(SHIFT_IN, afterC2);
                                    Assert.NotEqual(SHIFT_IN, afterC3);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                DataTestUtility.DropDatabase(connection, dbName);
            }
        }

        /// <summary>
        /// Helper method to enable trace flag 16240 for EBCDIC930 collation support.
        /// Returns false if the trace flag cannot be enabled (test should skip).
        /// </summary>
        private static bool TryEnableTraceFlag(SqlConnection connection)
        {
            using SqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = "DBCC TRACEON(16240, -1);";
            try
            {
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (SqlException ex)
            {
                // If trace flag cannot be enabled, skip the test
                if (ex.Message.Contains("permission") || ex.Message.Contains("not recognized"))
                {
                    return false;
                }
                throw;
            }
        }
    }
}
