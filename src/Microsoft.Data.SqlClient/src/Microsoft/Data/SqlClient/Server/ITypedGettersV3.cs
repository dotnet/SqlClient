// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;

namespace Microsoft.Data.SqlClient.Server
{
    // Interface for strongly-typed value getters
    internal interface ITypedGettersV3
    {
        // Null test
        //      valid for all types
        bool IsDBNull(int ordinal);

        // Check what type current sql_variant value is
        //      valid for SqlDbType.Variant
        SmiMetaData GetVariantType(int ordinal);

        //
        //  Actual value accessors
        //      valid type indicates column must be of the type or column must be variant 
        //           and GetVariantType returned the type
        //

        //  valid for SqlDbType.Bit
        bool GetBoolean(int ordinal);

        //  valid for SqlDbType.TinyInt
        byte GetByte(int ordinal);

        // valid for SqlDbTypes: Binary, VarBinary, Image, Udt, Xml, Char, VarChar, Text, NChar, NVarChar, NText
        //  (Character type support needed for ExecuteXmlReader handling)
        long GetBytesLength(int ordinal);
        int GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length);

        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        long GetCharsLength(int ordinal);
        int GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length);
        string GetString(int ordinal);

        // valid for SqlDbType.SmallInt
        short GetInt16(int ordinal);

        // valid for SqlDbType.Int
        int GetInt32(int ordinal);

        // valid for SqlDbType.BigInt, SqlDbType.Money, SqlDbType.SmallMoney
        long GetInt64(int ordinal);

        // valid for SqlDbType.Real
        float GetSingle(int ordinal);

        // valid for SqlDbType.Float
        double GetDouble(int ordinal);

        // valid for SqlDbType.Numeric (uses SqlDecimal since Decimal cannot hold full range)
        SqlDecimal GetSqlDecimal(int ordinal);

        // valid for DateTime & SmallDateTime
        DateTime GetDateTime(int ordinal);

        // valid for UniqueIdentifier
        Guid GetGuid(int ordinal);
    }
}

