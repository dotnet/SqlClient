/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BaseRTL.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Yet Another Wrapper Library (YAWL) base runtime services.
//
//  Comments:                                               (Reduced version for Bid2Etw28)
//              Last Modified: 06-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "yawl/BaseRTL.h"


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  POINTER SAFETY CHECK
//
BOOL WINAPI xBidValidAddress( LPCVOID lp, size_t nBytes, BOOL bReadWrite )
{
    return bReadWrite ? !IsBadWritePtr( (void*)lp, (UINT_PTR)nBytes )
                      : !IsBadReadPtr ( lp, (UINT_PTR)nBytes );
}

BOOL WINAPI xBidValidStringA( LPCSTR lpsz, int nLength, BOOL bReadWrite )
{
    BID_SRCFILE;
    BOOL bFail = FALSE;
    __try
    {
        if( nLength < 0 ) nLength = lstrlenA( lpsz );
        bFail = bReadWrite  ? IsBadWritePtr( (void*)lpsz, (UINT_PTR)nLength + 1 )
                            : IsBadStringPtrA( lpsz, (UINT_PTR)nLength + 1 );
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        bFail = TRUE;
    }
    return !bFail;
}

BOOL WINAPI xBidValidStringW( LPCWSTR lpsz, int nLength, BOOL bReadWrite )
{
    BID_SRCFILE;
    BOOL bFail = FALSE;
    __try
    {
        if( nLength < 0 ) nLength = lstrlenW( lpsz );
        bFail = bReadWrite ? IsBadWritePtr((void*)lpsz, (UINT_PTR)(nLength + 1) * sizeof(WCHAR))
                           : IsBadStringPtrW( lpsz, (UINT_PTR)nLength + 1 );
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        bFail = TRUE;
    }
    return !bFail;
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                         EXCEPTIONS                                          //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  SEH Filter  (support function for YAWL_EXCEPTION_EXECUTE_HANDLER and YAWL_SEH_FILTER)
//
DWORD _yawlSehFilter(UINT_PTR src, UINT_PTR info, PEXCEPTION_POINTERS pInfo, BOOL bRethrow)
{
    PEXCEPTION_RECORD pRecord = pInfo->ExceptionRecord;

    //
    //  NOTE: We intentionally do not check fmtStr for NULL because these tracepoints should not be
    //  explicitly disabled for entire component. If necessary, dynamic prefiltering can be used
    //  to control their output based on SrcFile/Linenum info.
    //  (means disable the tracepoint only when it was called from certain location in source code).
    //
    if( BidIsOn(BID_APIGROUP_TRACE) )
    {
        if( pRecord->ExceptionCode == EXCEPTION_ACCESS_VIOLATION )
        {
            if( pRecord->NumberParameters < 2 )
            {
                xBID_TCFS( fmtStr,
                            _T("<XeptFilter|CATCH|ERR> ")
                            _T("%p{EXCEPTION_RECORD}  NumParams: %d unexpected.\n") );

                xBidTrace(src, info, fmtStr, pRecord, pRecord->NumberParameters );
            }
            else
            {
                xBID_TCFS( fmtStr, _T("<XeptFilter|CATCH> ")
                            _T("%p{EXCEPTION_RECORD}  IP: %p  AV at: %p  write: %d{BOOL}\n") );

                xBidTrace(src, info, fmtStr, pRecord, pRecord->ExceptionAddress,
                        pRecord->ExceptionInformation[1], pRecord->ExceptionInformation[0] );
            }
        }
        else
        {
            xBID_TCFS(_fmt, _T("<XeptFilter|CATCH> ")
                    _T("%p{EXCEPTION_RECORD}  %08X{SEH_CODE}  IP: %p  bRethrow: %d{BOOL}\n") );

            xBidTrace(src, info, _fmt, pRecord,
                    pRecord->ExceptionCode, pRecord->ExceptionAddress, bRethrow );
        }
    }
    return bRethrow ? EXCEPTION_CONTINUE_SEARCH : EXCEPTION_EXECUTE_HANDLER;

} // _yawlSehFilter

#ifndef _BID_NO_SPECIAL_ALLOCATION
//
//  TODO: To be moved to the Pool Of Standard Extensions
//
BID_EXTENSION( EXCEPTION_RECORD )
{
    BID_EXTENSION2_PTR( EXCEPTION_RECORD, pRecord );

    if( BID_InBinaryMode() )
    {
        BidWriteInBinaryMode( pRecord, sizeof(EXCEPTION_RECORD) );
    }
    else
    {
        BidWrite5(
                _T("Next:    %p{EXCEPTION_RECORD}\n")
                _T("Code:    %08X{SEH_CODE}\n")
                _T("Flags:   %08X\n")
                _T("Addr:    %p\n")
                _T("nParams: %d\n")
                ,
                pRecord->ExceptionRecord,
                pRecord->ExceptionCode,
                pRecord->ExceptionFlags,
                pRecord->ExceptionAddress,
                pRecord->NumberParameters );

        if( pRecord->ExceptionCode == EXCEPTION_ACCESS_VIOLATION ||
            BID_LevelOfDetails() > BID_DETAILS_STD )
        {
            BidWrite2(
                _T("Inf[00]: %p\n")
                _T("Inf[01]: %p\n")
                ,
                pRecord->ExceptionInformation[0],
                pRecord->ExceptionInformation[1] );
        }

        if( BID_LevelOfDetails() > BID_DETAILS_STD )
        {
            xBID_XFS( fmtStr,
                _T("Inf[02]: %p\n")
                _T("Inf[03]: %p\n")
                _T("Inf[04]: %p\n")
                _T("Inf[05]: %p\n")
                _T("Inf[06]: %p\n")
                _T("Inf[07]: %p\n")
                _T("Inf[08]: %p\n")
                _T("Inf[09]: %p\n")
                _T("Inf[10]: %p\n")
                _T("Inf[11]: %p\n")
                _T("Inf[12]: %p\n")
                _T("Inf[13]: %p\n")
                _T("Inf[14]: %p\n") );

            xBidWrite(xBidHCtx, fmtStr,
                pRecord->ExceptionInformation[2],
                pRecord->ExceptionInformation[3],
                pRecord->ExceptionInformation[4],
                pRecord->ExceptionInformation[5],
                pRecord->ExceptionInformation[6],
                pRecord->ExceptionInformation[7],
                pRecord->ExceptionInformation[8],
                pRecord->ExceptionInformation[9],
                pRecord->ExceptionInformation[10],
                pRecord->ExceptionInformation[11],
                pRecord->ExceptionInformation[12],
                pRecord->ExceptionInformation[13],
                pRecord->ExceptionInformation[14] );
        }
    } //if BinaryMode

} // BID_EXTENSION

#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                  MEMORY MANAGEMENT HELPERS                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////

#if !defined( _MMW )

    //
    //  PreFast says that standard realloc can leak in low memory condition.
    //  Here we have an ability to substitute implementation.
    //
    void* __cdecl _yawlRealloc(void* ptr, size_t sizeInBytes)
    {
        void* newPtr = realloc(ptr, sizeInBytes);
        return newPtr;
    }

#endif // _MMW


//
//  MemStub
//
const int _yawlEmptyData[16] = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,'eNoN'};


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Dynamic Memory Block
//
//  NOTE: MemBlkRawBase and MemBlkBase are the only two classes that allowed to use
//        MMW_* macro wrappers directly. The rest of class hierachy must use YAWL_* memory API.
//
MemBlkRawBase::MemBlkRawBase(const MemBlkRawBase& other)
{
    _blockPtr = NULL;
    _bytesAllocated = 0;

    if( other.IsAllocated() ){
        copyFrom(other.getPtr(), other.BytesAllocated());
    }
}

DASSERT_COMPILER( sizeof(UINT) == 4 ); // UINT == UINT32 assumed below

void MemBlkRawBase::alloc(UINT_PTR src, UINT_PTR info, UINT sizeInItems, size_t bytesPerItem)
{
    //
    //  'bytesPerItem' comes as sizeof() constant. 
    //  We assert that 1MB per plain element of an array is bad design.
    //
    DASSERT( bytesPerItem < 1024 * 1024 );

    UINT64  size64      = UINT64(bytesPerItem) * sizeInItems;
    UINT    sizeInBytes = (UINT)size64;

    if( size64 != UINT64(sizeInBytes) )     // integer overflow
    {
        if( BidIsOn(BID_APIGROUP_TRACE) )
        {
            xBID_TCFS(fmtStr, _T("<MemBlkRaw|THROW|XC_ABORT> %p{.}  %u total bytes, overflow\n"));
            xBidTrace(src, info|BID_DEMAND_SRC, fmtStr, this, sizeInBytes);
        }
        XEPT_THROW(XC_ABORT);
    }

    if( _bytesAllocated != sizeInBytes )
    {
        _bytesAllocated = sizeInBytes;

        MMW_REALLOC( _blockPtr, BYTE, sizeInBytes );

        if( sizeInBytes != 0  &&  _blockPtr == NULL )
        {
            _bytesAllocated = 0;

            if( BidIsOn(BID_APIGROUP_TRACE) )
            {
                xBID_TCFS(fmtStr, _T("<MemBlk|THROW|XC_MEM> %p{.}  %u bytes\n"));
                xBidTrace(src, info|BID_DEMAND_SRC, fmtStr, this, sizeInBytes);
            }
            XEPT_THROW(XC_MEM);
        }
    }
}


void MemBlkRawBase::copyFrom(const BYTE* pOther, UINT sizeInBytes)
{
    DASSERT( sizeInBytes > 0 );
    BID_SRCFILE;

    bool bOk = false;

    __try
    {
        alloc( xBidSRC2, xBidFLAGS2(0), sizeInBytes, 1 );
        CopyMemory(getPtr(), pOther, sizeInBytes);

        bOk = true;
    }
    __except(YAWL_EXCEPTION_EXECUTE_HANDLER)
    {
        bOk = false;
    }

    if( !bOk )
    {
        BidTrace4( _T("<MemBlk::copyFrom|ERR|CATCH|MEM> src: %p  bytes: %u  ")
                   _T("%p{MemBlk}  _blockPtr: %p deleted.\n"),
                   pOther, sizeInBytes, this, _blockPtr );
        rawFree();
    }

} // MemBlkRawBase::copyFrom


void MemBlkRawBase::copyFrom(const MemBlkRawBase& other)
{
    if( other.IsAllocated() ){
        copyFrom(other.getPtr(), other.BytesAllocated());
    }
    else {
        Free();
    }
}

void MemBlkRawBase::rawFree()
{
    if( _blockPtr != NULL ){
        MMW_FREE( _blockPtr );
        _blockPtr = NULL;
    }
    _bytesAllocated = 0;
}

void MemBlkRawBase::Free(bool bAuto)
{
    if( !IsAllocated() ){
        DASSERT( _bytesAllocated == 0 );
        return;                                         // <<== EARLY EXIT
    }
    if( bAuto ){
        BidTraceU2( BID_ADV,  _T("<MemBlk|ADV> %p{.}  %p AutoCleanup\n"), this, _blockPtr );
    }
    rawFree();
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                    MISCELLANEOUS HELPERS                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////

int GetStrLenA(PCSTR str, int nLen)
{
    if( str == NULL ){
        return 0;                                       // <<== EARLY EXIT
    }

    UINT    cnt = (UINT)nLen;
    PCSTR   ptr = str;

    __try
    {
        while( cnt-- > 0 ){
            if( *ptr == '\0' ){
                return (int)(ptr - str);                // <<== NORMAL EXIT HERE
            }
            ptr++;
        }
        BidTrace2(BID_TAG1("ERR") _T("%p{PCSTR}  len: %u - no terminator\n"), str, nLen);
        ptr = str;
    }
    __except(EXCEPTION_EXECUTE_HANDLER)
    {
        BidTrace2(BID_TAG1("CATCH|ERR") _T("%p{PCSTR}  R/O AV at: %p\n"), str, ptr);
        ptr = str;
    }

    return (int)(ptr - str);

} // GetStrLenA

//
//  TODO: For 64 bit, properly handle extreme cases such as buffer >= 2Gb
//        (int)(ptr - str) may return invalid result.
//

int GetStrLenW(PCWSTR str, int nLen)
{
    if( str == NULL ){
        return 0;                                       // <<== EARLY EXIT
    }

    UINT    cnt = (UINT)nLen;
    PCWSTR  ptr = str;

    __try
    {
        while( cnt-- > 0 ){
            if( *ptr == L'\0' ){
                return (int)(ptr - str);                // <<== NORMAL EXIT HERE
            }
            ptr++;
        }
        BidTrace2(BID_TAG1("ERR") _T("%p{PCWSTR}  len: %u - no terminator\n"), str, nLen);
        ptr = str;
    }
    __except(EXCEPTION_EXECUTE_HANDLER)
    {
        BidTrace2(BID_TAG1("CATCH|ERR") _T("%p{PCWSTR}  R/O AV at: %p\n"), str, ptr);
        ptr = str;
    }

    return (int)(ptr - str);

} // GetStrLenW


/////////////////////////////////////////////////////////////////////////////////////////////////

int GetNumberOfProcessors()
{
    static int numOfProc = 0;

    if( 0 == numOfProc )
    {
        SYSTEM_INFO si;

        GetSystemInfo( &si );
        numOfProc = si.dwNumberOfProcessors;

        BidTrace1(BID_TAG1("RET") _T("%d\n"), numOfProc);
    }
    return numOfProc;

} // GetNumberOfProcessors


//
//  TimeStampCounter
//
bool IsSupportedTimeStampCounter()
{
    UINT64  tmpBuf;
    bool    bRet = true;

    __try
    {
        tmpBuf = ReadTimeStampCounter();
        bRet = (tmpBuf != ReadTimeStampCounter_NOT_IMPLEMENTED);
    }
    __except(EXCEPTION_EXECUTE_HANDLER)
    {
        bRet = false;
    }
    return bRet;

} // IsSupportedTimeStampCounter


static UINT64 WINAPI _yawlReadTheCounter()
{
    return ReadTimeStampCounter();
}

static UINT64 WINAPI _yawlQueryCounterApi()
{
    LARGE_INTEGER tmp;

    QueryPerformanceCounter(&tmp);
    return (UINT64)tmp.QuadPart;
}

static UINT64 WINAPI _yawlRDTSCStub()
{
    if( IsSupportedTimeStampCounter() ){
        _yawlReadTimeStampCounter = _yawlReadTheCounter;
    } else {
        _yawlReadTimeStampCounter = _yawlQueryCounterApi;
    }
    return YAWL_ReadTimeStampCounter();
}

ReadTimeStampCounter_t  _yawlReadTimeStampCounter = _yawlRDTSCStub;


/////////////////////////////////////////////////////////////////////////////////////////////////

class OSVerInfo
{
 public:
    static const OSVERSIONINFOEXW& OSVI()
    {
        if( !_bInitialized ) initialize();
        return _osvi;
    }

    static bool IsEx()
    {
        if( !_bInitialized ) initialize();
        return _bSupportedEx;
    }

    static bool IsUnicode()
    {
        if( !_bInitialized ) initialize();
        return _bUnicode;
    }

 private:
    static  OSVERSIONINFOEXW _osvi;
    static  bool             _bInitialized;
    static  bool             _bSupportedEx;
    static  bool             _bUnicode;

    static  void copyStr(OSVERSIONINFOEXW& osviW, const OSVERSIONINFOA& osviA)
    {
        PCSTR   srcA = osviA.szCSDVersion;
        PWSTR   dstW = osviW.szCSDVersion;
        int     cnt  = _countof(osviW.szCSDVersion);

        while( --cnt >= 0 ){
            *dstW++ = (WCHAR) *srcA++;
        }
    }

    static  void initialize()
    {
        BID_SRCFILE;
        DASSERT( !_bInitialized );
        bool bOk = true;

        YAWL_ZeroMemory( &_osvi, sizeof(_osvi) );
        _osvi.dwOSVersionInfoSize = sizeof(OSVERSIONINFOEXW);

        _bSupportedEx = true;
        _bUnicode     = true;

// Fix for warnings when building against WinBlue build 9444.0.130614-1739
// warning C4996: 'GetVersionExW': was declared deprecated
// externalapis\windows\winblue\sdk\inc\sysinfoapi.h(442)
// Deprecated. Use VerifyVersionInfo* or IsWindows* macros from VersionHelpers.
#pragma warning( disable : 4996 )
        if( !BidCHK( GetVersionExW((OSVERSIONINFOW*) &_osvi) ) )
#pragma warning( default : 4996 )
        {
            BidTrace0(BID_TAG1("INIT|FAIL|INFO") _T("GetVersionExW(INFOEXW)\n"));
            _bSupportedEx = false;

            YAWL_ZeroMemory( &_osvi, sizeof(_osvi) );
            _osvi.dwOSVersionInfoSize = sizeof(OSVERSIONINFOW);

// Fix for warnings when building against WinBlue build 9444.0.130614-1739
// warning C4996: 'GetVersionExW': was declared deprecated
// externalapis\windows\winblue\sdk\inc\sysinfoapi.h(442)
// Deprecated. Use VerifyVersionInfo* or IsWindows* macros from VersionHelpers.
#pragma warning( disable : 4996 )
            if( !BidCHK( GetVersionExW((OSVERSIONINFOW*) &_osvi) ) )
#pragma warning( default : 4996 )
            {
                BidTrace0(BID_TAG1("INIT|FAIL|INFO") _T("GetVersionExW(INFOW)\n"));
                _bUnicode = false;

                OSVERSIONINFOA osviA;

                YAWL_ZeroMemory( &osviA, sizeof(osviA) );
                osviA.dwOSVersionInfoSize = sizeof(osviA);

                YAWL_ZeroMemory( &_osvi, sizeof(_osvi) );

// Fix for warnings when building against WinBlue build 9444.0.130614-1739
// warning C4996: 'GetVersionExW': was declared deprecated
// externalapis\windows\winblue\sdk\inc\sysinfoapi.h(442)
// Deprecated. Use VerifyVersionInfo* or IsWindows* macros from VersionHelpers.
#pragma warning( disable : 4996 )
                if( BidCHK( GetVersionExA(&osviA) ) )
#pragma warning( default : 4996 )
                {
                    _osvi.dwOSVersionInfoSize   = osviA.dwOSVersionInfoSize;
                    _osvi.dwMajorVersion        = osviA.dwMajorVersion;
                    _osvi.dwMinorVersion        = osviA.dwMinorVersion;
                    _osvi.dwBuildNumber         = osviA.dwBuildNumber;
                    _osvi.dwPlatformId          = osviA.dwPlatformId;
                    copyStr(_osvi, osviA);
                }
                else
                {
                    BidTrace0(BID_TAG1("INIT|ERR|FATAL") _T("GetVersionExA(INFOA)\n"));
                    bOk = false;
                }
            }
        }

        WCHAR* pLastChar = &_osvi.szCSDVersion[_countof(_osvi.szCSDVersion) - 1];
        if( *pLastChar != L'\0' )
        {
            BidTrace1(  BID_TAG1("WARN") _T("_osvi.szCSDVersion - No terminator;")
                        _T(" '%lc' replaced with L'\\0'\n"),
                        *pLastChar );
            *pLastChar = L'\0';
        }

        _bInitialized = true;
        BidTrace3(BID_TAG1("RET") _T("%p{OSVERSIONINFOEXW}  VerEx: %d{bool}  Ok: %d{bool}\n"),
                    &_osvi, _bSupportedEx, bOk);

    } // initialize

    BID_EXTENSION_DECLARE( OSVERSIONINFOEXW );

}; // OSVerInfo


#ifndef _BID_NO_SPECIAL_ALLOCATION

BID_EXTENSION( OSVERSIONINFOEXW )
{
    BID_EXTENSION_REF( OSVERSIONINFOEXW, osvi );

    if( BID_InBinaryMode() )
    {
        BidWriteInBinaryMode( &osvi, sizeof(osvi) );
    }
    else
    {   //
        //  A little trick to mantain backward compatibility with header files.
        //  Last 4 bytes of OSVERSIONINFOEX structure were previously declared as WORD wReserved[2]
        //  and now they are WORD wSuiteMask, BYTE wProductType, BYTE wReserved.
        //  We just get these bytes without referencing the names of the fields.
        //
        union _exBytes {
            WORD w[2];
            BYTE b[4];
        } exBytes;

        const WORD* pTmp = &osvi.wServicePackMinor; // Last field before 4 bytes we're looking for.

        exBytes.w[0] = *(++pTmp);
        exBytes.w[1] = *(++pTmp);

        DASSERT( ((BYTE*)pTmp - (BYTE*)&osvi) == (sizeof(OSVERSIONINFOEXW) - sizeof(WORD)) );

        BidWrite8(
            _T("MajorVer: %u\n")
            _T("MinorVer: %u\n")
            _T("BuildNum: %u (0x%08X)\n")
            _T("PlatfId:  %u{VER_PLATFORM}\n")
            _T("CSDVer:   L\"%ls\"\n")
            _T("SPMajor:  %u\n")
            _T("SPMinor:  %u\n")
            ,
            osvi.dwMajorVersion,
            osvi.dwMinorVersion,
            osvi.dwBuildNumber, osvi.dwBuildNumber,
            osvi.dwPlatformId,
            osvi.szCSDVersion,
            osvi.wServicePackMajor,
            osvi.wServicePackMinor
        );
        BidWrite3(
            _T("SuiteMsk: 0x%04X{VER_SUITE}\n")
            _T("ProdType: %u{VER_NT}\n")
            _T("Reserved: 0x%02X\n")
            ,
            exBytes.w[0],   // WORD wSuiteMask;
            exBytes.b[2],   // BYTE wProductType;
            exBytes.b[3]    // BYTE wReserved;
        );
    } // if

} // BID_EXTENSION
#endif

static OSVerInfo _yawlOSVerInfo;

OSVERSIONINFOEXW OSVerInfo::_osvi;
bool            OSVerInfo::_bInitialized = false;
bool            OSVerInfo::_bSupportedEx = false;
bool            OSVerInfo::_bUnicode     = false;

#define YAWL_OSVER(osvi)    const OSVERSIONINFOEXW& osvi = _yawlOSVerInfo.OSVI()

/////////////////////////////////////////////////////////////////////////////////////////////////

bool IsSupportedAsyncFileIO()
{
    return IsPlatformNT();  // TODO: More detailed test needed; it may not _actually_ work
}

bool IsSupportedPlatform()
{
    //
    //  Minimal platform level. Currently we require Win98+ and WinNT 4.0+
    //  (won't run on Win32s, Win95 and WinNT 3.x)
    //
   #ifdef _WIN64

    return true;

   #else

    YAWL_OSVER(osvi);

    bool bOk = true;

    switch( osvi.dwPlatformId )
    {
     case VER_PLATFORM_WIN32s:
        BidTrace0(BID_TAG1("INFO") _T("VER_PLATFORM_WIN32s not supported.\n"));
        bOk = false;
        break;

     case VER_PLATFORM_WIN32_WINDOWS:
        bOk = (osvi.dwMinorVersion >= 10);
        if( osvi.dwMajorVersion != 4 ){
            BidTrace1(BID_TAG1("WARN") _T("VER_PLATFORM_WIN32_WINDOWS  MajorVer:%u  unexpected.\n"),
                      osvi.dwMajorVersion);
            bOk = (osvi.dwMajorVersion > 4);
        }
        break;

     case VER_PLATFORM_WIN32_NT:
        // bOk = (osvi.dwMajorVersion > 4); -- Use this when we drop support for WinNT 4.x
        bOk = (osvi.dwMajorVersion >= 4);
        break;

     default:
        //
        // Assuming that we can run on newer platform, let's keep bOk == true.
        //
        BidTrace1(BID_TAG1("INFO") _T("%d{VER_PLATFORM}  Unknown.\n"), osvi.dwPlatformId);
    }
    return bOk;

   #endif

} // IsSupportedPlatform


/////////////////////////////////////////////////////////////////////////////////////////////////
#ifndef _WIN64

bool IsSupportedUnicode()
{
    return _yawlOSVerInfo.IsUnicode();
}

bool IsPlatform9x()
{
    YAWL_OSVER(osvi);
    return osvi.dwPlatformId == VER_PLATFORM_WIN32_WINDOWS;
}

bool IsPlatformNT()
{
    YAWL_OSVER(osvi);
    return osvi.dwPlatformId == VER_PLATFORM_WIN32_NT;
}

bool IsPlatformWow64()
{
    BID_SRCFILE;

    typedef BOOL (WINAPI* IsWow64Process_t)(HANDLE hProcess, BOOL* pWow64Process);

    static int idxWow64 = 0;   // 0 - not initialized; 1 - false; 2 - true

    if( idxWow64 == 0 )
    {
        idxWow64 = 1;

        IsWow64Process_t IsWow64Process = (IsWow64Process_t) GetKernelApi("IsWow64Process");

        if( IsWow64Process != NULL )
        {
            BOOL bWow64 = FALSE;
            if( BidCHK(IsWow64Process(GetCurrentProcess(), &bWow64)) ){
                if( bWow64 ) idxWow64 = 2;
            }
        }
    }

    return (idxWow64 == 2);
}

#endif // _WIN64

/////////////////////////////////////////////////////////////////////////////////////////////////

HMODULE GetHandleNtDll()
{
    static HMODULE handle = NULL;
    if( handle == NULL ){
        handle = GetModuleHandleA("ntdll.dll");
        DASSERT( handle != NULL );
    }
    return handle;
}

HMODULE GetHandleKernel32()
{
    static HMODULE handle = NULL;
    if( handle == NULL ){
        handle = GetModuleHandleA("kernel32.dll");
        DASSERT( handle != NULL );
    }
    return handle;
}


FARPROC GetNtDllApi(PCSTR apiName)
{
    FARPROC pFunc = GetProcAddress( GetHandleNtDll(), apiName );

    if( pFunc == NULL )
    {
        DWORD dwError = GetLastError();
        BidTrace2( BID_TAG1("INFO|FAIL") _T("%d{WINERR}  \"%hs\"\n"), dwError, apiName );
        SetLastError( dwError );
    }
    return pFunc;
}

FARPROC GetKernelApi(PCSTR apiName)
{
    FARPROC pFunc = GetProcAddress( GetHandleKernel32(), apiName );

    if( pFunc == NULL )
    {
        DWORD dwError = GetLastError();
        BidTrace2( BID_TAG1("INFO|FAIL") _T("%d{WINERR}  \"%hs\"\n"), dwError, apiName );
        SetLastError( dwError );
    }
    return pFunc;
}



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                      EXECUTION CONTEXT                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  YieldExecutionContext (SwitchToThread or backward-compatible simulation via Sleep)
//
static void WINAPI _yawlSwitchToThreadSimulation(void)
{
    Sleep(0);
}

static void WINAPI _yawlSwitchToThreadStub(void)
{
    YieldExecutionContext_t pFunc = (YieldExecutionContext_t) GetKernelApi("SwitchToThread");
    _yawlYieldExecutionContext = pFunc ? pFunc : _yawlSwitchToThreadSimulation;
    (*_yawlYieldExecutionContext)();
}

YieldExecutionContext_t  _yawlYieldExecutionContext = _yawlSwitchToThreadStub;


//
//  Execution Context ID
//
static INT_PTR WINAPI _yawlDefaultGetCurrentExecutionContextId(void)
{
    return (INT_PTR)GetCurrentThreadId();
}

GetCurrentExecutionContextId_t
_yawlGetCurrentExecutionContextId = _yawlDefaultGetCurrentExecutionContextId;


//
//  Local Storage
//
TlsAlloc_t      _yawlTlsAlloc       = TlsAlloc;
TlsFree_t       _yawlTlsFree        = TlsFree;
TlsGetValue_t   _yawlTlsGetValue    = TlsGetValue;
TlsSetValue_t   _yawlTlsSetValue    = TlsSetValue;



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                  End of file "BaseRTL.cpp"                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////
