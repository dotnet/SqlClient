#ifndef __STDAFX_H__
#define __STDAFX_H__
#ifndef _NOLIST_HDRS
#pragma message("  stdafx.h")
#endif

#ifndef _WINDOWS
#define _WINDOWS
#endif
#undef  _CONSOLE

#ifndef _UNICODE
#define _UNICODE
#endif

#ifndef _YAWL_ENFORCE_SECURE_ZERO_MEMORY
#define _YAWL_ENFORCE_SECURE_ZERO_MEMORY
#endif

#define _BID_UNICODE_LOADER


#include "yawl/BaseRTL.h"
#include "yawl/CStr.h"
#include "yawl/Hashing.h"

//
// Comment out for development cycle
//
#include "AdoNetDiag.h"

#endif
