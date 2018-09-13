/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       EtwObject.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Encapsulates all interactions with ETW API.
//  Comments:
//              File Created : 20-Sep-2003
//              Last Modified: 14-Mar-2003
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __ETWOBJECT_H__ /////////////////////////////////////////////////////////////////////////
#define __ETWOBJECT_H__
#ifndef _NOLIST_HDRS
#pragma message("  EtwObject.h")
#endif

#include "services.h"

//
//  Event Tracing for Windows API (ETW).
//  Note that we DO use static linking here. On downgraded platforms, where AdvAPI32.dll doesn't
//  export tracing functions that we use, this dll just can't be loaded and the whole
//  subsystem will be automatically disabled.
//
#ifndef _SYS_GUID_OPERATORS_
#define _SYS_GUID_OPERATORS_
#endif
#include "WMIstr.h"
#include "Evntrace.h"


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ETW_RECORD
//
#define ETW_CLASSTYPE_TEXT_A        0x11
#define ETW_CLASSTYPE_TEXT_W        0x12

enum {
    mofID   = 0,
    mofArg1 = 1,
    mofArg2 = 2,
    mofMax  = 3
};

struct ETW_RECORD
{
    EVENT_TRACE_HEADER  Header;
    MOF_FIELD           MOF[mofMax];

    ETW_RECORD()                                                                { cleanup();    }

    void    Done()                                                              { cleanup();    }
    void    Init(PCGUID pGuid, const int* pIndexID, int numOfArgs);

 private:
    void    cleanup()                                   { YAWL_ZeroMemory(this, sizeof(*this)); }

}; // ETW_RECORD


#define ETW_RECORD_SIZE(numOfArgs)  \
                    (SHORT)(sizeof(EVENT_TRACE_HEADER) + (sizeof(MOF_FIELD) * (numOfArgs + 1)))


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  EtwApi
//
class EtwApi
{
    TRACEHANDLE     _hRegister;
    TRACEHANDLE     _hLogger;
    BidCtlCallback& _control;
    Guid            _transactGuid1;
    ETW_RECORD      _etwEventText;
    bool            _bValid;
    bool            _bEnabled;
    bool            _bEtw;
    bool            _bCopy;
    bool            _bAsciiMode;
    bool            _bRejected;

 public:
    EtwApi(BidCtlCallback& rBidCtl);
    ~EtwApi()                                                                       { Done();   }

    void    Done();
    bool    Init(const int* pIndexID, const Guid& rCtrlGuid);
    bool    IsEnabled() const                                               { return _bEnabled; }
    bool    IsValid() const                                                 { return _bValid;   }
    int     IndexID() const;

    void    TextA(PCSTR  str, int strLen);
    void    TextW(PCWSTR str, int strLen);

 private:
    void    traceEvent(ETW_RECORD* pEvent);
    static  ULONG WINAPI CtrlCallback(WMIDPREQUESTCODE code, PVOID ctx, ULONG*, PVOID Buffer);

}; // EtwApi


#ifndef TextT
  #ifdef _UNICODE
    #define TextT   TextW
  #else
    #define TextT   TextA
  #endif
#endif

#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "EtwObject.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
