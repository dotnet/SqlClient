/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BidImplApi.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   BID Subsystem Implementation interfaces
//
//  Comments:
//              Last Modified: 14-Sep-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __BIDIMPLAPI_H__ ////////////////////////////////////////////////////////////////////////
#define __BIDIMPLAPI_H__
#ifndef _NOLIST_HDRS
#pragma message("  BidImplApi.h")
#endif

#include  "BidCplApi.h"

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Prototypes for exported functions
//
#define _bid_DECL(rv_t,name,args)   rv_t WINAPI  Dll##name args
#define _bid_CDECL(rv_t,name,args)  rv_t __cdecl Dll##name args

#ifdef __cplusplus
extern "C" {
#endif

_bid_DECL(BOOL,     BidPutStrA,     (HANDLE hID, UINT_PTR src, UINT_PTR info, PCSTR  str));
_bid_DECL(BOOL,     BidPutStrW,     (HANDLE hID, UINT_PTR src, UINT_PTR info, PCWSTR str));

_bid_DECL(BOOL,     BidTraceVA,     (HANDLE hID, UINT_PTR src, UINT_PTR info, PCSTR  fmt, va_list args));
_bid_DECL(BOOL,     BidTraceVW,     (HANDLE hID, UINT_PTR src, UINT_PTR info, PCWSTR fmt, va_list args));

_bid_DECL(BOOL,     BidScopeEnterVA,(HANDLE hID, UINT_PTR src, UINT_PTR info, HANDLE* pHScp, PCSTR  stf, va_list va));
_bid_DECL(BOOL,     BidScopeEnterVW,(HANDLE hID, UINT_PTR src, UINT_PTR info, HANDLE* pHScp, PCWSTR stf, va_list va));
_bid_DECL(BOOL,     BidScopeLeave,  (HANDLE hID, UINT_PTR src, UINT_PTR info, HANDLE* pHScp));

_bid_DECL(BOOL,     BidEnabledA,    (HANDLE hID, UINT_PTR src, UINT_PTR info, PCSTR  tcs));
_bid_DECL(BOOL,     BidEnabledW,    (HANDLE hID, UINT_PTR src, UINT_PTR info, PCWSTR tcs));

_bid_DECL(int,      BidIndent,      (HANDLE hID, int nIdx) );
_bid_DECL(INT_PTR,  BidSnap,        (HANDLE hID, INT_PTR evtID, INT_PTR arg1, INT_PTR arg2));

_bid_DECL(BOOL,     BidAssert,      (HANDLE hID, UINT_PTR arg, UINT_PTR info));
_bid_DECL(INT_PTR,  BidCtlProc,     (HANDLE hID, INT_PTR cmdSpaceID, int cmd, INT_PTR arg1, INT_PTR arg2, INT_PTR arg3));
_bid_DECL(INT_PTR,  BidTouch,       (HANDLE hID, UINT_PTR scope, UINT code, INT_PTR arg1, INT_PTR arg2));

_bid_CDECL(BOOL,    BidScopeEnterCW,(HANDLE hID, UINT_PTR src, UINT_PTR info, HANDLE* pHScp, PCWSTR stf, ... ));
_bid_CDECL(BOOL,    BidTraceCW,     (HANDLE hID, UINT_PTR src, UINT_PTR info, PCWSTR fmt,  ... ));

#ifdef __cplusplus
}
#endif

#undef  _bid_DECL
#undef  _bid_CDECL


/////////////////////////////////////////////////////////////////////////////////////////////////
//                              SUBSYSTEM IMPLEMENTATION HELPERS                               //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Indentation
//
#define BID_INDENT_GET              -2
#define BID_INDENT_MAX              128

//
//  TODO: remove 'U' flavor?
//
#define BidIndentU_Get(f)           _bidIndent(f, BID_INDENT_GET)
#define BidIndentU_Set(f,n)         do{ DASSERT(n >= 0); (void)_bidIndent(f,n); }while(0)

#define BidIndent_Get()             BidIndentU_Get(_bidTrON)
#define BidIndent_Set(n)            BidIndentU_Set(_bidTrON, n)


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  DllBidSnap
//


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  DllBidTouch
//
#define BID_TouchCode(code)         ((code) & BID_TOUCHCODE_MASK)


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidHdr layout.
//  NOTE: Keep _bidVerTag (BidApi_Impl.h) synchronized.
//
#define BID_HdrAttrSECount(attr)    ((attr) & 0xFF)
#define BID_HdrAttrSize(attr)       ((((attr) >> 8) & 0xFF) * sizeof(DWORD))
#define BID_HdrAttrVersion(attr)    (((attr) >> 16) & 0xFFFF)

//
//  DllBidEntryPoint (cfgBits):
//  If BID_CFG_MASK_PAGE is set, then 0x04000000 means
//  "Module uses pre-release format of BidMetaText"
//


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  WINAPI Abstraction
//

#ifndef _BID_EXTERNAL_WINAPI


#endif // _BID_EXTERNAL_WINAPI


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                End of file "BidImplApi.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
