// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;

namespace Microsoft.Data.Common
{

    [SuppressUnmanagedCodeSecurityAttribute()]
    internal static class UnsafeNativeMethods
    {

        [Guid("00000567-0000-0010-8000-00AA006D2EA4"), InterfaceType(ComInterfaceType.InterfaceIsDual), ComImport, SuppressUnmanagedCodeSecurity]
        internal interface ADORecordConstruction
        {

            [return: MarshalAs(UnmanagedType.Interface)] object get_Row();

            //void put_Row(
            //    [In, MarshalAs(UnmanagedType.Interface)] object pRow);

            //void put_ParentRow(
            //    [In, MarshalAs(UnmanagedType.Interface)]object pRow);
        }

        [Guid("00000283-0000-0010-8000-00AA006D2EA4"), InterfaceType(ComInterfaceType.InterfaceIsDual), ComImport, SuppressUnmanagedCodeSecurity]
        internal interface ADORecordsetConstruction
        {

            [return: MarshalAs(UnmanagedType.Interface)] object get_Rowset();

            [Obsolete("not used", true)] void put_Rowset(/*deleted parameters signature*/);

            /*[return:MarshalAs(UnmanagedType.SysInt)]*/
            IntPtr get_Chapter();

            //[[PreserveSig]
            //iint put_Chapter (
            //         [In]
            //         IntPtr pcRefCount);

            //[[PreserveSig]
            //iint get_RowPosition (
            //         [Out, MarshalAs(UnmanagedType.Interface)]
            //         out object ppRowPos);

            //[[PreserveSig]
            //iint put_RowPosition (
            //         [In, MarshalAs(UnmanagedType.Interface)]
            //         object pRowPos);
        }


        [Guid("0C733A64-2A1C-11CE-ADE5-00AA0044773D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComImport, SuppressUnmanagedCodeSecurity]
        internal interface ICommandWithParameters
        {

            [Obsolete("not used", true)] void GetParameterInfo(/*deleted parameters signature*/);

            [Obsolete("not used", true)] void MapParameterNames(/*deleted parameter signature*/);

            /*[local]
            HRESULT SetParameterInfo(
                [in] DB_UPARAMS cParams,
                [in, unique, size_is((ULONG)cParams)] const DB_UPARAMS rgParamOrdinals[],
                [in, unique, size_is((ULONG)cParams)] const DBPARAMBINDINFO rgParamBindInfo[]
            );*/
            //[PreserveSig] System.Data.OleDb.OleDbHResult SetParameterInfo(
            //    [In] IntPtr cParams,
            //    [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] rgParamOrdinals,
            //    [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Struct)] System.Data.OleDb.tagDBPARAMBINDINFO[] rgParamBindInfo);
        }

        [Guid("2206CCB1-19C1-11D1-89E0-00C04FD7A829"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComImport, SuppressUnmanagedCodeSecurity]
        internal interface IDataInitialize
        {

        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct Trustee
        {
            internal IntPtr _pMultipleTrustee;        // PTRUSTEE
            internal int _MultipleTrusteeOperation;   // MULTIPLE_TRUSTEE_OPERATION
            internal int _TrusteeForm;                // TRUSTEE_FORM
            internal int _TrusteeType;                // TRUSTEE_TYPE
            [MarshalAs(UnmanagedType.LPTStr)]
            internal string _name;

            internal Trustee(string name)
            {
                _pMultipleTrustee = IntPtr.Zero;
                _MultipleTrusteeOperation = 0;              // NO_MULTIPLE_TRUSTEE
                _TrusteeForm = 1;              // TRUSTEE_IS_NAME
                _TrusteeType = 1;              // TRUSTEE_IS_USER
                _name = name;
            }
        }

        [DllImport(ExternDll.Advapi32, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern uint GetEffectiveRightsFromAclW(byte[] pAcl, ref Trustee pTrustee, out uint pAccessMask);

        [DllImport(ExternDll.Advapi32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static internal extern bool CheckTokenMembership(IntPtr tokenHandle, byte[] sidToCheck, out bool isMember);

        [DllImport(ExternDll.Advapi32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static internal extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr stringSid);

        [DllImport(ExternDll.Advapi32, EntryPoint = "CreateWellKnownSid", SetLastError = true, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern int CreateWellKnownSid(
            int sidType,
            byte[] domainSid,
            [Out] byte[] resultSid,
            ref uint resultSidLength);

        [DllImport(ExternDll.Advapi32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static internal extern bool GetTokenInformation(IntPtr tokenHandle, uint token_class, IntPtr tokenStruct, uint tokenInformationLength, ref uint tokenString);

        [DllImport(ExternDll.Kernel32, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern int lstrlenW(IntPtr ptr);

        /* For debugging purposes...
        [DllImport(ExternDll.Advapi32)]
        [return:MarshalAs(UnmanagedType.I4)]
        static internal extern int GetLengthSid(IntPtr sid1);
        */
    }
}
