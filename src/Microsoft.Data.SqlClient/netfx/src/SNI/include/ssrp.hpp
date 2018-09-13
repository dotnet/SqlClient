#ifndef _SSRP_HPP_
#define _SSRP_HPP_

namespace SSRP 
{
	DWORD SsrpGetInfo( __in LPWSTR wszServer, __in LPWSTR wszInstance, __inout ProtList *pProtocolList );
	DWORD SsrpEnumCore(LPSTR , char * , DWORD *, bool );
	bool GetAdminPort( const WCHAR *wszServer, const WCHAR *wszInstance, __inout USHORT *pPort );
};

#endif
