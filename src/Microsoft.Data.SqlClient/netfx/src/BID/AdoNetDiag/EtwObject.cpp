/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       EtwObject.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Encapsulates all interactions with ETW API.
//  Comments:
//              File Created : 20-Sep-2003
//              Last Modified: 14-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "EtwObject.h"

#include "BID_SRCFILE.h"
          BID_SRCFILE;


#define BIDX_APIGROUP_COPY_12           0x00001000
#define BIDX_APIGROUP_NO_ETW_28         0x10000000

//BID_METATEXT(_T("<ApiGroup|0x00001000> Enable self-diag shadow copy output"));
//BID_METATEXT(_T("<ApiGroup|0x10000000> Disable primary output"));

#define _CONVERSION_BUF_SIZE            2050

#define ETW_LEVEL_BIT_FAST_CONVERT      0x80    // (128)
#define ETW_LEVEL_BIT_DISABLE_COMPONENT 0x40    // (64)


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ETW_RECORD
//
void ETW_RECORD::Init(PCGUID pGuid, const int* pIndexID, int numOfArgs)
{
    Header.GuidPtr  = (ULONGLONG)pGuid;
    Header.Size     = ETW_RECORD_SIZE(numOfArgs);
    Header.Flags    = WNODE_FLAG_TRACED_GUID | WNODE_FLAG_USE_MOF_PTR | WNODE_FLAG_USE_GUID_PTR;

    MOF[mofID].DataPtr = (ULONGLONG) pIndexID;
    MOF[mofID].Length  = (ULONG) sizeof(*pIndexID);

    DASSERT( (UINT)Header.Size <= (UINT)sizeof(*this) );
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  EtwApi
//
EtwApi::EtwApi(BidCtlCallback& rBidCtl) : _control(rBidCtl)
{
    _hRegister  = 0;
    _hLogger    = 0;
    _bValid     = false;
    _bEnabled   = false;
    _bCopy      = false;
    _bAsciiMode = false;
    _bRejected  = false;
    _bEtw       = true;
}

void EtwApi::Done()
{
    if( !IsValid() ) return;

    if( _hLogger != 0 )
    {
        BidTrace2( BID_TAG _T("ID:%02d  disabling: 0x%016I64X\n"), IndexID(), _hLogger );
        _control.Set( 0 );
        _control.Disable();
        _hLogger = 0;
    }

    _bEnabled = false;

    if( _hRegister != 0 )
    {
        ULONG status = ERROR_SUCCESS;
        if( _bEtw ){
            status = UnregisterTraceGuids( _hRegister );
        }
        if( status != ERROR_SUCCESS )
        {
            BidTrace4( BID_TAG1("ERR")
                    _T("%p{.}  ID:%02d  UnregisterTraceGuids: %d  hRegister: 0x%016I64X\n"),
                    this, IndexID(), status, _hRegister );
        }
        _hRegister = 0;
    }

    _bRejected  = false;
    _bAsciiMode = false;
    _bValid     = false;

} // Done


bool EtwApi::Init(const int* pIndexID, const Guid& rCtrlGuid)
{
    DASSERT( !IsValid() );

    _transactGuid1.SeriesFrom( rCtrlGuid );
    _etwEventText.Init( _transactGuid1.GetPtr(), pIndexID, 1 );
    //
    // more ETW_RECORD templates...
    //

    _bEtw   = !BidIsOn(BIDX_APIGROUP_NO_ETW_28);
    _bCopy  = BidIsOn(BIDX_APIGROUP_COPY_12);
    _bValid = true;

    TRACE_GUID_REGISTRATION TraceGuidReg[] =
    {
        { _transactGuid1.GetPtr(), NULL }
    };

    ULONG status = ERROR_SUCCESS;

    if( _bEtw ){
        status = RegisterTraceGuids(
            CtrlCallback,       // Enable/disable function.
            this,               // RequestContext parameter
            rCtrlGuid.GetPtr(), // Provider GUID
            1,                  // TraceGuidReg array size
            TraceGuidReg,       // Array of TraceGuidReg structures
            NULL,               // Optional WMI - MOFImagePath
            NULL,               // Optional WMI - MOFResourceName
            &_hRegister         // Handle required to unregister.
        );
    }
    else {
        //
        //  Artificial activation
        //
        DWORD bits = BidGetApiGroupBits(0x0FFF0000) >> 16;
        if( bits == 0 ){
            bits = (BID_APIGROUP_SCOPE|BID_APIGROUP_TRACE);
        }
        _control.Set( bits );
        _bEnabled = true;
    }

    if( status != ERROR_SUCCESS )
    {
        BidTrace4( BID_TAG1("ERR") _T("%p{.} ID:%02d  RegisterTraceGuids: %d  L\"%s\"\n"),
                   this, IndexID(), status, (PCTSTR)rCtrlGuid.ToString() );
        _control.Set(0);

        return false;                                                   // <<== EARLY EXIT
    }

    if( _bRejected )
    {
        BidTrace2( BID_TAG1("WARN") _T("%p{.} ID:%02d  Requested rejection\n"),
                   this, IndexID() );
        return false;                                                   // <<== EARLY EXIT
    }

    _control.Enable();
    return true;

} // Init


ULONG WINAPI EtwApi::CtrlCallback( WMIDPREQUESTCODE code, PVOID ctx, ULONG*, PVOID Buffer )
{
    EtwApi* This    = (EtwApi*)ctx;
    ULONG   status  = ERROR_SUCCESS;
    ULONG   ulCtrls = 0;
    UCHAR   ucLevel = 0;

    BidScopeAuto2( BID_TAG _T("%p{.}  %d{WMIDPREQUESTCODE}\n"), This, code );

    switch( code )
    {
     case WMI_ENABLE_EVENTS:
        if( (This->_hLogger = GetTraceLoggerHandle(Buffer)) != NULL )
        {
            ulCtrls = GetTraceEnableFlags(This->_hLogger);
            ucLevel = GetTraceEnableLevel(This->_hLogger);
        }

        BidTrace5( _T("WMI_ENABLE_EVENTS: ID:%02d  %016I64X  ")
                   _T("Buffer: %p  Flags: %08X  Level: %02X\n"),
                   This->IndexID(), This->_hLogger, Buffer, ulCtrls, ucLevel );

        if( ulCtrls == 0 )
        {
            ulCtrls = (BID_APIGROUP_SCOPE|BID_APIGROUP_TRACE);
        }

        if( ucLevel != 0 )
        {
            This->_bAsciiMode = (ucLevel & ETW_LEVEL_BIT_FAST_CONVERT) != 0;
            This->_bRejected  = (ucLevel & ETW_LEVEL_BIT_DISABLE_COMPONENT) != 0;
        }

        if( !This->_bRejected )
        {
            This->_bEnabled = true;
            This->_control.Set( (DWORD)ulCtrls );
        }
        break;

     case WMI_DISABLE_EVENTS:
        BidTrace2( _T("WMI_DISABLE_EVENTS: ID:%02d  0x%016I64X\n"),
                    This->IndexID(), This->_hLogger );

        This->_control.Set( 0 );
        This->_bEnabled = false;
        This->_hLogger = 0;
        break;

     default:
        status = ERROR_INVALID_PARAMETER;
        BidTrace2( BID_TAG1("ERR") _T("ID:%02d  Unknown command code %d\n"),
                    This->IndexID(), code );
    }

    return status;

} // CtrlCallback


void EtwApi::traceEvent(ETW_RECORD* pEvent)
{
    ULONG status = ERROR_SUCCESS;

    if( _bEtw ){
        status = TraceEvent( _hLogger, (PEVENT_TRACE_HEADER)pEvent );
    }
    if( status != ERROR_SUCCESS )
    {
        BidTrace2( BID_TAG1("ERR") _T("%p{.}  %d{STATUS}\n"), this, status );
    }
}

/////////////////////////////////////////////////////////////////////////////////////////////////

static int fastConvert2Ascii( PSTR dstA, PCWSTR srcW, int dstCapacity, int srcLen )
{
    PSTR pBuf = dstA;

    while( --dstCapacity > 0 && --srcLen >= 0 )
    {
        if( (*pBuf = (char)(*srcW++)) == '\0' ) break;
        pBuf++;
    }
    if( *pBuf != '\0' ) *pBuf = '\0';

    return (int)(pBuf - dstA);
}

/////////////////////////////////////////////////////////////////////////////////////////////////

int EtwApi::IndexID() const
{
    int* pIndexID = (int*)_etwEventText.MOF[mofID].DataPtr;
    return pIndexID ? *pIndexID : 0;
}


void EtwApi::TextW(PCWSTR str, int strLen)
{
    DASSERT( strLen >= 0 );

    if( !_bEnabled ) return;                                        // <<== EARLY EXIT

    if( _bAsciiMode  &&  strLen < _CONVERSION_BUF_SIZE )
    {
        char dstBuf [_CONVERSION_BUF_SIZE];
        int  len = fastConvert2Ascii( dstBuf, str, _countof(dstBuf), strLen );

        TextA( dstBuf, len );
        return;                                                     // <<== EARLY EXIT
    }

    _etwEventText.Header.Class.Type     = ETW_CLASSTYPE_TEXT_W;
    _etwEventText.MOF[mofArg1].DataPtr  = (ULONGLONG) str;
    _etwEventText.MOF[mofArg1].Length   = (ULONG) ((strLen + 1) * sizeof(str[0]));

    traceEvent( &_etwEventText );

    if( _bCopy )
    {
        int dstLen = strLen;
        if( strLen > 0 && str[strLen-1] == L'\n' ){
            strLen--;
        }

        int   indexID  = *((int*)_etwEventText.MOF[mofID].DataPtr);
        DWORD threadID = GetCurrentThreadId();

       #ifdef _DEBUG_TRACE_ON
        BidTraceE5W( L"**%03X:%02d:%-3d %.*ls\n", threadID, indexID, dstLen, strLen, str );
       #else
        BidTraceE4W( L"%03X:%02d: %.*ls\n", threadID, indexID, strLen, str );
        UNUSED_ALWAYS(dstLen);
       #endif
    }

} // EtwApi::TextW


void EtwApi::TextA(PCSTR str, int strLen)
{
    DASSERT( strLen >= 0 );

    if( !_bEnabled ) return;                                        // <<== EARLY EXIT

    _etwEventText.Header.Class.Type     = ETW_CLASSTYPE_TEXT_A;
    _etwEventText.MOF[mofArg1].DataPtr  = (ULONGLONG) str;
    _etwEventText.MOF[mofArg1].Length   = (ULONG) ((strLen + 1) * sizeof(str[0]));

    traceEvent( &_etwEventText );

    if( _bCopy )
    {
        int dstLen = strLen;
        if( strLen > 0 && str[strLen-1] == '\n' ){
            strLen--;
        }

        int   indexID  = *((int*)_etwEventText.MOF[mofID].DataPtr);
        DWORD threadID = GetCurrentThreadId();

       #ifdef _DEBUG_TRACE_ON
        BidTraceE5A( "**%03X:%02d:%-3d %.*hs\n", threadID, indexID, dstLen, strLen, str );
       #else
        BidTraceE4A( "%03X:%02d: %.*hs\n", threadID, indexID, strLen, str );
        UNUSED_ALWAYS(dstLen);
       #endif
    }

} // EtwApi::TextA



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                End of file "EtwObject.cpp"                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////
