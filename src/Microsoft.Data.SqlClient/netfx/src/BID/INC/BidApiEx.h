/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BidApiEx.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics Interface declaration.
//              Macro wrappers with extended number of arguments.
//  Comments:
//              File Created : 26-Aug-2003
//              Last Modified: 15-May-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __BIDAPIEX_H__ //////////////////////////////////////////////////////////////////////////
#define __BIDAPIEX_H__

#include  "BidApi.h"


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                    MAIN TRACING FACILITY                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////

#define _bid_T10(x,ON,y,tcfmts, a,b,c,d,e,f,g,h,i,j)                                            \
  _bid_DO                                                                                       \
    if( ON )                                                                                    \
    {   _bid_SRCINFO                                                                            \
        _bid_TCFS_##x(_bidN(030), tcfmts)                                                       \
        if( _bidN(030) &&                                                                       \
          !_bidTrace##x(_bidSF,_bidLF(y),_bidN(030), a,b,c,d,e,f,g,h,i,j))        DBREAK();     \
    }                                                                                           \
  _bid_WHILE0

#define _bid_T11(x,ON,y,tcfmts, a,b,c,d,e,f,g,h,i,j,k)                                          \
  _bid_DO                                                                                       \
    if( ON )                                                                                    \
    {   _bid_SRCINFO                                                                            \
        _bid_TCFS_##x(_bidN(030), tcfmts)                                                       \
        if( _bidN(030) &&                                                                       \
          !_bidTrace##x(_bidSF,_bidLF(y),_bidN(030), a,b,c,d,e,f,g,h,i,j,k))      DBREAK();     \
    }                                                                                           \
  _bid_WHILE0

#define _bid_T12(x,ON,y,tcfmts, a,b,c,d,e,f,g,h,i,j,k,l)                                        \
  _bid_DO                                                                                       \
    if( ON )                                                                                    \
    {   _bid_SRCINFO                                                                            \
        _bid_TCFS_##x(_bidN(030), tcfmts)                                                       \
        if( _bidN(030) &&                                                                       \
          !_bidTrace##x(_bidSF,_bidLF(y),_bidN(030), a,b,c,d,e,f,g,h,i,j,k,l))    DBREAK();     \
    }                                                                                           \
  _bid_WHILE0

#define _bid_T13(x,ON,y,tcfmts, a,b,c,d,e,f,g,h,i,j,k,l,m)                                      \
  _bid_DO                                                                                       \
    if( ON )                                                                                    \
    {   _bid_SRCINFO                                                                            \
        _bid_TCFS_##x(_bidN(030), tcfmts)                                                       \
        if( _bidN(030) &&                                                                       \
          !_bidTrace##x(_bidSF,_bidLF(y),_bidN(030), a,b,c,d,e,f,g,h,i,j,k,l,m))  DBREAK();     \
    }                                                                                           \
  _bid_WHILE0

#define _bid_T14(x,ON,y,tcfmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)                                    \
  _bid_DO                                                                                       \
    if( ON )                                                                                    \
    {   _bid_SRCINFO                                                                            \
        _bid_TCFS_##x(_bidN(030), tcfmts)                                                       \
        if( _bidN(030) &&                                                                       \
          !_bidTrace##x(_bidSF,_bidLF(y),_bidN(030), a,b,c,d,e,f,g,h,i,j,k,l,m,n)) DBREAK();    \
    }                                                                                           \
  _bid_WHILE0

#define _bid_T15(x,ON,y,tcfmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)                                  \
  _bid_DO                                                                                       \
    if( ON )                                                                                    \
    {   _bid_SRCINFO                                                                            \
        _bid_TCFS_##x(_bidN(030), tcfmts)                                                       \
        if( _bidN(030) &&                                                                       \
          !_bidTrace##x(_bidSF,_bidLF(y),_bidN(030), a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)) DBREAK();  \
    }                                                                                           \
  _bid_WHILE0


#define BidTraceEx10A(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j)            _bid_T10(A,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTraceEx11A(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k)          _bid_T11(A,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTraceEx12A(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)        _bid_T12(A,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTraceEx13A(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)      _bid_T13(A,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTraceEx14A(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)    _bid_T14(A,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTraceEx15A(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)  _bid_T15(A,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidTraceEx10W(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j)            _bid_T10(W,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTraceEx11W(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k)          _bid_T11(W,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTraceEx12W(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)        _bid_T12(W,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTraceEx13W(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)      _bid_T13(W,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTraceEx14W(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)    _bid_T14(W,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTraceEx15W(bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)  _bid_T15(W,bit,flg,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidTrace10A(tcfs, a,b,c,d,e,f,g,h,i,j)                      _bid_T10(A,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTrace11A(tcfs, a,b,c,d,e,f,g,h,i,j,k)                    _bid_T11(A,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTrace12A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l)                  _bid_T12(A,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTrace13A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)                _bid_T13(A,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTrace14A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)              _bid_T14(A,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTrace15A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)            _bid_T15(A,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidTrace10W(tcfs, a,b,c,d,e,f,g,h,i,j)                      _bid_T10(W,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTrace11W(tcfs, a,b,c,d,e,f,g,h,i,j,k)                    _bid_T11(W,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTrace12W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l)                  _bid_T12(W,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTrace13W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)                _bid_T13(W,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTrace14W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)              _bid_T14(W,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTrace15W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)            _bid_T15(W,_bidTrON,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)


#if defined( _UNICODE)
  #define   BidTraceEx10     BidTraceEx10W
  #define   BidTraceEx11     BidTraceEx11W
  #define   BidTraceEx12     BidTraceEx12W
  #define   BidTraceEx13     BidTraceEx13W
  #define   BidTraceEx14     BidTraceEx14W
  #define   BidTraceEx15     BidTraceEx15W

  #define   BidTrace10       BidTrace10W
  #define   BidTrace11       BidTrace11W
  #define   BidTrace12       BidTrace12W
  #define   BidTrace13       BidTrace13W
  #define   BidTrace14       BidTrace14W
  #define   BidTrace15       BidTrace15W
#else
  #define   BidTraceEx10     BidTraceEx10A
  #define   BidTraceEx11     BidTraceEx11A
  #define   BidTraceEx12     BidTraceEx12A
  #define   BidTraceEx13     BidTraceEx13A
  #define   BidTraceEx14     BidTraceEx14A
  #define   BidTraceEx15     BidTraceEx15A

  #define   BidTrace10       BidTrace10A
  #define   BidTrace11       BidTrace11A
  #define   BidTrace12       BidTrace12A
  #define   BidTrace13       BidTrace13A
  #define   BidTrace14       BidTrace14A
  #define   BidTrace15       BidTrace15A
#endif



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                       SCOPE TRACKING                                        //
/////////////////////////////////////////////////////////////////////////////////////////////////

#define _bid_C10(x,ON,H,stf, a,b,c,d,e,f,g,h,i,j)                                               \
  _bid_DO                                                                                       \
    if( ON ){                                                                                   \
      _bid_STF_##x(_bidN(040), stf)                                                             \
      if(_bidN(040) && !_bidScopeEnter##x(H,_bidN(040), a,b,c,d,e,f,g,h,i,j))       DBREAK();   \
    }else{                                                                                      \
        _bidINIT_HSCP(H)                                                                        \
    }                                                                                           \
  _bid_WHILE0

#define _bid_C11(x,ON,H,stf, a,b,c,d,e,f,g,h,i,j,k)                                             \
  _bid_DO                                                                                       \
    if( ON ){                                                                                   \
      _bid_STF_##x(_bidN(040), stf)                                                             \
      if(_bidN(040) && !_bidScopeEnter##x(H,_bidN(040), a,b,c,d,e,f,g,h,i,j,k))     DBREAK();   \
    }else{                                                                                      \
        _bidINIT_HSCP(H)                                                                        \
    }                                                                                           \
  _bid_WHILE0

#define _bid_C12(x,ON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l)                                           \
  _bid_DO                                                                                       \
    if( ON ){                                                                                   \
      _bid_STF_##x(_bidN(040), stf)                                                             \
      if(_bidN(040) && !_bidScopeEnter##x(H,_bidN(040), a,b,c,d,e,f,g,h,i,j,k,l))   DBREAK();   \
    }else{                                                                                      \
        _bidINIT_HSCP(H)                                                                        \
    }                                                                                           \
  _bid_WHILE0


#define _bid_C13(x,ON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m)                                         \
  _bid_DO                                                                                       \
    if( ON ){                                                                                   \
      _bid_STF_##x(_bidN(040), stf)                                                             \
      if(_bidN(040) && !_bidScopeEnter##x(H,_bidN(040), a,b,c,d,e,f,g,h,i,j,k,l,m)) DBREAK();   \
    }else{                                                                                      \
        _bidINIT_HSCP(H)                                                                        \
    }                                                                                           \
  _bid_WHILE0


#define _bid_C14(x,ON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)                                       \
  _bid_DO                                                                                       \
    if( ON ){                                                                                   \
      _bid_STF_##x(_bidN(040), stf)                                                             \
      if(_bidN(040) && !_bidScopeEnter##x(H,_bidN(040), a,b,c,d,e,f,g,h,i,j,k,l,m,n)) DBREAK(); \
    }else{                                                                                      \
        _bidINIT_HSCP(H)                                                                        \
    }                                                                                           \
  _bid_WHILE0


#define _bid_C15(x,ON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)                                     \
  _bid_DO                                                                                       \
    if( ON ){                                                                                   \
      _bid_STF_##x(_bidN(040), stf)                                                             \
      if(_bidN(040) && !_bidScopeEnter##x(H,_bidN(040),a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)) DBREAK();\
    }else{                                                                                      \
        _bidINIT_HSCP(H)                                                                        \
    }                                                                                           \
  _bid_WHILE0

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#define BidScopeEnterEx10A(H,stf,a,b,c,d,e,f,g,h,i,j)           _bid_C10(A,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j)
#define BidScopeEnterEx11A(H,stf,a,b,c,d,e,f,g,h,i,j,k)         _bid_C11(A,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k)
#define BidScopeEnterEx12A(H,stf,a,b,c,d,e,f,g,h,i,j,k,l)       _bid_C12(A,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidScopeEnterEx13A(H,stf,a,b,c,d,e,f,g,h,i,j,k,l,m)     _bid_C13(A,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidScopeEnterEx14A(H,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n)   _bid_C14(A,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidScopeEnterEx15A(H,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o) _bid_C15(A,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidScopeEnterEx10W(H,stf,a,b,c,d,e,f,g,h,i,j)           _bid_C10(W,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j)
#define BidScopeEnterEx11W(H,stf,a,b,c,d,e,f,g,h,i,j,k)         _bid_C11(W,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k)
#define BidScopeEnterEx12W(H,stf,a,b,c,d,e,f,g,h,i,j,k,l)       _bid_C12(W,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidScopeEnterEx13W(H,stf,a,b,c,d,e,f,g,h,i,j,k,l,m)     _bid_C13(W,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidScopeEnterEx14W(H,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n)   _bid_C14(W,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidScopeEnterEx15W(H,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o) _bid_C15(W,_bidScpON,H,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidScopeEnter10A(stf, a,b,c,d,e,f,g,h,i,j)              _bidCT; BidScopeEnterEx10A(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j)
#define BidScopeEnter11A(stf, a,b,c,d,e,f,g,h,i,j,k)            _bidCT; BidScopeEnterEx11A(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k)
#define BidScopeEnter12A(stf, a,b,c,d,e,f,g,h,i,j,k,l)          _bidCT; BidScopeEnterEx12A(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidScopeEnter13A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m)        _bidCT; BidScopeEnterEx13A(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidScopeEnter14A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)      _bidCT; BidScopeEnterEx14A(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidScopeEnter15A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)    _bidCT; BidScopeEnterEx15A(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidScopeEnter10W(stf, a,b,c,d,e,f,g,h,i,j)              _bidCT; BidScopeEnterEx10W(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j)
#define BidScopeEnter11W(stf, a,b,c,d,e,f,g,h,i,j,k)            _bidCT; BidScopeEnterEx11W(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k)
#define BidScopeEnter12W(stf, a,b,c,d,e,f,g,h,i,j,k,l)          _bidCT; BidScopeEnterEx12W(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidScopeEnter13W(stf, a,b,c,d,e,f,g,h,i,j,k,l,m)        _bidCT; BidScopeEnterEx13W(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidScopeEnter14W(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)      _bidCT; BidScopeEnterEx14W(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidScopeEnter15W(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)    _bidCT; BidScopeEnterEx15W(&_bidScp,stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)


#define BidScopeLeaveEx4A(hScp,stf,a,b,c,d)             BidTrace4A(stf,a,b,c,d);            BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx5A(hScp,stf,a,b,c,d,e)           BidTrace5A(stf,a,b,c,d,e);          BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx6A(hScp,stf,a,b,c,d,e,f)         BidTrace6A(stf,a,b,c,d,e,f);        BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx7A(hScp,stf,a,b,c,d,e,f,g)       BidTrace7A(stf,a,b,c,d,e,f,g);      BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx8A(hScp,stf,a,b,c,d,e,f,g,h)     BidTrace8A(stf,a,b,c,d,e,f,g,h);    BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx9A(hScp,stf,a,b,c,d,e,f,g,h,i)   BidTrace9A(stf,a,b,c,d,e,f,g,h,i);  BidScopeLeaveEx(hScp)

#define BidScopeLeaveEx4W(hScp,stf,a,b,c,d)             BidTrace4W(stf,a,b,c,d);            BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx5W(hScp,stf,a,b,c,d,e)           BidTrace5W(stf,a,b,c,d,e);          BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx6W(hScp,stf,a,b,c,d,e,f)         BidTrace6W(stf,a,b,c,d,e,f);        BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx7W(hScp,stf,a,b,c,d,e,f,g)       BidTrace7W(stf,a,b,c,d,e,f,g);      BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx8W(hScp,stf,a,b,c,d,e,f,g,h)     BidTrace8W(stf,a,b,c,d,e,f,g,h);    BidScopeLeaveEx(hScp)
#define BidScopeLeaveEx9W(hScp,stf,a,b,c,d,e,f,g,h,i)   BidTrace9W(stf,a,b,c,d,e,f,g,h,i);  BidScopeLeaveEx(hScp)

#define BidScopeLeave4A(stf,a,b,c,d)                    BidTrace4A(stf,a,b,c,d);            BidScopeLeave()
#define BidScopeLeave5A(stf,a,b,c,d,e)                  BidTrace5A(stf,a,b,c,d,e);          BidScopeLeave()
#define BidScopeLeave6A(stf,a,b,c,d,e,f)                BidTrace6A(stf,a,b,c,d,e,f);        BidScopeLeave()
#define BidScopeLeave7A(stf,a,b,c,d,e,f,g)              BidTrace7A(stf,a,b,c,d,e,f,g);      BidScopeLeave()
#define BidScopeLeave8A(stf,a,b,c,d,e,f,g,h)            BidTrace8A(stf,a,b,c,d,e,f,g,h);    BidScopeLeave()
#define BidScopeLeave9A(stf,a,b,c,d,e,f,g,h,i)          BidTrace9A(stf,a,b,c,d,e,f,g,h,i);  BidScopeLeave()

#define BidScopeLeave4W(stf,a,b,c,d)                    BidTrace4W(stf,a,b,c,d);            BidScopeLeave()
#define BidScopeLeave5W(stf,a,b,c,d,e)                  BidTrace5W(stf,a,b,c,d,e);          BidScopeLeave()
#define BidScopeLeave6W(stf,a,b,c,d,e,f)                BidTrace6W(stf,a,b,c,d,e,f);        BidScopeLeave()
#define BidScopeLeave7W(stf,a,b,c,d,e,f,g)              BidTrace7W(stf,a,b,c,d,e,f,g);      BidScopeLeave()
#define BidScopeLeave8W(stf,a,b,c,d,e,f,g,h)            BidTrace8W(stf,a,b,c,d,e,f,g,h);    BidScopeLeave()
#define BidScopeLeave9W(stf,a,b,c,d,e,f,g,h,i)          BidTrace9W(stf,a,b,c,d,e,f,g,h,i);  BidScopeLeave()


#if defined( _UNICODE )
  #define BidScopeEnter10       BidScopeEnter10W
  #define BidScopeEnter11       BidScopeEnter11W
  #define BidScopeEnter12       BidScopeEnter12W
  #define BidScopeEnter13       BidScopeEnter13W
  #define BidScopeEnter14       BidScopeEnter14W
  #define BidScopeEnter15       BidScopeEnter15W

  #define BidScopeEnterEx10     BidScopeEnterEx10W
  #define BidScopeEnterEx11     BidScopeEnterEx11W
  #define BidScopeEnterEx12     BidScopeEnterEx12W
  #define BidScopeEnterEx13     BidScopeEnterEx13W
  #define BidScopeEnterEx14     BidScopeEnterEx14W
  #define BidScopeEnterEx15     BidScopeEnterEx15W

  #define BidScopeLeave4        BidScopeLeave4W
  #define BidScopeLeave5        BidScopeLeave5W
  #define BidScopeLeave6        BidScopeLeave6W
  #define BidScopeLeave7        BidScopeLeave7W
  #define BidScopeLeave8        BidScopeLeave8W
  #define BidScopeLeave9        BidScopeLeave9W

  #define BidScopeLeaveEx4      BidScopeLeaveEx4W
  #define BidScopeLeaveEx5      BidScopeLeaveEx5W
  #define BidScopeLeaveEx6      BidScopeLeaveEx6W
  #define BidScopeLeaveEx7      BidScopeLeaveEx7W
  #define BidScopeLeaveEx8      BidScopeLeaveEx8W
  #define BidScopeLeaveEx9      BidScopeLeaveEx9W

#else
  #define BidScopeEnter10       BidScopeEnter10A
  #define BidScopeEnter11       BidScopeEnter11A
  #define BidScopeEnter12       BidScopeEnter12A
  #define BidScopeEnter13       BidScopeEnter13A
  #define BidScopeEnter14       BidScopeEnter14A
  #define BidScopeEnter15       BidScopeEnter15A

  #define BidScopeEnterEx10     BidScopeEnterEx10A
  #define BidScopeEnterEx11     BidScopeEnterEx11A
  #define BidScopeEnterEx12     BidScopeEnterEx12A
  #define BidScopeEnterEx13     BidScopeEnterEx13A
  #define BidScopeEnterEx14     BidScopeEnterEx14A
  #define BidScopeEnterEx15     BidScopeEnterEx15A

  #define BidScopeLeave4        BidScopeLeave4A
  #define BidScopeLeave5        BidScopeLeave5A
  #define BidScopeLeave6        BidScopeLeave6A
  #define BidScopeLeave7        BidScopeLeave7A
  #define BidScopeLeave8        BidScopeLeave8A
  #define BidScopeLeave9        BidScopeLeave9A

  #define BidScopeLeaveEx4      BidScopeLeaveEx4A
  #define BidScopeLeaveEx5      BidScopeLeaveEx5A
  #define BidScopeLeaveEx6      BidScopeLeaveEx6A
  #define BidScopeLeaveEx7      BidScopeLeaveEx7A
  #define BidScopeLeaveEx8      BidScopeLeaveEx8A
  #define BidScopeLeaveEx9      BidScopeLeaveEx9A
#endif


////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Scope Tracking Automatic Wrapper
//
#if defined( __cplusplus )

    #define BidScopeAuto10A(stf,a,b,c,d,e,f,g,h,i,j)            _bidCTA; BidScopeEnterEx10A(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j)
    #define BidScopeAuto11A(stf,a,b,c,d,e,f,g,h,i,j,k)          _bidCTA; BidScopeEnterEx11A(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k)
    #define BidScopeAuto12A(stf,a,b,c,d,e,f,g,h,i,j,k,l)        _bidCTA; BidScopeEnterEx12A(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l)
    #define BidScopeAuto13A(stf,a,b,c,d,e,f,g,h,i,j,k,l,m)      _bidCTA; BidScopeEnterEx13A(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l,m)
    #define BidScopeAuto14A(stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n)    _bidCTA; BidScopeEnterEx14A(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n)
    #define BidScopeAuto15A(stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)  _bidCTA; BidScopeEnterEx15A(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

    #define BidScopeAuto10W(stf,a,b,c,d,e,f,g,h,i,j)            _bidCTA; BidScopeEnterEx10W(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j)
    #define BidScopeAuto11W(stf,a,b,c,d,e,f,g,h,i,j,k)          _bidCTA; BidScopeEnterEx11W(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k)
    #define BidScopeAuto12W(stf,a,b,c,d,e,f,g,h,i,j,k,l)        _bidCTA; BidScopeEnterEx12W(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l)
    #define BidScopeAuto13W(stf,a,b,c,d,e,f,g,h,i,j,k,l,m)      _bidCTA; BidScopeEnterEx13W(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l,m)
    #define BidScopeAuto14W(stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n)    _bidCTA; BidScopeEnterEx14W(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n)
    #define BidScopeAuto15W(stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)  _bidCTA; BidScopeEnterEx15W(&_bidScp,stf,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

    #if defined( _UNICODE )
      #define BidScopeAuto10     BidScopeAuto10W
      #define BidScopeAuto11     BidScopeAuto11W
      #define BidScopeAuto12     BidScopeAuto12W
      #define BidScopeAuto13     BidScopeAuto13W
      #define BidScopeAuto14     BidScopeAuto14W
      #define BidScopeAuto15     BidScopeAuto15W
    #else
      #define BidScopeAuto10     BidScopeAuto10A
      #define BidScopeAuto11     BidScopeAuto11A
      #define BidScopeAuto12     BidScopeAuto12A
      #define BidScopeAuto13     BidScopeAuto13A
      #define BidScopeAuto14     BidScopeAuto14A
      #define BidScopeAuto15     BidScopeAuto15A
    #endif

#endif  // __cplusplus

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Output helpers to be used in BIDEXTPROC implementation
//
#define _bid_W10(x,hCtx,fmts, a,b,c,d,e,f,g,h,i,j)                              \
        do{ _bid_XFS_##x(_bidN(100), fmts)                                      \
            (void)xBidWrite##x(hCtx, _bidN(100), a,b,c,d,e,f,g,h,i,j);          \
        } while(0)

#define _bid_W11(x,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k)                            \
        do{ _bid_XFS_##x(_bidN(100), fmts)                                      \
            (void)xBidWrite##x(hCtx, _bidN(100), a,b,c,d,e,f,g,h,i,j,k);        \
        } while(0)

#define _bid_W12(x,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l)                          \
        do{ _bid_XFS_##x(_bidN(100), fmts)                                      \
            (void)xBidWrite##x(hCtx, _bidN(100), a,b,c,d,e,f,g,h,i,j,k,l);      \
        } while(0)

#define _bid_W13(x,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)                        \
        do{ _bid_XFS_##x(_bidN(100), fmts)                                      \
            (void)xBidWrite##x(hCtx, _bidN(100), a,b,c,d,e,f,g,h,i,j,k,l,m);    \
        } while(0)

#define _bid_W14(x,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)                      \
        do{ _bid_XFS_##x(_bidN(100), fmts)                                      \
            (void)xBidWrite##x(hCtx, _bidN(100), a,b,c,d,e,f,g,h,i,j,k,l,m,n);  \
        } while(0)

#define _bid_W15(x,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)                    \
        do{ _bid_XFS_##x(_bidN(100), fmts)                                      \
            (void)xBidWrite##x(hCtx, _bidN(100), a,b,c,d,e,f,g,h,i,j,k,l,m,n,o);\
        } while(0)


#define BidWriteEx10A(hCtx,fmts, a,b,c,d,e,f,g,h,i,j)           _bid_W10(A,hCtx,fmts, a,b,c,d,e,f,g,h,i,j)
#define BidWriteEx11A(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k)         _bid_W11(A,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k)
#define BidWriteEx12A(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l)       _bid_W12(A,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidWriteEx13A(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)     _bid_W13(A,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidWriteEx14A(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)   _bid_W14(A,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidWriteEx15A(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o) _bid_W15(A,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidWriteEx10W(hCtx,fmts, a,b,c,d,e,f,g,h,i,j)           _bid_W10(W,hCtx,fmts, a,b,c,d,e,f,g,h,i,j)
#define BidWriteEx11W(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k)         _bid_W11(W,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k)
#define BidWriteEx12W(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l)       _bid_W12(W,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidWriteEx13W(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)     _bid_W13(W,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidWriteEx14W(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)   _bid_W14(W,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidWriteEx15W(hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o) _bid_W15(W,hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidWrite10A(fmts, a,b,c,d,e,f,g,h,i,j)                  BidWriteEx10A(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j)
#define BidWrite11A(fmts, a,b,c,d,e,f,g,h,i,j,k)                BidWriteEx11A(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k)
#define BidWrite12A(fmts, a,b,c,d,e,f,g,h,i,j,k,l)              BidWriteEx12A(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidWrite13A(fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)            BidWriteEx13A(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidWrite14A(fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)          BidWriteEx14A(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidWrite15A(fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)        BidWriteEx15A(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidWrite10W(fmts, a,b,c,d,e,f,g,h,i,j)                  BidWriteEx10W(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j)
#define BidWrite11W(fmts, a,b,c,d,e,f,g,h,i,j,k)                BidWriteEx11W(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k)
#define BidWrite12W(fmts, a,b,c,d,e,f,g,h,i,j,k,l)              BidWriteEx12W(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidWrite13W(fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)            BidWriteEx13W(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidWrite14W(fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)          BidWriteEx14W(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidWrite15W(fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)        BidWriteEx15W(_bidExt_hCtx,fmts, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#if defined( _UNICODE)
  #define BidWrite10     BidWrite10W
  #define BidWrite11     BidWrite11W
  #define BidWrite12     BidWrite12W
  #define BidWrite13     BidWrite13W
  #define BidWrite14     BidWrite14W
  #define BidWrite15     BidWrite15W

  #define BidWriteEx10   BidWriteEx10W
  #define BidWriteEx11   BidWriteEx11W
  #define BidWriteEx12   BidWriteEx12W
  #define BidWriteEx13   BidWriteEx13W
  #define BidWriteEx14   BidWriteEx14W
  #define BidWriteEx15   BidWriteEx15W
#else
  #define BidWrite10     BidWrite10A
  #define BidWrite11     BidWrite11A
  #define BidWrite12     BidWrite12A
  #define BidWrite13     BidWrite13A
  #define BidWrite14     BidWrite14A
  #define BidWrite15     BidWrite15A

  #define BidWriteEx10   BidWriteEx10A
  #define BidWriteEx11   BidWriteEx11A
  #define BidWriteEx12   BidWriteEx12A
  #define BidWriteEx13   BidWriteEx13A
  #define BidWriteEx14   BidWriteEx14A
  #define BidWriteEx15   BidWriteEx15A
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                      DEBUG-ONLY STUFF                                       //
/////////////////////////////////////////////////////////////////////////////////////////////////

#ifdef _DEBUG_TRACE_ON

  #define DTRACE10          BidTrace10
  #define DTRACE11          BidTrace11
  #define DTRACE12          BidTrace12
  #define DTRACE13          BidTrace13
  #define DTRACE14          BidTrace14
  #define DTRACE15          BidTrace15

  #define DSCOPE_ENTER10    BidScopeEnter10
  #define DSCOPE_ENTER11    BidScopeEnter11
  #define DSCOPE_ENTER12    BidScopeEnter12
  #define DSCOPE_ENTER13    BidScopeEnter13
  #define DSCOPE_ENTER14    BidScopeEnter14
  #define DSCOPE_ENTER15    BidScopeEnter15

  #define DSCOPE_LEAVE4     BidScopeLeave4
  #define DSCOPE_LEAVE5     BidScopeLeave5
  #define DSCOPE_LEAVE6     BidScopeLeave6
  #define DSCOPE_LEAVE7     BidScopeLeave7
  #define DSCOPE_LEAVE8     BidScopeLeave8
  #define DSCOPE_LEAVE9     BidScopeLeave9

  #define DTRACE10A         BidTrace10A
  #define DTRACE11A         BidTrace11A
  #define DTRACE12A         BidTrace12A
  #define DTRACE13A         BidTrace13A
  #define DTRACE14A         BidTrace14A
  #define DTRACE15A         BidTrace15A

  #define DSCOPE_ENTER10A   BidScopeEnter10A
  #define DSCOPE_ENTER11A   BidScopeEnter11A
  #define DSCOPE_ENTER12A   BidScopeEnter12A
  #define DSCOPE_ENTER13A   BidScopeEnter13A
  #define DSCOPE_ENTER14A   BidScopeEnter14A
  #define DSCOPE_ENTER15A   BidScopeEnter15A

  #define DSCOPE_LEAVE4A    BidScopeLeave4A
  #define DSCOPE_LEAVE5A    BidScopeLeave5A
  #define DSCOPE_LEAVE6A    BidScopeLeave6A
  #define DSCOPE_LEAVE7A    BidScopeLeave7A
  #define DSCOPE_LEAVE8A    BidScopeLeave8A
  #define DSCOPE_LEAVE9A    BidScopeLeave9A

 #if defined( __cplusplus )
  #define DSCOPE_AUTO10     BidScopeAuto10
  #define DSCOPE_AUTO11     BidScopeAuto11
  #define DSCOPE_AUTO12     BidScopeAuto12
  #define DSCOPE_AUTO13     BidScopeAuto13
  #define DSCOPE_AUTO14     BidScopeAuto14
  #define DSCOPE_AUTO15     BidScopeAuto15

  #define DSCOPE_AUTO10A    BidScopeAuto10A
  #define DSCOPE_AUTO11A    BidScopeAuto11A
  #define DSCOPE_AUTO12A    BidScopeAuto12A
  #define DSCOPE_AUTO13A    BidScopeAuto13A
  #define DSCOPE_AUTO14A    BidScopeAuto14A
  #define DSCOPE_AUTO15A    BidScopeAuto15A

 #endif

#else

  #define DTRACE10(tcfs, a,b,c,d,e,f,g,h,i,j)                   ((void)0)
  #define DTRACE11(tcfs, a,b,c,d,e,f,g,h,i,j,k)                 ((void)0)
  #define DTRACE12(tcfs, a,b,c,d,e,f,g,h,i,j,k,l)               ((void)0)
  #define DTRACE13(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)             ((void)0)
  #define DTRACE14(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)           ((void)0)
  #define DTRACE15(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)         ((void)0)

  #define DSCOPE_ENTER10(stf, a,b,c,d,e,f,g,h,i,j)              ((void)0)
  #define DSCOPE_ENTER11(stf, a,b,c,d,e,f,g,h,i,j,k)            ((void)0)
  #define DSCOPE_ENTER12(stf, a,b,c,d,e,f,g,h,i,j,k,l)          ((void)0)
  #define DSCOPE_ENTER13(stf, a,b,c,d,e,f,g,h,i,j,k,l,m)        ((void)0)
  #define DSCOPE_ENTER14(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)      ((void)0)
  #define DSCOPE_ENTER15(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)    ((void)0)

  #define DSCOPE_LEAVE4(stf,a,b,c,d)                            ((void)0)
  #define DSCOPE_LEAVE5(stf,a,b,c,d,e)                          ((void)0)
  #define DSCOPE_LEAVE6(stf,a,b,c,d,e,f)                        ((void)0)
  #define DSCOPE_LEAVE7(stf,a,b,c,d,e,f,g)                      ((void)0)
  #define DSCOPE_LEAVE8(stf,a,b,c,d,e,f,g,h)                    ((void)0)
  #define DSCOPE_LEAVE9(stf,a,b,c,d,e,f,g,h,i)                  ((void)0)

  #define DTRACE10A(tcfs, a,b,c,d,e,f,g,h,i,j)                  ((void)0)
  #define DTRACE11A(tcfs, a,b,c,d,e,f,g,h,i,j,k)                ((void)0)
  #define DTRACE12A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l)              ((void)0)
  #define DTRACE13A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)            ((void)0)
  #define DTRACE14A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)          ((void)0)
  #define DTRACE15A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)        ((void)0)

  #define DSCOPE_ENTER10A(stf, a,b,c,d,e,f,g,h,i,j)             ((void)0)
  #define DSCOPE_ENTER11A(stf, a,b,c,d,e,f,g,h,i,j,k)           ((void)0)
  #define DSCOPE_ENTER12A(stf, a,b,c,d,e,f,g,h,i,j,k,l)         ((void)0)
  #define DSCOPE_ENTER13A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m)       ((void)0)
  #define DSCOPE_ENTER14A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)     ((void)0)
  #define DSCOPE_ENTER15A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)   ((void)0)

  #define DSCOPE_LEAVE4A(stf,a,b,c,d)                           ((void)0)
  #define DSCOPE_LEAVE5A(stf,a,b,c,d,e)                         ((void)0)
  #define DSCOPE_LEAVE6A(stf,a,b,c,d,e,f)                       ((void)0)
  #define DSCOPE_LEAVE7A(stf,a,b,c,d,e,f,g)                     ((void)0)
  #define DSCOPE_LEAVE8A(stf,a,b,c,d,e,f,g,h)                   ((void)0)
  #define DSCOPE_LEAVE9A(stf,a,b,c,d,e,f,g,h,i)                 ((void)0)


 #if defined( __cplusplus )
  #define DSCOPE_AUTO10(stf, a,b,c,d,e,f,g,h,i,j)               ((void)0)
  #define DSCOPE_AUTO11(stf, a,b,c,d,e,f,g,h,i,j,k)             ((void)0)
  #define DSCOPE_AUTO12(stf, a,b,c,d,e,f,g,h,i,j,k,l)           ((void)0)
  #define DSCOPE_AUTO13(stf, a,b,c,d,e,f,g,h,i,j,k,l,m)         ((void)0)
  #define DSCOPE_AUTO14(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)       ((void)0)
  #define DSCOPE_AUTO15(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)     ((void)0)

  #define DSCOPE_AUTO10A(stf, a,b,c,d,e,f,g,h,i,j)              ((void)0)
  #define DSCOPE_AUTO11A(stf, a,b,c,d,e,f,g,h,i,j,k)            ((void)0)
  #define DSCOPE_AUTO12A(stf, a,b,c,d,e,f,g,h,i,j,k,l)          ((void)0)
  #define DSCOPE_AUTO13A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m)        ((void)0)
  #define DSCOPE_AUTO14A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n)      ((void)0)
  #define DSCOPE_AUTO15A(stf, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)    ((void)0)

 #endif

#endif // _DEBUG_TRACE_ON


/////////////////////////////////////////////////////////////////////////////////////////////////
//                             CUSTOM MACRO WRAPPERS FOR BidTrace                              //
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidTraceE - "Enabled" trace statement. Subsystem will not spend time to analyze TCS.
//
#define BidTraceE10A(tcfs, a,b,c,d,e,f,g,h,i,j)             _bid_T10(A,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTraceE11A(tcfs, a,b,c,d,e,f,g,h,i,j,k)           _bid_T11(A,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTraceE12A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l)         _bid_T12(A,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTraceE13A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)       _bid_T13(A,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTraceE14A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)     _bid_T14(A,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTraceE15A(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)   _bid_T15(A,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidTraceE10W(tcfs, a,b,c,d,e,f,g,h,i,j)             _bid_T10(W,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTraceE11W(tcfs, a,b,c,d,e,f,g,h,i,j,k)           _bid_T11(W,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTraceE12W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l)         _bid_T12(W,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTraceE13W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)       _bid_T13(W,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTraceE14W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)     _bid_T14(W,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTraceE15W(tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)   _bid_T15(W,TRUE,BID_ENA,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#if defined( _UNICODE)
  #define   BidTraceE10      BidTraceE10W
  #define   BidTraceE11      BidTraceE11W
  #define   BidTraceE12      BidTraceE12W
  #define   BidTraceE13      BidTraceE13W
  #define   BidTraceE14      BidTraceE14W
  #define   BidTraceE15      BidTraceE15W
#else
  #define   BidTraceE10      BidTraceE10A
  #define   BidTraceE11      BidTraceE11A
  #define   BidTraceE12      BidTraceE12A
  #define   BidTraceE13      BidTraceE13A
  #define   BidTraceE14      BidTraceE14A
  #define   BidTraceE15      BidTraceE15A
#endif


////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidTraceU - Uses user-defined, module-specific control flag.
//
#define BidTraceU10A(bit,tcfs, a,b,c,d,e,f,g,h,i,j)           _bid_T10(A,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTraceU11A(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k)         _bid_T11(A,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTraceU12A(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)       _bid_T12(A,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTraceU13A(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)     _bid_T13(A,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTraceU14A(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)   _bid_T14(A,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTraceU15A(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o) _bid_T15(A,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#define BidTraceU10W(bit,tcfs, a,b,c,d,e,f,g,h,i,j)           _bid_T10(W,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j)
#define BidTraceU11W(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k)         _bid_T11(W,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k)
#define BidTraceU12W(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)       _bid_T12(W,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l)
#define BidTraceU13W(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)     _bid_T13(W,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m)
#define BidTraceU14W(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)   _bid_T14(W,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n)
#define BidTraceU15W(bit,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o) _bid_T15(W,bit,0,tcfs, a,b,c,d,e,f,g,h,i,j,k,l,m,n,o)

#if defined( _UNICODE)
  #define   BidTraceU10     BidTraceU10W
  #define   BidTraceU11     BidTraceU11W
  #define   BidTraceU12     BidTraceU12W
  #define   BidTraceU13     BidTraceU13W
  #define   BidTraceU14     BidTraceU14W
  #define   BidTraceU15     BidTraceU15W
#else
  #define   BidTraceU10     BidTraceU10A
  #define   BidTraceU11     BidTraceU11A
  #define   BidTraceU12     BidTraceU12A
  #define   BidTraceU13     BidTraceU13A
  #define   BidTraceU14     BidTraceU14A
  #define   BidTraceU15     BidTraceU15A
#endif

#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                  End of file "BidApiEx.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
