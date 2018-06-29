/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       ModulePool.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Container for Client Module Objects.
//  Comments:
//              File Created : 08-Sep-2003
//              Last Modified: 30-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __MODULEPOOL_H__ ////////////////////////////////////////////////////////////////////////
#define __MODULEPOOL_H__
#ifndef _NOLIST_HDRS
#pragma message("  ModulePool.h")
#endif

#include "services.h"
#include "ModuleObject.h"

class ModulePool
{
 public:
    enum {
        CAPACITY  = 50,     // <<== Max number of modules to be connected to BID (per process)
        SelfSlot  = 0,
        FirstSlot = 1,
        AllocSize = CAPACITY + 1
    };

    ModulePool()                                    { _bAllocated = false; allocObjects();  }
    ~ModulePool()                                   { freeObjects();  }

    void Init( HANDLE hModule );
    void Done();

    bool Allocate( PModuleObject& rpModule );
    bool Activate( PModuleObject pModule, HANDLE* pID, DWORD* pCtlFlags );
    bool Release( HANDLE* pID );
    void Revert( PModuleObject pModule, HANDLE* pID, DWORD* pCtlFlags );

    PModuleObject GetSelf() const
    {
        DASSERT( _bNotFree[SelfSlot] != 0 );
        return _array[SelfSlot];
    }

    static bool IsValidIndex(int idx)
    {
        return UINT(idx) < UINT(AllocSize);
    }

    static PBidApi At(HANDLE hID)
    {
        int idx = (int)(INT_PTR)hID;
        return IsValidIndex(idx) ? _active[idx] : &_stub;
    }

    static PModuleObject AtPtr(HANDLE* pID);
    static void RudeDisconnect( HANDLE* pID, DWORD* pCtlFlags );

 private:
    PModuleObject   _array [AllocSize];
    bool            _bAllocated;

    static volatile LONG    _bNotFree [AllocSize];
    static volatile PBidApi _active   [AllocSize];
    static          IBidApi _stub;

    static void deactivate(int idx)
    {
        DASSERT( IsValidIndex(idx) );
        YAWL_InterlockedExchangePointer( (volatile PVOID*) &_active[idx], &_stub );
    }
    static bool isDeactivated(int idx)
    {
        DASSERT( IsValidIndex(idx) );
        return (_active[idx] == &_stub);
    }

    void allocObjects();
    void freeObjects();

    BID_EXTENSION_DECLARE( ModulePool );

}; // ModulePool

extern ModulePool   g_ModulePool;

#define g_Self      (*g_ModulePool.GetSelf())


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "ModulePool.h"                                  //
/////////////////////////////////////////////////////////////////////////////////////////////////
