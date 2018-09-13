#ifndef _SNI_PCH_
#define _SNI_PCH_


//The following lines should be enabled on SNIX 
//while being commented out on SNAC.
#ifndef _USE_OLD_IOSTREAMS
#define _USE_OLD_IOSTREAMS
#ifdef  _DEBUG
#pragma comment(lib,"msvcirtd")
#else   /* _DEBUG */
#pragma comment(lib,"msvcirt")
#endif  /* _DEBUG */
#endif  /* _USE_OLD_IOSTREAMS */


//	Define _WINSOCKAPI_ to keep windows.h from including winsock.h,
//	whose declarations would be redefined in winsock2.h
//
#if _WIN32_WINNT < 0x0500
#undef _WIN32_WINNT
#define _WIN32_WINNT 0x0500
#endif

#ifndef SNIX
// Prevent including wspiapi.h. Should use locwspiapi.h instead.
// locwspiapi.h is yukon specific header file.
#define _WSPIAPI_H_
#endif

#define _WINSOCKAPI_
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <mswsock.h>

#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <limits.h>

//The following one line will cause compilation error in SNAC!!!
//So it should be enabled on SNIX while commenting out on SNAC.

#include "sqlhdr.h" 


#ifdef SNIX
#include <objbase.h>
#include <assert.h>

//
// tchar.h is required to be included before strsafe.
// Otherwise, compiler complains.

#include <tchar.h>
#include <strsafe.h>
#else

#define Assert assert

#include <locwspiapi.h>
#include <sqlloc.h>

#include <assert.h>
#include "msdart.h"
#include "mpcs.h"
#include "asoshost.h"
#include <intsafe.h>


#define WCSSAFE_ERROR_ON_DEPRECATED_API
#undef NUMELEM // to avoid re-definition inside wcssafe.h
#include <wcssafe.h>
#include <wcssafe.inl>
#endif

#include "sni.hpp"
#include "sni_common.hpp"


#include "client_sos.hpp"

#include "sni_error.hpp"

#include "open.hpp"

#include "sni_io.hpp"
#include "sni_prov.hpp"

#include "util.hpp"

#include "reg.hpp"

#endif
