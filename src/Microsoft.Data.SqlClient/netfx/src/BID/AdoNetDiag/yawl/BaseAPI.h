/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BaseAPI.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Yet Another Wrapper Libray (YAWL) main include file.
//
//  Comments:   Provides access to Win32/Win64 API and defines some useful stuff.
//              Supposed to be the first include file in each compilation unit.
//
//                                                          (Reduced version for Bid2Etw28)
//              Last Modified: 23-Mar-2005
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __BASEAPI_H__ ///////////////////////////////////////////////////////////////////////////
#define __BASEAPI_H__
#ifndef _NOLIST_HDRS
#pragma message("  BaseAPI-28.h")
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                       STANDARD PRESET                                       //
/////////////////////////////////////////////////////////////////////////////////////////////////

#if defined( NDEBUG )
  #define _INLINE
#else
  #define _DIAG
  #define _SRCINFO
#endif

#if defined( _MT) && !defined( _NO_THREADS)
  #define _THREADS
#endif

#if !defined( _NO_WIN32_COM ) && !defined( _WIN32_COM )
  #define _WIN32_COM
#endif

#ifndef STRICT
  #define STRICT
#endif

#ifndef WIN32_LEAN_AND_MEAN
  #define WIN32_LEAN_AND_MEAN
#endif

#ifndef _WIN32_WINNT
  #define _WIN32_WINNT            0x0400
#endif

#ifdef _WIN32_COM
  #define _WIN32_DCOM             // If COM is enabled, we also assume DCOM
#endif

#if defined( _UNICODE ) && !defined( UNICODE )
  #define UNICODE
#endif
#if defined( UNICODE ) && !defined( _UNICODE )
  #define _UNICODE
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                     DETERMINE COMPILER                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////

#define _MICROSOFT      10
#define _INTEL          20
#define _WATCOM         30
#define _BORLAND        40

#if defined( __cplusplus )
  #define   c_or_cpp    _T(" C++")
  #define   c_or_cpp_A  " C++"
#else
  #define   c_or_cpp    _T(" C")
  #define   c_or_cpp_A  " C"
#endif

#if defined( __BORLANDC__ )
  #define  _COMPILER            _BORLAND
  #define  _COMPILER_Name       _T("Borland") c_or_cpp
  #define  _COMPILER_Name_A     "Borland" c_or_cpp_A
  #define  _COMPILER_VERSION    __BORLANDC__
#elif defined( __WATCOMC__ )
  #define  _COMPILER            _WATCOM
  #define  _COMPILER_Name       _T("Watcom") c_or_cpp
  #define  _COMPILER_Name_A     "Watcom" c_or_cpp_A
  #define  _COMPILER_VERSION    __WATCOMC__
#elif defined( __ICL )
  #define  _COMPILER            _INTEL
  #define  _COMPILER_Name       _T("Intel") c_or_cpp
  #define  _COMPILER_Name_A     "Intel" c_or_cpp_A
  #define  _COMPILER_VERSION    __ICL
#elif defined( _MSC_VER )
  #define  _COMPILER            _MICROSOFT
  #define  _COMPILER_Name       _T("Microsoft") c_or_cpp
  #define  _COMPILER_Name_A     "Microsoft" c_or_cpp_A
  #define  _COMPILER_VERSION    _MSC_VER
#else
  #define  _COMPILER            0
  #define  _COMPILER_Name       _T("Unknown") c_or_cpp
  #define  _COMPILER_Name_A     "Unknown" c_or_cpp_A
  #define  _COMPILER_VERSION    0
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                 CHECK FOR COMPILER VERSION                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////

#if ( _COMPILER == _MICROSOFT )
  #if ( _COMPILER_VERSION < 1200 )
    #error Old version of compiler. Should be at least MSVC++ 6.0
  #endif
#else
  #error Compiler not supported.
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                DISABLE UNNECESSARY WARNINGS                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////

#if ( _COMPILER == _MICROSOFT )

  #if !defined( _ALL_WARNINGS )
    #pragma warning(disable: 4061)      // Not all enum values present in switch statement
    #pragma warning(disable: 4127)      // constant expression for TRACE/ASSERT
    #pragma warning(disable: 4201)      // winnt.h uses nameless structs
    #pragma warning(disable: 4214)      // winnt.h uses nonint packed fields
    #pragma warning(disable: 4275)      // nonexport class is a base for export one
    #pragma warning(disable: 4511)      // copy constructor could not be generated
    #pragma warning(disable: 4512)      // assignment operator could not be generated
    #pragma warning(disable: 4705)      // statement has no effect in optimized code
    #pragma warning(disable: 4725)      // FDIV Pentium(r) known issue
    #pragma warning(disable: 4310)      // Cast truncates constant value

    #if defined( _DIAG ) || defined( _SRCINFO )
      #pragma warning(disable: 4706)    // assignment within conditional expression
    #else
      #pragma warning(disable: 4702)    // unreachable code caused by optimizations
      #pragma warning(disable: 4791)    // loss of debugging info in retail version
    #endif

    #if !defined( _SHOW_INLINE )
      #pragma warning(disable: 4505)    // unreferenced static function removed
      #pragma warning(disable: 4514)    // unreferenced inline function removed
      #pragma warning(disable: 4710)    // inline function not expanded
    #endif
  #endif

#endif // _COMPILER

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Custom Header.
//  Usage: CL ... -D_CH=\"YourPath/YourProfile.Ext\"
//  YourProfile.Ext usually #define/#undef some symbols to customize particular build
//
#ifdef _CH
  #ifndef _CH_INCLUDED_
    #define  _CH_INCLUDED_
    #pragma  message("    BaseAPI.h includes Custom Header _CH=" _CH)
    #include _CH
  #endif
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//                              CONNECT TO OS API (if not yet)                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////
#ifndef WINAPI
  #include <windows.h>
#endif

#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>
#include <share.h>
#include <malloc.h>
#include <time.h>
#include <locale.h>
#include <limits.h>
#include <tchar.h>

#ifdef _MBCS
  #include  <mbctype.h>
  #include  <mbstring.h>
#endif

#ifdef _THREADS
  #if !defined(_P_NOWAIT)
    #include  <process.h>
  #endif
#endif

#if defined( __cplusplus )
  #ifdef _CPPUNWIND
    #include  <stdexcpt.h>
  #endif
#endif

#if defined(_CPPRTTI)
  #include  <typeinfo.h>
#endif

#if defined(_WIN32_COM)
  #define RPC_NO_WINDOWS_H
  #define COM_NO_WINDOWS_H
  #include  <ole2.h>
#endif

#ifndef _HRESULT_DEFINED
  #define _HRESULT_DEFINED
  typedef LONG HRESULT;
#endif

#ifndef MAKE_HRESULT
  #include  <WinError.h>
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  RESTORE WARNINGS
//
#pragma warning(default: 4214)      // nonint packed fields

#if !defined( _ALL_WARNINGS )
  #pragma warning(disable: 4201)    // MSVC 6 restores this warning
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                      MORE SYSTEM TYPES                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////

typedef __int64                 int64;
typedef unsigned __int64        uint64;

typedef int64                   INT64;
typedef uint64                  UINT64;
typedef uint64                  QWORD;

#ifndef _TCHAR
#define _TCHAR                  TCHAR
#endif
#ifndef PCTSTR
#define PCTSTR                  LPCTSTR
#endif
#ifndef PCVOID
#define PCVOID                  LPCVOID
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                    SOME USEFUL CONSTANTS                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////

#if defined( _WINDLL )
  #define       exe_or_dll      _T("-dll")
  #define       exe_or_dll_A    "-dll"
#else
  #define       exe_or_dll
  #define       exe_or_dll_A
#endif

#if defined( _WIN64 )
  #define       w32_64          _T("64")
  #define       w32_64_A        "64"
#elif defined( _WIN32 )
  #define       w32_64          _T("32")
  #define       w32_64_A        "32"
#else
  #error _WIN32 or _WIN64 must be defined
#endif

#if defined( _UNICODE )
  #define       uni_or_ansi     _T(" Unicode")
  #define       uni_or_ansi_A   " Unicode"
#else
  #define       uni_or_ansi
  #define       uni_or_ansi_A
#endif

#if defined( _CONSOLE )
  #define       _TARGET_Name    _T("Con") w32_64 exe_or_dll
  #define       _TARGET_Name_A  "Con" w32_64_A exe_or_dll_A
#elif defined( _WINDOWS )
  #define       _TARGET_Name    _T("Win") w32_64 exe_or_dll
  #define       _TARGET_Name_A  "Win" w32_64_A exe_or_dll_A
  #define       _GUI
#else
  #pragma message("Warning: Neither _CONSOLE nor _WINDOWS defined; possible configuration problem")
  #define       _TARGET_Name    _T("App") w32_64 exe_or_dll
  #define       _TARGET_Name_A  "App" w32_64_A exe_or_dll_A
#endif

#if defined(_FULLDIAG)
  #define       _BUILD_Type     _T("FullDiag") uni_or_ansi
  #define       _BUILD_Type_A   "FullDiag" uni_or_ansi_A
#elif defined(_DIAG)
  #define       _BUILD_Type     _T("Debug") uni_or_ansi
  #define       _BUILD_Type_A   "Debug" uni_or_ansi_A
#elif defined(_RELEASE_QFE)
  #define       _BUILD_Type     _T("QFE Release") uni_or_ansi
  #define       _BUILD_Type_A   "QFE Release" uni_or_ansi_A
#elif defined(_RELEASE_PDB)
  #define       _BUILD_Type     _T("PDB Release") uni_or_ansi
  #define       _BUILD_Type_A   "PDB Release" uni_or_ansi_A
#elif defined(NDEBUG)
  #define       _BUILD_Type     _T("Release") uni_or_ansi
  #define       _BUILD_Type_A   "Release" uni_or_ansi_A
#else
  #define       _BUILD_Type     _T("Regular") uni_or_ansi
  #define       _BUILD_Type_A   "Regular" uni_or_ansi_A
#endif

#define APP_TITLE(AppName)      _T(#AppName) _T(" with ") _COMPILER_Name _T(". ") \
                                _BUILD_Type _T(" build, ") _TARGET_Name

#define APP_TITLE_A(AppName)    #AppName " with " _COMPILER_Name_A ". " \
                                _BUILD_Type_A " build, " _TARGET_Name_A

/////////////////////////////////////////////////////////////////////////////////////////////////

#define     WHITE_SPACES        _T(" \t\n\v\f\r")
#define     WHITE_SPACES_A      " \t\n\v\f\r"

#ifndef _T
  #define _T(quote)             __TEXT(quote)
#endif

#ifndef DEFAULT_BUFSIZE
                                // Default size for temp. string buffers (chars, not bytes)
  #define   DEFAULT_BUFSIZE     512
#endif

#ifndef STACKBUF_THRESHOLD
                                // Larger temp buffers should be allocated in the heap
  #define   STACKBUF_THRESHOLD  2048
#endif

#ifdef _WIN64
                                // 0000000000000000 + null_char + extra
  #define   HEXADDR_BUFSIZE     18
#else
                                // 00000000 + null_char + extra
  #define   HEXADDR_BUFSIZE     10
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                     SOME USEFUL MACROS                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef _countof
  #define _countof(obj)         (sizeof(obj)/sizeof(obj[0]))
#endif

#define LoByte(w)               ((BYTE)((UINT_PTR)(w) & 0xFF))
#define HiByte(w)               ((BYTE)(((UINT_PTR)(w) >> 8) & 0xFF))

#define LoWord(l)               ((WORD)((UINT_PTR)(l) & 0xFFFF))
#define HiWord(l)               ((WORD)(((UINT_PTR)(l) >> 16) & 0xFFFF))

#define FieldOffset(type, fld)  ((int)(&((type *)1)->fld)-1)
#define MakeLong(low, high)     ((long)(((WORD)(low)) | (((DWORD)((WORD)(high))) << 16)))

#define TypeCast(typ,lval)      (*((typ*)&(lval)))

#ifdef __cplusplus
  #define PtrToRef(typ,ptr)     ((typ&)*((typ*)(ptr)))
#else
  #define PtrToRef(typ,ptr)     __PtrToRef_requires_C_plus_plus
#endif

#define rc(i)                   MAKEINTRESOURCE(i)
#define rcA(i)                  MAKEINTRESOURCEA(i)

#ifndef IS_INTRESOURCE
  #define IS_INTRESOURCE(_r)    (((UINT_PTR)(_r) >> 16) == 0)
#endif

#ifdef __cplusplus
  #define HInst()               ((HINSTANCE)::GetModuleHandle(NULL))
#else
  #define HInst()               ((HINSTANCE)GetModuleHandle(NULL))
#endif

#define WD(rect)                ((rect).right-(rect).left)
#define HT(rect)                ((rect).bottom-(rect).top)

#ifndef UNUSED
  #if defined( _DIAG) || defined( _SRCINFO)
    #define   UNUSED(x)
  #else
    #define   UNUSED(x)         x
  #endif
  #define     UNUSED_ALWAYS(x)  x
#endif

#define FORCEINLINE             __forceinline



#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                   End of file "BaseAPI.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
