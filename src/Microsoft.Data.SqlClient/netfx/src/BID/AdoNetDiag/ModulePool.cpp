/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       ModulePool.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Container for Client Module Objects.
//  Comments:
//              File Created : 08-Sep-2003
//              Last Modified: 14-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "ModulePool.h"

#include "BID_SRCFILE.h"
          BID_SRCFILE;

//
//  ModulePool has only one instance
//
ModulePool  g_ModulePool;
IBidApi     ModulePool::_stub(0);
PBidApi     volatile ModulePool::_active   [ModulePool::AllocSize];
LONG        volatile ModulePool::_bNotFree [ModulePool::AllocSize];


void ModulePool::Init( HANDLE hModule )
{
    allocObjects();
    DVERIFY( 0 == YAWL_InterlockedExchange(&_bNotFree[SelfSlot], 1) );
    ModuleObject::InitSelfDescriptior( _array[SelfSlot], hModule );
}

void ModulePool::Done()
{
    if( !_bAllocated ){
        BidTraceU0( BID_ADV, BID_TAG1("MULTICALL|PERF|ADV") _T("\n") );
        return;
    }
    for( int i = FirstSlot; i < AllocSize; i++ )
    {
        deactivate(i);
        _array[i]->Done(true);
    }
    ModuleObject::DoneSelfDescriptior( _array[SelfSlot] );
    freeObjects();
}

bool ModulePool::Allocate( PModuleObject& rpModule )
{
    for( int i = FirstSlot; i < AllocSize; i++ )
    {
        if( 0 == YAWL_InterlockedCompareExchange( &_bNotFree[i], 1, 0 ) )
        {
            DASSERT( _bNotFree[i] == 1 );
            rpModule = _array[i];
            return true;                                        // <<== NORMAL EXIT HERE
        }
    }

    BidTrace1( BID_TAG1("WARN|RES") _T("All %d slots occupied.\n"), ModulePool::CAPACITY );
    BidTraceU1( BID_ADV, BID_TAG1("WARN|ADV") _T("%p{ModulePool}\n"), this );

    rpModule = NULL;
    return false;
}

bool ModulePool::Activate( PModuleObject pModule, HANDLE* pID, DWORD* pCtlFlags )
{
    DASSERT_POINTER( pModule );

    int idx = pModule->IndexID();

    DASSERT( IsValidIndex(idx) );
    DASSERT( _bNotFree[idx] != 0 );
    DASSERT( _array[idx] == pModule );

    YAWL_InterlockedExchangePointer( (volatile PVOID*) &_active[idx], (PBidApi)_array[idx] );

    bool bOk = false;
    __try
    {
        *pID = (HANDLE)(INT_PTR)idx;
        *pCtlFlags = pModule->GetCtrlFlags();
        bOk = true;
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        BidTrace3(  BID_TAG1("CATCH")
                    _T("%p{PModuleObject}  pID: %p{HANDLE*}  pCtlFlags: %p{DWORD*}\n"),
                    pModule, pID, pCtlFlags );
        bOk = false;
    }
    return bOk;
}

bool ModulePool::Release( HANDLE* pID )
{
    bool bOk = true;

    PModuleObject pModule = AtPtr(pID);

    if( ModuleObject::IsValidPtr(pModule) )
    {
        int idx = pModule->IndexID();
        DASSERT( IsValidIndex(idx) );

        deactivate(idx);
        pModule->Done();

        if( 0 == YAWL_InterlockedExchange(&_bNotFree[idx], 0) )
        {
            BidTraceU1( BID_ADV, BID_TAG1("MULTICALL|PERF|ADV") _T("idx:%d\n"), idx );
        }
    }
    else
    {
        bOk = false;
    }
    return bOk;
}

void ModulePool::Revert( PModuleObject pModule, HANDLE* pID, DWORD* pCtlFlags )
{
    if( !Release(pID) )
    {
        int idx = pModule->IndexID();
        DASSERT( IsValidIndex(idx) );
        deactivate(idx);
        pModule->Done();
        YAWL_InterlockedExchange(&_bNotFree[idx], 0);
    }
    RudeDisconnect(pID, pCtlFlags);
}

/////////////////////////////////////////////////////////////////////////////////////////////////

PModuleObject ModulePool::AtPtr(HANDLE* pID)
{
    HANDLE hID;
    __try {
        hID = *pID;
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER ){
        hID = BID_NOHANDLE;
    }
    return (PModuleObject)At(hID);
}

void ModulePool::RudeDisconnect( HANDLE* pID, DWORD* pCtlFlags )
{
    __try
    {
        *pID = BID_NOHANDLE;
        *pCtlFlags = 0;
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER )
    {
        BidxMessage2( _T("%00:DISCONNECT FAILURE pID: %p{HANDLE*}, pCtlFlags: %p{DWORD*}\n"),
                      pID, pCtlFlags );
    }
}

void ModulePool::allocObjects()
{
    if( _bAllocated ) return;

    YAWL_ZeroMemory(&_array, sizeof(_array));
    YAWL_ZeroMemory((PVOID)&_bNotFree, sizeof(_bNotFree));

    int i;
    for( i = 0; i < AllocSize; i++ ){
        deactivate(i);
    }

    _bAllocated = true;

    for( i = 0; i < AllocSize; i++ ){
        YAWL_NEW( _array[i], ModuleObject, (i) );
    }
}

void ModulePool::freeObjects()
{
    if( !_bAllocated ) return;

    for( int i = 0; i < AllocSize; i++ )
    {
        DASSERT( isDeactivated(i) );
        YAWL_InterlockedExchange( &_bNotFree[i], 1 );  // when deleted, it is not "free"
        YAWL_DELETE( _array[i] );
    }

    _bAllocated = false;
}

/////////////////////////////////////////////////////////////////////////////////////////////////

BID_EXTENSION( ModulePool )
{
    BID_EXTENSION_REF(ModulePool, pool);

    int     details = BID_LevelOfDetails();
    int     numOfConnectedModules = 0;

    for( int i = ModulePool::FirstSlot; i < ModulePool::AllocSize; i++ ){
        if( pool._bNotFree[i] != 0 ) numOfConnectedModules++;
    }

    BidWrite2( _T("capacity:%d  connected:%d\n"),
               ModulePool::CAPACITY, numOfConnectedModules );

    if( details >= BID_DETAILS_STD )
    {
        bool bMaxDetails = (details == BID_DETAILS_MAX);

        if( details > BID_DETAILS_STD ){
            BidWrite1( _T("%p{ModuleObject} :(self)\n"), pool._array[ModulePool::SelfSlot] );
        }

        CStr  strBuf;

        for( int i = ModulePool::FirstSlot; i < ModulePool::AllocSize; i++ )
        {
            if( pool. _bNotFree[i] != 0 || bMaxDetails )
            {
                PModuleObject pp = pool._array[i];
                BidWrite2( _T("%p{ModuleObject} %s\n"), pp, pp->ShortDescription(strBuf) );
            }
        }
    }

} // BID_EXTENSION

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                End of file "ModulePool.cpp"                                 //
/////////////////////////////////////////////////////////////////////////////////////////////////
