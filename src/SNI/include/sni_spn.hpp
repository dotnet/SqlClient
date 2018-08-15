//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: sni_spn.hpp
// @Owner: nantu, petergv
// @Test: milu
//
// <owner current="true" primary="true">nantu</owner>
// <owner current="true" primary="false">petergv</owner>
//
// Purpose: SNI Spn utilities
//
// Notes:
//	
// @EndHeader@
//****************************************************************************
#ifndef _SNI_SPN_07_15_2009_
#define _SNI_SPN_07_15_2009_

#include "sni.hpp"

#include <winbase.h>
#include <Ntdsapi.h>
#include <dsgetdc.h>
#include <lm.h>
#define SECURITY_WIN32
#include <Security.h>

#include "sni_sspi.hpp"

#define SQL_SERVICECLASS (LPCTSTR)_T("MSSQLSvc")
#define SQL_SERVICECLASS_W L"MSSQLSVC"

// Spn function pointers
typedef DWORD (__stdcall * DSMAKESPN_FN)(             
										LPCTSTR ServiceClass, 
										LPCTSTR ServiceName, 
										LPCTSTR InstanceName, 
										USHORT InstancePort, 
										LPCTSTR Referrer, 
										DWORD* pcSpnLength, 
										LPTSTR pszSpn
										);

typedef DWORD (__stdcall * DSBIND_FN)( TCHAR * DomainControllerAddress,   // in, optional
                                     TCHAR * DnsDomainName,             // in, optional
                                     HANDLE * phDS);
typedef DWORD (__stdcall * DSUNBIND_FN)( HANDLE * phDS);
typedef DWORD (__stdcall * DSGETSPN_FN)( DS_SPN_NAME_TYPE ServiceType,
                                      LPCTSTR ServiceClass,
                                      LPCTSTR ServiceName,
                                      USHORT InstancePort,
                                      USHORT cInstanceNames,
                                      LPCTSTR *pInstanceNames,
                                      const USHORT *pInstancePorts,
                                      DWORD *pcSpn,
                                      LPTSTR **prpszSpn);
typedef VOID (__stdcall * DSFREESPNARRAY_FN)( DWORD cSpn,
                                           LPTSTR *rpszSpn);
typedef DWORD (__stdcall * DSWRITEACCOUNTSPN_FN)( HANDLE hDS,
                                               DS_SPN_WRITE_OP Operation,
                                               LPCTSTR pszAccount,
                                               DWORD cSpn,
                                               LPCTSTR *rpszSpn);
typedef VOID (__stdcall * DSFREENAMERESULT_FN)( DS_NAME_RESULT *pResult);
typedef DWORD (__stdcall * DSCRACKNAMES_FN)( HANDLE hDS,
                                          DS_NAME_FLAGS flags,
                                          DS_NAME_FORMAT formatOffered,
                                          DS_NAME_FORMAT formatDesired,
                                          DWORD cNames,
                                          LPTSTR *rpNames,
                                          PDS_NAME_RESULT *ppResult);
typedef DWORD (__stdcall * DSGETDCNAME_FN)( LPCTSTR ComputerName,
                                            LPCTSTR DomainName,
                                            GUID *DomainGuid,
                                            LPCTSTR SiteName,
                                            ULONG Flags,
                                            PDOMAIN_CONTROLLER_INFO *DomainControllerInfo);

typedef NET_API_STATUS (__stdcall * NETAPIBUFFERFREE_FN)( LPVOID Buffer );

typedef BOOLEAN (__stdcall * GETCOMPUTERNAMEEX_FN)( COMPUTER_NAME_FORMAT NameType,  
                                                   LPTSTR lpBuffer, 
                                                   LPDWORD nSize);

typedef BOOLEAN (__stdcall * GETCOMPUTEROBJECTNAME_FN)( EXTENDED_NAME_FORMAT  NameFormat,
                                                   LPTSTR lpNameBuffer,
                                                   PULONG nSize );

typedef BOOLEAN (__stdcall * GETUSERNAMEEX_FN)( EXTENDED_NAME_FORMAT NameFormat,  
                                               LPTSTR lpNameBuffer,
                                               PULONG nSize);

typedef struct _DsFunctionTable
{
	DSMAKESPN_FN DsMakeSpn;
	DSBIND_FN DsBind;
	DSUNBIND_FN DsUnBind;
	DSGETSPN_FN DsGetSpn;
	DSFREESPNARRAY_FN DsFreeSpnArray;
	DSWRITEACCOUNTSPN_FN DsWriteAccountSpn;
	DSFREENAMERESULT_FN DsFreeNameResult;
	DSCRACKNAMES_FN DsCrackNames;
	DSGETDCNAME_FN DsGetDcName;
	NETAPIBUFFERFREE_FN NetApiBufferFree;
	GETCOMPUTERNAMEEX_FN GetComputerNameEx;
	GETCOMPUTEROBJECTNAME_FN GetComputerObjectName;
	GETUSERNAMEEX_FN GetUserNameEx;
} DsFunctionTable;


//	Unique states for the error messages 26037 and 26038 (failed to
//	add/remove SPN).  
//	Generally, do not modify or remove values, and only add new values at 
//	the end since there may be references to them in BOL and external
//	blogs.  
//
enum EAddRemoveSpnFailedState 
{
	x_earsSuccess = 0,		//	Never log this one
	x_earsGetIpAllPortAndInstanceName, 
	x_earsDsGetSpn, 
	x_earsDsGetDcName, 
	x_earsDsBind, 
	x_earsAllocUserName1, 
	x_earsAllocUserName2, 
	x_earsGetUserName1, 
	x_earsGetUserName2, 
	x_earsGetUserNameEx1, 
	x_earsGetUserNameEx2,	// 10
	x_earsDsCrackNames,
	x_earsCopyName, 
	x_earsDsCrackNamesInvalidData, 
	x_earsGetComputerObjectName, 
	x_earsDsWriteAccountSpn,
	x_earsGetIpAllPort,
	x_earsGetInstanceName,
	x_earsDsGetSpnPort,
	x_earsDsGetSpnInstanceName
};


class SNI_Spn
{
public:
	
	static DWORD MakeSpn(__in LPTSTR szServer, __in LPTSTR szInstName,  USHORT usPort, LPTSTR szSpn, __in DWORD cszSpn);

	static DWORD SpnInit();

	static void SpnTerminate();

	static DWORD AddRemoveSpn(__in LPCTSTR pszInstanceSPN, DWORD dwPortNum, BOOL fAdd, DWORD * pdwState);
	
};

#endif
