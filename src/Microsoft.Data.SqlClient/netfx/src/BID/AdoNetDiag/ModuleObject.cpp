/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       ModuleObject.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Client Module Object.
//  Comments:
//              File Created : 08-Sep-2003
//              Last Modified: 23-Mar-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "ModuleObject.h"

#include "BID_SRCFILE.h"
          BID_SRCFILE;

//
//  Buffer size (in chars) for sprintf formatting
//
const int BufSize = 2050;

//
//  Object Maintenance
//
ModuleObject::ModuleObject(int ordinal) : _etwApi(_ctlCallback)
{
    _indexID    = ordinal;
    _bInUse     = false;
    _bActivated = false;
    BidObtainItemID(&_objID, BID_TAG1("ID|OBJ") _T("%p{.}"), this);
}

ModuleObject::~ModuleObject()
{
    Done(true);
    BidRecycleItemID(&_objID, BID_TAG1("ID|OBJ"));
}

bool ModuleObject::IsValid() const
{
    return IBidApi::IsValid();
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Initialization / Finalization
//
void ModuleObject::Done(bool bForcedCleanup)
{
    if( !IsInUse() ) return;

    BidScopeAuto4( BID_TAG _T("%p{.} ID:%02d  %s%s\n"),
                   this, IndexID(), (PCTSTR)_identity,
                   bForcedCleanup ? _T(" : FORCED") : _T("") );

    ReportDisconnection(bForcedCleanup);

    _unloadCallback.Done(IndexID(), bForcedCleanup);
    _etwApi.Done();
    _ctlCallback.Done();
    _instanceIdProvider.Done();
    _identity.Done();
    _modPath.Done();
    _modHandle.Done();
    _cfgBits.Done();
    _bInUse     = false;
    _bActivated = false;
}

bool ModuleObject::Init(const BindingContract& binding,
                        DWORD* pGblFlags, BID_CTLCALLBACK ctlProc, PBIDHOOKS pHooks)
{
    BidScopeAuto1(BID_TAG _T("%p{.}"), this);

    DASSERT( !IsInUse() );
    DASSERT( !IsActivated() );

    binding.Populate(_cfgBits, _modHandle, _modPath, _identity);
    _unloadCallback.Init(pHooks);
    _ctlCallback.Init(_cfgBits, pGblFlags, ctlProc);
    _instanceIdProvider.Init();
    _bInUse = true;

    bool bOk = _unloadCallback.IsValid();
    if( bOk ){
        bOk = _etwApi.Init(IndexIDPtr(), _identity.GetGuidRef());
    }
    if( bOk ){
        BidUpdateItemID(&_objID, BID_TAG1("ID|OBJ") _T("%s"), (PCTSTR)_modPath);
    }

    BidTrace4(BID_TAG1("RET") _T("%u#  %d{bool}  ID:%02d  %s\n"),
              ObjID(), bOk, IndexID(), (PCTSTR)_identity.ToString());
    return bOk;
}

DWORD ModuleObject::GetCtrlFlags() const
{
    return _ctlCallback.GetCache();
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Activation reporting
//
void ModuleObject::ReportConnection()
{
    if( !IsActivated() )
    {
        _bActivated = true;

        BidxMessage4( _T("%02d:CONNECTED [%p]%s  %s\n"), IndexID(),
                    (HMODULE)_modHandle, (PCTSTR)_modPath, _identity.ToString() );
    }
}

void ModuleObject::ReportDisconnection(bool bForced)
{
    if( IsActivated() )
    {
        BidxMessage4( _T("%02d:DISCONNECTED [%p]\"%s\"%s\n"),
                    IndexID(), (HMODULE)_modHandle, (PCTSTR)_identity,
                    bForced ? _T(" : FORCED") : _T("") );

        _bActivated = false;
    }
}

void ModuleObject::ReportRejection(const BindingContract& binding)
{
    BidxMessage4( _T("00:REJECTED(%d) [%p]%s  \"%s\"\n"), binding.GetVersion(),
                binding.GetModuleHandle(), binding.GetModulePath(), binding.GetIdentity() );

}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Static Helpers
//
HMODULE ModuleObject::_hModuleSelf = (HMODULE)BID_NOHANDLE;

HMODULE ModuleObject::GetSelfModuleHandle()
{
    DASSERT( _hModuleSelf != (HMODULE)BID_NOHANDLE );
    return _hModuleSelf;
}

void ModuleObject::InitSelfDescriptior( PModuleObject pModule, HANDLE hModule )
{
    _hModuleSelf = (HMODULE)hModule;

    BindingContract binding;
    binding.Init( hModule );

    if( !pModule->Init(binding, NULL, NULL, NULL) )
    {
        BidTrace1(BID_TAG1("ERR") _T("%p{.}\n"), pModule);
    }
}

void ModuleObject::DoneSelfDescriptior( PModuleObject pSelf )
{
    pSelf->Done();
}

bool ModuleObject::IsValidPtr(PModuleObject pObj)
{
    bool bValid = false;

    __try
    {
        bValid = pObj->IsValid();
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        bValid = false;
    }

    BidTraceU1( BidIsOn(BID_APIGROUP_TRACE) && !bValid,
                BID_TAG1("RET") _T("false  %p{PModuleObject}\n"), pObj );
    return bValid;
}



/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Implementation Helpers
//
void ModuleObject::hexDump( PCVOID pBlob, int sizeInBytes )
{
    const int NCols = 16;

    BYTE*   data    = (BYTE*)pBlob;
    int     NRows   = sizeInBytes / NCols;
    int     NRemain = sizeInBytes % NCols;

    char    buf [NCols+1];

    indent_In();

    int i = 0;
    int n = 0;

    for( n = 0; n < NRows; n++ )
    {
        i = n * NCols;

        for (int j = 0; j < NCols; j++) {
            buf[j] = (char)data [i+j];
            if (buf[j] < ' ') buf[j] = '.';
        }
        buf[NCols] = '\0';

        traceA( "%08X:  "
                "%02X %02X %02X %02X %02X %02X %02X %02X | "
                "%02X %02X %02X %02X %02X %02X %02X %02X  "
                "%hs\n",
                i,
                data[i],    data[i+1],  data[i+2],  data[i+3],
                data[i+4],  data[i+5],  data[i+6],  data[i+7],
                data[i+8],  data[i+9],  data[i+10], data[i+11],
                data[i+12], data[i+13], data[i+14], data[i+15],
                buf );
    }

    if( NRemain > 0 )
    {
        char  cvtBuf[50];
        CSTRA_(tmpBuf, 100);

        i = NRows * NCols;

        _snprintf(cvtBuf, _countof(cvtBuf)-1, "%08X:  ", i);
        tmpBuf = cvtBuf;

        for (n = 0; n < NRemain; n++)
        {
            _snprintf(cvtBuf, _countof(cvtBuf)-1, "%02X ", data[i+n]);
            tmpBuf << cvtBuf;
            if (n == ((NCols / 2) - 1)) tmpBuf << "| ";
        }
        for (n = NRemain; n < NCols; n++)
        {
            tmpBuf << "   ";
            if (n == ((NCols / 2) - 1)) tmpBuf << "| ";
        }
        tmpBuf << " ";
        for (n = 0; n < NRemain; n++)
        {
            char chr = data[i+n];
            if (chr < ' ') chr = '.';
            tmpBuf << chr;
        }
        traceA( "%hs\n", tmpBuf );
    }

    indent_Out();

} // ModuleObject::hexDump


void ModuleObject::indent_In()
{
    Indent(BID_INDENT_IN);
}

void ModuleObject::indent_Out()
{
    Indent(BID_INDENT_OUT);
}

void __cdecl ModuleObject::traceA(PCSTR fmt, ... )
{
    va_list va;
    va_start(va, fmt);
    TraceVA( 0, BID_ENA, fmt, va );
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Plain Text Output
//
BOOL ModuleObject::PutStrA( UINT_PTR /*src*/, UINT_PTR /*info*/, PCSTR  str )
{
    int strLen = GetStrLenA(str);
    _etwApi.TextA(str, strLen);
    return TRUE;
}

BOOL ModuleObject::PutStrW( UINT_PTR /*src*/, UINT_PTR /*info*/, PCWSTR str )
{
    int strLen = GetStrLenW(str);
    _etwApi.TextW(str, strLen);
    return TRUE;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Trace
//
BOOL ModuleObject::TraceVA(UINT_PTR src, UINT_PTR info, PCSTR  fmt, va_list argptr)
{
    BOOL bRet = TRUE;

    if( BID_InfoIsBlob(info) )
    {
        //
        //  For simplicity of this implementation we assume that
        //  BidWriteBin / BidTraceBin are enabled and explicit prefiltering is implemented
        //  in the caller.
        //
        PCVOID* pArgs   = (PCVOID*)argptr;
        PCVOID  pBlob   = *pArgs++;
        int     blobSz  = (int)(INT_PTR)(*pArgs);

        hexDump( pBlob, blobSz );
    }
    else if( BID_InfoIsEnabled(info) || EnabledA(src, info, fmt) )
    {
        char    Buf [BufSize];
        int     len;

        if( BID_NotAPointer(fmt) )
        {
            len = _snprintf( Buf, _countof(Buf)-1, "<strippedFormat %d>\n", BID_GetIndex(fmt) );
        }
        else
        {
            len = _vsnprintf( Buf, _countof(Buf), fmt, argptr );
            Buf [_countof(Buf) - 1] = '\0';
            if( len < 0 )
            {
                Buf [0] = '\0';
                len = 0;
                BidTrace2(BID_TAG1("ERR") _T("%u#  %p{PCSTR}\n"), ObjID(), fmt);
            }
            else if( len == _countof(Buf) )
            {
                strcpy( &Buf [_countof(Buf)-5], "...\n" );
                len = _countof(Buf)-1;
            }
        }

        _etwApi.TextA(Buf, len);
    }
    return bRet;

} // ModuleObject::TraceVA


BOOL ModuleObject::TraceVW(UINT_PTR src, UINT_PTR info, PCWSTR fmt, va_list argptr)
{
    BOOL bRet = TRUE;

    if( BID_InfoIsBlob(info) )
    {
        //
        //  For simplicity of this implementation we assume that
        //  BidWriteBin / BidTraceBin are enabled and explicit prefiltering is implemented
        //  in the caller.
        //
        PCVOID* pArgs   = (PCVOID*)argptr;
        PCVOID  pBlob   = *pArgs++;
        int     blobSz  = (int)(INT_PTR)(*pArgs);

        hexDump( pBlob, blobSz );
    }
    else if( BID_InfoIsEnabled(info) || EnabledW(src, info, fmt) )
    {
        WCHAR   Buf [BufSize];
        int     len;

        if( BID_NotAPointer(fmt) )
        {
            len = _snwprintf(Buf, _countof(Buf)-1, L"<strippedFormat %d>\n", BID_GetIndex(fmt));
        }
        else
        {
            len = _vsnwprintf( Buf, _countof(Buf), fmt, argptr );
            Buf [_countof(Buf) - 1] = L'\0';
            if( len < 0 )
            {
                Buf [0] = L'\0';
                len = 0;
                BidTrace2(BID_TAG1("ERR") _T("%u#  %p{PCWSTR}\n"), ObjID(), fmt);
            }
            else if( len == _countof(Buf) )
            {
                wcscpy( &Buf [_countof(Buf)-5], L"...\n" );
                len = _countof(Buf)-1;
            }
        }

        _etwApi.TextW(Buf, len);
    }
    return bRet;

} // ModuleObject::TraceVW


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Scope
//
#define HDR_LEAVE_W    L"leave_Xx\n"
#define HDR_LEAVE_A     "leave_Xx\n"
#define HDR_ENTER_W    L"enter_Xx "
#define HDR_ENTER_A     "enter_Xx "
#define HDR_LEN         9
#define HDR_IND_IDX     6

DASSERT_COMPILER( _countof(HDR_ENTER_A) == _countof(HDR_ENTER_W) );
DASSERT_COMPILER( _countof(HDR_ENTER_A) == _countof(HDR_LEAVE_A) );
DASSERT_COMPILER( _countof(HDR_ENTER_A) == HDR_LEN+1 );


static void makeHdrA(PSTR Buf, int nIdx, bool bEnter)
{
    static char hexDigit[] = "0123456789ABCDEF";

    strcpy(Buf, bEnter ? HDR_ENTER_A : HDR_LEAVE_A);

    DASSERT( Buf [HDR_IND_IDX] == 'X' );
    DASSERT( Buf [HDR_IND_IDX+1] == 'x' );

    Buf [HDR_IND_IDX]   = hexDigit[ ((nIdx & 0xF0) >> 4) ];
    Buf [HDR_IND_IDX+1] = hexDigit[ (nIdx & 0x0F) ];

    DASSERT( Buf [HDR_LEN] == '\0' );
}

static void makeHdrW(PWSTR Buf, int nIdx, bool bEnter)
{
    static WCHAR hexDigit[] = L"0123456789ABCDEF";

    wcscpy(Buf, bEnter ? HDR_ENTER_W : HDR_LEAVE_W);

    DASSERT( Buf [HDR_IND_IDX] == L'X' );
    DASSERT( Buf [HDR_IND_IDX+1] == L'x' );

    Buf [HDR_IND_IDX]   = hexDigit[ ((nIdx & 0xF0) >> 4) ];
    Buf [HDR_IND_IDX+1] = hexDigit[ (nIdx & 0x0F) ];

    DASSERT( Buf [HDR_LEN] == L'\0' );
}

#ifdef _UNICODE
  #define makeHdrT  makeHdrW
#else
  #define makeHdrT  makeHdrA
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////

BOOL ModuleObject::ScopeEnterVA(UINT_PTR src, UINT_PTR info, HANDLE* pHScp,
                                PCSTR  stf, va_list argptr)
{
    if( !EnabledA(src, info, stf) )
    {
        *pHScp = BID_NOHANDLE;
        return TRUE;                                                // <<== EARLY EXIT
    }

    Indent_ACCESS();

    char    Buf [BufSize + HDR_LEN + 2];
    PSTR    pBuf = Buf + HDR_LEN;
    int     len = 0;
    int     nIndent = Indent_Level;

    makeHdrA(Buf, nIndent+1, true);

    DASSERT( pBuf[-1] == ' ' );
    DASSERT( pBuf[0] == '\0' );

    if( BID_NotAPointer(stf) )
    {
        len = _snprintf(pBuf, BufSize, "<strippedScope %d>\n", BID_GetIndex(stf));
    }
    else
    {
        len = _vsnprintf(pBuf, BufSize, stf, argptr);
        if( len < 0 )
        {
            pBuf [0] = '\0';
            len = 0;
            BidTrace2(BID_TAG1("ERR") _T("%u#  %p{PCSTR}\n"), ObjID(), stf);
        }
        else if( len == BufSize )
        {
            strcpy( &pBuf[BufSize-4], "...\n" );
            len = BufSize;
            DASSERT( pBuf[len-1] == '\n' );
            DASSERT( pBuf[len]   == '\0' );
        }
    }

    len += HDR_LEN;
    pBuf = &Buf[len-1];
    DASSERT( pBuf[1] == '\0' );

    if( *pBuf != '\n' )
    {
        *(++pBuf) = '\n';
        *(++pBuf) = '\0';
        len++;
    }

    DASSERT( len == GetStrLenA(Buf) );
    DASSERT( len < _countof(Buf) );

    *pHScp = (HANDLE)(INT_PTR)(nIndent);

    Indent_Increment();
    Indent_STORE();

    _etwApi.TextA(Buf, len);
    return TRUE;

} // ModuleObject::ScopeEnterVA


BOOL ModuleObject::ScopeEnterVW(UINT_PTR src, UINT_PTR info, HANDLE* pHScp,
                                PCWSTR stf, va_list argptr)
{
    if( !EnabledW(src, info, stf) )
    {
        *pHScp = BID_NOHANDLE;
        return TRUE;                                                // <<== EARLY EXIT
    }

    Indent_ACCESS();

    WCHAR   Buf [BufSize + HDR_LEN + 2];
    PWSTR   pBuf = Buf + HDR_LEN;
    int     len = 0;
    int     nIndent = Indent_Level;

    makeHdrW(Buf, nIndent+1, true);

    DASSERT( pBuf[-1] == L' ' );
    DASSERT( pBuf[0] == L'\0' );

    if( BID_NotAPointer(stf) )
    {
        len = _snwprintf(pBuf, BufSize, L"<strippedScope %d>\n", BID_GetIndex(stf));
    }
    else
    {
        len = _vsnwprintf(pBuf, BufSize, stf, argptr);
        if( len < 0 )
        {
            pBuf [0] = L'\0';
            len = 0;
            BidTrace2(BID_TAG1("ERR") _T("%u#  %p{PCWSTR}\n"), ObjID(), stf);
        }
        else if( len == BufSize )
        {
            wcscpy( &pBuf[BufSize-4], L"...\n" );
            len = BufSize;
            DASSERT( pBuf[len-1] == L'\n' );
            DASSERT( pBuf[len]   == L'\0' );
        }
    }

    len += HDR_LEN;
    pBuf = &Buf[len-1];
    DASSERT( pBuf[1] == L'\0' );

    if( *pBuf != L'\n' )
    {
        *(++pBuf) = L'\n';
        *(++pBuf) = L'\0';
        len++;
    }

    DASSERT( len == GetStrLenW(Buf) );
    DASSERT( len < _countof(Buf) );

    *pHScp = (HANDLE)(INT_PTR)(nIndent);

    Indent_Increment();
    Indent_STORE();

    _etwApi.TextW(Buf, len);
    return TRUE;

} // ModuleObject::ScopeEnterVW


BOOL ModuleObject::ScopeLeave(UINT_PTR /*src*/, UINT_PTR /*info*/, HANDLE* pHScp)
{
    HANDLE hscp = *pHScp;
    if( hscp != BID_NOHANDLE )
    {
        int nSet = (int)(INT_PTR)hscp;
        Indent_ACCESS();
        Indent_Set( nSet );
        Indent_STORE();

        TCHAR   Buf [HDR_LEN + 1];
        makeHdrT(Buf, nSet+1, false);

        DASSERT( Buf[HDR_LEN-1] == _T('\n') );
        DASSERT( Buf[HDR_LEN] == _T('\0') );

        _etwApi.TextT(Buf, HDR_LEN);

        *pHScp = BID_NOHANDLE;
    }
    return TRUE;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Output Control
//
BOOL ModuleObject::EnabledA(UINT_PTR src, UINT_PTR info, PCSTR  tcs)
{
    UNUSED_ALWAYS(src); UNUSED_ALWAYS(info);
    return (tcs != 0);
}

BOOL ModuleObject::EnabledW(UINT_PTR src, UINT_PTR info, PCWSTR tcs)
{
    UNUSED_ALWAYS(src); UNUSED_ALWAYS(info);
    return (tcs != 0);
}


int ModuleObject::Indent( int nIndent )
{
    Indent_ACCESS();
    int tmp = Indent_Level;

    switch( nIndent )
    {
     case BID_INDENT_IN:
        Indent_Increment();
        break;

     case BID_INDENT_OUT:
        Indent_Decrement();
        break;

     case BID_INDENT_GET:
        // skipping Indent_STORE()
        return tmp;

     default:
        Indent_Set(nIndent);
    }
    Indent_STORE();

    return tmp;

} // ModuleObject::Indent


DASSERT_COMPILER( BID_INDENT_OUT == -3 );
DASSERT_COMPILER( BID_INDENT_GET == -2 );
DASSERT_COMPILER( BID_INDENT_IN  == -1 );

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Snap
//
INT_PTR ModuleObject::Snap(INT_PTR evtID, INT_PTR arg1, INT_PTR arg2)
{
    UNUSED_ALWAYS(evtID); UNUSED_ALWAYS(arg1); UNUSED_ALWAYS(arg2);
    return 0;
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Services
//
BOOL ModuleObject::Assert(UINT_PTR arg, UINT_PTR info)
{
    UNUSED_ALWAYS(arg); UNUSED_ALWAYS(info);

    return FALSE;   // Just requests DebugBreak in debug builds
}

/////////////////////////////////////////////////////////////////////////////////////////////////

#define BUF1_SIZE   250
#define BUF2_SIZE   350

void ModuleObject::traceItemIDA(PCSTR strApiName, int itemID, PCSTR strID, va_list args)
{
    char    Buf [BUF1_SIZE];
    int     len;

    if( BID_NotAPointer(strID) )
    {
        len = _snprintf( Buf, _countof(Buf)-1, "<strippedTextID %d>\n", BID_GetIndex(strID) );
    }
    else
    {
        len = _vsnprintf( Buf, _countof(Buf), strID, args );
        Buf [_countof(Buf) - 1] = '\0';
        if( len < 0 )
        {
            Buf [0] = '\0';
            len = 0;
            BidTrace2(BID_TAG1("ERR") _T("%u#  %p{PCSTR}\n"), ObjID(), strID);

        }
        else if( len == _countof(Buf) )
        {
            strcpy( &Buf [_countof(Buf)-5], "...\n" );
            len = _countof(Buf)-1;
        }
    }

    CSTRA_  (Buf2, BUF2_SIZE);
    PSTR    pBuf2 = Buf2.GetBuffer(BUF2_SIZE-1);

    _snprintf( pBuf2, BUF2_SIZE-1, strApiName, itemID );
    pBuf2[BUF2_SIZE-1] = '\0';
    Buf2.ReleaseBuffer();

    Buf2 += Buf;

    PutStrA(NULL, BID_ENA|BID_SLN, Buf2);

} // ModuleObject::traceItemIDA


void ModuleObject::traceItemIDW(PCWSTR strApiName, int itemID, PCWSTR strID, va_list args)
{
    WCHAR   Buf [BUF1_SIZE];
    int     len;

    if( BID_NotAPointer(strID) )
    {
        len = _snwprintf( Buf, _countof(Buf)-1, L"<strippedTextID %d>\n", BID_GetIndex(strID) );
    }
    else
    {
        len = _vsnwprintf( Buf, _countof(Buf), strID, args );
        Buf [_countof(Buf) - 1] = L'\0';
        if( len < 0 )
        {
            Buf [0] = L'\0';
            len = 0;
            BidTrace2(BID_TAG1("ERR") _T("%u#  %p{PCWSTR}\n"), ObjID(), strID);
        }
        else if( len == _countof(Buf) )
        {
            wcscpy( &Buf [_countof(Buf)-5], L"...\n" );
            len = _countof(Buf)-1;
        }
    }

    CSTRW_  (Buf2, BUF2_SIZE);
    PWSTR   pBuf2 = Buf2.GetBuffer(BUF2_SIZE-1);

    _snwprintf( pBuf2, BUF2_SIZE-1, strApiName, itemID );
    pBuf2[BUF2_SIZE-1] = L'\0';
    Buf2.ReleaseBuffer();

    Buf2 += Buf;
    PutStrW(NULL, BID_ENA|BID_SLN, Buf2);

} // ModuleObject::traceItemIDW

#undef BUF1_SIZE
#undef BUF2_SIZE

/////////////////////////////////////////////////////////////////////////////////////////////////

INT_PTR ModuleObject::Touch(UINT_PTR scope, UINT code, INT_PTR arg1, INT_PTR arg2)
{
    INT_PTR retCode = 0;

    switch( BID_TouchCode(code) )
    {
     case BID_TOUCH_OBTAIN_ITEM_IDA:
        {
            int tmpVar = _instanceIdProvider.GenerateDefault();
            retCode = (INT_PTR)tmpVar;

            if( bItemID() )
            {
                PCSTR     textID = (PCSTR) scope;
                INT_PTR   vaList[2];
                vaList[0] = arg1;   // invariant;
                vaList[1] = arg2;   // associate;

                traceItemIDA("ObtainIDa %u# ", tmpVar, textID, (va_list)vaList);
            }
        } break;

     case BID_TOUCH_OBTAIN_ITEM_IDW:
        {
            int tmpVar = _instanceIdProvider.GenerateDefault();
            retCode = (INT_PTR)tmpVar;

            if( bItemID() )
            {
                PCWSTR    textID = (PCWSTR) scope;
                INT_PTR   vaList[2];
                vaList[0] = arg1;   // invariant;
                vaList[1] = arg2;   // associate;

                traceItemIDW(L"ObtainIDw %u# ", tmpVar, textID, (va_list)vaList);
            }
        } break;


     case BID_TOUCH_UPDATE_ITEM_IDA:
        {
            retCode = (INT_PTR)(FALSE);   // BOOL: itemID modified

            if( bItemID() )
            {
                PCSTR   textID      = (PCSTR) scope;
                int     itemID      = *((int*)arg1);
                INT_PTR vaList[2]   = {arg2, 0};

                traceItemIDA("UpdateIDa %u# ", itemID, textID, (va_list)vaList);
            }
        } break;

     case BID_TOUCH_UPDATE_ITEM_IDW:
        {
            retCode = (INT_PTR)(FALSE);   // BOOL: itemID modified

            if( bItemID() )
            {
                PCWSTR  textID      = (PCWSTR) scope;
                int     itemID      = *((int*)arg1);
                INT_PTR vaList[2]   = {arg2, 0};

                traceItemIDW(L"UpdateIDw %u# ", itemID, textID, (va_list)vaList);
            }
        } break;


     case BID_TOUCH_RECYCLE_ITEM_IDA:
        {
            if( bItemID() )
            {
                PCSTR   textID = (PCSTR) scope;
                int     itemID = (int)arg1;
                INT_PTR empty[2]  = {0,0};

                traceItemIDA("RecycleIDa %u# ", itemID, textID, (va_list)empty);
            }
        } break;

     case BID_TOUCH_RECYCLE_ITEM_IDW:
        {
            if( bItemID() )
            {
                PCWSTR  textID = (PCWSTR) scope;
                int     itemID = (int)arg1;
                INT_PTR empty[2]  = {0,0};

                traceItemIDW(L"RecycleIDw %u# ", itemID, textID, (va_list)empty);
            }
        } break;

     default:
        retCode = 0;
    }
    return retCode;

} // ModuleObject::Touch


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  CONTROL CENTRE
//

static PCSTR g_CmdSpaces [] =
{
    _BID_IDENTITY_A,            // CMDSPACE_SELFIDENTITY
};
static const int NumOfCmdSpaces = _countof(g_CmdSpaces);

//
//  Returns a CommandSpaceID so it will be possible to recognize control commands
//  issued as BidCtlProc( cmdSpaceID, BID_CMD(nnn), ... )
//
static INT_PTR getCmdSpaceID( PCSTR textID )
{
    if( textID != NULL )
    {
        int     i;
        INT_PTR dwID = (INT_PTR)ModuleObject::GetSelfModuleHandle();

        __try
        {
            for( i = 0; i < NumOfCmdSpaces; i++ )
            {
                if( lstrcmpiA(textID, g_CmdSpaces[i]) == 0 )
                {
                    return dwID;                                // <<== Normal EXIT is here
                }
                dwID++;
            }
        }
        __except( EXCEPTION_EXECUTE_HANDLER )
        {
            BidTrace2( BID_TAG1("CATCH|ERR") _T("idx: %d, textID: %p{PCSTR}\n"),
                       i, textID );
        }
    }

    return 0;

} // getCmdSpaceID

//
//  Returns the name of the command space by its sequential number.
//  To be used by Control Panel app to discover supported command spaces.
//
static int getCmdSpaceName( int idxCmdSpace, PSTR strBuf, int strBufCapacity )
{
    #define MIN_CAPACITY  3

    //
    //  Wrong index, the length of missing CmdSpaceName will be 0.
    //
    if( idxCmdSpace < 0 || idxCmdSpace >= NumOfCmdSpaces )
    {
        return 0;
    }

    int len = GetStrLenA( g_CmdSpaces[idxCmdSpace] );

    //
    //  If the string buffer or its capacity were not specified,
    //  return the required capacity (the length of CmdSpaceName plus null terminator).
    //
    if( strBufCapacity < MIN_CAPACITY || strBuf == NULL )
    {
        return (len < MIN_CAPACITY) ? MIN_CAPACITY : len + 1;
    }

    //
    //  Copy the string to the output buffer and return its length, NOT counting null terminator.
    //  If the buffer is not long enough, truncate the string and return the required capacity
    //  as a negative number (capacity DOES count null terminator).
    //
    PSTR  pDst = strBuf;
    PCSTR pSrc = g_CmdSpaces[idxCmdSpace];
    int   cnt  = strBufCapacity - 1;
    bool  bOk  = true;

    __try
    {
        while( cnt > 0 && *pSrc != '\0' )
        {
            *pDst++ = *pSrc++;
            --cnt;
        }
        *pDst = '\0';
    }
    __except( EXCEPTION_EXECUTE_HANDLER )
    {
        bOk = false;
    }

    if( !bOk )
    {
        //
        //  Copy operation failed. Possibly bad Dst buffer was provided.
        //
        len = 0;
        BidTrace3( BID_TAG1("CATCH|ERR") _T("dstBuf: %p{PSTR}  dstCapacity: %d  cmdSpaceIdx: %d\n"),
                   strBuf, strBufCapacity, idxCmdSpace );
    }
    else if( len >= strBufCapacity )
    {
        strBuf [strBufCapacity - MIN_CAPACITY + 0] = '.';
        strBuf [strBufCapacity - MIN_CAPACITY + 1] = '.';
        strBuf [strBufCapacity - MIN_CAPACITY + 2] = '\0';
        len = -(len + 1);
    }
    return len;

} // getCmdSpaceName


/////////////////////////////////////////////////////////////////////////////////////////////////

INT_PTR ModuleObject::CtlProc(INT_PTR cmdSpaceID, int cmd, INT_PTR arg1, INT_PTR arg2, INT_PTR arg3)
{
    INT_PTR retVal = 0;

    if( cmd < BID_CMD(BID_DCSCMD_BASE) )
    {
        return 0;   // No command spaces implemented.
    }

    //
    //  Current version doesn't support component model of BID implementation
    //
    if( cmdSpaceID != BID_CMDSPACE_DEFAULT )
    {
        BidTrace4(  BID_TAG1("ERR")
                    _T("%u# ID:%02d  Unsupported command space %p for predefined command %d\n"),
                    ObjID(), IndexID(), cmdSpaceID, cmd );
        return 0;
    }

    switch( cmd )
    {
     case BID_DCSCMD_CMDSPACE_COUNT:
        retVal = NumOfCmdSpaces;
        break;

     case BID_DCSCMD_CMDSPACE_ENUM:
        retVal = (INT_PTR) getCmdSpaceName( (int)arg1, (PSTR)arg2, (int)arg3 );
        break;

     case BID_DCSCMD_CMDSPACE_QUERY:
        retVal = getCmdSpaceID( (PCSTR)arg2 );
        break;

     case BID_DCSCMD_PARSE_STRING:
     case BID_DCSCMD_PARSE_STRING + BID_CMD_UNICODE:
        break;

     case BID_DCSCMD_GET_EVENT_ID:
     case BID_DCSCMD_GET_EVENT_ID + BID_CMD_UNICODE:
        **((INT_PTR**)&arg3) = 0;
        break;

     case BID_DCSCMD_GET_EVENT_ID + BID_CMD_REVERSE:
     case BID_DCSCMD_GET_EVENT_ID + BID_CMD_REVERSE + BID_CMD_UNICODE:
        //  TBD...
        //  flags = (DWORD)a1;
        //  pEvtID = (INT_PTR*)a3;
        break;

     case BID_DCSCMD_ADD_EXTENSION:
     case BID_DCSCMD_ADD_EXTENSION + BID_CMD_UNICODE:
        break;

     case BID_DCSCMD_ADD_METATEXT:
     case BID_DCSCMD_ADD_METATEXT + BID_CMD_UNICODE:
        break;

     case BID_DCSCMD_ADD_RESHANDLE:
     case BID_DCSCMD_ADD_RESHANDLE + BID_CMD_UNICODE:
        //  TBD...
        //  flags = (DWORD)a1;
        //  hRes  = (HMODULE)a2;
        break;

     case BID_DCSCMD_ADD_RESHANDLE + BID_CMD_REVERSE:
     case BID_DCSCMD_ADD_RESHANDLE + BID_CMD_REVERSE + BID_CMD_UNICODE:
        //  TBD...
        //  flags = (DWORD)a1;
        //  hRes  = (HMODULE)a2;
        break;

     case BID_DCSCMD_FLUSH_BUFFERS:
        break;

     default:
         BidTrace3( BID_TAG1("WARN") _T("%u#  ID:%02d  Unknown command %d.\n"),
                    ObjID(), IndexID(), cmd );
        retVal = 0;

    } // switch

    return retVal;

} // ModuleObject::CtlProc



/////////////////////////////////////////////////////////////////////////////////////////////////

PCTSTR ModuleObject::ShortDescription(CStr& strBuf) const
{
    #define _BUFSIZE 512

    PTSTR  tmpBuf = strBuf.GetBuffer(_BUFSIZE);

    _sntprintf( tmpBuf, _BUFSIZE, _T("ID:%02d  [%p]%s \"%s\""),
                IndexID(), (HMODULE)_modHandle, (PCTSTR)_modPath, (PCTSTR)_identity );

    tmpBuf[_BUFSIZE-1] = _T('\0');
    strBuf.ReleaseBuffer();

    return strBuf.GetStrPtr();
    #undef _BUFSIZE
}


BID_EXTENSION( ModuleObject )
{
    BID_EXTENSION_REF(ModuleObject, obj);

    BidWrite4( _T("bInUse: %d{bool}  bActivated: %d{bool}  apiGroupBits: %08X  cfgBits: %08X\n"),
               obj._bInUse, obj._bActivated, obj._ctlCallback.GetCache(), (DWORD)obj._cfgBits);

    if( obj._bInUse && BID_LevelOfDetails() >= BID_DETAILS_STD )
    {
        BidWrite1( _T("%p{ModuleHandle}\n"),    &obj._modHandle );
        BidWrite1( _T("%p{ModulePath}\n"),      &obj._modPath );
        BidWrite1( _T("%p{ModuleIdentity}\n"),  &obj._identity );
        BidWrite1( _T("%p{UnloadCallback}\n"),  &obj._unloadCallback );
    }

} // BID_EXTENSION


/////////////////////////////////////////////////////////////////////////////////////////////////
//                               End of file "ModuleObject.cpp"                                //
/////////////////////////////////////////////////////////////////////////////////////////////////
