/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       services.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Miscellaneous services and "building blocks".
//  Comments:
//              File Created : 14-Aug-2003
//              Last Modified: 09-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __SERVICES_H__ //////////////////////////////////////////////////////////////////////////
#define __SERVICES_H__
#ifndef _NOLIST_HDRS
#pragma message("  services.h")
#endif

#include "yawl/BaseRTL.h"
#include "yawl/CStr.h"
#include "yawl/Hashing.h"
#include "yawl/Guid.h"

#define  _BID_IDENTITY_A    "ADONETDIAG.ETW"
#include "BidImplApi.h"



/////////////////////////////////////////////////////////////////////////////////////////////////
//                                        BaseServices                                         //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ServiceMessage delivers trace output from the diagnostic component itself.
//
#define _SMPUT                              ServiceMessage::Put
#define BidxMessage0(txt)                   _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),txt)
#define BidxMessage1(fmt,a)                 _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a)
#define BidxMessage2(fmt,a,b)               _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b)
#define BidxMessage3(fmt,a,b,c)             _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b,c)
#define BidxMessage4(fmt,a,b,c,d)           _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b,c,d)
#define BidxMessage5(fmt,a,b,c,d,e)         _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b,c,d,e)
#define BidxMessage6(fmt,a,b,c,d,e,f)       _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b,c,d,e,f)
#define BidxMessage7(fmt,a,b,c,d,e,f,g)     _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b,c,d,e,f,g)
#define BidxMessage8(fmt,a,b,c,d,e,f,g,h)   _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b,c,d,e,f,g,h)
#define BidxMessage9(fmt,a,b,c,d,e,f,g,h,i) _SMPUT(xBidSRC,xBidFLAGS(BID_SLN),fmt,a,b,c,d,e,f,g,h,i)

class ServiceMessage
{
 public:
    static void __cdecl Put( UINT_PTR src, UINT_PTR info, PCTSTR fmt, ... );

 protected:
    virtual void putMsg( UINT_PTR src, UINT_PTR info, PCTSTR fmt, va_list argptr, bool& bOk );

    static void setActiveImplementation(const ServiceMessage* pClassInstance);
    static void resetDefaultImplementation();

 private:
    static ServiceMessage*  pServiceMessenger;

}; // ServiceMessage


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  HealthMeter (rudimentary foundation)
//
class HealthMeter
{
    UINT64  _totalExceptions;

 public:
    HealthMeter();

    void    Done();
    void    Init();
    void    Report();

    void    IncrementExceptionCounter();

}; // HealthMeter

extern  HealthMeter g_HealthMeter;


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  API Implementation Interface
//
class IBidApi
{
 public:
    IBidApi()                   {}
    IBidApi(int)                { _indexID = -1;    }
    virtual ~IBidApi()          { _indexID = -1;    }

    int     IndexID() const     { return _indexID;  }
    virtual bool    IsValid()   const { return _indexID >= 0; }


    virtual BOOL    PutStrA     (UINT_PTR src, UINT_PTR info, PCSTR  str);
    virtual BOOL    PutStrW     (UINT_PTR src, UINT_PTR info, PCWSTR str);

    virtual BOOL    TraceVA     (UINT_PTR src, UINT_PTR info, PCSTR  fmt, va_list args);
    virtual BOOL    TraceVW     (UINT_PTR src, UINT_PTR info, PCWSTR fmt, va_list args);

    virtual BOOL    ScopeEnterVA(UINT_PTR src, UINT_PTR info, HANDLE* pHScp, PCSTR  stf, va_list va);
    virtual BOOL    ScopeEnterVW(UINT_PTR src, UINT_PTR info, HANDLE* pHScp, PCWSTR stf, va_list va);
    virtual BOOL    ScopeLeave  (UINT_PTR src, UINT_PTR info, HANDLE* pHScp);

    virtual BOOL    EnabledA    (UINT_PTR src, UINT_PTR info, PCSTR  tcs);
    virtual BOOL    EnabledW    (UINT_PTR src, UINT_PTR info, PCWSTR tcs);

    virtual int     Indent      (int nIdx);
    virtual INT_PTR Snap        (INT_PTR evtID, INT_PTR arg1, INT_PTR arg2);

    virtual BOOL    Assert      (UINT_PTR arg, UINT_PTR info);
    virtual INT_PTR CtlProc     (INT_PTR cmdSpaceID, int cmd, INT_PTR a1, INT_PTR a2, INT_PTR a3);
    virtual INT_PTR Touch       (UINT_PTR scope, UINT code, INT_PTR arg1, INT_PTR arg2);

 protected:
    int     _indexID;
    const   int* IndexIDPtr() const  { return &_indexID; }

}; // IBidApi

typedef IBidApi*        PBidApi;


#ifdef _UNICODE
  #define PutStrT       PutStrW
  #define TraceVT       TraceVW
  #define ScopeEnterT   ScopeEnterW
  #define EnabledT      EnabledW
#else
  #define PutStrT       PutStrA
  #define TraceVT       TraceVA
  #define ScopeEnterT   ScopeEnterA
  #define EnabledT      EnabledA
#endif


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Execution Context Local Storage
//
#define TLS_NOVALUE (DWORD)(-1)

class ExecutionContextLocalStorage
{
 public:
    ~ExecutionContextLocalStorage()                             { if( IsInitialized() ) Done(); }

    static bool  IsInitialized()                             { return _tlsIndex != TLS_NOVALUE; }
    static void  Init();
    static void  Done();

    static PVOID Get()
    {
        DASSERT( IsInitialized() );
        return YAWL_TlsGetValue( _tlsIndex );
    }
    static void  Set(PVOID value)
    {
        DASSERT( IsInitialized() );
        YAWL_TlsSetValue( _tlsIndex, value );
    }

 protected:
    static DWORD _tlsIndex;
};

extern ExecutionContextLocalStorage     g_ExecutionContextLocalStorage;


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Indentation
//
//  NOTE: In this version of adapter, indentation is the only info stored in TLS.
//        So we use TLS value directly instead of allocating a struct to be pointed by TLS value.
//
union IndentInfo
{
    struct  {
      short Level;
      bool  Needed;
    }       ps;
    PVOID   pvRaw;

    IndentInfo()
    {
        pvRaw = g_ExecutionContextLocalStorage.Get();
    }
    void Store()
    {
        g_ExecutionContextLocalStorage.Set(pvRaw);
    }

}; // IndentInfo

#define Indent_ACCESS()     IndentInfo indentationLocalObject
#define Indent_STORE()      indentationLocalObject.Store()

#define Indent_Needed       indentationLocalObject.ps.Needed
#define Indent_Level        indentationLocalObject.ps.Level

#define Indent_Increment()  do{                                     \
                                int tmp = (int)Indent_Level + 1;    \
                                if( tmp > BID_INDENT_MAX ){         \
                                    tmp = BID_INDENT_MAX;           \
                                }                                   \
                                Indent_Level = (short)tmp;          \
                            }while(0)

#define Indent_Decrement()  do{                                     \
                                int tmp = (int)Indent_Level - 1;    \
                                if( tmp < 0 ) tmp = 0;              \
                                Indent_Level = (short)tmp;          \
                            }while(0)

#define Indent_Set(nIndent) do{                                     \
                                if( nIndent < 0 ) nIndent = 0;      \
                                if( nIndent > BID_INDENT_MAX ){     \
                                    nIndent = BID_INDENT_MAX;       \
                                }                                   \
                                Indent_Level = (short)nIndent;      \
                            }while(0)


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  InstanceIdProvider
//
class InstanceIdProvider
{
 public:
    InstanceIdProvider();

    void    Done()  {}
    void    Init()  {}

    int     GenerateDefault();

 protected:
    volatile LONG _source;

}; // InstanceIdProvider


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ModulePath
//
class ModulePath
{
    CStr    _fullPath;
    CStr    _nameOnly;
    CStr    _pathOnly;
    PCTSTR  _nameExt;

 public:
    ModulePath()                                                            { _nameExt = NULL;  }
    ModulePath(const ModulePath& other)                                     { copyFrom(other);  }
    const ModulePath& operator=(const ModulePath& other)       { copyFrom(other); return *this; }

    void    Done();
    void    Init(CREFSTR pathStr);

    bool    IsEmpty() const                                       { return _fullPath.IsEmpty(); }

    operator PCTSTR() const                                                 { return _fullPath; }

    CStr    GetFullPath() const                                             { return _fullPath; }
    PCTSTR  GetNameExt() const                                              { return _nameExt;  }
    CStr    GetNameOnly() const;
    CStr    GetPathOnly() const;

 private:
    void    copyFrom(const ModulePath& other);
    int     nameExtOffset() const;

}; // ModulePath


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ModuleHandle
//
#define ModuleHandle_NOVALUE    (HMODULE)(-1)

class ModuleHandle
{
    HMODULE     _hModule;

 public:
    ModuleHandle()                                          { _hModule = ModuleHandle_NOVALUE;  }

    operator HMODULE() const                                                { return _hModule;  }

    void    Done()                                          { _hModule = ModuleHandle_NOVALUE;  }
    void    Init(HMODULE hModule, PCVOID codeAddress);

    CStr    GetFileName() const;
    bool    IsEmpty() const                         { return _hModule == ModuleHandle_NOVALUE;  }

}; // ModuleHandle


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Provides access to the return address. Example:
//      void Foo( int needAtLeastOneArg )
//      {
//          PCVOID retAddr = RETURN_ADDRESS( &needAtLeastOneArg );
//          ...
//      }
//
#if _MSC_VER >= 1300                    // This also covers _WIN64
  #ifdef __cplusplus
  extern "C"
  #endif
  void* _ReturnAddress(void);
  #pragma intrinsic(_ReturnAddress)

  #define RETURN_ADDRESS(pFirstArg)     (PCVOID)_ReturnAddress()
#else
  #define RETURN_ADDRESS(pFirstArg)     (*((PCVOID*)(((char*)(pFirstArg)) - _INTSIZEOF(char))))
#endif

//
//  Gets HMODULE from code address. Allows to recognize the caller. Example:
//      void Bar( int needAtLeastOneArg )
//      {
//          HMODULE hCaller = GetModuleHandleFromAddress( RETURN_ADDRESS( &needAtLeastOneArg ) );
//          GetModuleFileName( hCaller, ... );
//          ...
//      }
//
HMODULE GetModuleHandleFromAddress( PCVOID codeAddress );


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ModuleIdentity
//
class ModuleIdentity
{
 public:
    void        Done()                                      { _textID.Empty(); _guidID.Done();  }
    void        Init(PCSTR sIdentity, const ModulePath& modPath);
    bool        IsValid() const                                     { return !_textID.IsEmpty();}

    CStr        TextID() const                                              { return _textID;   }
    operator    PCTSTR() const                                              { return _textID;   }
    const Guid& GetGuidRef() const                                          { return _guidID;   }
    CStr        ToString() const;

 private:
    CStr        _textID;
    Guid        _guidID;

}; // ModuleIdentity


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidConfigBits
//
class BidConfigBits
{
    DWORD   _data;

 public:
    BidConfigBits()                                                         { _data = 0;        }

    void    Done()                                                          { _data = 0;        }
    void    Init(DWORD cfgBits)                                             { _data = cfgBits;  }
    bool    IsValid() const                         { return (_data & BID_CFG_ACTIVE_BID) != 0; }

    operator DWORD() const                                                  { return _data;     }

    bool    Approved() const;

    //
    //  Configuration Properties
    //
    UINT AcpOrUtf8() const          { return ((_data & BID_CFG_UTF8) != 0) ? CP_UTF8 : CP_ACP;  }
    bool AltPage() const                        { return ((_data & BID_CFG_MASK_PAGE) != 0);    }
    bool ControlCallback() const                { return (_data & BID_CFG_CTLCALLBACK) != 0;    }
    bool DebugBreak() const                     { return (_data & BID_CFG_DEBUG_BREAK) != 0;    }
    bool DebugTrace() const                     { return (_data & BID_CFG_DEBUG_TRACE) != 0;    }
    bool NoSourceFileInfo() const               { return (_data & BID_CFG_NO_SRCINFO) != 0;     }
    bool NoSpecialAllocation() const          { return (_data & BID_CFG_NO_SPECIAL_ALLOC) != 0; }

}; // BidConfigBits


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidExtendedInfo
//
class BidExtendedInfo
{
    HMODULE _modHandle;
    CStr    _modPath;
    bool    _bValid;

 public:
    BidExtendedInfo()                                                   { cleanup();            }

    void    Done()                                                      { cleanup();            }
    void    Init(PBIDEXTINFO pExt, const BidConfigBits& cfgBits);
    bool    IsValid() const                                             { return _bValid;       }

    bool    IsModulePath() const                                { return !_modPath.IsEmpty();   }
    HMODULE ModuleHandle() const                                        { return _modHandle;    }
    CStr    ModulePath() const                                          { return _modPath;      }

 private:
    void    cleanup();
    void    setModPath(PCSTR str, UINT codePage);

}; // BidExtendedInfo


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidSectionHeader
//
class BidSectionHeader
{
    PBIDMARKER  _marker;
    DWORD       _attributes;
    DWORD       _checksum;
    bool        _bValid;

 public:
    BidSectionHeader()                                                          { cleanup();    }

    void    Done()                                                              { cleanup();    }
    void    Init(PBIDSECTHDR pHdr);
    bool    IsEmpty() const                                         { return _attributes == 0;  }
    bool    IsValid() const                                         { return _bValid;           }

    DWORD   Checksum() const                                                { return _checksum; }
    int     HeaderSize() const                      { return BID_HdrAttrSize(_attributes);      }
    int     NumOfMarkers() const                    { return BID_HdrAttrSECount(_attributes);   }
    int     Version() const                         { return BID_HdrAttrVersion(_attributes);   }

    PBIDMARKER  Marker() const                                              { return _marker;   }

 private:
    void    cleanup();

    BID_EXTENSION_DECLARE( BidSectionHeader );

}; // BidSectionHeader


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  UnloadCallback
//
class UnloadCallback
{
 public:
    enum                            //  system  dbg     opt
    {                               //  x86:    173     145
        eMinCodeSize     = 150,     //  ia64:
        eDefaultCodeSize = 256      //  amd64:
    };

    UnloadCallback()                              { _unloadCallbackPtr = NULL; _bValid = false; }
    ~UnloadCallback()                                                              { cleanup(); }

    void    Done(int indexID, bool bForcedCleanup = false);
    void    Init(PBIDHOOKS pHooks, int sizeInBytes = eDefaultCodeSize);

    void    Execute(int indexID);

    PCVOID  CallbackPtr() const                            { return (PCVOID)_unloadCallbackPtr; }
    bool    IsEmpty() const                                { return _unloadCallbackPtr == NULL; }
    bool    IsValid() const                                                   { return _bValid; }

 private:
    BIDUNLOADCB     _unloadCallbackPtr;
    MemBlkRaw<BYTE> _codeFragmentBuf;
    bool            _bValid;

    void    cleanup();
    bool    isCodeTheSame();

}; // UnloadCallback


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidCtlCallback
//
class BidCtlCallback
{
    enum EStatus { eInvalid, eNone, ePointer, eCallback };

    DWORD*          _pCtlFlags;
    BID_CTLCALLBACK _ctlProc;
    DWORD           _cache;
    EStatus         _status;
    bool            _bEnabled;

 public:
    BidCtlCallback();
    ~BidCtlCallback()               { Done();   }

    void    Done();
    void    Init(const BidConfigBits& cfgBits, DWORD* pGblFlags, BID_CTLCALLBACK ctlProc);
    bool    IsValid() const         { return _status != eInvalid; }

    DWORD   Set(DWORD bits);
    DWORD   Get();

    void    Disable()               { _bEnabled = false;}
    void    Enable()                { _bEnabled = true; }
    DWORD   GetCache() const        { return _cache;    }
    void    SetCache(DWORD bits)    { _cache = bits;    }
    void    UploadCache()           { Set( _cache );    }

}; // BidCtlCallback


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BindingContract
//
class BindingContract
{
    int                 _version;
    BidConfigBits       _cfgBits;
    BidSectionHeader    _header;
    BidExtendedInfo     _extInfo;
    ModuleHandle        _modHandle;
    ModulePath          _modPath;
    ModuleIdentity      _identity;
    bool                _bValid;

 public:
    BindingContract()                                          { _version = 0; _bValid = false; }

    void    Done();
    void    Init( HANDLE hModule );
    void    Init( int bInitAndVer, PCSTR sIdentity, DWORD cfgBits, PCVOID codeAddress,
                  PBIDEXTINFO pExtInfo, PBIDSECTHDR pHdr );
    bool    IsValid() const                                                   { return _bValid; }

    bool    Approved() const;
    void    Populate( BidConfigBits& rCfgBits, ModuleHandle& rModHandle,
                      ModulePath& rModPath, ModuleIdentity& rIdentity ) const;

    PCTSTR  GetIdentity() const                                 { return (PCTSTR)_identity;     }
    HMODULE GetModuleHandle() const                             { return (HMODULE)_modHandle;   }
    PCTSTR  GetModulePath() const                               { return (PCTSTR)_modPath;      }
    int     GetVersion() const                                              { return _version;  }

    BID_EXTENSION_DECLARE( BindingContract );

 private:
    bool    Constrained() const;

}; // BindingContract



#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                  End of file "services.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
