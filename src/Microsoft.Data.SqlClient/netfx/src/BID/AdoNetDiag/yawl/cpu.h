/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  File:       Cpu.h
//
//  Copyright:  (c) Microsoft Corporation
//
//  Contents:   Processor Architecture Specific Details     (Reduced version for Bid2Etw28)
//
//  Comments:
//              File Created : 10-Aug-2003
//              Last Modified: 10-Aug-2003
//
//              <owner current="true" primary="true">kisakov</owner>
//
#ifndef __CPU_H__ ///////////////////////////////////////////////////////////////////////////////
#define __CPU_H__
#ifndef _NOLIST_HDRS
#pragma message("  Cpu-28.h")
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////
//
//  ReadTimeStampCounter (RDTSC)
//
#define ReadTimeStampCounter_NOT_IMPLEMENTED  (UINT64)0

#if !defined( ReadTimeStampCounter)

  #if defined( _M_IX86 )

    #pragma warning( push )
    #pragma warning( disable : 4035 )   // no return value (eax:edx assumed)

    inline UINT64 ReadTimeStampCounter()
    {
        _asm _emit 0Fh
        _asm _emit 31h
    }

    #pragma warning( pop )

  #elif defined( _M_IA64 )

    extern "C" UINT64 __getReg(int regNum);
    #pragma intrinsic(__getReg)
                                        // INL_REGID_APITC
    #define ReadTimeStampCounter()      __getReg(3116)

  #elif defined( _M_AMD64 )

    UINT64 __rdtsc (VOID);
    #pragma intrinsic(__rdtsc)

    #define ReadTimeStampCounter        __rdtsc

  #else

    #define ReadTimeStampCounter()      ReadTimeStampCounter_NOT_IMPLEMENTED

    #pragma message("NOTE: ReadTimeStampCounter is not implemented for this architecture.")

  #endif

#else

  #pragma message("NOTE: ReadTimeStampCounter defined externally.")

#endif


#endif //////////////////////////////////////////////////////////////////////////////////////////
//                                    End of file "Cpu.h"                                      //
/////////////////////////////////////////////////////////////////////////////////////////////////
