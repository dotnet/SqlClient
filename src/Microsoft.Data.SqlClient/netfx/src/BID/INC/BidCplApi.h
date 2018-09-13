/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BidCplApi.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   BID Control Panel API
//              Interface Declaration part
//  Comments:
//              Last Modified: 04-Oct-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __BIDCPLAPI_H__ /////////////////////////////////////////////////////////////////////////
#define __BIDCPLAPI_H__

#include "BidApi.h"


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                 PREDEFINED CONTROL COMMANDS                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////

_bid_INLINE void WINAPI BidCplFlush();




/////////////////////////////////////////////////////////////////////////////////////////////////
//                          COMMAND SPACE "System.Channels.Callback"                           //
/////////////////////////////////////////////////////////////////////////////////////////////////

#define BID_CMDSPACE_CALLBACK_NAME  "System.Channels.Callback"

typedef void (WINAPI* BID_CMDSPACE_CALLBACK_CBA)(HANDLE hID, PCSTR  str, int len);
typedef void (WINAPI* BID_CMDSPACE_CALLBACK_CBW)(HANDLE hID, PCWSTR str, int len);

_bid_INLINE HANDLE WINAPI BidCplSetTextCallbackExA( INT_PTR                     cmdSpaceID,
                                                    BID_CMDSPACE_CALLBACK_CBA   fCallback,
                                                    PCSTR                       descr);

_bid_INLINE HANDLE WINAPI BidCplSetTextCallbackExW( INT_PTR                     cmdSpaceID,
                                                    BID_CMDSPACE_CALLBACK_CBW   fCallback,
                                                    PCWSTR                      descr);

HANDLE WINAPI BidCplSetTextCallbackA(BID_CMDSPACE_CALLBACK_CBA fCallback, PCSTR  descr);
HANDLE WINAPI BidCplSetTextCallbackW(BID_CMDSPACE_CALLBACK_CBW fCallback, PCWSTR descr);

#ifdef _UNICODE
  #define BID_CMDSPACE_CALLBACK_CB  BID_CMDSPACE_CALLBACK_CBW
  #define BidCplSetTextCallbackEx   BidCplSetTextCallbackExW
  #define BidCplSetTextCallback     BidCplSetTextCallbackW
#else
  #define BID_CMDSPACE_CALLBACK_CB  BID_CMDSPACE_CALLBACK_CBA
  #define BidCplSetTextCallbackEx   BidCplSetTextCallbackExA
  #define BidCplSetTextCallback     BidCplSetTextCallbackA
#endif

_bid_INLINE BOOL WINAPI BidCplRemoveTextCallbackEx(INT_PTR cmdSpaceID, HANDLE hCB);
            BOOL WINAPI BidCplRemoveTextCallback(HANDLE hCB);

/////////////////////////////////////////////////////////////////////////////////////////////////

typedef void (WINAPI* BID_CMDSPACE_CALLBACK_RCBA)( HANDLE hID, UINT_PTR src, UINT_PTR info,
                                                   int indent, PCSTR str, int len );
typedef void (WINAPI* BID_CMDSPACE_CALLBACK_RCBW)( HANDLE hID, UINT_PTR src, UINT_PTR info,
                                                   int indent, PCWSTR str, int len );

_bid_INLINE HANDLE WINAPI BidCplSetRawCallbackExA( INT_PTR                      cmdSpaceID,
                                                   BID_CMDSPACE_CALLBACK_RCBA   fCallback,
                                                   PCSTR                        descr);

_bid_INLINE HANDLE WINAPI BidCplSetRawCallbackExW( INT_PTR                      cmdSpaceID,
                                                   BID_CMDSPACE_CALLBACK_RCBW   fCallback,
                                                   PCWSTR                       descr);

HANDLE WINAPI BidCplSetRawCallbackA(BID_CMDSPACE_CALLBACK_RCBA fCallback, PCSTR  descr);
HANDLE WINAPI BidCplSetRawCallbackW(BID_CMDSPACE_CALLBACK_RCBW fCallback, PCWSTR descr);

#ifdef _UNICODE
  #define BID_CMDSPACE_CALLBACK_RCB BID_CMDSPACE_CALLBACK_RCBW
  #define BidCplSetRawCallbackEx    BidCplSetRawCallbackExW
  #define BidCplSetRawCallback      BidCplSetRawtCallbackW
#else
  #define BID_CMDSPACE_CALLBACK_RCB BID_CMDSPACE_CALLBACK_RCBA
  #define BidCplSetRawCallbackEx    BidCplSetRawCallbackExA
  #define BidCplSetRawCallback      BidCplSetRawCallbackA
#endif

_bid_INLINE BOOL WINAPI BidCplRemoveRawCallbackEx(INT_PTR cmdSpaceID, HANDLE hCB);
            BOOL WINAPI BidCplRemoveRawCallback(HANDLE hCB);


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                COMMAND SPACE "System.Filter"                                //
/////////////////////////////////////////////////////////////////////////////////////////////////

#define BID_CMDSPACE_FILTER_NAME                "System.Filter"

#define BID_CMD_FILTER_COMPONENT_ACTIVATION     BID_CMD(1)
#define BID_CMD_FILTER_TRACESET_ACTIVATION      BID_CMD(2)
  #define BID_CMD_FILTER_ACTIVATION_RESET       0
  #define BID_CMD_FILTER_ACTIVATION_INIT        1
  #define BID_CMD_FILTER_ACTIVATION_ACTIVATE    2
  #define BID_CMD_FILTER_ACTIVATION_DEACTIVATE  3
  #define BID_CMD_FILTER_ACTIVATION_DONE        4

#define BID_CMD_FILTER_DEFAULT_SET              BID_CMD(3)
#define BID_CMD_FILTER_DEFAULT_GET              BID_CMD_R(3)
  #define BID_CMD_FILTER_DEFAULT_SWITCH         0
  #define BID_CMD_FILTER_DEFAULT_APIGROUP_BITS  1

#define BID_CMD_FILTER_APIGROUP_BITSA           BID_CMD(4)
#define BID_CMD_FILTER_APIGROUP_BITSW           BID_CMD_U(4)

#define BID_CMD_FILTER_COMPONENT_ENABLEA        BID_CMD(5)
#define BID_CMD_FILTER_COMPONENT_ENABLEW        BID_CMD_U(5)
#define BID_CMD_FILTER_COMPONENT_DISABLEA       BID_CMD_R(5)
#define BID_CMD_FILTER_COMPONENT_DISABLEW       BID_CMD_UR(5)

#define BID_CMD_FILTER_TRACESET_KEYWORD_INCLA   BID_CMD(6)
#define BID_CMD_FILTER_TRACESET_KEYWORD_INCLW   BID_CMD_U(6)
#define BID_CMD_FILTER_TRACESET_KEYWORD_EXCLA   BID_CMD_R(6)
#define BID_CMD_FILTER_TRACESET_KEYWORD_EXCLW   BID_CMD_UR(6)

#ifdef _UNICODE
  #define BID_CMD_FILTER_APIGROUP_BITS          BID_CMD_FILTER_APIGROUP_BITSW
  #define BID_CMD_FILTER_COMPONENT_ENABLE       BID_CMD_FILTER_COMPONENT_ENABLEW
  #define BID_CMD_FILTER_COMPONENT_DISABLE      BID_CMD_FILTER_COMPONENT_DISABLEW
  #define BID_CMD_FILTER_TRACESET_KEYWORD_INCL  BID_CMD_FILTER_TRACESET_KEYWORD_INCLW
  #define BID_CMD_FILTER_TRACESET_KEYWORD_EXCL  BID_CMD_FILTER_TRACESET_KEYWORD_EXCLW
#else
  #define BID_CMD_FILTER_APIGROUP_BITS          BID_CMD_FILTER_APIGROUP_BITSA
  #define BID_CMD_FILTER_COMPONENT_ENABLE       BID_CMD_FILTER_COMPONENT_ENABLEA
  #define BID_CMD_FILTER_COMPONENT_DISABLE      BID_CMD_FILTER_COMPONENT_DISABLEA
  #define BID_CMD_FILTER_TRACESET_KEYWORD_INCL  BID_CMD_FILTER_TRACESET_KEYWORD_INCLA
  #define BID_CMD_FILTER_TRACESET_KEYWORD_EXCL  BID_CMD_FILTER_TRACESET_KEYWORD_EXCLA
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
#ifdef __cplusplus

namespace BidCpl {

//
//  PreFilter API helpers:
//  Activation interface
//
class IActivatable
{
 public:
    virtual void Activate() = 0;
    virtual void Deactivate() = 0;
};

//
//  Activation Helper
//
class Activation
{
 public:
    Activation(IActivatable& target) : _target(target)          { _target.Activate();   }
    ~Activation()                                               { _target.Deactivate(); }

 private:
    IActivatable& _target;

    Activation(const Activation&);                  // No copy
    Activation& operator =(const Activation&);      // No assignment
};

//
//  Helper Structure
//
struct BitSetPair
{
    DWORD   Mask;
    DWORD   Bits;

    BitSetPair(DWORD mask, DWORD bits) : Mask(mask), Bits(bits) {}
};


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ComponentFilter
//
class ComponentFilter : public IActivatable
{
    HANDLE  _handle;
    void    clear()                                 { _handle = NULL; }

    enum { MaxCompIDsInCtorOrInit = 5 };

 public:
    ComponentFilter()                               { clear();  }
    virtual ~ComponentFilter()                      { Done();   }

    ComponentFilter(bool list2Enable) {
        clear(); init(list2Enable, NULL);
    }
    ComponentFilter(bool l2E, PCTSTR cid1) {
        clear(); init(l2E, cid1, NULL);
    }
    ComponentFilter(bool l2E, PCTSTR cid1, PCTSTR cid2) {
        clear(); init(l2E, cid1, cid2, NULL);
    }
    ComponentFilter(bool l2E, PCTSTR cid1, PCTSTR cid2, PCTSTR cid3) {
        clear(); init(l2E, cid1, cid2, cid3, NULL);
    }
    ComponentFilter(bool l2E, PCTSTR cid1, PCTSTR cid2, PCTSTR cid3, PCTSTR cid4) {
        clear(); init(l2E, cid1, cid2, cid3, cid4, NULL);
    }
    ComponentFilter(bool l2E, PCTSTR cid1, PCTSTR cid2, PCTSTR cid3, PCTSTR cid4, PCTSTR cid5) {
        clear(); init(l2E, cid1, cid2, cid3, cid4, cid5, NULL);
    }

    void Done();

    ComponentFilter& Init(bool list2Enable) {
        init(list2Enable, NULL); return *this;
    }
    ComponentFilter& Init(bool l2E, PCTSTR cid1) {
        init(l2E, cid1, NULL); return *this;
    }
    ComponentFilter& Init(bool l2E, PCTSTR cid1, PCTSTR cid2) {
        init(l2E, cid1, cid2,NULL); return *this;
    }
    ComponentFilter& Init(bool l2E, PCTSTR cid1, PCTSTR cid2, PCTSTR cid3) {
        init(l2E, cid1, cid2, cid3,NULL); return *this;
    }
    ComponentFilter& Init(bool l2E, PCTSTR cid1, PCTSTR cid2, PCTSTR cid3, PCTSTR cid4) {
        init(l2E, cid1, cid2, cid3, cid4, NULL); return *this;
    }
    ComponentFilter& Init(bool l2E, PCTSTR cid1, PCTSTR cid2, PCTSTR cid3, PCTSTR cid4, PCTSTR cid5) {
        init(l2E, cid1, cid2, cid3, cid4, cid5, NULL); return *this;
    }

    //  IActivatable Members
    virtual void Activate();
    virtual void Deactivate();

    //
    //  Useful for one-line installation:
    //  filter.Init(BidCpl::AllDisabled).EnableComponentTrace("FOO.1").AndActivate();
    //
    ComponentFilter& AndActivate()
    {
        Activate();
        return *this;
    }

    bool    GetTraceEnabledByDefault() const;
    void    SetTraceEnabledByDefault(bool bEnabled);

    DWORD   GetDefaultApiGroupBits() const;
    void    SetDefaultApiGroupBits(DWORD bits);
    DWORD   SetApiGroupBits(PCTSTR moduleIdentity, DWORD mask, DWORD bits);

    ComponentFilter& EnableComponentTrace(PCTSTR moduleIdentity);
    ComponentFilter& DisableComponentTrace(PCTSTR moduleIdentity);

    static  void    Reset();
    static  INT_PTR CmdSpaceID();

 private:
    static INT_PTR      cmdSpaceID;
    static const char   cmdSpaceName[];

    void __cdecl init(bool list2Enable, ... /* PCTSTR compIdentity,,, */);

}; // ComponentFilter


//
//  Values for ComponentFilter::Init list2Enable argument:
//
//  Init(BidCpl::EnableList, _T("MOD1"), _T("MOD2"))  - tracing enabled only for modules MOD1 and MOD2
//  Init(BidCpl::DisableList, _T("MOD1"), _T("MOD2")) - tracing enabled for everyone except MOD1 and MOD2
//  Init(BidCpl::AllEnabled)                          - respectively
//  Init(BidCpl::AllDisabled)                         - respectively
//  Init(BidCpl::AllDisabled).EnableComponentTrace(_T("MOD1") == Init(BidCpl::EnableList, _T("MOD1"))
//  Init(BidCpl::AllEnabled).DisableComponentTrace(_T("MOD1") == Init(BidCpl::DisableList, _T("MOD1"))
//
const bool
    EnableList  = true,
    DisableList = false,

    AllEnabled  = false,
    AllDisabled = true;


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  TraceSetFilter
//
class TraceSetFilter : public IActivatable
{
    HANDLE  _handle;

    enum { MaxKeywordsInInit = 5 };

 public:
    TraceSetFilter()                            { _handle = NULL;   }
    virtual ~TraceSetFilter()                           { Done();   }

    void            Done();
    TraceSetFilter& Init();

    TraceSetFilter& KeywordsInclude(PCTSTR kw1) {
        return keywordsCommand(false, kw1, NULL);
    }
    TraceSetFilter& KeywordsInclude(PCTSTR kw1, PCTSTR kw2) {
        return keywordsCommand(false, kw1, kw2, NULL);
    }
    TraceSetFilter& KeywordsInclude(PCTSTR kw1, PCTSTR kw2, PCTSTR kw3) {
        return keywordsCommand(false, kw1, kw2, kw3, NULL);
    }
    TraceSetFilter& KeywordsInclude(PCTSTR kw1, PCTSTR kw2, PCTSTR kw3, PCTSTR kw4) {
        return keywordsCommand(false, kw1, kw2, kw3, kw4, NULL);
    }
    TraceSetFilter& KeywordsInclude(PCTSTR kw1, PCTSTR kw2, PCTSTR kw3, PCTSTR kw4, PCTSTR kw5) {
        return keywordsCommand(false, kw1, kw2, kw3, kw4, kw5, NULL);
    }

    TraceSetFilter& KeywordsExclude(PCTSTR kw1) {
        return keywordsCommand(true, kw1, NULL);
    }
    TraceSetFilter& KeywordsExclude(PCTSTR kw1, PCTSTR kw2) {
        return keywordsCommand(true, kw1, kw2, NULL);
    }
    TraceSetFilter& KeywordsExclude(PCTSTR kw1, PCTSTR kw2, PCTSTR kw3) {
        return keywordsCommand(true, kw1, kw2, kw3, NULL);
    }
    TraceSetFilter& KeywordsExclude(PCTSTR kw1, PCTSTR kw2, PCTSTR kw3, PCTSTR kw4) {
        return keywordsCommand(true, kw1, kw2, kw3, kw4, NULL);
    }
    TraceSetFilter& KeywordsExclude(PCTSTR kw1, PCTSTR kw2, PCTSTR kw3, PCTSTR kw4, PCTSTR kw5) {
        return keywordsCommand(true, kw1, kw2, kw3, kw4, kw5, NULL);
    }

    virtual void Activate();
    virtual void Deactivate();

    //
    //  Useful for one-line installation:
    //  ts.Init().KeywordsInclude(_T("FOO"),_T("BAR")).KeywordsExclude(_T("BAZ")).AndActivate();
    //
    TraceSetFilter& AndActivate()
    {
        Activate();
        return *this;
    }

    static void    Reset();

 private:
    static INT_PTR CmdSpaceID()                         { return ComponentFilter::CmdSpaceID(); }

    TraceSetFilter& __cdecl keywordsCommand(bool bReverse, ... /* PCTSTR keyword,,, */);

}; // TraceSetFilter


} // namespace BidCpl

#endif // __cplusplus


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                   INLINE IMPLEMENTATIONS                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////
#if !defined( _BID_NO_INLINE )

//
//  Predefined Control Commands
//
_bid_INLINE void WINAPI BidCplFlush()
{
    xBidCtlProc(BID_DCSCMD_FLUSH_BUFFERS, 0, 0, 0);
}


//
//  TextOutput Callback
//
_bid_INLINE HANDLE WINAPI
BidCplSetTextCallbackExA(INT_PTR cmdSpaceID, BID_CMDSPACE_CALLBACK_CBA fCallback, PCSTR descr)
{
    DASSERT( BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME) == cmdSpaceID );
    return (HANDLE) xBidCtlProcEx(cmdSpaceID, BID_CMD(1), fCallback, descr, 0);
}

_bid_INLINE HANDLE WINAPI
BidCplSetTextCallbackExW(INT_PTR cmdSpaceID, BID_CMDSPACE_CALLBACK_CBW fCallback, PCWSTR descr)
{
    DASSERT( BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME) == cmdSpaceID );
    return (HANDLE) xBidCtlProcEx(cmdSpaceID, BID_CMD_U(1), fCallback, descr, 0);
}

_bid_INLINE BOOL WINAPI BidCplRemoveTextCallbackEx(INT_PTR cmdSpaceID, HANDLE hCB)
{
    DASSERT( BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME) == cmdSpaceID );
    return (0 != xBidCtlProcEx(cmdSpaceID, BID_CMD_UR(1), hCB, 0, 0));
}


//
//  Raw Output Callback
//
_bid_INLINE HANDLE WINAPI
BidCplSetRawCallbackExA(INT_PTR cmdSpaceID, BID_CMDSPACE_CALLBACK_RCBA fCallback, PCSTR descr)
{
    DASSERT( BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME) == cmdSpaceID );
    return (HANDLE) xBidCtlProcEx(cmdSpaceID, BID_CMD(2), fCallback, descr, 0);
}

_bid_INLINE HANDLE WINAPI
BidCplSetRawCallbackExW(INT_PTR cmdSpaceID, BID_CMDSPACE_CALLBACK_RCBW fCallback, PCWSTR descr)
{
    DASSERT( BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME) == cmdSpaceID );
    return (HANDLE) xBidCtlProcEx(cmdSpaceID, BID_CMD_U(2), fCallback, descr, 0);
}

_bid_INLINE BOOL WINAPI BidCplRemoveRawCallbackEx(INT_PTR cmdSpaceID, HANDLE hCB)
{
    DASSERT( BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME) == cmdSpaceID );
    return (0 != xBidCtlProcEx(cmdSpaceID, BID_CMD_UR(2), hCB, 0, 0));
}


#endif // _BID_NO_INLINE



#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "BidCplApi.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
