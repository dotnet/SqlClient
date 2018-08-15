//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: sm.hpp
// @Owner: petergv, nantu
// @Test: milu
//
// <owner current="true" primary="true">petergv</owner>
// <owner current="true" primary="false">nantu</owner>
//
// Purpose: SNI Shared Memory Provider
//
// Notes:
//	

// @EndHeader@
//****************************************************************************
#ifndef _SM_HPP_
#define _SM_HPP_

//----------------------------------------------------------------------------
// NAME: Sm
//  
// PURPOSE:
//		Defines the Sm provider class.
//  
// NOTES:
//  
//----------------------------------------------------------------------------
class Sm : public SNI_Provider
{

private:	
	static DWORD OpenNpBasedYukon( __in SNI_CONSUMER_INFO *  pConsumerInfo, 
								   __out SNI_Conn	 		 ** ppConn,
								   __in ProtElem 		 *  pProtElem, 
								   __out SNI_Provider 	 ** ppProv,
								   BOOL	          		fSync ); 

	static DWORD CreateSNIConn( __in SNI_CONSUMER_INFO *  pConsumerInfo, 
								__out SNI_Conn 		  ** ppConn, 
								__in ProtElem 		  *  pProtElem, 
								BOOL 				 fSync ); 

public:

	static DWORD Initialize( PSNI_PROVIDER_INFO pInfo );

	static DWORD Terminate();

	static DWORD OpenWithFallback( __in SNI_CONSUMER_INFO *  pConsumerInfo, 
								   __out SNI_Conn	 	  	 ** ppConn,
								   __in ProtElem 		 *  pProtElem, 
								   __out SNI_Provider 	 ** ppProv,
								   BOOL	    			fSync ); 

	static DWORD LoadInstapiIfNeeded(const __in LPCSTR szSharedPathLocation, const __in LPCSTR szInstapidllname);


	static DWORD IsYukonByInstanceString(__in_opt LPWSTR wszInstance, __out BOOL * isYukon, __out_opt BOOL * pfNew, __out BOOL * pfVersionRetrieved);

	static BOOL IsShilohClustered(LPWSTR wszInstance);
	static BOOL IsClustered(__in LPWSTR wszInstance);

	static DWORD GetThreadSID( __out SID  ** ppSID );

};

#endif
