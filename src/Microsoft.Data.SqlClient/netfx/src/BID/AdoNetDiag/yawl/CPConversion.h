/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       CPConversion.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   CodePage and CHAR/WCHAR conversion wrappers 
//
//  Comments:                                               (Reduced version for Bid2Etw28)
//              File Created : 10-Apr-1996
//              Last Modified: 08-Apr-2003
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __CPCONVERSION_H__ //////////////////////////////////////////////////////////////////////
#define __CPCONVERSION_H__
#ifndef _NOLIST_HDRS
#pragma message("  CPConversion-28.h")
#endif

#include "yawl/BaseRTL.h"

/////////////////////////////////////////////////////////////////////////////////////////////////
//                              CHAR / WCHAR Conversion wrappers                               //
/////////////////////////////////////////////////////////////////////////////////////////////////

//
//  Default CodePage
//
#define _defCP  CP_ACP

//
//  Unicode -> Ansi/MultiByte
//
int     WINAPI _mbLen(LPCWSTR src, UINT dstCP, int srcCnt = -1);
int     WINAPI _toMB(LPSTR dst, LPCWSTR src, int dstCnt, UINT dstCP, int srcCnt = -1);

//
//  Ansi/MultiByte -> Unicode
//
int     WINAPI _uniLen(LPCSTR src, UINT srcCP, int srcCnt = -1);
int     WINAPI _toUni(LPWSTR dst, LPCSTR src, int dstCnt, UINT srcCP, int srcCnt = -1);


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                End of file "CPConversion.h"                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////
