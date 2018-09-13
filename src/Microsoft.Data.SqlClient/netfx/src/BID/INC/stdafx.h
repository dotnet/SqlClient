#ifndef __STDAFX_H__
#define __STDAFX_H__
#ifndef _NOLIST_HDRS
#pragma message("  stdafx.h")
#endif

  #if defined( _RELEASE_PDB)
    #define _NO_DEBUG_TRACE
    #define _NO_ASSERT
  #endif

  #ifdef _CFG
    #ifndef _CFG_INCLUDED
      #define  _CFG_INCLUDED
      #pragma  message("    stdafx.h includes _CFG=\"" _CFG "\"")
      #include _CFG
    #endif
  #endif

  #ifdef _STD_PCH
    #ifndef __BASERTL_H__
      #include "yawl/BaseRTL.h"
    #endif
  #endif

#endif
