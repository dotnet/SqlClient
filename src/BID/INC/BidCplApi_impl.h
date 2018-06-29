/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BidCplApi_impl.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   BID Control Panel API
//              Interface Implementation part
//  Comments:
//              Last Modified: 04-Oct-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __BIDCPLAPI_IMPL_H__ ////////////////////////////////////////////////////////////////////
#define __BIDCPLAPI_IMPL_H__

#ifndef __BIDCPLAPI_H__
  #error BidCplApi.h must be included before BidCplApi_impl.h
#endif
#ifndef _BIDIMPL_INCLUDED
  #error BID Interface Implementation must be included before BidCplApi_impl.h
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                          COMMAND SPACE "System.Channels.Callback"                           //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  TextOutput Callback
//
HANDLE WINAPI BidCplSetTextCallbackA(BID_CMDSPACE_CALLBACK_CBA fCallback, PCSTR  descr)
{
    INT_PTR cmdSpaceID = BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME);
    return BidCplSetTextCallbackExA(cmdSpaceID, fCallback, descr);
}

HANDLE WINAPI BidCplSetTextCallbackW(BID_CMDSPACE_CALLBACK_CBW fCallback, PCWSTR descr)
{
    INT_PTR cmdSpaceID = BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME);
    return BidCplSetTextCallbackExW(cmdSpaceID, fCallback, descr);
}

BOOL WINAPI BidCplRemoveTextCallback(HANDLE hCB)
{
    INT_PTR cmdSpaceID = BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME);
    return BidCplRemoveTextCallbackEx(cmdSpaceID, hCB);
}

//
//  Raw Output Callback
//
HANDLE WINAPI BidCplSetRawCallbackA(BID_CMDSPACE_CALLBACK_RCBA fCallback, PCSTR  descr)
{
    INT_PTR cmdSpaceID = BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME);
    return BidCplSetRawCallbackExA(cmdSpaceID, fCallback, descr);
}

HANDLE WINAPI BidCplSetRawCallbackW(BID_CMDSPACE_CALLBACK_RCBW fCallback, PCWSTR descr)
{
    INT_PTR cmdSpaceID = BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME);
    return BidCplSetRawCallbackExW(cmdSpaceID, fCallback, descr);
}

BOOL WINAPI BidCplRemoveRawCallback(HANDLE hCB)
{
    INT_PTR cmdSpaceID = BidGetCmdSpaceID(BID_CMDSPACE_CALLBACK_NAME);
    return BidCplRemoveRawCallbackEx(cmdSpaceID, hCB);
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                COMMAND SPACE "System.Filter"                                //
/////////////////////////////////////////////////////////////////////////////////////////////////
#ifdef __cplusplus

namespace BidCpl {

//
//  ComponentFilter
//
const INT_PTR CmdSpaceNotInitialized = -1;

const char  ComponentFilter::cmdSpaceName[] = BID_CMDSPACE_FILTER_NAME;
INT_PTR     ComponentFilter::cmdSpaceID     = CmdSpaceNotInitialized;

void ComponentFilter::Done()
{
    if( _handle != NULL )
    {
        Deactivate();
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_COMPONENT_ACTIVATION,
                    _handle, BID_CMD_FILTER_ACTIVATION_DONE, 0 );
        clear();
    }
}

void __cdecl ComponentFilter::init(bool list2Enable, ... /* PCTSTR compIdentity,,, */)
{
    Done();

    _handle = (HANDLE) BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_COMPONENT_ACTIVATION,
                                   NULL, BID_CMD_FILTER_ACTIVATION_INIT, 0 );

    if( _handle == NULL )
    {
        BidTraceU1( BID_ADV, BID_TAG1("FAIL|ADV") _T("module: %Iu\n"), xBidID );
        return;                                                         // << == EARLY EXIT
    }

    SetTraceEnabledByDefault( !list2Enable );

    va_list argptr;
    va_start(argptr, list2Enable);

    PCTSTR  identity = va_arg(argptr, PCTSTR);
    int     cnt      = 0;

    while( ++cnt <= MaxCompIDsInCtorOrInit && identity != NULL )
    {
        if( list2Enable ){
            EnableComponentTrace(identity);
        } else {
            DisableComponentTrace(identity);
        }
        identity = va_arg(argptr, PCTSTR);
    }

} // init


bool ComponentFilter::GetTraceEnabledByDefault() const
{
    DWORD tmpBuf = 0;
    if( _handle != NULL ){
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_DEFAULT_GET,
                    _handle, &tmpBuf, BID_CMD_FILTER_DEFAULT_SWITCH );
    }
    return (tmpBuf != 0);
}

void ComponentFilter::SetTraceEnabledByDefault(bool bEnabled)
{
    if( _handle != NULL ){
        DWORD tmpBuf = bEnabled ? 1 : 0;
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_DEFAULT_SET,
                    _handle, &tmpBuf, BID_CMD_FILTER_DEFAULT_SWITCH );
    }
}


DWORD ComponentFilter::GetDefaultApiGroupBits() const
{
    DWORD tmpBuf = 0;
    if( _handle != NULL ){
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_DEFAULT_GET,
                    _handle, &tmpBuf, BID_CMD_FILTER_DEFAULT_APIGROUP_BITS );
    }
    return tmpBuf;
}

void ComponentFilter::SetDefaultApiGroupBits(DWORD bits)
{
    if( _handle != NULL ){
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_DEFAULT_SET,
                    _handle, &bits, BID_CMD_FILTER_DEFAULT_APIGROUP_BITS );
    }
}

DWORD ComponentFilter::SetApiGroupBits(PCTSTR moduleIdentity, DWORD mask, DWORD bits)
{
    if( _handle == NULL ){
        return 0;                                                       // <<== EARLY EXIT
    }
    BitSetPair dataBlock(mask, bits);

    return (DWORD) BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_APIGROUP_BITS,
                               _handle, moduleIdentity, &dataBlock );
}

ComponentFilter& ComponentFilter::EnableComponentTrace(PCTSTR moduleIdentity)
{
    if( _handle != NULL ){
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_COMPONENT_ENABLE,
                    _handle, moduleIdentity, 0 );
    }
    return *this;
}

ComponentFilter& ComponentFilter::DisableComponentTrace(PCTSTR moduleIdentity)
{
    if( _handle != NULL ){
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_COMPONENT_DISABLE,
                    _handle, moduleIdentity, 0 );
    }
    return *this;
}


// IActivatable Members
void ComponentFilter::Activate()
{
    if( _handle != NULL ){
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_COMPONENT_ACTIVATION,
                    _handle, BID_CMD_FILTER_ACTIVATION_ACTIVATE, 0 );
    }
}
void ComponentFilter::Deactivate()
{
    if( _handle != NULL ){
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_COMPONENT_ACTIVATION,
                    _handle, BID_CMD_FILTER_ACTIVATION_DEACTIVATE, 0 );
    }
}


void ComponentFilter::Reset() // static
{
    BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_COMPONENT_ACTIVATION,
                NULL, BID_CMD_FILTER_ACTIVATION_RESET, 0 );
}


INT_PTR ComponentFilter::CmdSpaceID() // static
{
    if( cmdSpaceID == CmdSpaceNotInitialized )
    {
        cmdSpaceID = BidGetCmdSpaceID( cmdSpaceName );

        BidTraceU1( cmdSpaceID == NULL && BID_ADV,
                    BID_TAG1("ERR|ADV")
                    _T("Selected BID implementation doesn't support command space \"%hs\"\n"),
                    cmdSpaceName );
    }
    return cmdSpaceID;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  TraceSetFilter
//
void TraceSetFilter::Done()
{
    if( _handle != NULL )
    {
        Deactivate();
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_TRACESET_ACTIVATION,
                    _handle, BID_CMD_FILTER_ACTIVATION_DONE, 0 );
        _handle = NULL;
    }
}

TraceSetFilter& TraceSetFilter::Init()
{
    Done();

    _handle = (HANDLE) BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_TRACESET_ACTIVATION,
                                   NULL, BID_CMD_FILTER_ACTIVATION_INIT, 0 );

    BidTraceU1( _handle == NULL && BID_ADV,
                BID_TAG1("FAIL|ADV") _T("module: %Iu\n"), xBidID );
    return *this;
}


// IActivatable Members
void TraceSetFilter::Activate()
{
    if( _handle != NULL )
    {
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_TRACESET_ACTIVATION,
                    _handle, BID_CMD_FILTER_ACTIVATION_ACTIVATE, 0 );
    }
}
void TraceSetFilter::Deactivate()
{
    if( _handle != NULL )
    {
        BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_TRACESET_ACTIVATION,
                    _handle, BID_CMD_FILTER_ACTIVATION_DEACTIVATE, 0 );
    }
}

TraceSetFilter& __cdecl TraceSetFilter::keywordsCommand(bool bReverse, ... /* PCTSTR keyword,,, */)
{
    if( _handle == NULL ){
        Init();
    }
    if( _handle != NULL )
    {
        int cmdCode = bReverse ? BID_CMD_FILTER_TRACESET_KEYWORD_EXCL
                               : BID_CMD_FILTER_TRACESET_KEYWORD_INCL;

        va_list argptr;
        va_start(argptr, bReverse);

        PCTSTR  keyword = va_arg(argptr, PCTSTR);
        int     cnt     = 0;

        while( ++cnt <= MaxKeywordsInInit && keyword != NULL )
        {
            BidCtlProc( CmdSpaceID(), cmdCode, _handle, keyword, 0 );
            keyword = va_arg(argptr, PCTSTR);
        }
    }
    return *this;

} // keywordsCommand


void TraceSetFilter::Reset() // static
{
    BidCtlProc( CmdSpaceID(), BID_CMD_FILTER_TRACESET_ACTIVATION,
                NULL, BID_CMD_FILTER_ACTIVATION_RESET, 0 );
}



} // namespace BidCpl

#endif // __cplusplus



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                   OUTLINE IMPLEMENTATIONS                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
#if defined( _BID_NO_INLINE )

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
//                               End of file "BidCplApi_impl.h"                                //
/////////////////////////////////////////////////////////////////////////////////////////////////
