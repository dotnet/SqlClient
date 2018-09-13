/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       BaseRTL.h
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
#ifndef __BASERTL_H__ ///////////////////////////////////////////////////////////////////////////
#define __BASERTL_H__
#ifndef _NOLIST_HDRS
#pragma message("  BaseRTL-28.h")
#endif

#include "yawl/BaseAPI.h"
#include "yawl/cpu.h"
#include "BidApi.h"

#if _MSC_VER < 1200
  #error Compiler version is not supported. Must be VC++ 6.0 or newer.
#endif
#ifndef __cplusplus
  #error 'C' is not supported. Must be 'C++'
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  POINTER SAFETY CHECK
//
BOOL WINAPI xBidValidAddress    (LPCVOID lp, size_t nBytes, BOOL bReadWrite);
BOOL WINAPI xBidValidStringA    (LPCSTR  lpsz, int nLength, BOOL bReadWrite);
BOOL WINAPI xBidValidStringW    (LPCWSTR lpsz, int nLength, BOOL bReadWrite);

inline BOOL WINAPI BidValidAddress( LPCVOID lp, size_t nBytes, BOOL bReadWrite = TRUE)
    { return xBidValidAddress( lp, nBytes, bReadWrite );      }

inline BOOL WINAPI BidValidStringA( LPCSTR  lpsz, int nLength = -1, BOOL bReadWrite = FALSE)
    { return xBidValidStringA( lpsz, nLength, bReadWrite );   }

inline BOOL WINAPI BidValidStringW( LPCWSTR lpsz, int nLength = -1, BOOL bReadWrite = FALSE)
    { return xBidValidStringW( lpsz, nLength, bReadWrite );   }

#ifdef _UNICODE
  #define xBidValidString       xBidValidStringW
  #define BidValidString        BidValidStringW
#else
  #define xBidValidString       xBidValidStringA
  #define BidValidString        BidValidStringA
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////

#define DASSERT_NULL_OR_POINTER(p)  \
        DASSERT( ((p) == NULL) || xBidValidAddress((p), sizeof(*(p)), FALSE))

#define DASSERT_NULL_OR_STRING(p)   \
        DASSERT( ((p) == NULL) || xBidValidString((p), -1, FALSE))

#define DASSERT_NULL_OR_STRING_A(p) \
        DASSERT( ((p) == NULL) || xBidValidStringA((p), -1, FALSE))

#define DASSERT_POINTER(p)          \
        DASSERT( ((p) != NULL) && xBidValidAddress((p), sizeof(*(p)), FALSE))

#define DASSERT_POINTER_RW(p)       \
        DASSERT( ((p) != NULL) && xBidValidAddress((p), sizeof(*(p)), TRUE))

#define DASSERT_RESOURCE_ID(lpSz)   \
        DASSERT( ((UINT_PTR)(lpSz) & ~0xFFFF) == 0 || xBidValidString(lpSz, -1, FALSE))

#define DASSERT_RESOURCE_ID_A(lpSz)   \
        DASSERT( ((UINT_PTR)(lpSz) & ~0xFFFF) == 0 || xBidValidStringA(lpSz, -1, FALSE))

#define DASSERT_STRING(p)           \
        DASSERT( xBidValidString((p), -1, FALSE) )

#define DASSERT_STRING_A(p)         \
        DASSERT( xBidValidStringA((p), -1, FALSE) )

#define DASSERT_STRING_W(p)         \
        DASSERT( xBidValidStringW((p), -1, FALSE) )


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                         EXCEPTIONS                                          //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Define the symbols below to incorporate YAWL exceptions into existing code base, for example:
//
//  #define _YAWL_XEPT_BASE                 :public exception
//  #define _YAWL_XEPT_BASE_CTOR1(cat)      :exception()
//  #define _YAWL_XEPT_BASE_CTOR2(cat,id)   :exception()
//
#ifndef _YAWL_XEPT_BASE
  #define _YAWL_XEPT_BASE
  #define _YAWL_XEPT_BASE_CTOR1(cat)
  #define _YAWL_XEPT_BASE_CTOR2(cat,id)
#else
  #pragma message("NOTE: _YAWL_XEPT_BASE redefined.")
#endif

//
//  Redefine the following macros in order to use different class for YAWL exceptions
//
#ifndef _XEPT_THROW_DEFINED
  #define XEPT_THROW(category)          throw Xept(category)
  #define XEPT_THROW1(category,code)    throw Xept(category,code)
  #define _XEPT_THROW_DEFINED
#else
  #pragma message("NOTE: XEPT_THROW redefined.")
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////

enum XeptCategory
{
    XC_OTHER,   //  Unspecified.
    XC_MEM,     //  Out Of Memory.
    XC_IOFAIL,  //  IO problem such as Device Failure, etc. Not for normal things such as EOF!
    XC_ARGS,    //  Invalid input data.
    XC_NOTDONE, //  Operation cannot be completed.
    XC_ABORT,   //  Unrecoverable error causes entire operation to be cancelled.

    XC_Count    //  To be used as index range, i.e. mapTable [XC_Count] = {...}
};

class Xept _YAWL_XEPT_BASE
{
    UINT_PTR     _details;
    XeptCategory _category;
    bool         _detailsInUse;

 public:
    Xept(XeptCategory category = XC_OTHER)      _YAWL_XEPT_BASE_CTOR1(category)
    {
        _details      = 0;
        _category     = category;
        _detailsInUse = false;
    }
    Xept(XeptCategory category, UINT_PTR id)    _YAWL_XEPT_BASE_CTOR2(category,id)
    {
        _details      = id;
        _category     = category;
        _detailsInUse = true;
    }

    XeptCategory XeptCategory() const       { return _category;         }
    UINT         XeptCode() const           { return (UINT)_details;    }
    UINT_PTR     XeptDetails() const        { return _details;          }
    bool         XeptHasDetails() const     { return _detailsInUse;     }

}; // Xept

/////////////////////////////////////////////////////////////////////////////////////////////////

#define YAWL_THROW(category,txt)                                        \
        {                                                               \
            BidTraceEx0A(BidIsOn(BID_APIGROUP_TRACE),                   \
                        BID_DEMAND_SRC|BID_SLN,                         \
                        BID_TAG1A("THROW|" #category) txt);             \
            XEPT_THROW(category);                                       \
        }


#define YAWL_THROW1(category,code,txt)                                  \
        {                                                               \
            BidTraceEx1A(BidIsOn(BID_APIGROUP_TRACE),                   \
                        BID_DEMAND_SRC|BID_SLN,                         \
                        BID_TAG1A("THROW|" #category) "%p " txt,        \
                        code);                                          \
            XEPT_THROW1(category, (UINT_PTR)code);                      \
        }


#define YAWL_THROW_FROM_METHOD(category,txt)                            \
        {                                                               \
            BidTraceEx1A(BidIsOn(BID_APIGROUP_TRACE),                   \
                        BID_DEMAND_SRC|BID_SLN,                         \
                        BID_TAG1A("THROW|" #category) "%p{.} " txt,     \
                        this);                                          \
            XEPT_THROW(category);                                       \
        }


#define YAWL_THROW1_FROM_METHOD(category,code,txt)                      \
        {                                                               \
            BidTraceEx2A(BidIsOn(BID_APIGROUP_TRACE),                   \
                        BID_DEMAND_SRC|BID_SLN,                         \
                        BID_TAG1A("THROW|" #category) "%p{.}  %p " txt, \
                        this, code);                                    \
            XEPT_THROW1(category, (UINT_PTR)code);                      \
        }


#define YAWL_THROW_IF(cond,category,txt)                if(cond) YAWL_THROW(category,txt)
#define YAWL_THROW1_IF(cond,category,code,txt)          if(cond) YAWL_THROW1(category,code,txt)
#define YAWL_THROW_FROM_METHOD_IF(cond,cat,txt)         if(cond) YAWL_THROW_FROM_METHOD(cat,txt)
#define YAWL_THROW1_FROM_METHOD_IF(cond,cat,code,txt)   if(cond) YAWL_THROW1_FROM_METHOD(cat,code,txt)


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Win32 Structured Exception Handling (SEH) Filter
//
DWORD _yawlSehFilter(UINT_PTR src, UINT_PTR info, PEXCEPTION_POINTERS pInfo, BOOL bDontCatch);

#define YAWL_SEH_FILTER(pInfo,dontCatch) \
                                        _yawlSehFilter(xBidSRC2, xBidFLAGS2(0), pInfo, dontCatch)

#define YAWL_EXCEPTION_EXECUTE_HANDLER  YAWL_SEH_FILTER(GetExceptionInformation(), FALSE)



/////////////////////////////////////////////////////////////////////////////////////////////////
//                        MEMORY MANAGEMENT WRAPPERs (MMW, replaceable)                        //
/////////////////////////////////////////////////////////////////////////////////////////////////

#ifdef _MMW

  #ifndef _MMW_FILE_INCLUDED
    #define _MMW_FILE_INCLUDED
    #pragma message("NOTE: BaseRTL.h includes Memory Management Wrappers _MMW=\"" _MMW "\"")
    #include _MMW
  #endif

#else

    #ifdef _FULLDIAG
      #define _CRTDBG_MAP_ALLOC
      #include  <crtdbg.h>
    #endif

    #define MMW_INIT(arg)                   ((void)0)
    #define MMW_DONE(arg)                   ((void)0)

    #define MMW_MALLOC(ptr,typ,nBytes)      ptr = (typ*) malloc(nBytes)
    #define MMW_REALLOC(ptr,typ,nBytes)     ptr = (typ*) _yawlRealloc(ptr,nBytes)
    #define MMW_FREE(ptr)                   free((void*)(ptr))
    #define MMW_MSIZE(ptr)                  _msize((void*)(ptr))

    #define MMW_NEW(ptr,typ,args)           ptr = new typ args
    #define MMW_DELETE(ptr)                 delete (ptr)
    #define MMW_DELETE_VEC(ptr)             delete[] (ptr)
    #define MMW_DELETE_THIS()               delete this;

    //
    //  Custom wrapper for standard 'realloc'
    //
    void* __cdecl _yawlRealloc(void*, size_t);

#endif // _MMW



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                 YAWL MEMORY MANAGEMENT API                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  NOTE: The use of YAWL_<memoryAllocation> API is mandatory across all YAWL components.
//
#define YAWL_INIT   MMW_INIT
#define YAWL_DONE   MMW_DONE

#define YAWL_MSIZE  MMW_MSIZE


#define _yawlThrowNull(ptr,sz)  \
        YAWL_THROW1_IF((PCVOID)(ptr) == NULL && (sz) > 0, XC_MEM, sz, "bytes(hex)")


#define YAWL_MALLOC(ptr, typ, nBytes)           \
        {                                       \
            DASSERT((ptr) == NULL);             \
            DASSERT(nBytes > 0);                \
            MMW_MALLOC(ptr, typ, nBytes);       \
            _yawlThrowNull(ptr, nBytes);        \
        }

#define YAWL_REALLOC(ptr, typ, nBytes)          \
        {                                       \
            MMW_REALLOC(ptr, typ, nBytes);      \
            _yawlThrowNull(ptr, nBytes);        \
        }

#define YAWL_FREE(ptr)                          \
        if( (ptr) != NULL ){                    \
            MMW_FREE(ptr);                      \
            (ptr) = NULL;                       \
        }

#define YAWL_NEW(ptr, typ, args)                \
        {                                       \
            DASSERT((ptr) == NULL);             \
            MMW_NEW(ptr, typ, args);            \
            YAWL_THROW_IF((ptr) == NULL, XC_MEM, #typ ## #args); \
        }

#define YAWL_DELETE(ptr)                        \
        if( (ptr) != NULL ){                    \
            MMW_DELETE(ptr);                    \
            (ptr) = NULL;                       \
        }

#define YAWL_DELETE_EX(typ,ptr)                 \
        if( (ptr) != NULL ){                    \
            MMW_DELETE((typ)(ptr));             \
            (ptr) = NULL;                       \
        }

#define YAWL_DELETE_VEC(ptr)                    \
        if( (ptr) != NULL ){                    \
            MMW_DELETE_VEC(ptr);                \
            (ptr) = NULL;                       \
        }

#define YAWL_DELETE_VEC_EX(typ, ptr)            \
        if( (ptr) != NULL ){                    \
            MMW_DELETE_VEC((typ)(ptr));         \
            (ptr) = NULL;                       \
        }

#define YAWL_DELETE_THIS()                      \
        {                                       \
            DASSERT( (void*)this != NULL );     \
            MMW_DELETE_THIS();                  \
        }

//
//  MemStub
//
extern  const int _yawlEmptyData[16];


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Dynamic Memory Block (raw; primitive types)
//
class MemBlkRawBase
{
    BYTE*   _blockPtr;
    UINT    _bytesAllocated;

 public:
    ~MemBlkRawBase()                                                            { Free(true);   }

    void    Free(bool bAuto = false);
    bool    IsAllocated() const                                     { return _blockPtr != NULL; }
    UINT    BytesAllocated() const                                  { return _bytesAllocated;   }
    UINT    BytesInUse() const                                      { return _bytesAllocated;   }

 protected:
    MemBlkRawBase()                                    { _blockPtr = NULL; _bytesAllocated = 0; }
    MemBlkRawBase(const MemBlkRawBase&);

    void    alloc(UINT_PTR src, UINT_PTR info, UINT sizeInItems, size_t bytesPerItem);
    void    copyFrom(const BYTE* pOther, UINT sizeInBytes);
    void    copyFrom(const MemBlkRawBase& other);
    BYTE*   getPtr() const                      { DASSERT(_blockPtr != NULL); return _blockPtr; }
    void    rawFree();

}; // MemBlkRawBase


template<typename TYP>
class MemBlkRaw : public MemBlkRawBase
{
 public:
    MemBlkRaw()                         {}
    MemBlkRaw(const MemBlkRaw& other)                                       { copyFrom(other);  }

    const MemBlkRaw& operator= (const MemBlkRaw& other)
    {
        copyFrom(other);
        return *this;
    }

    void Alloc(UINT_PTR src, UINT_PTR info, UINT numOfItems)
    {
        alloc(src, info, numOfItems, sizeof(TYP));
    }

    TYP* operator->()                                                           { return Ptr(); }
    TYP* operator&()                                                            { return Ptr(); }
    bool operator==(int iNul)                   { DASSERT( iNul == 0 ); return !IsAllocated();  }
    TYP& operator[](int idx)                { DASSERT(UINT(idx) < Count()); return Ptr()[idx];  }

    TYP* Ptr()                  { return IsAllocated() ? (TYP*)getPtr() : (TYP*)_yawlEmptyData; }
    UINT Size() const                           { return BytesAllocated() / (UINT)sizeof(TYP);  }
    UINT Count() const                              { return BytesInUse() / (UINT)sizeof(TYP);  }

}; // MemBlkRaw

#define MemBlkRaw_ALLOC(blk,size)       blk.Alloc(xBidSRC2, xBidFLAGS2(0), size)


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                    MISCELLANEOUS HELPERS                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Various info from the OS
//
int     GetNumberOfProcessors();

bool    IsPlatform64Bit();
bool    IsPlatform9x();
bool    IsPlatformMultiProc();
bool    IsPlatformNT();
bool    IsPlatformWow64();

bool    IsSupportedAsyncFileIO();
bool    IsSupportedPlatform();  // see if platform meets minimal requirements to run on
bool    IsSupportedUnicode();

#ifdef _WIN64
  #define IsPlatform64Bit()     true
  #define IsPlatform9x()        false
  #define IsPlatformNT()        true
  #define IsPlatformWow64()     false
  #define IsSupportedUnicode()  true
#else
  #define IsPlatform64Bit()     false
#endif

#define IsPlatformMultiProc()   (GetNumberOfProcessors() > 1)

//
//  TimeStampCounter
//
bool    IsSupportedTimeStampCounter();

typedef UINT64 (WINAPI* ReadTimeStampCounter_t)();
extern  ReadTimeStampCounter_t  _yawlReadTimeStampCounter;

#define YAWL_ReadTimeStampCounter   (*_yawlReadTimeStampCounter)


/////////////////////////////////////////////////////////////////////////////////////////////////

int     GetStrLenA(PCSTR  str, int nLen = -1);
int     GetStrLenW(PCWSTR str, int nLen = -1);

inline  bool IsCharSeparatorA(char  ch)                     { return ch ==  '\\' || ch ==  '/'; }
inline  bool IsCharSeparatorW(WCHAR ch)                     { return ch == L'\\' || ch == L'/'; }

#ifdef _UNICODE
  #define GetStrLen         GetStrLenW
  #define IsCharSeparator   IsCharSeparatorW
#else
  #define GetStrLen         GetStrLenA
  #define IsCharSeparator   IsCharSeparatorA
#endif

FARPROC GetNtDllApi(PCSTR apiName);
FARPROC GetKernelApi(PCSTR apiName);


/////////////////////////////////////////////////////////////////////////////////////////////////
//                             COMPATIBILITY / ABSTRACTION WRAPPERS                            //
/////////////////////////////////////////////////////////////////////////////////////////////////

#if _MSC_VER >= 1300    // Also covers _WIN64

    #define YAWL_InterlockedExchange                InterlockedExchange
    #define YAWL_InterlockedExchangePointer         InterlockedExchangePointer
    #define YAWL_InterlockedCompareExchange         InterlockedCompareExchange
    #define YAWL_InterlockedCompareExchangePointer  InterlockedCompareExchangePointer

    #define YAWL_InterlockedIncrement               InterlockedIncrement
    #define YAWL_InterlockedDecrement               InterlockedDecrement

#else

    #define YAWL_InterlockedExchange(ptr,val)       InterlockedExchange((LONG*)ptr,val)
    #define YAWL_InterlockedExchangePointer(p,v)    (PVOID)InterlockedExchange((LONG*)p,(LONG)v)

    #define YAWL_InterlockedCompareExchange(p,v,c)  \
                                    (LONG)InterlockedCompareExchange((PVOID*)p,(PVOID)v,(PVOID)c)

    #define YAWL_InterlockedCompareExchangePointer(p,v,c) \
                                                    InterlockedCompareExchange((PVOID*)p,v,c)

    #define YAWL_InterlockedIncrement(pval)         InterlockedIncrement((LONG*)pval)
    #define YAWL_InterlockedDecrement(pval)         InterlockedDecrement((LONG*)pval)

#endif

#define YAWL_Sleep                                  Sleep

#define YAWL_CopyMemory                             CopyMemory
#define YAWL_FillMemory                             FillMemory
#define YAWL_MoveMemory                             MoveMemory
#define YAWL_ZeroMemory                             ZeroMemory

#ifdef SecureZeroMemory
  #define YAWL_SecureZeroMemory                     SecureZeroMemory
#else
  #ifdef _YAWL_DONT_ENFORCE_SECURE_ZERO_MEMORY
    #define YAWL_SecureZeroMemory                   ZeroMemory
  #else
    #define YAWL_SecureZeroMemory                   _ERROR__SecureZeroMemory_Not_Defined
  #endif
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                      EXECUTION CONTEXT                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////

typedef void (WINAPI* YieldExecutionContext_t)(void);

extern  YieldExecutionContext_t         _yawlYieldExecutionContext;
#define YieldExecutionContext           (*_yawlYieldExecutionContext)


typedef INT_PTR (WINAPI* GetCurrentExecutionContextId_t)(void);

extern  GetCurrentExecutionContextId_t  _yawlGetCurrentExecutionContextId;
#define GetCurrentExecutionContextId    (*_yawlGetCurrentExecutionContextId)


//
//  Local Storage
//
typedef DWORD (WINAPI* TlsAlloc_t)(void);
typedef BOOL  (WINAPI* TlsFree_t)(DWORD dwTlsIndex);
typedef PVOID (WINAPI* TlsGetValue_t)(DWORD dwTlsIndex);
typedef BOOL  (WINAPI* TlsSetValue_t)(DWORD dwTlsIndex, PVOID lpTlsValue);

extern  TlsAlloc_t              _yawlTlsAlloc;
extern  TlsFree_t               _yawlTlsFree;
extern  TlsGetValue_t           _yawlTlsGetValue;
extern  TlsSetValue_t           _yawlTlsSetValue;

#define YAWL_TlsAlloc           (*_yawlTlsAlloc)
#define YAWL_TlsFree            (*_yawlTlsFree)
#define YAWL_TlsGetValue        (*_yawlTlsGetValue)
#define YAWL_TlsSetValue        (*_yawlTlsSetValue)


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                  End of file "BaseRTL.h"                                    //
/////////////////////////////////////////////////////////////////////////////////////////////////
