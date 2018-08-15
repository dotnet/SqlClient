/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       ModuleObject.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Client Module Object.
//
//  Comments:
//              File Created : 08-Sep-2003
//              Last Modified: 14-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __MODULEOBJECT_H__ //////////////////////////////////////////////////////////////////////
#define __MODULEOBJECT_H__
#ifndef _NOLIST_HDRS
#pragma message("  ModuleObject.h")
#endif

#include "services.h"
#include "EtwObject.h"

class   ModuleObject;
typedef ModuleObject*   PModuleObject;

class ModuleObject : public IBidApi
{
    BidConfigBits       _cfgBits;
    ModuleHandle        _modHandle;
    ModulePath          _modPath;
    ModuleIdentity      _identity;
    UnloadCallback      _unloadCallback;
    BidCtlCallback      _ctlCallback;
    InstanceIdProvider  _instanceIdProvider;
    EtwApi              _etwApi;
    int                 _objID;
    bool                _bInUse;
    bool                _bActivated;

 public:
    ModuleObject(int ordinal);
    virtual ~ModuleObject();

    virtual bool IsValid() const;

    void    Done(bool bForcedCleanup = false);
    bool    Init(const BindingContract& binding, DWORD* pCtlFlags, BID_CTLCALLBACK ctlProc,
                 PBIDHOOKS pHooks);

    DWORD   GetCtrlFlags() const;
    bool    IsInUse() const                                             { return _bInUse;       }
    bool    IsActivated() const                                         { return _bActivated;   }
    int     ObjID() const                                               { return _objID;        }

    void    ReportConnection();
    void    ReportDisconnection(bool bForced);
    static  void ReportRejection(const BindingContract& binding);

    static  void InitSelfDescriptior( PModuleObject pSelf, HANDLE hModule );
    static  void DoneSelfDescriptior( PModuleObject pSelf );
    static  bool IsValidPtr(PModuleObject pObj);

    static  HMODULE GetSelfModuleHandle();

    //
    //  IBidApi interface
    //
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
    virtual INT_PTR CtlProc     (INT_PTR cmdSpaceID, int cmd, INT_PTR arg1, INT_PTR arg2, INT_PTR arg3);
    virtual INT_PTR Touch       (UINT_PTR scope, UINT code, INT_PTR arg1, INT_PTR arg2);

    PCTSTR  ShortDescription(CStr& strBuf) const;

    void    traceItemIDA(PCSTR strApiName, int itemID, PCSTR strID, va_list args);
    void    traceItemIDW(PCWSTR strApiName, int itemID, PCWSTR strID, va_list args);
    bool    bItemID() const                                     { return _etwApi.IsEnabled();   }

 private:
    void    hexDump(PCVOID pBlob, int sizeInBytes);
    void    indent_In();
    void    indent_Out();
    void    __cdecl traceA(PCSTR fmt, ... );

    static  HMODULE _hModuleSelf;

    BID_EXTENSION_DECLARE( ModuleObject );

}; // ModuleObject


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                End of file "ModuleObject.h"                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////
