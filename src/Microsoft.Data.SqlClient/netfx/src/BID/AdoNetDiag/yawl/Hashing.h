/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       Hashing.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Common helpers for text hashing; class Guid
//
//  Comments:                                               (Reduced version for Bid2Etw28)
//              File Created : 10-Apr-1996
//              Last Modified: 14-Sep-2003
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __HASHING_H__ ///////////////////////////////////////////////////////////////////////////
#define __HASHING_H__
#ifndef _NOLIST_HDRS
#pragma message("  Hashing-28.h")
#endif

#include  "yawl/BaseRTL.h"

/////////////////////////////////////////////////////////////////////////////////////////////////

void    FakeGuidFromTextA(GUID& rGuid, PCSTR  str, int nLen = -1);
void    FakeGuidFromTextW(GUID& rGuid, PCWSTR str, int nLen = -1);

#ifdef _UNICODE
  #define FakeGuidFromText FakeGuidFromTextW
#else
  #define FakeGuidFromText FakeGuidFromTextA
#endif

#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                  End of file "Hashing.h"                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////
