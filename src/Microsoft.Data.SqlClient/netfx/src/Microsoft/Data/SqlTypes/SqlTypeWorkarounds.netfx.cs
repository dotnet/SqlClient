// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
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
        #region Work around inability to access SqlDecimal._data1/2/3/4
        internal static void SqlDecimalExtractData(SqlDecimal d, out uint data1, out uint data2, out uint data3, out uint data4)
        {
            SqlDecimalHelper.s_decompose(d, out data1, out data2, out data3, out data4);
        }

        private static class SqlDecimalHelper
        {
            internal delegate void Decomposer(SqlDecimal value, out uint data1, out uint data2, out uint data3, out uint data4);
            internal static readonly Decomposer s_decompose = GetDecomposer();

            private static Decomposer GetDecomposer()
            {
                Decomposer decomposer = null;
                try
                {
                    decomposer = GetFastDecomposer();
                }
                catch
                {
                    // If an exception occurs for any reason, swallow & use the fallback code path.
                }

                return decomposer ?? FallbackDecomposer;
            }

            private static Decomposer GetFastDecomposer()
            {
                // This takes advantage of the fact that for [Serializable] types, the member fields are implicitly
                // part of the type's serialization contract. This includes the fields' names and types. By default,
                // [Serializable]-compliant serializers will read all the member fields and shove the data into a
                // SerializationInfo dictionary. We mimic this behavior in a manner consistent with the [Serializable]
                // pattern, but much more efficiently.
                //
                // In order to make sure we're staying compliant, we need to gate our checks to fulfill some core
                // assumptions. Importantly, the type must be [Serializable] but cannot be ISerializable, as the
                // presence of the interface means that the type wants to be responsible for its own serialization,
                // and that member fields are not guaranteed to be part of the serialization contract. Additionally,
                // we need to check for [OnSerializing] and [OnDeserializing] methods, because we cannot account
                // for any logic which might be present within them.

                if (!typeof(SqlDecimal).IsSerializable)
                {
                    SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposer | Info | SqlDecimal isn't Serializable. Less efficient fallback method will be used.");
                    return null; // type is not serializable - cannot use fast path assumptions
                }

                if (typeof(ISerializable).IsAssignableFrom(typeof(SqlDecimal)))
                {
                    SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposer | Info | SqlDecimal is ISerializable. Less efficient fallback method will be used.");
                    return null; // type contains custom logic - cannot use fast path assumptions
                }

                foreach (MethodInfo method in typeof(SqlDecimal).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.IsDefined(typeof(OnDeserializingAttribute)) || method.IsDefined(typeof(OnDeserializedAttribute)))
                    {
                        SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposer | Info | SqlDecimal contains custom serialization logic. Less efficient fallback method will be used.");
                        return null; // type contains custom logic - cannot use fast path assumptions
                    }
                }

                // GetSerializableMembers filters out [NonSerialized] fields for us automatically.

                FieldInfo fiData1 = null, fiData2 = null, fiData3 = null, fiData4 = null;
                foreach (MemberInfo candidate in FormatterServices.GetSerializableMembers(typeof(SqlDecimal)))
                {
                    if (candidate is FieldInfo fi && fi.FieldType == typeof(uint))
                    {
                        if (fi.Name == "m_data1")
                        { fiData1 = fi; }
                        else if (fi.Name == "m_data2")
                        { fiData2 = fi; }
                        else if (fi.Name == "m_data3")
                        { fiData3 = fi; }
                        else if (fi.Name == "m_data4")
                        { fiData4 = fi; }
                    }
                }

                if (fiData1 is null || fiData2 is null || fiData3 is null || fiData4 is null)
                {
                    SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposer | Info | Expected SqlDecimal fields are missing. Less efficient fallback method will be used.");
                    return null; // missing one of the expected member fields - cannot use fast path assumptions
                }

                Type refToUInt32 = typeof(uint).MakeByRefType();
                DynamicMethod dm = new(
                    name: "sqldecimal-decomposer",
                    returnType: typeof(void),
                    parameterTypes: new[] { typeof(SqlDecimal), refToUInt32, refToUInt32, refToUInt32, refToUInt32 },
                    restrictedSkipVisibility: true); // perf: JITs method at delegate creation time

                ILGenerator ilGen = dm.GetILGenerator();
                ilGen.Emit(OpCodes.Ldarg_1); // eval stack := [UInt32&]
                ilGen.Emit(OpCodes.Ldarg_0); // eval stack := [UInt32&] [SqlDecimal]
                ilGen.Emit(OpCodes.Ldfld, fiData1); // eval stack := [UInt32&] [UInt32]
                ilGen.Emit(OpCodes.Stind_I4); // eval stack := <empty>
                ilGen.Emit(OpCodes.Ldarg_2); // eval stack := [UInt32&]
                ilGen.Emit(OpCodes.Ldarg_0); // eval stack := [UInt32&] [SqlDecimal]
                ilGen.Emit(OpCodes.Ldfld, fiData2); // eval stack := [UInt32&] [UInt32]
                ilGen.Emit(OpCodes.Stind_I4); // eval stack := <empty>
                ilGen.Emit(OpCodes.Ldarg_3); // eval stack := [UInt32&]
                ilGen.Emit(OpCodes.Ldarg_0); // eval stack := [UInt32&] [SqlDecimal]
                ilGen.Emit(OpCodes.Ldfld, fiData3); // eval stack := [UInt32&] [UInt32]
                ilGen.Emit(OpCodes.Stind_I4); // eval stack := <empty>
                ilGen.Emit(OpCodes.Ldarg_S, (byte)4); // eval stack := [UInt32&]
                ilGen.Emit(OpCodes.Ldarg_0); // eval stack := [UInt32&] [SqlDecimal]
                ilGen.Emit(OpCodes.Ldfld, fiData4); // eval stack := [UInt32&] [UInt32]
                ilGen.Emit(OpCodes.Stind_I4); // eval stack := <empty>
                ilGen.Emit(OpCodes.Ret);

                return (Decomposer)dm.CreateDelegate(typeof(Decomposer), null /* target */);
            }

            // Used in case we can't use a [Serializable]-like mechanism.
            private static void FallbackDecomposer(SqlDecimal value, out uint data1, out uint data2, out uint data3, out uint data4)
            {
                if (value.IsNull)
                {
                    data1 = default;
                    data2 = default;
                    data3 = default;
                    data4 = default;
                }
                else
                {
                    int[] data = value.Data; // allocation
                    data4 = (uint)data[3]; // write in reverse to avoid multiple bounds checks
                    data3 = (uint)data[2];
                    data2 = (uint)data[1];
                    data1 = (uint)data[0];
                }
            }
        }
        #endregion

        #region Work around inability to access SqlGuid.ctor(byte[], bool)
        private static readonly Func<byte[], SqlGuid> s_sqlGuidfactory = CtorHelper.CreateFactory<SqlGuid, byte[], bool>(); // binds to SqlGuid..ctor(byte[], bool) if it exists

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
