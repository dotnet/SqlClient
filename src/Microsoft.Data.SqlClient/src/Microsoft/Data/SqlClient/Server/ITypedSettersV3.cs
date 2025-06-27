// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;

namespace Microsoft.Data.SqlClient.Server
{
    // interface for strongly-typed value setters
    internal interface ITypedSettersV3
    {
        // By value setters (data copy across the interface boundary implied)
        //  All setters are valid for SqlDbType.Variant

        // SetVariantMetaData is used to set the precise type of data just before pushing 
        //  data into a variant type via one of the other setters. It 
        //  is only valid to set metadata with a SqlDbType that associated with the 
        //  data setter that will be called.
        //  Since LOBs, Udt's and fixed-length types are not currently stored in a variant, 
        //  the following pairs are the only setters/sqldbtypes that need this call:
        //      NVarChar/VarChar + SetString (needed only for non-global collation, i.e. SqlString)
        //      Money/SmallMoney + SetInt64
        void SetVariantMetaData(int ordinal, SmiMetaData metaData);

        // Set value to null
        //  valid for all types
        void SetDBNull(int ordinal);

        //  valid for SqlDbType.Bit
        void SetBoolean(int ordinal, bool value);

        //  valid for SqlDbType.TinyInt
        void SetByte(int ordinal, byte value);

        // Semantics for SetBytes are to modify existing value, not overwrite
        //  Use in combination with SetLength to ensure overwriting when necessary
        // valid for SqlDbTypes: Binary, VarBinary, Image, Udt, Xml
        //      (VarBinary assumed for variants)
        int SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length);
        void SetBytesLength(int ordinal, long length);

        // Semantics for SetChars are to modify existing value, not overwrite
        //  Use in combination with SetLength to ensure overwriting when necessary
        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        //      (NVarChar and global clr collation assumed for variants)
        int SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length);
        void SetCharsLength(int ordinal, long length);

        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        void SetString(int ordinal, string value, int offset, int length);

        // valid for SqlDbType.SmallInt
        void SetInt16(int ordinal, short value);

        // valid for SqlDbType.Int
        void SetInt32(int ordinal, int value);

        // valid for SqlDbType.BigInt, SqlDbType.Money, SqlDbType.SmallMoney
        void SetInt64(int ordinal, long value);

        // valid for SqlDbType.Real
        void SetSingle(int ordinal, float value);

        // valid for SqlDbType.Float
        void SetDouble(int ordinal, double value);

        // valid for SqlDbType.Numeric (uses SqlDecimal since Decimal cannot hold full range)
        void SetSqlDecimal(int ordinal, SqlDecimal value);

        // valid for DateTime & SmallDateTime
        void SetDateTime(int ordinal, DateTime value);

        // valid for UniqueIdentifier
        void SetGuid(int ordinal, Guid value);
    }
}

