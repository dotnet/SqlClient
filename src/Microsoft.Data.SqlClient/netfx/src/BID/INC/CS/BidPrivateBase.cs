// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

[ComVisible(false)]
internal static partial class Bid
{
    //+//////////////////////////////////////////////////////////////////////////////////////////
    //                                                                                         //
    //                                      INTERFACE                                          //
    //                                                                                         //
    //+//////////////////////////////////////////////////////////////////////////////////////////
    //
    //  ApiGroup control flags are accessible from attached diagnostic subsystem via corresponding
    //  delegate, so the output can be enabled/disabled on the fly.
    //
    internal enum ApiGroup : uint
    {
        Off = 0x00000000,

        Default = 0x00000001,   // Bid.TraceEx (Always ON)
        Trace = 0x00000002,   // Bid.Trace, Bid.PutStr
        Scope = 0x00000004,   // Bid.Scope{Enter|Leave|Auto}
        Perf = 0x00000008,   // TBD..
        Resource = 0x00000010,   // TBD..
        Memory = 0x00000020,   // TBD..
        StatusOk = 0x00000040,   // S_OK, STATUS_SUCCESS, etc.
        Advanced = 0x00000080,   // Bid.TraceEx

        Pooling = 0x00001000,
        Dependency = 0x00002000,
        StateDump = 0x00004000,
        Correlation = 0x00040000,

        MaskBid = 0x00000FFF,
        MaskUser = 0xFFFFF000,
        MaskAll = 0xFFFFFFFF
    }

    internal static bool TraceOn
    {
        get { return (modFlags & ApiGroup.Trace) != 0; }
    }

    internal static bool AdvancedOn
    {
        get { return (modFlags & ApiGroup.Advanced) != 0; }
    }

    internal static bool IsOn(ApiGroup flag)
    {
        return (modFlags & flag) != 0;
    }

    private static IntPtr __noData = (IntPtr)(-1);

    internal static IntPtr NoData
    {
        get { return __noData; }
    }

    internal static void PutStr(string str)
    {
    }

    internal static void Trace(string strConst)
    {
    }

    internal static void Trace(string fmtPrintfW, string a1)
    {
    }

    internal static void ScopeLeave(ref IntPtr hScp)
    {
        hScp = NoData;
    }

    internal static void ScopeEnter(out IntPtr hScp, string strConst)
    {
        hScp = NoData;
    }

    internal static void ScopeEnter(out IntPtr hScp, string fmtPrintfW, int a1)
    {
        hScp = NoData;
    }

    internal static void ScopeEnter(out IntPtr hScp, string fmtPrintfW, int a1, int a2)
    {
        hScp = NoData;
    }

    internal static void TraceBin(string constStrHeader, byte[] buff, UInt16 length)
    {
    }

    private static ApiGroup modFlags = ApiGroup.Off;

} // Bid{PrivateBase}

/// <summary>
/// This attribute is used by FxCopBid rule to mark methods that accept format string and list of arguments that match it
/// FxCopBid rule uses this attribute to check if the method needs to be included in checks and to read type mappings
/// between the argument type to printf Type spec.
///
/// If you need to rename/remove the attribute or change its properties, make sure to update the FxCopBid rule!
/// </summary>
[System.Diagnostics.ConditionalAttribute("CODE_ANALYSIS")]
[System.AttributeUsage(AttributeTargets.Method)]
internal sealed class BidMethodAttribute : Attribute
{
    internal BidMethodAttribute()
    {
    }
}

/// <summary>
/// This attribute is used by FxCopBid rule to tell FXCOP the 'real' type sent to the native trace call for this argument. For
/// example, if Bid.Trace accepts enumeration value, but marshals it as string to the native trace method, set this attribute
/// on the argument and set ArgumentType = typeof(string)
/// 
/// It can be applied on a parameter, to let FxCopBid rule know the format spec type used for the argument, or it can be applied on a method,
/// to insert additional format spec arguments at specific location.
/// 
/// If you need to rename/remove the attribute or change its properties, make sure to update the FxCopBid rule!
/// </summary>
[System.Diagnostics.ConditionalAttribute("CODE_ANALYSIS")]
[System.AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Method, AllowMultiple = true)]
internal sealed class BidArgumentTypeAttribute : Attribute
{
    // this overload can be used on the argument itself
    internal BidArgumentTypeAttribute(Type bidArgumentType)
    {
        this.ArgumentType = bidArgumentType;
        this.Index = -1; // if this c-tor is used on methods, default index value is 'last'
    }

    public readonly Type ArgumentType;
    // should be used only if attribute is applied on the method
    public readonly int Index;
}
