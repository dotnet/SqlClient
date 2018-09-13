#ifndef _REG_HPP_
#define _REG_HPP_

DWORD GetProtocolList( __inout ProtList * pProtList, 
							   const WCHAR * wszServer,
							   const WCHAR * wszOriginalServer );

DWORD GetProtocolList( 	ProtList * pProtList, 
							const WCHAR * wszServer,
							const WCHAR * wszOriginalServer,
							TCHAR * pszProt);

DWORD GetProtocolDefaults( 	__out ProtElem * pProtElem,
									const WCHAR * pwszProtocol,
									const WCHAR * wszServer );

DWORD GetUserInstanceDllPath( __out_bcount(cchDllPathSize) LPSTR szDllPath, 
									__in DWORD cchDllPathSize,
									__out DWORD* pErrorState);

namespace LastConnectCache
{
	void Initialize();
	
	void Shutdown();

	BOOL GetEntry( const WCHAR * wszAlias, __out ProtElem * pProtElem );

	void SetEntry( const WCHAR * wszAlias, __in ProtElem * pProtElem );

	void RemoveEntry( const WCHAR * wszAlias );
}

#endif
