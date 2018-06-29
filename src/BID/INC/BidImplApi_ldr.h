/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BidImplApi_ldr.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Loader details specific to the BID implementation dll
//
//  Comments:
//              File Created : 20-May-2003
//              Last Modified: 12-Jan-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __BIDIMPLAPI_LDR_H__ ////////////////////////////////////////////////////////////////////
#define __BIDIMPLAPI_LDR_H__
#ifndef _NOLIST_HDRS
#pragma message("  BidImplApi_ldr.h")
#endif

#define _BIDIMPL_LDR_INCLUDED

//
//  strlen("BidImplementation.dll") should not exceed _BIDIMPL_LDR_MODNAME_MAX chars.
//  Otherwise self-diagnostics won't work.
//
#define _BIDIMPL_LDR_MODNAME_MAX    64

#define _BIDIMPL_LDR_REGKEY         BID_T("SOFTWARE\\Microsoft\\BidInterface\\SelfDiag\\")
#define _BIDIMPL_LDR_REGKEY_BUFLEN  (_countof(_BIDIMPL_LDR_REGKEY) + _BIDIMPL_LDR_MODNAME_MAX + 1)
#define _BIDIMPL_LDR_MODNAME        &_bidImplLdrRegKey[_countof(_BIDIMPL_LDR_REGKEY)-1]

static BID_TCHAR _bidImplLdrRegKey  [_BIDIMPL_LDR_REGKEY_BUFLEN] = _BIDIMPL_LDR_REGKEY;

//
//  Function prototypes should be visible in BidApi_ldr.h
//
static void _bidImplLdrGetRegKeyName(void);
static void _bidImplLdrVerifyModuleName(BID_PCTSTR modPath, int modPathCapacity, BOOL* pbEnabled);

//
//  By default implementation dll doesn't request self-diagnostics
//
#define _BIDLDR_ENABLE_DEFAULT      FALSE
#define _BIDLDR_MSG_DEFAULT         FALSE

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Microsoft CRT - specific way to get HMODULE for current dll.
//
#ifndef _BID_GET_HDLL

  #ifdef _BID_NO_CRT
    #pragma message("WARN: _BID_NO_CRT defined while _BID_GET_HDLL isn't. Possible link problem")
  #endif

  extern "C" static BOOL WINAPI _bidImplLdrRawDllMain(HANDLE hDll, DWORD reason, LPVOID lpv)
  {
    lpv;
    if( reason == DLL_PROCESS_ATTACH ){
        _bidHDLL = hDll;
    }
    return TRUE;
  }
  extern "C" BOOL (WINAPI *_pRawDllMain)(HANDLE, DWORD, LPVOID) = &_bidImplLdrRawDllMain;

  #undef  xBidHDLL
  #define xBidHDLL      (HMODULE)_bidHDLL

  #define _BID_GET_HDLL

#else

  #ifndef xBidHDLL
    #define xBidHDLL    (HMODULE)BID_NOHANDLE
  #endif

#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Actual loader code is included here
//
#ifdef _BIDLDR_INCLUDED
  #error BidApi_ldr.h cannot be included before BidImplApi_ldr.h
#endif
#define  _BIDLDR_DEFAULT_DLL     BID_T("BidLab_Self.dll")
#include "BidApi_ldr.h"

/////////////////////////////////////////////////////////////////////////////////////////////////

static int _bidLdrGetNameExt( BID_PCTSTR modPath, int modPathCapacity, BID_PCTSTR* ppName )
{
    BID_PCTSTR pTmp, pEnd;

    DASSERT( modPath [modPathCapacity-1] == BID_T('\0') );

    pTmp = modPath;
    pEnd = modPath + modPathCapacity;

    while( pTmp < pEnd && *pTmp != BID_T('\0') )
    {
        pTmp++;
    }
    DASSERT( *pTmp == BID_T('\0') );
    pEnd = pTmp;

    while( --pTmp >= modPath )
    {
        if( *pTmp == BID_T('\\') || *pTmp == BID_T('/') ) break;
    }

    pTmp++;
    DASSERT( (UINT)(UINT_PTR)(pTmp - modPath) < (UINT)modPathCapacity );

    *ppName = pTmp;

    return (int)(pEnd - pTmp);

} // _bidLdrGetNameExt


static void _bidImplLdrGetRegKeyName( void )
{
    BOOL  bOk = FALSE;

    if( xBidHDLL != BID_NOHANDLE && xBidHDLL != NULL )
    {
        DWORD       dwRet;
        BID_TCHAR   modPath [_MAX_PATH + 10];

        modPath[0] = BID_T('\0');
        dwRet = BID_GetModuleFileName( xBidHDLL, modPath, _countof(modPath)-1 );
        modPath [_countof(modPath)-1] = BID_T('\0');

        if( dwRet > 0  &&  dwRet < _countof(modPath)-1 )
        {
            BID_PCTSTR pTmp = NULL;
            if( _bidLdrGetNameExt(modPath, _countof(modPath), &pTmp) > 0 )
            {
                BID_lstrcpyn( _BIDIMPL_LDR_MODNAME, pTmp, _BIDIMPL_LDR_MODNAME_MAX );
                bOk = TRUE;
            }
        }
    } // if

    if( !bOk )
    {
        //
        //  For some reason we couldn't get module name. Let's try predefined one.
        //
        BID_lstrcpyn( _BIDIMPL_LDR_MODNAME, BID_T(":NoName"), _BIDIMPL_LDR_MODNAME_MAX );
    }
    _bidImplLdrRegKey [_countof(_bidImplLdrRegKey) - 1] = BID_T('\0');

} // _bidImplLdrGetRegKeyName

/////////////////////////////////////////////////////////////////////////////////////////////////

static void _bidImplLdrVerifyModuleName(BID_PCTSTR modPath, int modPathCapacity, BOOL* pbEnabled)
{
    //
    //  Prevent recursive LoadLibrary of itself.
    //  This can happen if SelfDiag RegKey points to the same dll.
    //
    BID_PCTSTR  modName = NULL;
    HMODULE     hMod    = NULL;

    _bidLdrGetNameExt( modPath, modPathCapacity, &modName );
    hMod = BID_GetModuleHandle( modName );

    if( hMod == NULL )
    {
        //  The only valid reason for GetModuleHandle to fail is "Module Not Loaded".
        //  All others are illegal here and we should disable loading of self-diagnostics.
        //
        *pbEnabled = (GetLastError() == ERROR_MOD_NOT_FOUND);
        DASSERT( xBidHDLL != NULL );
    }
    else
    {
        *pbEnabled = (hMod != xBidHDLL);
    }
}


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                               End of file "BidImplApi_ldr.h"                                //
/////////////////////////////////////////////////////////////////////////////////////////////////
