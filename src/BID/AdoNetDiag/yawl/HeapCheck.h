/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       HeapCheck.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Debug-Only service that automatically runs CRT heap validation at app exit time.
//
//  Comments:   Special allocation with __declspec(alloc) makes the heap to be checked
//              after ALL legal cleanups, including static destructors.
//
//              To be included after EntryPoint function, at the end of main src file.
//
//              // mainfile.{c|cpp}
//              void __cdecl main() {   // whatever entrypoint
//                  ....
//              }
//
//              #include  "yawl/HeapCheck.h"
//              // End Of File
//                                                          (Reduced version for Bid2Etw28)
//              File Created : 17-Jul-2003
//              Last Modified: 10-Sep-2003
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __HEAPCHECK_H__ /////////////////////////////////////////////////////////////////////////
#define __HEAPCHECK_H__

#ifdef _DEBUG

#ifndef _NOLIST_HDRS
#pragma message("  HeapCheck-28.h")
#endif

#ifndef _YAWL_HEAPCHECK_OUTPUT_A
  #ifndef _BID_DECLARED
    #error Built-In Diagnostics (BID) Api is not included.
  #endif
  #define _YAWL_HEAPCHECK_OUTPUT_A   BidTrace0A
#endif

#include <crtdbg.h>

static int __cdecl _yawlHeapAutoCheck(void) {
    if( _CrtDumpMemoryLeaks() ){
        _YAWL_HEAPCHECK_OUTPUT_A( "\n *** MEMORY LEAKS! ***\n\n" );
    }
    return 0;
}
static int __cdecl _yawlSetHeapAutoCheck(void) {
    _onexit(_yawlHeapAutoCheck);
    return 0;
}

#ifndef _BID_CRTSECT_DECLARED
  #ifdef _WIN64
    #pragma section(".CRT$XCBid",long,read)
  #elif defined(_ARM_)
    #pragma section(".CRT$XCBid",read)
  #else
    #pragma data_seg(".CRT$XCBid")
    #pragma data_seg()
  #endif
  #define _BID_CRTSECT_DECLARED
#endif

__declspec(allocate(".CRT$XCBid"))
static int (__cdecl* _yawlHeapAutoCheckHook)(void) = _yawlSetHeapAutoCheck;


#endif // _DEBUG

#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                 End of file "HeapCheck.h"                                   //
/////////////////////////////////////////////////////////////////////////////////////////////////
