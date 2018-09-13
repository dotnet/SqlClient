//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: NLregC.h
// @Owner: petergv, nantu
// @test: milu
//
// <owner current="true" primary="true">petergv</owner>
// <owner current="true" primary="false">nantu</owner>
//
// Purpose: Header File for Registry Manipulation Routines (Client Side)
//
// Notes:
//          
// @EndHeader@
//****************************************************************************

#ifndef _NLregC_h
#define _NLregC_h

#ifdef UNICODE
  #ifndef _UNICODE
    #define _UNICODE
  #endif
#endif

#include "WINDOWS.h"
#include "TCHAR.h"

#define CS_MAX	256

extern "C" 
{

// Header file defining Client Side Network Utility manipulation
// routines.
//

#define CS_FLAG_GENERAL_ENCRYPT		1
#define CS_FLAG_GENERAL_TRUST_CERT	2

// The follow defines describe the names identifying each protocol.  
// 
#define CS_PROTOCOL_SM			TEXT("Sm")
#define CS_PROTOCOL_NP			TEXT("Np")
#define CS_PROTOCOL_TCP			TEXT("Tcp")
#define CS_PROTOCOL_VIA			TEXT("Via")


// The follow defines describe the "Property" index associated with
// each protocol.
//
// NP Properties
//
#define CS_PROP_NP_DEFAULT_PIPE		1

// TCP Properties
//
#define CS_PROP_TCP_DEFAULT_PORT		1
#define CS_PROP_TCP_KEEP_ALIVE			2
#define CS_PROP_TCP_KEEP_ALIVE_INTERVAL	3

// VIA Properties
//
#define CS_PROP_VIA_DEFAULT_PORT	1
#define CS_PROP_VIA_DEFAULT_NIC	2
#define CS_PROP_VIA_VENDOR_DLL	3

// Supported VIA vendors, and their DLLs
//
#define CS_VALUE_VIA_VENDOR_NAME_QLOGIC	TEXT("QLogic")
#define CS_VALUE_VIA_VENDOR_DLL_QLOGIC	"QLVipl.dll"


typedef struct cs_protocol_info
{
	TCHAR szDLLname     [CS_MAX];
	TCHAR szProtocolName[CS_MAX];
	DWORD dwNumberOfFlags;
	DWORD dwNumberOfProperties;

} CS_PROTOCOL_INFO;

typedef struct cs_protocol_property
{
	TCHAR szPropertyName[CS_MAX];
	DWORD dwPropertyType;
	union
	{
		DWORD dwDoubleWordValue;
		TCHAR szStringValue[CS_MAX];
	} PropertyValue;

} CS_PROTOCOL_PROPERTY;

typedef struct cs_alias
{
	TCHAR	szTarget          [CS_MAX];
	TCHAR	szProtocol        [CS_MAX];
	TCHAR	szConnectionString[CS_MAX];
    BOOL    fEncryptionOn;

} CS_ALIAS;

typedef struct cs_dblibinfo
{
	TCHAR szFileName[CS_MAX];
	DWORD dwProductVersionMS;
	DWORD dwProductVersionLS;
	DWORD dwDate;
	DWORD dwSize;
	BOOL  fANSItoOEM;
	BOOL  fUseInternationalSettings;

} CS_DBLIBINFO;

typedef struct cs_netlibinfo
{
	TCHAR szProtocolName[CS_MAX];
	TCHAR szDLLname[CS_MAX];
	DWORD dwProductVersionMS;
	DWORD dwProductVersionLS;
	DWORD dwDate;
	DWORD dwSize;

} CS_NETLIBINFO;

// Enum for Local DB error states
typedef enum 
{
	NO_INSTALLATION,
	INVALID_CONFIG,
	NO_SQLUSERINSTANCEDLL_PATH,
	INVALID_SQLUSERINSTANCEDLL_PATH	
} LocalDBErrorState;

//
// Define Client Network Utility API's
//

// CSgetAliases
//
//	This function gets the Aliases currently defined by the caller.
//
//	Inputs:
//		pszAliasesPresent = [out] A buffer that will receive the list of alias
//							      names present on this system.
//								  Eash name will be seperated by a NULL terminator.
//								  The final value will have two NULL terminators.
//								  If this value is NULL, then this function will
//								  return the required buffer size (in bytes) to
//								  store all present protocol presently supported
//								  on this system (including NULL terminators).
//
//		pdwBufferSize = [in/out] A pointer to a DWORD location that contains
//							     the size (in bytes) of the szAliasesPresent
//							     buffer.  If szAliasesPresent is NULL, then
//							     this function will return the required
//							     size (in bytes) of the buffer necessary
//							     to contain the list of protocols supported.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetAliases( __out_bcount_opt(*pdwBufferSize) __nullnullterminated TCHAR * pszAliasesPresent,
						     __inout DWORD * pdwBufferSize );


// CSgetAlias
//
//	This function gets the Alias currently defined by the caller.
//
//	Inputs:
//		szAliasName = [in] The name of the alias to
//						   retrieve information for.
//
//		pCSalias = [out] A pointer to a pre-allocated area that will receive
//						 property values for the alias.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
LONG __stdcall CSgetAlias( TCHAR    * szAliasName,
						   __out_opt CS_ALIAS * pCSalias );


// CSaddAliases
//
//	This function adds a new alias as defined by the caller.
//
//	Inputs:
//		szAliasName = [in] The name of the alias to add.
//
//		pCSalias    = [in] The properties of the alias to add.
//
//		fOverWrite  = [in] If the alias exists, then overwrite it.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
LONG __stdcall CSaddAlias( __in TCHAR    * szAliasName,
						   __in CS_ALIAS * pCSalias,
						   BOOL       fOverWrite );


// CSdeleteAlias
//
//	This function deletes the alias specified by the caller.
//
//	Inputs:
//		szAliasName = [in] The name of the alias to delete.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
LONG __stdcall CSdeleteAlias( TCHAR * szAliasName );


// CSgetDBLIBinfo
//
//	This function retrieves the information associated with DBLIB
//
//	Inputs:
//		infoDBLIB = [out] A pre-allocated structure that will receive the
//						 DBLIB information.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
LONG __stdcall CSgetDBLIBinfo( __out  CS_DBLIBINFO * infoDBLIB );


// CSsetDBLIBinfo
//
//	This function sets the information associated with DBLIB.  Note that the only
//	items that can be set are the "Automatic ANSI to OEM conversion" flag and
//  the "Use international settings" flag.
//
//	Inputs:
//		infoDBLIB = [in] A structure that contains the DBLIB info to set
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
LONG __stdcall CSsetDBLIBinfo( __in CS_DBLIBINFO * infoDBLIB );


// CSgetNETLIBinfo
//
//	This function retrieves the information associated with Netlib
//
//	Inputs:
//		szProtocol = [in] The protocol to retrive Netlib
//						  information about.  This name can be obtained
//						  by calling GetProtocolsSupported().
//
//		infoNETLIB = [out] A pre-allocated structure that will receive the
//						 Netlib information.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
LONG __stdcall CSgetNETLIBinfo( __in __nullterminated TCHAR		  * szProtocol,
							    __out CS_NETLIBINFO * infoNETLIB );

// CSsetDefaults
//
//	This function set all default values associated with the client Registry
//  SNI9.0 key.
//
//	Inputs:
//		fOverWriteAll = [in] If this value is TRUE, then ALL values will be overwritten
//							 with the default values.  Otherwise, existing values will
//							 remain unchanged.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSsetDefaults( BOOL fOverWriteAll );


// CSgetNumberOfGeneralFlags
//
//	This function retrieves the number of general (global) flags currently supported
//
//	Inputs:
//		pdwNumberOfFlags = [out] A pointer to a DWORD variable that will receive the number
//								 of general flags currently supported on this SQL system.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetNumberOfGeneralFlags( __out_opt DWORD * pdwNumberOfFlags );


// CSgetGeneralFlagProperty
//
//	This function retrieves the property values associated with each general flag
//
//	Inputs:
//		dwFlagIndex = [in] The index of the flag information to retrieve (indices start
//				  	  	   with a value of 1.
//
//		szLabel     = [out] A pointer to a preallocated buffer that will receive the
//				 		   description of the flag being retrieved.
//
//		pdwFlagState = [out] A pointer to a DWORD that will receive the value of the flag.
//				 	         Normally a value of zero means FALSE, whereas a value of 1
//						     means TRUE.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetGeneralFlagProperty( __in DWORD   dwFlagIndex,
										 __out_bcount( CS_MAX * sizeof(TCHAR) ) __nullterminated TCHAR   szLabel[CS_MAX],
										 __out DWORD * pdwFlagState );

// SQL BU 335223: PREfast warning 5457 in SNI.
// The function above is deprecated.
// CSgetGeneralFlagProperty
//
//	This function retrieves the property values associated with each general flag
//
//	Inputs:
//		dwFlagIndex = [in] The index of the flag information to retrieve (indices start
//				  	  	   with a value of 1.
//
//		szLabel     = [out] A pointer to a preallocated buffer that will receive the
//				 		   description of the flag being retrieved.
//		
//		dwcbLabel = [in] Size of the szLabel buffer in bytes. If szLabel is null, this parameter is ignored.
//
//		pdwFlagState = [out] A pointer to a DWORD that will receive the value of the flag.
//				 	         Normally a value of zero means FALSE, whereas a value of 1
//						     means TRUE.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//

LONG __stdcall CSgetGeneralFlagPropertyEx( __in DWORD   dwFlagIndex,
										 __out_bcount(dwcbLabel) __nullterminated TCHAR   szLabel[CS_MAX],
										 __in DWORD dwcbLabel,
										 __out DWORD * pdwFlagState );

// CSsetGeneralFlagProperty
//
//	This function sets the property values associated with each general flag.
//
//	Inputs:
//		FlagIndex = [in] The index of the general flag information to set (indices start
//								  with a value of 1.
//
//		dwFlagState = [in] A DWORD that will contain the value of the general flag.
//							      Normally a value of zero means FALSE, whereas a value of 1
//								  means TRUE.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSsetGeneralFlagProperty( DWORD  dwFlagIndex,
									     DWORD  dwFlagState );


// CSgetProtocolsSupported
//
//	This function retieves the list of protocols supported on the
//  current system.
//
//	Inputs:
//		szProtocolsSupported = [out] A buffer that will receive the list of protocol
//							         names supported on this system.
//								     Eash name will be seperated by a NULL terminator.
//								     The final value will have two NULL terminators.
//								     If this value is NULL, then this function will
//								     return the required buffer size (in bytes) to
//								     store all present protocol presently supported
//								     on this system (including NULL terminators).
//
//		pdwBufferSize = [in/out] A pointer to a DWORD location that contains
//							    the size (in bytes) of the szProtocolNames
//							    buffer.  If szProtocolNames is NULL, then
//							    this function will return the required
//							    size (in bytes) of the buffer necessary
//							    to contain the list of protocols supported.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetProtocolsSupported( __out_bcount_opt(*pdwBufferSize) __nullnullterminated TCHAR * szProtocolsSupported,
									    __inout DWORD * pdwBufferSize );


// CSgetProtocolOrder
//
//	This function retieves the list of protocols currently active on the
//  current system.
//
//	Inputs:
//		szProtocolOrder = [out] A buffer that will receive the list of protocol
//							    names active on this system.
//							    Eash name will be seperated by a NULL terminator.
//							    The final value will have two NULL terminators.
//							    If this value is NULL, then this function will
//							    return the required buffer size (in bytes) to
//							    store all present protocol presently active
//							    on this system (including NULL terminators).
//
//		pdwBufferSize = [in/out] A pointer to a DWORD location that contains
//							    the size (in bytes) of the szProtocolNames
//							    buffer.  If szProtocolNames is NULL, then
//							    this function will return the required
//							    size (in bytes) of the buffer necessary
//							    to contain the list of protocols supported.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetProtocolOrder( __out_bcount_opt(*pdwBufferSize) __nullnullterminated TCHAR * szProtocolOrder,
								   __inout DWORD * pdwBufferSize );


// CSsetProtocolOrder
//
//	This function retieves the list of protocols currently active on the
//  current system.
//
//	Inputs:
//		szProtocolOrder = [out] A buffer that will provide the list of protocol
//							    names active on this system.
//							    Eash name will be seperated by a NULL terminator.
//							    The final value will have two NULL terminators.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSsetProtocolOrder( __in TCHAR * szProtocolOrder );


// CSgetNumberOfProtocolFlags
//
//	This function retrieves the number of protocol flags currently supported
//
//	Inputs:
//		szProtocol = [in] Name of the Protocol.
//
//		pdwNumberOfFlags = [out] A pointer to a DWORD variable that will receive the number
//								 of protocol flags currently supported on this SQL system.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetNumberOfProtocolFlags( TCHAR * szProtocol,
										   __out DWORD * pdwNumberOfFlags );


// CSgetNumberOfProtocolProperties
//
//	This function retrieves the number of protocol flags currently supported
//
//	Inputs:
//		szProtocol = [in] Name of the Protocol.
//
//		pdwNumberOfProperties = [out] A pointer to a DWORD variable that will receive the number
//								      of protocol properties currently supported on this SQL system.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetNumberOfProtocolProperties( TCHAR	* szProtocol,
											    __out DWORD * pdwNumberOfProperties );


// CSgetProtocolInfo
//
//	This function sets the general info for the protocol specified.
//
//	Inputs:
//		szProtocol = [in] Name of the Protocol to set info.
//
//		pProtocolInfo = [out] A pointer to a structure that will contain
//							  the information to retrieve.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetProtocolInfo( TCHAR		       * szProtocol,
								  __out_opt CS_PROTOCOL_INFO * pProtocolInfo );

// CSgetProtocolFlag
//
//	This function retieves the protocol flag info.
//
//	Inputs:
//		szProtocol = [in] Name of the Protocol to set info.
//
//		dwPropertyIndex = [in] Index of property value to retrieve (indices start
//							   at 1).
//
//		szFlagLabel = [out] A pointer to a location that will receive the
//							flag label.
//
//		dwFlagValue = [out] A pointer to a location that will receive the
//							flag value.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetProtocolFlag( __in __nullterminated TCHAR * szProtocol,
								  __in DWORD   dwPropertyIndex,
								  __out_bcount( CS_MAX * sizeof( TCHAR ) ) __nullterminated TCHAR * szFlagLabel,
								  __out_opt DWORD * dwFlagValue );

// SQL BU 335223: PREfast warning 5457 in SNI.
// The function above is deprecated.
//
// CSgetProtocolFlagEx
//
//	This function retieves the protocol flag info.
//
//	Inputs:
//		szProtocol = [in] Name of the Protocol to set info.
//
//		dwPropertyIndex = [in] Index of property value to retrieve (indices start
//							   at 1).
//
//		szFlagLabel = [out] A pointer to a location that will receive the
//							flag label.
//
//		dwcbFlagLabe= [in] Size of szFlagLable buffer in bytes, if szFlagLabel is null, this parameter is ignored.
//
//		dwFlagValue = [out] A pointer to a location that will receive the
//							flag value.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//

LONG __stdcall CSgetProtocolFlagEx(__in __nullterminated TCHAR * szProtocol,
								  __in DWORD   dwPropertyIndex,
								  __out_bcount_opt(dwcbFlagLabel) __nullterminated TCHAR * szFlagLabel,
								  __in_range(0,CS_MAX * sizeof (TCHAR)) DWORD dwcbFlagLabel,
								  __out DWORD * dwFlagValue );

// CSsetProtocolFlagProperty
//
//	This function sets the protocol flag value.
//
//	Inputs:
//		szProtocol = [in] Name of the Protocol to set info.
//
//		dwFlagIndex = [in] Index of property value to set (indices start
//						   at 1).
//
//		dwFlagValue = [out] The flag value.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSsetProtocolFlag( TCHAR * szProtocol,
								  DWORD   dwFlagIndex,
								  DWORD   dwFlagValue );


// CSgetProtocolProperty
//
//	This function retieves the protocol property.
//
//	Inputs:
//		szProtocol  = [in]Name of protocol.
//
//		dwPropertyIndex = [in] Index of property value to retrieve (indices start
//							   at 1).
//
//		pPropertyProperty = [out] A pointer to a structure that will contain
//								  the information retrieved.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetProtocolProperty( __in __nullterminated TCHAR				   * szProtocol,
									  __in DWORD			         dwPropertyIndex,
									  __out CS_PROTOCOL_PROPERTY * pPropertyProperty );


// CSsetProtocolProperty
//
//	This function sets the protocol property.
//
//	Inputs:
//		szProtocol = [in] Name of the Protocol to set info.
//
//		dwPropertyIndex = [in] Index of property value to set (indices start
//							   at 1).
//
//		pPropertyProperty = [in] A pointer to a structure that will contain
//								  the information to set.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSsetProtocolProperty( __in __nullterminated TCHAR		           * szProtocol,
									  __in DWORD				     dwPropertyIndex,
									  __in CS_PROTOCOL_PROPERTY * pPropertyProperty );


// CScreateLastConnectionCache(BOOL fOverwrite)
//
// Inputs:
//		fOverwrite = [in]  When LastConnectionCache exit, set fOverwrite true will overwrite it; otherwise
//		this function will leave the LastConnectionCache intact.
// 
// Returns:
// 	ERROR_SUCCESS The key did not exist and was created. 
//	ERROR_ALREADY_EXISTS The key existed. 
//	Any other is sytem error of RegCreateKeyEx().
//

LONG __stdcall CScreateLastConnectionCache(BOOL fOverwrite);

// CSdeleteLastConnectionCache
//
//	This function deletes LastConnectionCache Entry
//
//	Inputs:
//		none
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG _stdcall CSdeleteLastConnectionCache(void);

// CSdeleteAllCachedValues
//
//	This function deletes all of the currently Cached values.
//
//	Inputs:
//		none
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSdeleteAllCachedValues( void );


// CSgetCachedValueList
//
//	This function gets the list of names of Cache values.
//
//	Inputs:
//		szCacheNameList  = List of Cached entry names in multi-string 
//						   format.
//						   Eash name will be seperated by a NULL terminator.
//						   The final value will have two NULL terminators.
//						   If this value is NULL, then this function will
//						   return a sufficient buffer size (in bytes) to
//						   store all present Cached values names (including 
//						   NULL terminators).
//
//		pdwBufferSize = [in/out] A pointer to a DWORD location that contains
//							     the size (in bytes) of the szCacheNameList
//							     buffer.  If szAliasesPresent is NULL, then
//							     this function will return the required
//							     size (in bytes) of a buffer sufficient
//							     to contain the list cached value names.
//
//		pdwMaxValueLen = [out] If szCacheNameList is NULL, and this is
//							   non-NULL, it will be filled with the the size 
//							   of the longest value among the cached values, 
//							   in bytes. If szCacheNameList is non-NULL, this
//							   parameter is ignored.  
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetCachedValueList( __out_bcount_opt(*pdwBufferSize) __nullnullterminated TCHAR * szCacheNameList,
									 __inout DWORD * pdwBufferSize, 
									 __out_opt DWORD * pdwMaxValueLen ); 


// CSgetCachedValue
//
//	This function gets the Cache value.
//
//	Inputs:
//		szCacheName  = Name of Cached entry.
//		szCacheValue = Value of Cached entry.
//		dwValueBufferSize = size of the buffer for szCacheValue, 
//							in bytes
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSgetCachedValue( TCHAR * szCacheName,
							     __out_bcount(dwCacheValueSize) TCHAR * szCacheValue, 
							     DWORD	 dwCacheValueSize );


// CSsetCachedValue
//
//	This function sets the Cache value.
//
//	Inputs:
//		szCacheName  = Name of Cached entry.
//		szCacheValue = Value of Cached entry.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSsetCachedValue( TCHAR * szCacheName,
							     __in_opt TCHAR * szCacheValue );


// CSdeleteCachedValue
//
//	This function sets the Cache value.
//
//	Inputs:
//		szCacheName  = Name of Cached entry to delte.
//
//	Returns:
//		ERROR_SUCCECS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSdeleteCachedValue( TCHAR * szCachedName );

// CSdeleteHive
//
//	This function recursively deletes the whole client-side SNI hive.  
//
//	Returns:
//		ERROR_SUCCESS if the call succeeds
//		else, error value of failure
//
//
LONG __stdcall CSdeleteHive(); 

} // extern "C"

#pragma region LocalDB_Functions
// CSgetUserInstanceDllPath
//
//   This function retrieves the Dll Path of the latest LocalDB instance installed
//
//   Inputs:
//		pszDllPath = Pointer to a buffer that receives the dll path.
//		cchDllPathSize = The size of buffer pszDllPath
//		pErrorState = Pointer to ErrorState
//
//   Returns:
//		ERROR_SUCCESS if the call succeeds along with appropriate pszDllPath
//		else, error value of failure, corresponding error state 
//		and pszDllPath=NULL 
//
LONG _stdcall CSgetUserInstanceDllPath(__out_bcount(cchDllPathSize) LPSTR szDllPath, 
											__in DWORD cchDllPathSize,  
											__out LocalDBErrorState* pErrorState);

#pragma endregion LocalDB_Functions

#endif // _NLregC_h
