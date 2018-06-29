/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       AdoNetDiag.cpp
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Built-In Diagnostics (BID) adapter to ETW. Text Streaming Version.
//              Main file.
//  Comments:
//              File Created : 21-Jul-2003
//              Last Modified: 14-Mar-2004
//
//              <owner current="true" primary="true">kisakov</owner>
//
/////////////////////////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "AdoNetDiag.h"

//
//  BID API for self-diagnostics.
//  Configure 'HKLM\Software\Microsoft\BidInterface\SelfDiag\AdoNetDiag.dll' to enable self-diag.
//  Rules are the same as for the primary key 'HKLM\Software\Microsoft\BidInterface\Loader'.
//
#include "BidImplApi_ldr.h"

#include "BID_SRCFILE.h"
          BID_SRCFILE;

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  SEH Guards
//
#define SEH_WRAPPER_(hID, MethodCall, retValAtXept)         \
        __try                                               \
        {                                                   \
            return g_ModulePool.At(hID)->MethodCall;        \
        }                                                   \
        __except( YAWL_EXCEPTION_EXECUTE_HANDLER )          \
        {                                                   \
            g_HealthMeter.IncrementExceptionCounter();      \
            return retValAtXept;                            \
        }


#define SEH_WRAPPER_RETURN_BOOL(hID,MethodCall)             SEH_WRAPPER_(hID,MethodCall,TRUE)
#define SEH_WRAPPER_RETURN_INT(hID,MethodCall)              SEH_WRAPPER_(hID,MethodCall,0)
#define SEH_WRAPPER_RETURN_INT_PTR(hID,MethodCall)          SEH_WRAPPER_(hID,MethodCall,0)


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                        EXPORTED API                                         //
/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Plain Text String
//
BOOL WINAPI DllBidPutStrA( HANDLE hID, UINT_PTR src, UINT_PTR info, PCSTR str )
{
    SEH_WRAPPER_RETURN_BOOL( hID, PutStrA(src, info, str) );
}

BOOL WINAPI DllBidPutStrW( HANDLE hID, UINT_PTR src, UINT_PTR info, PCWSTR str )
{
    SEH_WRAPPER_RETURN_BOOL( hID, PutStrW(src, info, str) );
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Main Tracing Facility
//
BOOL WINAPI DllBidTraceVA( HANDLE hID, UINT_PTR src, UINT_PTR info, PCSTR fmt, va_list argptr )
{
    SEH_WRAPPER_RETURN_BOOL( hID, TraceVA(src, info, fmt, argptr) );
}

BOOL WINAPI DllBidTraceVW( HANDLE hID, UINT_PTR src, UINT_PTR info, PCWSTR fmt, va_list argptr )
{
    SEH_WRAPPER_RETURN_BOOL( hID, TraceVW(src, info, fmt, argptr) );
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Scope Tracking
//
BOOL WINAPI DllBidScopeEnterVA( HANDLE hID, UINT_PTR src, UINT_PTR info, HANDLE* pHScp,
                                PCSTR stf, va_list va )
{
    SEH_WRAPPER_RETURN_BOOL( hID, ScopeEnterVA(src, info, pHScp, stf, va) );
}

BOOL WINAPI DllBidScopeEnterVW( HANDLE hID, UINT_PTR src, UINT_PTR info, HANDLE* pHScp,
                                PCWSTR stf, va_list va )
{
    SEH_WRAPPER_RETURN_BOOL( hID, ScopeEnterVW(src, info, pHScp, stf, va) );
}

BOOL WINAPI DllBidScopeLeave( HANDLE hID, UINT_PTR src, UINT_PTR info, HANDLE* pHScp )
{
    SEH_WRAPPER_RETURN_BOOL( hID, ScopeLeave(src, info, pHScp) );
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  Output Control
//
BOOL WINAPI DllBidEnabledA( HANDLE hID, UINT_PTR src, UINT_PTR info, PCSTR tcs )
{
    SEH_WRAPPER_RETURN_BOOL( hID, EnabledA(src, info, tcs) );
}

BOOL WINAPI DllBidEnabledW( HANDLE hID, UINT_PTR src, UINT_PTR info, PCWSTR tcs )
{
    SEH_WRAPPER_RETURN_BOOL( hID, EnabledW(src, info, tcs) );
}

int WINAPI DllBidIndent( HANDLE hID, int nIndent )
{
    SEH_WRAPPER_RETURN_INT( hID, Indent(nIndent) );
}

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  PERFORMANCE / RESOURCE MONITORING
//
INT_PTR WINAPI DllBidSnap( HANDLE hID, INT_PTR evtID, INT_PTR arg1, INT_PTR arg2 )
{
    //
    //  TODO: Custom SEH wrapping to minimize overhead
    //
    SEH_WRAPPER_RETURN_INT_PTR( hID, Snap(evtID, arg1, arg2) );
}

//
//  ASSERTION
//
BOOL WINAPI DllBidAssert( HANDLE hID, UINT_PTR arg, UINT_PTR info )
{
    SEH_WRAPPER_RETURN_BOOL( hID, Assert(arg, info) );
}

//
//  CONTROL CENTRE
//
INT_PTR WINAPI DllBidCtlProc( HANDLE hID, INT_PTR cmdSpaceID, int cmd,
                              INT_PTR a1, INT_PTR a2, INT_PTR a3 )
{
    SEH_WRAPPER_RETURN_INT_PTR( hID, CtlProc(cmdSpaceID, cmd, a1, a2, a3) );
}

INT_PTR WINAPI DllBidTouch( HANDLE hID, UINT_PTR scope, UINT code, INT_PTR arg1, INT_PTR arg2 )
{
    SEH_WRAPPER_RETURN_INT_PTR( hID, Touch(scope, code, arg1, arg2) );
}


/////////////////////////////////////////////////////////////////////////////////////////////////
//                             INITIALIZATION AND DYNAMIC BINDING                              //
/////////////////////////////////////////////////////////////////////////////////////////////////

#define _BID_APIENTRY(name)    pHooks->name = Dll##name;

static bool SetApiHooks( PBIDHOOKS pHooks )
{
    bool bOk = true;

    __try {
        if( pHooks != NULL )
        {
            if( pHooks->SanityCheck != BID_SANITY_CHECK )
            {
                BidTrace1(BID_TAG1("ERR") _T("Invalid SanityCheck: %08X\n"), pHooks->SanityCheck);
                bOk = false;
            }
            else
            {   //
                //  The statement below puts the addresses of all exportable functions
                //  to (*pHooks). Uses _BID_APIENTRY macro defined above.
                //
                BID_LIST_API_ENTRIES
            }
        }
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER ){
        bOk = false;
    }
    return bOk;
}

#undef _BID_APIENTRY


static bool ValidHandleHolder( HANDLE* pID )
{
    //
    //  Adapter will accept new connection only if client's module handle is properly
    //  initialized before connection. This will filter out multiple initialization.
    //
    HANDLE  hInitialValue;
    bool    bOk;

    __try {
        hInitialValue = *pID;
    }
    __except( YAWL_EXCEPTION_EXECUTE_HANDLER ){
        hInitialValue = (HANDLE)(-2);
    }
    bOk = (hInitialValue == BID_NOHANDLE);

    BidTraceU2( BidIsOn(BID_APIGROUP_TRACE) && !bOk,
                BID_TAG1("RET") _T("false  %p{HANDLE*}  %p\n"), pID, hInitialValue );
    return bOk;
}

DASSERT_COMPILER( BID_NOHANDLE != (HANDLE)(-2) );


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  BidEntryPoint
//
BOOL WINAPI DllBidEntryPoint(HANDLE* pID, int bInitAndVer, PCSTR sIdentity,
                             DWORD cfgBits, DWORD* pCtlFlags, BID_CTLCALLBACK ctlProc,
                             PBIDEXTINFO pExtInfo, PBIDHOOKS pHooks, PBIDSECTHDR pHdr )
{
    bool bOk = true;

    BidScopeAuto1(BID_TAG _T("bInitAndVer:%d"), bInitAndVer);
    BidTrace8(
            BID_TAG1("ARGS")
            _T("%p{HANDLE*}  sIdentity:\"%hs\"  cfgBits:%08X  pCtlFlags:%p{DWORD*}  ")
            _T("%p{BID_CTLCALLBACK}  %p{PBIDEXTINFO}  %p{PBIDHOOKS}  %p{PBIDSECTHDR}\n")
            ,
            pID, sIdentity ? sIdentity : "<BadPtr>",
            cfgBits, pCtlFlags, ctlProc, pExtInfo, pHooks, pHdr );


    if( bInitAndVer > 0 && bInitAndVer <= BID_VER )
    {
        //
        //  BidLoad() was called.
        //  Initialization for every dll with BID interface is implemented here.
        //
        if( bInitAndVer < BID_VER )
        {
            //
            //  Adjust the implementation in order to support the client component that uses
            //  previous version of the interface. If not, the connection must be declined.
            //
            bOk = false;
        }
        else if( !ValidHandleHolder(pID) )
        {
            //
            //  Client's placeholder is not properly initialized.
            //  Possibly recursive connection request; should be rejected.
            //
            bOk = false;
        }
        else // bInitAndVer == BID_VER
        {
            PModuleObject   pModule = NULL;
            BindingContract binding;

            //
            //  Extract initial BID information from client module
            //  and see if this module can be connected.
            //
            binding.Init(bInitAndVer, sIdentity, cfgBits, ctlProc, pExtInfo, pHdr);
            bOk = binding.Approved();

            if( bOk )
            {
                //
                //  Binding contract approved; allocate ModuleObject
                //
                bOk = g_ModulePool.Allocate(pModule);
            }
            if( bOk )
            {
                //
                //  Extract remaining BID information from the module and fill ModuleObject
                //
                bOk = pModule->Init(binding, pCtlFlags, ctlProc, pHooks);

                //
                //  Enable connection
                //
                if( bOk ){
                    bOk = SetApiHooks(pHooks);
                }
                if( bOk ){
                    bOk = g_ModulePool.Activate(pModule, pID, pCtlFlags);
                }

                if( !bOk ){
                    //
                    //  If something went wrong, revert allocation
                    //
                    g_ModulePool.Revert(pModule, pID, pCtlFlags);
                }
                else {
                    //
                    //  Successfully connected
                    //
                    pModule->ReportConnection();

                    BidTraceU1( BidIsOn(BID_APIGROUP_RSRC),
                                BID_TAG1("RSRC") _T("%p{ModulePool}\n"), &g_ModulePool );
                }
            }

            if( !bOk ){
                ModuleObject::ReportRejection(binding);
            }

        } // if bInitAndVer == BID_VER
    }
    else if( bInitAndVer == 0 )
    {
        //
        //  BidUnload() was called.
        //  This represents successfull de-initialization processed by the client dll.
        //
        bOk = g_ModulePool.Release( pID );
        if( !bOk ){
            g_ModulePool.RudeDisconnect( pID, pCtlFlags );
        }
    }
    else
    {   //
        //  (bInitAndVer > BID_VER) means that client uses the newer version of the interface
        //  and the implementation doesn't support it.
        //  (bInitAndVer < 0) means some unknown (generic) error at initialization, so the
        //  subsystem is not supposed to be connected anyway.
        //
        bOk = false;
    }

    BidTrace1( BID_TAG1("RET") _T("%d{BOOL}\n"), bOk );
    return (BOOL)bOk;

} // DllBidEntryPoint


/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ServiceMessageRedirector hooks up BidxMessage API to the main output stream.
//  To see such output, enable the adapter itself as just yet another ETW provider.
//
class ServiceMessageRedirector : public ServiceMessage
{
 public:
    ServiceMessageRedirector()                                         { _bInitialized = false; }
    ~ServiceMessageRedirector()                                   { if( _bInitialized ) Done(); }

    void Init()                         { setActiveImplementation(this); _bInitialized =  true; }
    void Done()                         { resetDefaultImplementation();  _bInitialized = false; }

 protected:
    virtual void putMsg( UINT_PTR src, UINT_PTR info, PCTSTR fmt, va_list argptr, bool& bOk )
    {
        if( !g_Self.TraceVT(src, info, fmt, argptr) ){
            bOk = false;
        }
    }

    bool _bInitialized;
};

static ServiceMessageRedirector     g_ServiceMessageRedirector;

/////////////////////////////////////////////////////////////////////////////////////////////////

double g_dummyVar;
static void MakeSureFpuSupportIncluded()
{
    g_dummyVar = 1234.5678;
}


struct ModuleScope
{
    static void DoInitialize( HANDLE hModule )
    {
        BidScopeAuto1(BID_TAG _T("%p{HMODULE}"), hModule);

        MakeSureFpuSupportIncluded();
        g_ExecutionContextLocalStorage.Init();
        g_ServiceMessageRedirector.Init();
        g_HealthMeter.Init();
        g_ModulePool.Init( hModule );
    }

    static void DoFinalize()
    {
        BidScopeAuto(BID_TAG);

        g_HealthMeter.Report();
        g_ModulePool.Done();
        g_HealthMeter.Done();
        g_ServiceMessageRedirector.Done();
        g_ExecutionContextLocalStorage.Done();
    }

}; // ModuleScope


/////////////////////////////////////////////////////////////////////////////////////////////////
//                                       DLL ENTRY POINT                                       //
/////////////////////////////////////////////////////////////////////////////////////////////////

BOOL APIENTRY DllMain( HANDLE hModule, DWORD dwReason, LPVOID /*lp*/ )
{
    switch( dwReason )
    {
     case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls((HMODULE)hModule);
        ModuleScope::DoInitialize(hModule);
        break;

     case DLL_PROCESS_DETACH:
        ModuleScope::DoFinalize();
        break;
    }
    return TRUE;

} // DllMain.


#include  "yawl/heapcheck.h"

/////////////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "AdoNetDiag.cpp"                                //
/////////////////////////////////////////////////////////////////////////////////////////////////
