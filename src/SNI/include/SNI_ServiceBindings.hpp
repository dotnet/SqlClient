//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: SNI_ServiceBindings.hpp
// See SQLDevDash for source code ownership
//
// Purpose: SNI SPN-matching utilities for Service Bindings
//
// Notes:
//	
// @EndHeader@
//****************************************************************************


#ifndef _SNI_SERVICE_BINDINGS_07_15_2009_
#define _SNI_SERVICE_BINDINGS_07_15_2009_

// include sni_spn.hpp to share  the definition of the Service Class
#include "sni_spn.hpp"

class SNI_ServiceBindings
{
public:
	//----------------------------------------------------------------------------
	// NAME: SNI_ServiceBindings::SetHostNamesAndAcceptedSPNs
	//	
	// PURPOSE:
	//		Saves off the input SPN Approved List. If run on a non-clustered instance, also retrieves 
	//		host names, IPv4 and IPv6 addresses, and BackConnectionHostNames for the host machine.
	//
	// PARAMETERS:
	//		WCHAR **pwszAllowedSPNs (optional)
	//			If specified, provides a user-configured list of Accepted SPNs which are to be
	//			explicitly allowed for a Service Bindings check. An array of null-terminated
	//			WCHAR strings.
	//		DWORD dwcAllowedSPNs
	//			Number of strings in the pwszAllowedSPNs array.
	//
	// RETURNS:
	//			On successful initialization, ERROR_SUCCESS.
	//		On failure to initialize, other error code.
	//
	// CONTRACT:
	//		1) Must be called exactly once
	//		
	//		2) Must be called before MatchSPN can be called
	//		
	//		3) Must not be called simultaneously with any other SNI_ServiceBindings API.
	//		
	//		4) When called, SNI_ServiceBindings takes ownership of the supplied SPN Approved List. 
	//		Caller should not free the supplied pointer regardless of success or failure of SetHosNames.
	//
	// NOTES:
	//		1) This function retrieves BackConnectionHostNames from the LSA's registry value. Since that
	//		registry value does not exist by default, if any error occurs while retrieving the 
	//		BackConnectionHostNames, the error is ignored and initialization continues.
	//----------------------------------------------------------------------------
	static DWORD SetHostNamesAndAcceptedSPNs(__in_ecount_opt(dwcAllowedSPNs) WCHAR **pwszAcceptedSPNs, DWORD dwcAcceptedSPNs);

	
	//----------------------------------------------------------------------------
	// NAME: SNI_ServiceBindings::SetClusterAddresses
	//	
	// PURPOSE:
	//		Accepts the cluster Virtual IP addresses in preparation for later matching of 
	//		Service Bindings.
	//
	// PARAMETERS:
	//		ADDRINFOW *paiwClusterAddresses
	//			An ADDRINFOW linked list holding the cluster virtual IP Address(es)
	//
	// RETURNS:
	//			On successful assigning, ERROR_SUCCESS.
	//		On any failure, other error code.
	//
	// CONTRACT:
	//		1) Must be called at least once for a cluster.
	//		
	//		2) Must never be called for a non-cluster. 
	//		
	//		2) On a cluster, must be called before MatchSPN can be called
	//		
	//		4) May be called multiple times simultaneously or sequentially, but must not be called 
	//		simultaneously with any other SNI_ServiceBindings API.
	// NOTES:
	//		1) This function overwrites some but not all of the information collected by SetHostNamesAndAcceptedSPNs.
	//		It DOES overwrite the two IP address lists. It does NOT overwrite the SPN Approved List.
	//----------------------------------------------------------------------------
	static DWORD SetClusterAddresses(__in ADDRINFOW *paiwClusterAddresses);
	
	
	//----------------------------------------------------------------------------
	// NAME: SNI_ServiceBindings::SetClusterNames
	//	
	// PURPOSE:
	//		Accepts the cluster Network Name in preparation for later matching of 
	//		Service Bindings.
	//
	// PARAMETERS:
	//		LPWSTR wszClusterHostName
	//			A null-terminated string containing the Cluster Network Name
	//
	// RETURNS:
	//			On successful assigning, ERROR_SUCCESS.
	//		On any failure, other error code.
	//
	// CONTRACT:
	//		1) Must be called exactly once for a cluster.
	//		
	//		2) Must not be called for a non-cluster. 
	//		
	//		3) On a cluster, must be called before MatchSPN can be called.
	//
	//		4) Must not be called simultaneously with any other SNI_ServiceBindings API.
	// NOTES:
	//		1) Since BackConnectionHostNames applies to the physical node, this function does not
	//		retrieve the BackConnectionHostNames list, after it clears the hostname list.
	//----------------------------------------------------------------------------
	static DWORD SetClusterNames(__in_z LPWSTR wszClusterHostName);
	
	//----------------------------------------------------------------------------
	// NAME: SNI_ServiceBindings::MatchSPN
	//	
	// PURPOSE:
	//		Main entry point for matching a client-supplied target against the information collected by
	//		by SNI_ServiceBindings.
	//
	// PARAMETERS:
	//		LPWSTR wszClientSuppliedSPN
	//			The target name retrieved from the negotiated authentication context. Must not be NULL.
	//
	//		SNIAuthErrStates *pSSPIFailureState
	//			Failure state for failures during SPN matching, intended to be used for error reporting purposes. 
	//			Must not be NULL.
	//		
	// RETURNS:
	//			On successful match, ERROR_SUCCESS.
	//		On mismatch, SEC_E_BAD_BINDINGS.
	//		On other error, appropriate error code (e.g., ERROR_OUTOFMEMORY)
	//
	//----------------------------------------------------------------------------
	static DWORD MatchSPN(__in_z LPWSTR wszClientSuppliedSPN, __out SNIAuthErrStates *pSSPIFailureState);
	
	//----------------------------------------------------------------------------
	// NAME: SNI_ServiceBindings::Release
	//	
	// PURPOSE:
	//		Explicit finalization for the SNI_ServiceBindings class. Cleans up any allocations
	//		and cleans up Winsock, if it was loaded. 
	// CONTRACT:
	//		1) Must not be called simultaneously with any other SNI_ServiceBindings API.
	//		
	//		2) May be called multiple times.
	//		
	//		3) May be called even if SNI_ServiceBindings was never initialized 
	//		(successfully or unsuccessfully).
	//----------------------------------------------------------------------------
	static void  Release();

#ifndef SNIX
	
private:
	static volatile LONG s_lClusterAddressesInitialized;
	static bool s_fClusterHostNamesInitialized;
	
	static bool s_fWSAStarted;
	
	static struct in_addr *s_piaIPv4Address;
	static DWORD   s_dwcIPv4Address;

	static struct in6_addr *s_pi6aIPv6Address;
	static DWORD   s_dwcIPv6Address;

	static LPWSTR *s_pwszHostNames;
	static DWORD  s_dwcHostNames;

	static LPWSTR *s_pwszSPN;
	static DWORD   s_dwcSPN;

	// API descriptions for Helpers are given in the CPP file.
	static DWORD MatchHostOrIPv4Address(__in_z LPWSTR wszHost, __out SNIAuthErrStates *pSSPIFailureState);
	static DWORD MatchIPv6Address(__in_z LPWSTR wszHost, __out SNIAuthErrStates *pSSPIFailureState);

	static DWORD MatchApprovedList(__in_z LPWSTR wszSPN);
	
	static DWORD MatchAgainstNameList(__in_z LPWSTR wszHost);
	static DWORD MatchAgainstIpv4AddressList(__in PSOCKADDR_STORAGE psaIpv4Address);
	static DWORD MatchAgainstIpv6AddressList(__in PSOCKADDR_STORAGE psaIpv6Address);

	static DWORD InitializeWSA();

	static DWORD GetBackConnectionHostNames(__deref_out __nullnullterminated WCHAR **pwszBackConnectionHostNames);
	static DWORD AllocAndSetHostname(COMPUTER_NAME_FORMAT NameType);
	static DWORD RepackSzIntoWsz(__in_z LPCSTR szMbcsString);
	static void ReleaseIPs();
	static void ReleaseNames();

	inline static void BidTraceAddedIPv4Address(__in struct in_addr *psin_addr);
	inline static void BidTraceAddedIPv6Address(__in struct in6_addr *psin6_addr);

	inline static bool IsIn4AddrLoopback(const PSOCKADDR_IN psaiAddress);
	inline static bool IsIn6AddrLoopback(const PSOCKADDR_IN6 psai6Address);

#endif // ifndef SNIX
};

#endif
