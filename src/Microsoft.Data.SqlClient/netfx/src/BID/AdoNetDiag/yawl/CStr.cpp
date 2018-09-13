/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       CStr.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Redesign of MFC' CString
//
//  Comments:   Uses pseudo-template implementation CStr_impl.h and CStr_impl.cpp.
//
//              File Created : 12-Apr-1996
//              Last Modified: 10-Jun-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include  "stdafx.h"
#include  "yawl/CStr.h"


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                     CStr, CStrA, CStrW                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////
#define     __IMPL_CSTR__

const LONG  CStr_EmptyString [4] = { StrData::fSTATIC|StrData::fRDONLY + 1, 0, 0, 0 };

//
//  MaxLen of String Digest to be shown in BidExtension
//
UINT CStr_DumpStrLen = 80;

//
//  Define(implement) CStrA
//
#define     _A_CSTR
#undef      _W_CSTR
#include    "yawl/CStr_impl.cpp"

//
//  Define(implement) CStrW
//
#define     _W_CSTR
#undef      _A_CSTR
#include    "yawl/CStr_impl.cpp"

//
//  Cleanup internal typedefs
//
#undef      _W_CSTR
#undef      __DECL_CSTR__
#define     __CSTR_IMPL_H__
#include    "yawl/CStr_impl.h"
#undef      __CSTR_IMPL_H__

#undef      __IMPL_CSTR__

//
//  important for _duplicateRef / _removeRef
//
DASSERT_COMPILER( sizeof(PVOID) == sizeof(CStr) );


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                   End of file "CStr.cpp"                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////
