// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlTypes
{
    /// <summary>
    /// This type provides workarounds for the separation between System.Data.Common
    /// and Microsoft.Data.SqlClient.  The latter wants to access internal members of the former, and
    /// this class provides ways to do that.  We must review and update this implementation any time the
    /// implementation of the corresponding types in System.Data.Common change.
    /// </summary>
    internal static partial class SqlTypeWorkarounds
    {
        #region Work around inability to access SqlMoney.ctor(long, int) and SqlMoney.ToSqlInternalRepresentation
        // Documentation for internal ctor:
        // https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/system.data.sqltypes.sqlmoney.-ctor
        private static readonly Func<long, SqlMoney> s_sqlMoneyfactory =
            CtorHelper.CreateFactory<SqlMoney, long, int>(); // binds to SqlMoney..ctor(long, int) if it exists

        /// <summary>
        /// Constructs a SqlMoney from a long value without scaling. The ignored parameter exists
        /// only to distinguish this constructor from the constructor that takes a long.
        /// Used only internally.
        /// </summary>
        internal static SqlMoney SqlMoneyCtor(long value, int ignored)
        {
            SqlMoney val;
            if (s_sqlMoneyfactory is not null)
            {
                val = s_sqlMoneyfactory(value);
            }
            else
            {
                // SqlMoney is a long internally. Dividing by 10,000 gives us the decimal representation
                val = new SqlMoney(((decimal)value) / 10000);
            }

            return val;
        }

        internal static long SqlMoneyToSqlInternalRepresentation(SqlMoney money)
        {
            return SqlMoneyHelper.s_sqlMoneyToLong(ref money);
        }

        private static class SqlMoneyHelper
        {
            internal delegate long SqlMoneyToLongDelegate(ref SqlMoney @this);
            internal static readonly SqlMoneyToLongDelegate s_sqlMoneyToLong = GetSqlMoneyToLong();

            internal static SqlMoneyToLongDelegate GetSqlMoneyToLong()
            {
                SqlMoneyToLongDelegate del = null;
                    try
                    {
                        del = GetFastSqlMoneyToLong();
                    }
                    catch
                    {
                        // If an exception occurs for any reason, swallow & use the fallback code path.
                    }

                return del ?? FallbackSqlMoneyToLong;
            }

            private static SqlMoneyToLongDelegate GetFastSqlMoneyToLong()
            {
                // Note: Although it would be faster to use the m_value member variable in
                //    SqlMoney, but because it is not documented, we cannot use it. The method
                //    we are calling below *is* documented, despite it being internal.
                // Documentation for internal method:
                // https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/system.data.sqltypes.sqlmoney.tosqlinternalrepresentation
                MethodInfo toSqlInternalRepresentation = typeof(SqlMoney).GetMethod("ToSqlInternalRepresentation",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.ExactBinding,
                    null, CallingConventions.Any, new Type[] { }, null);

                if (toSqlInternalRepresentation is not null && toSqlInternalRepresentation.ReturnType == typeof(long))
                {
                    // On Full Framework, invoking the MethodInfo first before wrapping
                    // a delegate around it will produce better codegen. We don't need
                    // to inspect the return value; we just need to call the method.

                    _ = toSqlInternalRepresentation.Invoke(new SqlMoney(0), new object[0]);

                    // Now create the delegate. This is an open delegate, meaning the
                    // "this" parameter will be provided as arg0 on each call.

                    var del = (SqlMoneyToLongDelegate)toSqlInternalRepresentation.CreateDelegate(typeof(SqlMoneyToLongDelegate), target: null);

                    // Now we can cache the delegate and invoke it over and over again.
                    // Note: the first parameter to the delegate is provided *byref*.

                    return del;
                }

                SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.GetFastSqlMoneyToLong | Info | SqlMoney.ToSqlInternalRepresentation() not found. Less efficient fallback method will be used.");
                return null; // missing the expected method - cannot use fast path
            }

            // Used in case we can't use a [Serializable]-like mechanism.
            private static long FallbackSqlMoneyToLong(ref SqlMoney value)
            {
                if (value.IsNull)
                {
                    return default;
                }
                else
                {
                    decimal data = value.ToDecimal();
                    return (long)(data * 10000);
                }
            }
        }
        #endregion

        #region Work around SqlDecimal.WriteTdsValue not existing in netfx

        /// <summary>
        /// Implementation that mimics netcore's WriteTdsValue method.
        /// </summary>
        /// <remarks>
        /// Although calls to this method could just be replaced with calls to
        /// <see cref="SqlDecimal.Data"/>, using this mimic method allows netfx and netcore
        /// implementations to be more cleanly switched.
        /// </remarks>
        /// <param name="value">SqlDecimal value to get data from.</param>
        /// <param name="data1">First data field will be written here.</param>
        /// <param name="data2">Second data field will be written here.</param>
        /// <param name="data3">Third data field will be written here.</param>
        /// <param name="data4">Fourth data field will be written here.</param>
        internal static void SqlDecimalExtractData(
            SqlDecimal value,
            out uint data1,
            out uint data2,
            out uint data3,
            out uint data4)
        {
            // Note: Although it would be faster to use the m_data[1-4] member variables in
            //    SqlDecimal, we cannot use them because they are not documented. The Data property
            //    is less ideal, but is documented.
            int[] data = value.Data;
            data1 = (uint)data[0];
            data2 = (uint)data[1];
            data3 = (uint)data[2];
            data4 = (uint)data[3];
        }
        
        #endregion

        #region Work around inability to access SqlBinary.ctor(byte[], bool)
        // Documentation of internal constructor:
        // https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/system.data.sqltypes.sqlbinary.-ctor
        private static readonly Func<byte[], SqlBinary> s_sqlBinaryfactory =
            CtorHelper.CreateFactory<SqlBinary, byte[], bool>();

        internal static SqlBinary SqlBinaryCtor(byte[] value, bool ignored)
        {
            SqlBinary val;
            if (s_sqlBinaryfactory is not null)
            {
                val = s_sqlBinaryfactory(value);
            }
            else
            {
                val = new SqlBinary(value);
            }

            return val;
        }
        #endregion

        #region Work around inability to access SqlGuid.ctor(byte[], bool)
        // Documentation for internal constructor:
        // https://learn.microsoft.com/en-us/dotnet/framework/additional-apis/system.data.sqltypes.sqlguid.-ctor
        private static readonly Func<byte[], SqlGuid> s_sqlGuidfactory = 
            CtorHelper.CreateFactory<SqlGuid, byte[], bool>();

        internal static SqlGuid SqlGuidCtor(byte[] value, bool ignored)
        {
            SqlGuid val;
            if (s_sqlGuidfactory is not null)
            {
                val = s_sqlGuidfactory(value);
            }
            else
            {
                val = new SqlGuid(value);
            }

            return val;
        }
        #endregion

        private static class CtorHelper
        {
            // Returns null if .ctor(TValue, TIgnored) cannot be found.
            // Caller should have fallback logic in place in case the API doesn't exist.
            internal unsafe static Func<TValue, TInstance> CreateFactory<TInstance, TValue, TIgnored>() where TInstance : struct
            {
                try
                {
                    ConstructorInfo fullCtor = typeof(TInstance).GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.ExactBinding,
                        null, new[] { typeof(TValue), typeof(TIgnored) }, null);
                    if (fullCtor is not null)
                    {
                        // Need to use fnptr rather than delegate since MulticastDelegate expects to point to a MethodInfo,
                        // not a ConstructorInfo. The convention for invoking struct ctors is that the caller zeros memory,
                        // then passes a ref to the zeroed memory as the implicit arg0 "this". We don't need to worry
                        // about keeping this pointer alive; the fact that we're instantiated over TInstance will do it
                        // for us.
                        //
                        // On Full Framework, creating a delegate to InvocationHelper before invoking it for the first time
                        // will cause the delegate to point to the pre-JIT stub, which has an expensive preamble. Instead,
                        // we invoke InvocationHelper manually with a captured no-op fnptr. We'll then replace it with the
                        // real fnptr before creating a new delegate (pointing to the real codegen, not the stub) and
                        // returning that new delegate to our caller.

                        static void DummyNoOp(ref TInstance @this, TValue value, TIgnored ignored)
                        { }

                        IntPtr fnPtr;
                        TInstance InvocationHelper(TValue value)
                        {
                            TInstance retVal = default; // ensure zero-inited
                            ((delegate* managed<ref TInstance, TValue, TIgnored, void>)fnPtr)(ref retVal, value, default);
                            return retVal;
                        }

                        fnPtr = (IntPtr)(delegate* managed<ref TInstance, TValue, TIgnored, void>)(&DummyNoOp);
                        InvocationHelper(default); // no-op to trigger JIT

                        fnPtr = fullCtor.MethodHandle.GetFunctionPointer(); // replace before returning to caller
                        return InvocationHelper;
                    }
                }
                catch
                {
                }

                SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.CtorHelper.CreateFactory | Info | {0}..ctor({1}, {2}) not found. Less efficient fallback method will be used.", typeof(TInstance).Name, typeof(TValue).Name, typeof(TIgnored).Name);
                return null; // factory not found or an exception occurred
            }
        }
    }
}
