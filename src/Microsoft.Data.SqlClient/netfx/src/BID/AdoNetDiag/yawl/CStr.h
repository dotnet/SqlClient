/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       CStr.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Redesign of MFC' CString                    (Reduced version for Bid2Etw28)
//
//  Comments:   Uses pseudo-template implementation CStr_impl.h and CStr_impl.cpp.
//
//              File Created : 12-Apr-1996
//              Last Modified: 10-Jun-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __CSTR_H__ //////////////////////////////////////////////////////////////////////////////
#define __CSTR_H__
#ifndef _NOLIST_HDRS
#pragma message("  CStr-28.h")
#endif

#include "yawl/BaseRTL.h"
#include "yawl/CPConversion.h"


class CStrA;
class CStrW;

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                     CStr, CStrA, CStrW                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////
#define __CSTR_IMPL_H__
#define __DECL_CSTR__

//
//  For CStr::Cut() and other editting functions
//
#define ToTheEnd                    0x7FFFFFFF

//
//  For internal use
//
typedef UINT CSTRSZ;
extern const LONG CStr_EmptyString  [4];
extern UINT  CStr_DumpStrLen;

//
//  Declare CStrA
//
#define     _A_CSTR
#undef      _W_CSTR
#include    "yawl/CStr_impl.h"

//
//  Declare CStrW
//
#define     _W_CSTR
#undef      _A_CSTR
#include    "yawl/CStr_impl.h"

//
//  Cleanup internal typedefs
//
#undef      _W_CSTR
#undef      __DECL_CSTR__
#include    "yawl/CStr_impl.h"

#undef __CSTR_IMPL_H__

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  CStr with statically allocated buffer
//
#define CSTRA_(str,size)                                            \
        BYTE str##_bufA [sizeof(StrDataA) + (size+1)*sizeof(char)]; \
        CStrA str (str##_bufA, sizeof(str##_bufA))

#define CSTRW_(str,size)                                            \
        BYTE str##_bufW [sizeof(StrDataW) + (size+1)*sizeof(WCHAR)];\
        CStrW str (str##_bufW, sizeof(str##_bufW))

#define CSTR_RET(str)   str

/////////////////////////////////////////////////////////////////////////////////////////////////

typedef const CStrA&    CREFSTRA;
typedef const CStrW&    CREFSTRW;
typedef       CStrA&    REFSTRA;
typedef       CStrW&    REFSTRW;

#ifdef _UNICODE
  #define   CStr        CStrW
  #define   StrData     StrDataW
  #define   CSTR_       CSTRW_
  #define   CREFSTR     CREFSTRW
  #define   REFSTR      REFSTRW
#else
  #define   CStr        CStrA
  #define   StrData     StrDataA
  #define   CSTR_       CSTRA_
  #define   CREFSTR     CREFSTRA
  #define   REFSTR      REFSTRA
#endif


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                    End of file "CStr.h"                                     //
/////////////////////////////////////////////////////////////////////////////////////////////////
