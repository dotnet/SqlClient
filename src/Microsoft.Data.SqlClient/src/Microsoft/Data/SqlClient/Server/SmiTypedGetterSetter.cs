// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Server
{
    // Central interface for getting/setting data values from/to a set of values indexed by ordinal 
    //  (record, row, array, etc)
    //  Which methods are allowed to be called depends on SmiMetaData type of data offset.
    internal abstract class SmiTypedGetterSetter : ITypedGettersV3, ITypedSettersV3
    {
        #region Read/Write
        
        /// <summary>
        /// Are calls to Get methods allowed?
        /// </summary>
        protected abstract bool CanGet { get; }

        /// <summary>
        /// Are calls to Set methods allowed?
        /// </summary>
        protected abstract bool CanSet { get; }
        
        #endregion

        #region Getters
        // Null test
        //      valid for all types
        public virtual bool IsDBNull(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // Check what type current sql_variant value is
        //      valid for SqlDbType.Variant
        public virtual SmiMetaData GetVariantType(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        //  valid for SqlDbType.Bit
        public virtual bool GetBoolean(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        //  valid for SqlDbType.TinyInt
        public virtual byte GetByte(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbTypes: Binary, VarBinary, Image, Udt, Xml, Char, VarChar, Text, NChar, NVarChar, NText
        //  (Character type support needed for ExecuteXmlReader handling)
        public virtual long GetBytesLength(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }
        public virtual int GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        public virtual long GetCharsLength(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }
        public virtual int GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }
        public virtual string GetString(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.SmallInt
        public virtual short GetInt16(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Int
        public virtual int GetInt32(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.BigInt, SqlDbType.Money, SqlDbType.SmallMoney
        public virtual long GetInt64(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Real
        public virtual float GetSingle(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Float
        public virtual double GetDouble(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Numeric (uses SqlDecimal since Decimal cannot hold full range)
        public virtual SqlDecimal GetSqlDecimal(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for DateTime, SmallDateTime, Date, and DateTime2
        public virtual DateTime GetDateTime(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for UniqueIdentifier
        public virtual Guid GetGuid(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Time
        public virtual TimeSpan GetTimeSpan(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for DateTimeOffset
        public virtual DateTimeOffset GetDateTimeOffset(int ordinal)
        {
            if (!CanGet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for structured types
        //  This method called for both get and set.
        internal virtual SmiTypedGetterSetter GetTypedGetterSetter(int ordinal)
        {
            throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
        }
        
        #endregion

        #region Setters

        // Set value to null
        //  valid for all types
        public virtual void SetDBNull(int ordinal)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        //  valid for SqlDbType.Bit
        public virtual void SetBoolean(int ordinal, bool value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        //  valid for SqlDbType.TinyInt
        public virtual void SetByte(int ordinal, byte value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // Semantics for SetBytes are to modify existing value, not overwrite
        //  Use in combination with SetLength to ensure overwriting when necessary
        // valid for SqlDbTypes: Binary, VarBinary, Image, Udt, Xml
        //      (VarBinary assumed for variants)
        public virtual int SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }
        public virtual void SetBytesLength(int ordinal, long length)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // Semantics for SetChars are to modify existing value, not overwrite
        //  Use in combination with SetLength to ensure overwriting when necessary
        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        //      (NVarChar and global clr collation assumed for variants)
        public virtual int SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }
        public virtual void SetCharsLength(int ordinal, long length)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        public virtual void SetString(int ordinal, string value, int offset, int length)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.SmallInt
        public virtual void SetInt16(int ordinal, short value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Int
        public virtual void SetInt32(int ordinal, int value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.BigInt, SqlDbType.Money, SqlDbType.SmallMoney
        public virtual void SetInt64(int ordinal, long value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Real
        public virtual void SetSingle(int ordinal, float value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Float
        public virtual void SetDouble(int ordinal, double value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Numeric (uses SqlDecimal since Decimal cannot hold full range)
        public virtual void SetSqlDecimal(int ordinal, SqlDecimal value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for DateTime, SmallDateTime, Date, and DateTime2
        public virtual void SetDateTime(int ordinal, DateTime value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for UniqueIdentifier
        public virtual void SetGuid(int ordinal, Guid value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for SqlDbType.Time
        public virtual void SetTimeSpan(int ordinal, TimeSpan value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        // valid for DateTimeOffset
        public virtual void SetDateTimeOffset(int ordinal, DateTimeOffset value)
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        public virtual void SetVariantMetaData(int ordinal, SmiMetaData metaData)
        {
            // ******** OBSOLETING from SMI -- this should have been removed from ITypedSettersV3
            //  Intended to be removed prior to RTM.  Sub-classes need not implement

            // Implement body with throw because there are only a couple of ways to get to this code:
            //  1) Client is calling this method even though the server negotiated for V3+ and dropped support for V2-.
            //  2) Server didn't implement V2- on some interface and negotiated V2-.
            throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
        }

        // valid for multivalued types only
        internal virtual void NewElement()
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        internal virtual void EndElements()
        {
            if (!CanSet)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.InvalidSmiCall);
            }
            else
            {
                throw ADP.InternalError(ADP.InternalErrorCode.UnimplementedSMIMethod);
            }
        }

        #endregion

    }
}
