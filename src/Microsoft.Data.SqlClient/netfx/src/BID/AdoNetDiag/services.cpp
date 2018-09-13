/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       services.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Miscellaneous services and "building blocks".
//  Comments:
//              File Created : 14-Aug-2003
//              Last Modified: 23-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "services.h"

#include "BID_SRCFILE.h"
          BID_SRCFILE;

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ServiceMessage delivers trace output from the diagnostic component itself.
//
static          ServiceMessage                      defaultMessenger;
ServiceMessage* ServiceMessage::pServiceMessenger = &defaultMessenger;

void __cdecl ServiceMessage::Put( UINT_PTR src, UINT_PTR info, PCTSTR fmt, ... )
{
    va_list argptr;
    va_start( argptr, fmt );

    bool bOk = true;
    pServiceMessenger->putMsg(src, info, fmt, argptr, bOk);
    if( !bOk )
    {
        //
        //  TODO: How do we catch this in retail build of diagnostic dll,
        //  (which is the most common case)? Using just BidBreak() seems too dangerous..
        //
        DBREAK();
    }
}

void ServiceMessage::putMsg( UINT_PTR src, UINT_PTR info, PCTSTR fmt, va_list argptr, bool& bOk )
{
    //
    //  Default implementation simply sends output to self-diagnostics
    //
    if( !xBidTraceV(src, info, fmt, argptr) ){
        bOk = false;
    }
}

void ServiceMessage::setActiveImplementation(const ServiceMessage* pClassInstance)
{
    pServiceMessenger = const_cast<ServiceMessage*>(pClassInstance);
    BidTraceU1(BID_ADV, BID_TAG1("ADV") _T("%p{ServiceMessage}\n"), pServiceMessenger);
}

void ServiceMessage::resetDefaultImplementation()
{
    pServiceMessenger = &defaultMessenger;
    BidTraceU1(BID_ADV, BID_TAG1("ADV") _T("%p{ServiceMessage}\n"), pServiceMessenger);
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  HealthMeter (rudimentary foundation)
//
HealthMeter::HealthMeter()
{
    _totalExceptions = 0;
}

void HealthMeter::Done()
{
    _totalExceptions = 0;
}

void HealthMeter::Init()
{
    _totalExceptions = 0;
}

void HealthMeter::Report()
{
    if( _totalExceptions > 0 ){
        BidxMessage1( _T("00:WARNING - %u internal exception(s) caught. ")
                      _T("Possible problems with diagnostic instrumentation."),
                      _totalExceptions );
    }
    _totalExceptions = 0;
}


void HealthMeter::IncrementExceptionCounter()
{
    _totalExceptions++;
}

HealthMeter g_HealthMeter;


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  IBidApi interface stubs
//
BOOL    IBidApi::PutStrA(UINT_PTR, UINT_PTR, PCSTR)                         { return TRUE;      }
BOOL    IBidApi::PutStrW(UINT_PTR, UINT_PTR, PCWSTR)                        { return TRUE;      }
BOOL    IBidApi::TraceVA(UINT_PTR, UINT_PTR, PCSTR, va_list)                { return TRUE;      }
BOOL    IBidApi::TraceVW(UINT_PTR, UINT_PTR, PCWSTR, va_list)               { return TRUE;      }
BOOL    IBidApi::EnabledA(UINT_PTR, UINT_PTR, PCSTR  tcs)                   { return tcs != 0;  }
BOOL    IBidApi::EnabledW(UINT_PTR, UINT_PTR, PCWSTR tcs)                   { return tcs != 0;  }
int     IBidApi::Indent(int)                                                { return 0;         }
INT_PTR IBidApi::Snap(INT_PTR, INT_PTR, INT_PTR)                            { return 0;         }
BOOL    IBidApi::Assert(UINT_PTR, UINT_PTR)                                 { return TRUE;      }
INT_PTR IBidApi::CtlProc(INT_PTR, int, INT_PTR, INT_PTR, INT_PTR)           { return 0;         }
INT_PTR IBidApi::Touch(UINT_PTR, UINT, INT_PTR, INT_PTR)                    { return 0;         }

BOOL IBidApi::ScopeEnterVA(UINT_PTR, UINT_PTR, HANDLE* pHScp, PCSTR, va_list)
{
    *pHScp = NULL;  return TRUE;
}
BOOL IBidApi::ScopeEnterVW(UINT_PTR, UINT_PTR, HANDLE* pHScp, PCWSTR, va_list)
{
    *pHScp = NULL;  return TRUE;
}
BOOL IBidApi::ScopeLeave(UINT_PTR, UINT_PTR, HANDLE* pHScp)
{
    *pHScp = BID_NOHANDLE;  return TRUE;
}



/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Execution Context Local Storage
//
//  We use semantics of Win32 Thread Local Storage API but deal with loadable wrappers.
//  Execution Context can be a thread, fiber, SQLOS task, etc. depending on
//  the scheduler implementation details.
//
void ExecutionContextLocalStorage::Init()
{
    if( _tlsIndex != TLS_NOVALUE )
    {
        BidTraceU0( BID_ADV, BID_TAG1("MULTICALL|PERF|ADV") _T("\n") );
    }
    else
    {
        _tlsIndex = YAWL_TlsAlloc();
        DASSERT( _tlsIndex < TLS_NOVALUE );
        BidTraceU1( BID_ADV, BID_TAG1("ADV") _T("TlsIndex: %d\n"), _tlsIndex );
    }

} // ExecutionContextLocalStorage::Init


void ExecutionContextLocalStorage::Done()
{
    if( _tlsIndex == TLS_NOVALUE )
    {
        BidTraceU0( BID_ADV, BID_TAG1("MULTICALL|PERF|ADV") _T("\n") );
    }
    else
    {
        BidTraceU1( BID_ADV, BID_TAG1("ADV") _T("TlsIndex: %d\n"), _tlsIndex );
        YAWL_TlsFree( _tlsIndex );
        _tlsIndex = TLS_NOVALUE;
    }

} // ExecutionContextLocalStorage::Done


DWORD ExecutionContextLocalStorage::_tlsIndex = TLS_NOVALUE;
ExecutionContextLocalStorage                    g_ExecutionContextLocalStorage;


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  InstanceIdProvider
//
//  NOTE: going to become quite complex, so it's a separate class at first place
//
InstanceIdProvider::InstanceIdProvider()
{
    _source = 0;
}

int InstanceIdProvider::GenerateDefault()
{
    static volatile LONG _source = 0;   //  TempFix: Use global counter

    LONG newValue = YAWL_InterlockedIncrement(&_source);

    if( newValue == 0 )
    {
        newValue = YAWL_InterlockedIncrement(&_source);
        DASSERT( newValue != 0 );
    }
    return (int)newValue;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ModulePath
//
void ModulePath::Init(CREFSTR pathStr)
{
    _fullPath = pathStr;

    if( _fullPath.IsEmpty() )
    {
        _nameExt = NULL;
    }
    else
    {
        TCHAR ch;
        int   idx = _fullPath.GetLength();

        while( --idx >= 0 )
        {
            ch = _fullPath [idx];
            if( ch == _T('\\') || ch == _T('/') ) break;
        }

        idx++;
        DASSERT( (UINT)idx <= (UINT)Length(_fullPath) );
        _nameExt = _fullPath.GetStrPtr() + idx;

        BidTraceU3( BID_ADV, BID_TAG1("ADV") _T("name: \"%s\"  nameExt: \"%s\"  path: \"%s\"\n"),
                    GetNameOnly(), (PCTSTR)_nameExt, GetPathOnly() );
    }

    _nameOnly.Empty();
    _pathOnly.Empty();

} // Init


void ModulePath::Done()
{
    _fullPath.Empty();
    _nameExt = NULL;
    _nameOnly.Empty();
    _pathOnly.Empty();
}


CStr ModulePath::GetNameOnly() const
{
    if( _fullPath.IsEmpty() )
    {
        DASSERT( _nameOnly.IsEmpty() );
        return CStr();
    }
    if( _nameOnly.IsEmpty() )
    {
        int nFirst, nCount;

        DASSERT_STRING( _nameExt );
        nFirst = (int)(_nameExt - _fullPath.GetStrPtr());
        nCount = _fullPath.ReverseFind(_T('.')) - nFirst;

        const_cast<ModulePath*>(this)->_nameOnly = _fullPath.Mid( nFirst, nCount );
    }
    return _nameOnly;

} // GetNameOnly


CStr ModulePath::GetPathOnly() const
{
    if( _fullPath.IsEmpty() )
    {
        DASSERT( _pathOnly.IsEmpty() );
        return NULL;
    }
    if( _pathOnly.IsEmpty() )
    {
        int nFirst = (int)(_nameExt - _fullPath.GetStrPtr());
        const_cast<ModulePath*>(this)->_pathOnly = _fullPath.Left( nFirst );
    }
    return _pathOnly;

} // GetPathOnly


void ModulePath::copyFrom(const ModulePath& other)
{
    _fullPath = other._fullPath;
    _nameExt  = _fullPath.IsEmpty() ? NULL : _fullPath.GetStrPtr() + other.nameExtOffset();
    _nameOnly.Empty();
    _pathOnly.Empty();
}


int ModulePath::nameExtOffset() const
{
    DASSERT( !_fullPath.IsEmpty() );
    DASSERT( _nameExt != NULL );
    return (int)(_nameExt - _fullPath.GetStrPtr());
}



/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ModuleHandle
//
void ModuleHandle::Init(HMODULE hModule, PCVOID codeAddress)
{
    if( hModule == ModuleHandle_NOVALUE && codeAddress != NULL )
    {
        hModule = GetModuleHandleFromAddress( codeAddress );
        if( hModule == NULL ){
            hModule = ModuleHandle_NOVALUE;
        }
    }
    if( hModule == NULL )
    {
        hModule = GetModuleHandle( NULL );
        if( BidCHK(NULL == hModule) ){
            hModule = ModuleHandle_NOVALUE;
        }
    }

    _hModule = hModule;

    if( _hModule == ModuleHandle_NOVALUE )
    {
        BidTrace2(BID_TAG1("ERR") _T("hModule: %p  codeAddress: %p\n"), hModule, codeAddress);
    }
    else
    {
        BidTrace3(BID_TAG _T("%p{HMODULE}  codeAddress: %p  \"%s\"\n"),
                  _hModule, codeAddress, (PCTSTR)GetFileName() );
    }

} // Init


CStr ModuleHandle::GetFileName() const
{
    CStr    retStr;
    TCHAR   tmpBuf [_MAX_PATH + 10];

    tmpBuf[0] = _T('\0');

    if( 0 == GetModuleFileName((HINSTANCE)_hModule, tmpBuf, _countof(tmpBuf)) )
    {
       #if !defined( _UNICODE )

        BidCHK(FALSE);

       #else // _UNICODE
        //
        //  First try was Unicode. If platform is supposed to support this, trace error code
        //  (otherwise there is no reason to complain). Then try ANSI version.
        //
        DWORD dwError = GetLastError();
        if( IsSupportedUnicode() ){
            BidTrace1( BID_TAG1("ERR") _T("GetModuleFileNameW: %u{WINERR}\n"), dwError );
        }

        char tmpBufA [_MAX_PATH + 10];

        tmpBufA[0] = '\0';
        BidCHK( 0 != GetModuleFileNameA((HINSTANCE)_hModule, tmpBufA, _countof(tmpBufA)) );

        tmpBufA [_countof(tmpBufA)-1] = '\0';
        retStr = tmpBufA;

       #endif // _UNICODE
    }
    else
    {
        tmpBuf [_countof(tmpBuf)-1] = _T('\0');
        retStr = tmpBuf;
    }

    return retStr;

} // GetFileName


HMODULE GetModuleHandleFromAddress( PCVOID codeAddress )
{
    MEMORY_BASIC_INFORMATION mbi;
    mbi.AllocationBase = NULL;

    if( VirtualQuery(codeAddress, &mbi, sizeof(mbi)) != sizeof(mbi) )
    {
        BidTrace1(BID_TAG1("ERR") _T("%p\n"), codeAddress);
        mbi.AllocationBase = ModuleHandle_NOVALUE;
    }
    return (HMODULE)mbi.AllocationBase;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ModuleIdentity
//
void ModuleIdentity::Init(PCSTR sIdentity, const ModulePath& modPath)
{
    _textID = sIdentity;
    if( _textID.IsEmpty() ){
        _textID = modPath.GetNameExt();
    }
    _guidID.Init(_textID);
}

CStr ModuleIdentity::ToString() const
{
    CSTR_(tmpBuf, 256);

    tmpBuf << _T("\"") << _textID << _T("\" ") << _guidID.ToString();
    return CSTR_RET(tmpBuf);
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidConfigBits
//
bool BidConfigBits::Approved() const
{
    bool bOk = IsValid();

    if( bOk && !AltPage() )
    {
        //
        //  This adapter does not support modules with compressed metadata (TCFS, STF, etc)
        //
        bOk = (_data & BID_CFG_PACK_MASK) == 0;
        if( !bOk )
        {
            BidxMessage0(
                _T("00:WARNING - ")
                _T("Current version doesn't support compressed diagnostic metadata\n")
            );
        }
    }

    return bOk;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidExtendedInfo
//
void BidExtendedInfo::Init(PBIDEXTINFO pExt, const BidConfigBits& cfgBits)
{
    if( pExt == NULL )
    {
        cleanup();
        _bValid = true;
        return;                                                         // <<== EARLY EXIT
    }
    __try
    {
        _modHandle = pExt->hModule;
        if( _modHandle == NULL )
        {
            _modHandle = ModuleHandle_NOVALUE;
            BidTrace2(BID_TAG1("WARN") _T("%p{BIDEXTINFO}  hModule=NULL; making it %p\n"),
                      pExt, _modHandle);
        }

        if( pExt->ModulePath != NULL )
        {
            _modPath = pExt->ModulePath;
        }
        else if( pExt->ModulePathA != NULL )
        {
            setModPath(pExt->ModulePathA, cfgBits.AcpOrUtf8());
        }

        _bValid = true;

    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        cleanup();
        // TODO: Add more detailed self-diagnostics
    }

} // Init

void BidExtendedInfo::cleanup()
{
    _modHandle = ModuleHandle_NOVALUE;
    _modPath.Empty();
    _bValid = false;
}

void BidExtendedInfo::setModPath(PCSTR str, UINT codePage)
{
    _modPath = CStr(str, codePage);
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidSectionHeader
//
void BidSectionHeader::Init(PBIDSECTHDR pHdr)
{
    cleanup();
    _bValid = true;
    if( pHdr == NULL ) return;                                          // <<== EARLY EXIT

    bool bAV = false;

    __try
    {
        //
        //  Basic verification
        //
        if( _bValid )
        {
            _bValid = (pHdr->SanityCheck == BID_SANITY_CHECK);
        }
        if( _bValid )
        {
            _bValid = (0 == strcmp(pHdr->Signature, BID_HEADER_SIGNATURE));
        }
        if( _bValid )
        {
            _marker = pHdr->Marker;
            _bValid = (_marker != NULL);
        }
        if( _bValid )
        {
            _attributes = pHdr->Attributes;
            _bValid = (_attributes != 0);
        }
        #if 0   // _checksum is not used right now; format of pHdr->Checksum to be changed.
        if( _bValid )
        {
            _checksum = pHdr->Checksum;
        }
        #endif
        //
        //  Constrains
        //
        if( _bValid )
        {
            _bValid = (HeaderSize() == sizeof(BIDSECTHDR));
        }
        if( _bValid )
        {
            _bValid = (Version() == BID_VER);
        }
        if( _bValid )
        {
            _bValid = (NumOfMarkers() == BID_SE_COUNT);
        }

    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        _bValid = false;
        bAV = true;
    }

    if( bAV ){
        BidTrace1(BID_TAG1("ERR|AV") _T("%p{PBIDSECTHDR}\n"), pHdr);
    }

} // BidSectionHeader::Init


void BidSectionHeader::cleanup()
{
    _marker     = NULL;
    _attributes = 0;
    _checksum   = 0;
    _bValid     = false;
}

BID_EXTENSION( BidSectionHeader )
{
    BID_EXTENSION_REF(BidSectionHeader, o);

    BidWrite7(
        _T("marker:     %p{PBIDMARKER}\n")
        _T("attributes: %08X\n")
        _T("  HdrSize:  %d\n")
        _T("  nMarkers: %d\n")
        _T("  Version:  %d\n")
        _T("checksum:   %08X\n")
        _T("bValid:     %d{bool}\n")
        ,
        o._marker,
        o._attributes,
            o.HeaderSize(),
            o.NumOfMarkers(),
            o.Version(),
        o._checksum,
        o._bValid
    );

} // BID_EXTENSION


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  UnloadCallback
//
void UnloadCallback::Init(PBIDHOOKS pHooks, int sizeInBytes)
{
    //BID_SRCFILE;
    DASSERT( IsEmpty() );
    DASSERT( sizeInBytes >= eMinCodeSize );

    BIDUNLOADCB callbackPtr = NULL;

    _bValid = true;

    __try
    {
        callbackPtr = (pHooks != NULL) ? pHooks->UnloadCallback : NULL;
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        callbackPtr = NULL;
        _bValid = false;
        BidTrace2( BID_TAG1("CATCH|ERR") _T("pHooks: %p  sizeInBytes: %d\n"),
                   pHooks, sizeInBytes );
    }

    if( callbackPtr == NULL )
    {
        BidTraceU0(BID_ADV, BID_TAG1("ADV") _T("callbackPtr NULL\n"));
        return;                                                             // <<== EARLY EXIT
    }

    int     actualSize;
    bool    bOk  = false;
    BYTE*   pSrc = (BYTE*) callbackPtr;

    //
    //  See how many bytes of code accessible. Adjust sizeInBytes if necessary.
    //
    int     idx = sizeInBytes;
    BYTE    tmp;
    __try
    {
        while( --idx > 0 ){
            tmp = *pSrc++;
        }
        actualSize = sizeInBytes;
        bOk = true;
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        actualSize = sizeInBytes - idx;
        BidTrace3( BID_TAG1("WARN") _T("%p{BIDUNLOADCB}  requested: %u  accessible: %u bytes\n"),
                   callbackPtr, sizeInBytes, actualSize );
        bOk = (actualSize >= eMinCodeSize);
    }
    DASSERT( idx >= 0 );

    //
    //  Get a snapshot of code pointed by callbackPtr.
    //  When it's time to call (*callbackPtr), we'll compare actual code pattern
    //  to make sure that it is still the same code that we have at this moment.
    //
    if( bOk )
    {
        MemBlkRaw_ALLOC( _codeFragmentBuf, actualSize );
        pSrc = (BYTE*) callbackPtr;

        __try
        {
            for( int i = 0; i < actualSize; i++ ){
                _codeFragmentBuf[i] = *pSrc++;
            }
            bOk = true;
        }
        __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
        {
            BidTrace2(BID_TAG1("ERR") _T("%p{BIDUNLOADCB}  %u bytes R/O AV\n"),
                      callbackPtr, actualSize );
            bOk = false;
        }
    }

    if( bOk ){
        _unloadCallbackPtr = callbackPtr;
    }
    else {
        cleanup();
    }

} // Init

void UnloadCallback::Done(int indexID, bool bForcedCleanup)
{
    if( bForcedCleanup && IsValid() ){
        Execute(indexID);
    }
    cleanup();
}

void UnloadCallback::Execute(int indexID)
{
    if( IsEmpty() ) {
        BidTraceU0(BID_ADV, BID_TAG1("ADV|NOOP") _T("empty\n"));
        return;                                                     // <<== EARLY EXIT
    }

    bool bOk = false;
    __try
    {
        if( isCodeTheSame() )
        {
            (*_unloadCallbackPtr)(TRUE);
            bOk = true;
        }
        else
        {
            BidxMessage1(_T("%02d:WARNING - UnloadCallback possibly broken."), indexID);
            bOk = false;
        }
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        bOk = false;
    }

    if( !bOk )
    {
        BidxMessage1(_T("%02d:WARNING - UnloadCallback wasn't called."), indexID);
    }

} // Execute


void UnloadCallback::cleanup()
{
    _unloadCallbackPtr = NULL;
    _codeFragmentBuf.Free();
    _bValid = false;
}

bool UnloadCallback::isCodeTheSame()
{
    DASSERT( _unloadCallbackPtr != NULL );

    //
    //  Make sure that _unloadCallbackPtr points to exactly the same code that was there
    //  at initialization time.
    //
    //  NOTE: there is no exception handling in this function because it gets called only from
    //  UnloadCallback::Execute, inside try/except block.
    //
    BYTE*   pSrc = (BYTE*) _unloadCallbackPtr;
    int     cnt  = _codeFragmentBuf.Size();

    for( int i = 0; i < cnt; i++ )
    {
        if( _codeFragmentBuf[i] != *pSrc )
        {
            BidTrace5( BID_TAG1("ERR")
                    _T("was: %p{BIDUNLOADCB}  now: %p{BIDUNLOADCB}  ")
                    _T("offset: %u  pattern: %02X  current: %02X\n"),
                    _codeFragmentBuf, _unloadCallbackPtr, i, _codeFragmentBuf[i], *pSrc );

            return false;                                               // <<== EARLY EXIT
        }
        pSrc++;
    }
    return true;

} // isCodeTheSame


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidCtlCallback
//
BidCtlCallback::BidCtlCallback()
{
    _pCtlFlags  = NULL;
    _ctlProc    = NULL;
    _cache      = 0;
    _status     = eInvalid;
    _bEnabled   = false;
}


void BidCtlCallback::Done()
{
    if( !IsValid() ) return;

    _pCtlFlags  = NULL;
    _ctlProc    = NULL;
    _status     = eInvalid;
    _bEnabled   = false;
}


void BidCtlCallback::Init(const BidConfigBits& cfgBits, DWORD* pCtlFlags, BID_CTLCALLBACK ctlProc)
{
    DASSERT( !IsValid() );

    if( cfgBits.ControlCallback() )
    {
        _status = eCallback;
        _pCtlFlags = NULL;
        _ctlProc = ctlProc;
        if( _ctlProc == NULL || !BidValidAddress(_ctlProc, 1, FALSE) ){
            _status = eNone;
        }
    }
    else
    {
        _status = ePointer;
        _pCtlFlags = pCtlFlags;
        _ctlProc = NULL;
        if( _pCtlFlags == NULL || !BidValidAddress(_pCtlFlags, sizeof(DWORD)) ){
            _status = eNone;
        }
    }
} // Init


DWORD BidCtlCallback::Set(DWORD bits)
{
    DASSERT( IsValid() );

    DWORD dwRet = _cache;
    _cache = bits;
    if( _bEnabled )
    {
        __try
        {
            switch( _status )
            {
             case eCallback:
                dwRet = (*_ctlProc)(0xFFFFFFFF, bits);
                break;

             case ePointer:
                dwRet = *_pCtlFlags;
                *_pCtlFlags = bits;
                break;

             default:
                /*none*/;
            }
        }
        __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
        {
            dwRet = 0;
        }
    }
    return dwRet;

} // Set


DWORD BidCtlCallback::Get()
{
    DASSERT( IsValid() );

    DWORD dwRet = _cache;
    if( _bEnabled )
    {
        __try
        {
            switch( _status )
            {
             case eCallback:
                dwRet = (*_ctlProc)(0,0);
                break;

             case ePointer:
                dwRet = *_pCtlFlags;
                break;

             default:
                /*none*/;
            }
            _cache = dwRet;
        }
        __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
        {
            dwRet = _cache;
        }
    }
    return dwRet;

} // Get


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BindingContract
//
void BindingContract::Done()
{
    _bValid = false;
    _identity.Done();
    _modPath.Done();
    _modHandle.Done();
    _extInfo.Done();
    _header.Done();
    _cfgBits.Done();
    _version = 0;
}

void BindingContract::Init( int bInitAndVer, PCSTR sIdentity, DWORD cfgBits, PCVOID codeAddress,
                            PBIDEXTINFO pExtInfo, PBIDSECTHDR pHdr )
{
    BidScopeAuto6( BID_TAG
            _T("%p{.}  ver:%d  \"%hs\"  cfg: %08X  ctlCB: %p  %p{PBIDEXTINFO}  %p{PBIDSECTHDR}"),
            this, bInitAndVer, sIdentity, cfgBits, codeAddress, pHdr );

    DASSERT( !IsValid() );
    DASSERT( bInitAndVer > 0 );

    _version = bInitAndVer;
    _cfgBits.Init(cfgBits);
    _header.Init(pHdr);
    _extInfo.Init(pExtInfo, _cfgBits);
    _modHandle.Init(_extInfo.ModuleHandle(), codeAddress);
    _modPath.Init(_extInfo.IsModulePath() ? _extInfo.ModulePath() : _modHandle.GetFileName());
    _identity.Init(sIdentity, _modPath);
    _bValid = true;

} // Init


void BindingContract::Init(HANDLE hModule)
{
    DASSERT( !IsValid() );

    _version = BID_VER;
    _cfgBits.Init(BID_CFG_ACTIVE_BID);
    _header.Init(NULL);
    _extInfo.Init(NULL, _cfgBits);
    _modHandle.Init((HMODULE)hModule, NULL);
    _modPath.Init(_extInfo.IsModulePath() ? _extInfo.ModulePath() : _modHandle.GetFileName());
    _identity.Init(_BID_IDENTITY_A, _modPath);
    _bValid = true;
}


bool BindingContract::Approved() const
{
    bool bOk = IsValid();

    if( bOk ) bOk = !Constrained();
    if( bOk ) bOk = _cfgBits.Approved();
    if( bOk ) bOk = _header.IsValid();
    return bOk;
}

bool BindingContract::Constrained() const
{
    //
    //  System components used by this DLL cannot be its clients,
    //  even if they try to establish connection.
    //  (they won't because they are not BID instrumented, but anyway)
    //
    static PCTSTR constrains[] =
    {
        _T("ntdll.dll"),
        _T("kernel32.dll"),
        _T("rpcrt4.dll"),
        _T("wintrust.dll"),
        _T("secur32.dll"),
        _T("advapi32.dll"),
        _T("user32.dll")
    };
    bool bToBeRejected = false;

    for( int i = 0; i < _countof(constrains); i++ )
    {
        if( 0 == _tcsicmp(_modPath.GetNameExt(), constrains[i]) ){
            bToBeRejected = true;
            break;                                                  // <<== EXIT LOOP
        }
    }
    return bToBeRejected;

} // Constrained


void BindingContract::Populate( BidConfigBits& rCfgBits, ModuleHandle& rModHandle,
                                ModulePath& rModPath, ModuleIdentity& rIdentity ) const
{
    DASSERT( IsValid() );
    rCfgBits    = _cfgBits;
    rModHandle  = _modHandle;
    rModPath    = _modPath;
    rIdentity   = _identity;
}

/////////////////////////////////////////////////////////////////////////////////////////////////

BID_EXTENSION( BindingContract )
{
    BID_EXTENSION_REF(BindingContract, o);

    BidWrite9(
        _T("%p{BidSectionHeader}  cfgBits: %08X  identity: %s\n")
        _T("modPath:   \"%s\" (%s)\n")
        _T("modHandle: %p{HMODULE}  \"%s\"\n")
        _T("extInfo:   %p{HMODULE}  \"%s\"\n")
        ,
        &o._header, (DWORD)o._cfgBits,  o._identity.ToString(),
        o._modPath.GetFullPath(),       o._modPath.GetNameExt(),
        (HMODULE)o._modHandle,          o._modHandle.GetFileName(),
        o._extInfo.ModuleHandle(),      o._extInfo.ModulePath()
    );

} // BID_EXTENSION

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "services.cpp"                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////
