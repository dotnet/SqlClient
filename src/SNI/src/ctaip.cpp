//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: ctaip.cpp
// @Owner: zlin
//
// Purpose: SNI CTAIP Provider
//
// Notes:
//          
//****************************************************************************
#include "snipch.hpp"
#include "ctaip.hpp"
#include <in6addr.h>

CPL_ASSERT(sizeof(CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead) == 4);

// CTAIP provider's offset data length is 2
//
const USHORT CrTrAdditionalInfoProtocol::CTAIPPacket::sm_cbPrependData = 2;

// Set token length for version 1, based on version 0
//
BEGIN_SET_CTAIPTOKEN_LENGTH(1, 0)
	SET_CTAIPTOKENTYPE_LENGTH(CTAIPPacketToken_IPv4, 4)
	SET_CTAIPTOKENTYPE_LENGTH(CTAIPPacketToken_IPv6, 16)
	SET_CTAIPTOKENTYPE_LENGTH(CTAIPPacketToken_FromSecurityProxy, 0)
	// -- New Token Type length can be defined like following
	// SET_CTAIPTOKENTYPE_LENGTH(CTAIPPacketToken_PHONENUM, 15)
END_SET_CTAIPTOKEN_LENGTH()

BEGIN_ENABLE_CTAIP_VERSION()
	ENABLE_CTAIP_VERSION(1)
END_ENABLE_CTAIP_VERSION()

// -- For breaking change i.e. token size, we should have a new major version 2 based on version 1
// BEGIN_SET_CTAIPTOKEN_LENGTH(2, 1)
//	 SET_CTAIPTOKENTYPE_LENGTH(CTAIPPacketToken_IPv6, 18)
// END_SET_CTAIPTOKEN_LENGTH()
//
// BEGIN_ENABLE_CTAIP_VERSION()
//	 ENABLE_CTAIP_VERSION(1)
//	 ENABLE_CTAIP_VERSION(2)
// END_ENABLE_CTAIP_VERSION()
//
// -- and adding following in the header file
// DECLARE_CTAIP_VERSION(2);

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::CrTrAdditionalInfoProtocol
//
// PURPOSE:
//    Constructor.
//
// RETURNS: None
//
// NOTES: 
//     Client address is either extracted from SNI_Conn or explicitly passed.
//
CrTrAdditionalInfoProtocol::CrTrAdditionalInfoProtocol(SNI_Conn *pConn):
	SNI_Provider( pConn )
{
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::~CrTrAdditionalInfoProtocol
//
// PURPOSE:
//    Dtor.
//
// RETURNS: None
//
// NOTES:
//
CrTrAdditionalInfoProtocol::~CrTrAdditionalInfoProtocol()
{
	DeleteCriticalSection( &m_CS );
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::FInit
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::FInit()
{
    return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::InitX
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::InitX(__in LPVOID pInfo)
{
    CPL_ASSERT(sizeof(m_addressDataBuf) >= sizeof(in6_addr));
    CPL_ASSERT(sizeof(m_addressDataBuf) >= sizeof(IN_ADDR));

    SNICTAIPProviderInfo*  pCtaipInfo  = (SNICTAIPProviderInfo*)pInfo;
        
	DWORD dwErr = SNICritSec::Initialize( &m_CS );

	if (ERROR_SUCCESS != dwErr)
	{
		m_State = error_init;
	}
	else
	{
		m_Prot = CTAIP_PROV;
		m_State = CTAIP_state::running;
		m_addressTokenType = CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_invalid;
        m_securityProxyTokenType = CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_invalid;

		// If client SNI_Conn obj is passed in, get its IP addr or copy from explicit bytes
		//
		if (pCtaipInfo != NULL)
		{
		    if (pCtaipInfo->pConn != NULL)
            {      
    			dwErr = InitClientAddress(pCtaipInfo->pConn, m_addressTokenType, m_addressDataBuf, sizeof(m_addressDataBuf));
            }
            else
            {
                dwErr = InitClientAddress(pCtaipInfo->prgbAddress, pCtaipInfo->cbAddress, m_addressTokenType, m_addressDataBuf, sizeof(m_addressDataBuf));
            }

            if (ERROR_SUCCESS != dwErr)
			{
				SetState(CTAIP_state::error_init, dwErr);
			}
            else
            {
                if (pCtaipInfo->fFromDataSecurityProxy)
                {
                    m_securityProxyTokenType = CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_FromSecurityProxy;
                }
            }
		}
	}

    return dwErr;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::Close
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::Close(__in DWORD dwCloseFlags)
{
	return m_pNext->Close(dwCloseFlags);
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::Initialize
//
// PURPOSE:
//    Be called when initializing SNI
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::Initialize( PSNI_PROVIDER_INFO pInfo )
{
	pInfo->ProvNum = CTAIP_PROV;
	pInfo->Offset = 2;
	pInfo->fBaseProv = FALSE;
	pInfo->Size = CrTrAdditionalInfoProtocol::CTAIPPacket::sm_cbPrependData /* offset */ + 
		sizeof(BYTE) /* token Type is one byte*/ + sizeof(in_addr6) + sizeof(BYTE) /*security proxy token type */ +
		sizeof(CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead);
	pInfo->fInitialized = TRUE; 

	return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::ReadAsync
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::ReadAsync(__inout SNI_Packet ** ppNewPacket, LPVOID pPacketKey)
{
	if (m_State != running)
	{
		// Already hit error
		return ERROR_FAIL;
	}

	DWORD dwErr = m_pNext->ReadAsync(ppNewPacket, pPacketKey);
	if (dwErr == ERROR_SUCCESS)
	{
		// read done, unwrapping the packet
		//
		dwErr = CTAIPPacket::ProcessSNIPacket(*ppNewPacket, m_pConn, this);
		if (dwErr != ERROR_SUCCESS)
		{
			SetState(error_read, dwErr);
		}
	}
	
	return dwErr;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::ReadDone
//
// PURPOSE:
//    SNI provider interface
//
// NOTES:
//    Not supported
//
DWORD CrTrAdditionalInfoProtocol::ReadDone( __inout SNI_Packet ** ppPacket, 
										   __inout SNI_Packet ** ppLeftOver, 
										   DWORD         dwBytes, 
										   DWORD         dwError )
{
	DWORD dwErr = m_pNext->ReadDone(ppPacket, ppLeftOver, dwBytes, dwError);
	if (dwError == ERROR_SUCCESS)
	{
		// read done, unwrapping the packet
		//
		dwErr = CTAIPPacket::ProcessSNIPacket(*ppPacket, m_pConn, this);
		if (dwErr != ERROR_SUCCESS)
		{
			SetState(error_read, dwErr);
		}
	}

	return dwErr;
}

///----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::ReadSync
//
// PURPOSE:
//    SNI provider interface
//
// NOTES:
//    Not supported
//
DWORD CrTrAdditionalInfoProtocol::ReadSync(__out SNI_Packet ** ppNewPacket, int timeout)
{
	if (m_State != running)
	{
		// Already hit error
		//
		return ERROR_FAIL;
	}

	DWORD dwErr = m_pNext->ReadSync(ppNewPacket, timeout);
	if (dwErr == ERROR_SUCCESS)
	{
		// read done, unwrapping the packet
		//
		dwErr = CTAIPPacket::ProcessSNIPacket(*ppNewPacket, m_pConn, this);
		if (dwErr != ERROR_SUCCESS)
		{
			SetState(error_read, dwErr);
		}
	}
	
	return dwErr;
}

///----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::Release
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: none
//
void CrTrAdditionalInfoProtocol::Release()
{
	m_pNext->Release();
	delete this;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::WriteAsync
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::WriteAsync(SNI_Packet * pPacket, SNI_ProvInfo * pProvInfo)
{
	if ( m_State != running )
	{
		return ERROR_FAIL;
	}

	DWORD dwErr = WritePacketData(pPacket);

	if (dwErr == ERROR_SUCCESS)
	{
		dwErr = m_pNext->WriteAsync(pPacket, pProvInfo);
	}

	return	dwErr;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::WriteDone
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::WriteDone(SNI_Packet ** ppPacket, DWORD dwBytes, DWORD dwError)
{
	return m_pNext->WriteDone(ppPacket, dwBytes, dwError);
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::WriteSync
//
// PURPOSE:
//    SNI provider interface
//
// RETURNS: error code
//
// NOTES: never be called
//
DWORD CrTrAdditionalInfoProtocol::WriteSync(SNI_Packet * pPacket, SNI_ProvInfo * pProvInfo)
{
	if ( m_State != running )
	{
		return ERROR_FAIL;
	}

	DWORD dwErr = WritePacketData(pPacket);

	if (dwErr == ERROR_SUCCESS)
	{
		dwErr = m_pNext->WriteSync(pPacket, pProvInfo);
	}

	return dwErr;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::WritePacketData
//
// PURPOSE:
//    Alters packet to provide CTAIP data.
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::WritePacketData(__in SNI_Packet *pPacket)
{
    DWORD dwErr = ERROR_SUCCESS;
    
	// Prepend 2 byte CTAIP offset and append CTAIP Token stream header
	//
	CTAIPPacket::TokenStreamHead* pPacketHeader = CTAIPPacket::SetTokenStreamHead(pPacket);
	if ( pPacketHeader == NULL)
	{
		// data buf is not big enough
		//
		dwErr =  ERROR_INSUFFICIENT_BUFFER;
	}

	// Appending token if we have client ip address info
	//
	if ( dwErr == ERROR_SUCCESS && m_addressTokenType != CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_invalid)
	{
		dwErr = CTAIPPacket::AppendTokenStream(pPacket, pPacketHeader, m_addressTokenType, m_addressDataBuf, sizeof(m_addressDataBuf));
	}

    // Appending token if we have client ip address info
	//
	if ( dwErr == ERROR_SUCCESS && m_securityProxyTokenType != CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_invalid)
	{
		dwErr = CTAIPPacket::AppendTokenStream(pPacket, pPacketHeader, m_securityProxyTokenType, NULL, 0);
	}

    if (dwErr != ERROR_SUCCESS)
	{
		SetState(error_send, dwErr);
	}

    return dwErr;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::InitClientAddress
//
// PURPOSE:
//    Get ip addr from client SNI_Conn object
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::InitClientAddress(__in SNI_Conn * pSNIConn, __out BYTE& TokenType, __out_bcount(cbOut)PVOID pOut, __in USHORT cbOut)
{
	BYTE pbAddrInfo[sizeof(SOCKADDR_STORAGE)] = { 0 };
	BYTE * sin_pAddr = NULL;
	ULONG cbCopied = 0;

	// Query IP addr from SNI Connection
	//
	DWORD dwErr = SNIGetInfoEx(pSNIConn, SNI_QUERY_CONN_IPADDR, pbAddrInfo, sizeof(pbAddrInfo));
	if (ERROR_SUCCESS != dwErr)
	{
		return ERROR_INVALID_DATA;
	}

	switch( *(short*)pbAddrInfo)
	{
		case AF_INET:
			cbCopied = sizeof(IN_ADDR);
			sin_pAddr = (BYTE*) &(((PSOCKADDR_IN)pbAddrInfo)->sin_addr);
			TokenType = CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv4;
			break;

		case AF_INET6:
			cbCopied = sizeof(in6_addr);
			sin_pAddr = (BYTE*) &(((PSOCKADDR_IN6)pbAddrInfo)->sin6_addr);
			TokenType = CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv6;
			break;

		default:
			return ERROR_INVALID_DATA;
	}
	
	// Check if IP data length is greater than data buf length
	//
	if (cbOut < cbCopied)
	{
		return ERROR_INSUFFICIENT_BUFFER;
	}

	memcpy(pOut, sin_pAddr, cbCopied);

	return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::InitClientAddress
//
// PURPOSE:
//    Get ip addr from explicit address
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::InitClientAddress(__in_bcount(cbAddress) LPCBYTE prgbAddress, __in ULONG cbAddress, __out BYTE& TokenType, __out_bcount(cbOut)PVOID pOut, __in USHORT cbOut)
{
    if (cbOut < cbAddress)
    {
		return ERROR_INSUFFICIENT_BUFFER;
    }

    switch (cbAddress)
	{
		case sizeof(IN_ADDR):
			TokenType = CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv4;
			break;

		case sizeof(in6_addr):
			TokenType = CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv6;
			break;

		default:
			return ERROR_INVALID_DATA;
	}

    memcpy(pOut, prgbAddress, cbAddress);
	return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::SetClientAddress
//
// PURPOSE:
//    Sets client address
//
// RETURNS: DWORD
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::SetClientAddress(__in_bcount(cbAddress) LPCBYTE prgbAddress, __in ULONG cbAddress, __in BYTE bTokenType)
{
    if (cbAddress > sizeof(m_addressDataBuf))
    {
        return ERROR_INSUFFICIENT_BUFFER;
    }

    memcpy(m_addressDataBuf, prgbAddress, cbAddress);
    m_addressTokenType = bTokenType;
    
    return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::SetFromSecurityProxy
//
// PURPOSE:
//    Sets from security proxy bit
//
// RETURNS: DWORD
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::SetFromSecurityProxy(__in BYTE bTokenType)
{
    m_securityProxyTokenType = bTokenType;
    
    return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::GetClientAddressInfo
//
// PURPOSE:
//    Gets client address
//
// RETURNS: DWORD
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::GetClientAddressInfo(__out SNICTAIPAddressInfo* pAddressInfo)
{
    CPL_ASSERT(sizeof(pAddressInfo->rgbAddress) >= sizeof(m_addressDataBuf));
        
    ULONG cbOut;
    switch (m_addressTokenType)
	{
		case CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv4:
			cbOut = sizeof(IN_ADDR);
			break;

		case CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv6:
			cbOut = sizeof(in6_addr);
			break;

		default:
			return ERROR_INVALID_DATA;
			break;
	}

    memcpy(pAddressInfo->rgbAddress, m_addressDataBuf, cbOut);
    pAddressInfo->cbAddress = cbOut;

    pAddressInfo->fFromDataSecurityProxy = (m_securityProxyTokenType != CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_invalid);
    
	return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::SetState
//
// PURPOSE:
//    Report error(when hitting failure)
//
// RETURNS: none
//
// NOTES:
//
void CrTrAdditionalInfoProtocol::SetState(CTAIP_state state, DWORD dwNativeErr)
{
	CAutoSNICritSec a_cs( m_CS, SNI_AUTOCS_ENTER );
	if (m_State != running)
	{
		m_State = state;
	}

	if (dwNativeErr != ERROR_SUCCESS)
	{
		DWORD dwSNIErr = 0;
		switch(dwNativeErr)
		{
			case ERROR_INSUFFICIENT_BUFFER:
				dwSNIErr = SNIE_69;
				break;
			case ERROR_VERSION_PARSE_ERROR:
				dwSNIErr = SNIE_70;
				break;
			case ERROR_BAD_FORMAT:
				dwSNIErr = SNIE_71;
				break;
			case ERROR_PROC_NOT_FOUND:
				dwSNIErr = SNIE_15;
				break;
			case ERROR_INVALID_TOKEN:
				dwSNIErr = SNIE_72;
				break;
			case ERROR_INVALID_DATA:
				dwSNIErr = SNIE_73;
				break;
			case ERROR_REPARSE_ATTRIBUTE_CONFLICT:
				dwSNIErr = SNIE_74;
				break;
			default:
				dwSNIErr = SNIE_75;
				break;
		}

		SNI_SET_LAST_ERROR( CTAIP_PROV, dwSNIErr, dwNativeErr );
	}
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::CTAIPPacket::GetTokenLength
//
// PURPOSE:
//    Get CTAIP token length based on CTAIP version.
//
// RETURNS: length or 0 if not found
//
// NOTES:
//
USHORT CrTrAdditionalInfoProtocol::CTAIPPacket::GetTokenLength(BYTE verMajor, BYTE tokenType)
{
	return CTAIPT_Token_Parser::GetTokenLength(verMajor, tokenType);
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::CTAIPPacket::SetTokenStreamHead
//
// PURPOSE:
//    When wrapping a TDS packet, this function prepends the (2 bytes) offset to the packet
//    and appends the CTAIP token stream header
//
// RETURNS: pointer to the CTAIP header in the packet, or NULL if packet length is invalid
//
// NOTES:
//
CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead * 
CrTrAdditionalInfoProtocol::CTAIPPacket::SetTokenStreamHead(SNI_Packet * pSNIPacket)
{
	DWORD cbBuf;
	BYTE * pBuf;
	TokenStreamHead * pTokenStreamHeader = nullptr;

	// Get data buf address and size, and calculate the CTAIP header position in the packet
	//
	SNIPacketGetData(pSNIPacket, &pBuf, &cbBuf);
	pTokenStreamHeader = (TokenStreamHead *)(pBuf + cbBuf);

	// Set CTAIP offset before the data buf
	//
	USHORT usCTAIPTokenOffset = (USHORT)cbBuf + CrTrAdditionalInfoProtocol::CTAIPPacket::sm_cbPrependData;
	SNIPacketPrependData(pSNIPacket, (BYTE *)&usCTAIPTokenOffset, CrTrAdditionalInfoProtocol::CTAIPPacket::sm_cbPrependData);

	// After appending with the CTAIP header , the new buffer size should be:
	// cbBuf + CTAIP offset (2 byte) + sizeof(TokenStreamHead)
	// If buf size is not big enough, return NULL
	//
	if (SNIPacketGetBufActualSize(pSNIPacket) < cbBuf + CrTrAdditionalInfoProtocol::CTAIPPacket::sm_cbPrependData + sizeof(TokenStreamHead))
	{
		return NULL;
	}

	// Append Token Header
	//
	CTAIPPacket::TokenStreamHead pPacketHeader = {CTAIP_VERSION_MAJOR, CTAIP_VERSION_MINOR, 0};
	SNIPacketAppendData(pSNIPacket, (BYTE*)&pPacketHeader, sizeof(TokenStreamHead)); 

	return pTokenStreamHeader;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::CTAIPPacket::AppendTokenStream
//
// PURPOSE:
//    Append CTAIP Toekn message tp a packet, adjusts the buf size after
//    and increases the length value in the Header
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::CTAIPPacket::AppendTokenStream(
	SNI_Packet * pSNIPacket,
	TokenStreamHead * pTokenHeader,
	BYTE TokenType,
	__in_bcount(cbData) BYTE * pData,
	USHORT cbData)
{
	// Append token length (one byte)
	//
	SNIPacketAppendData(pSNIPacket, (BYTE*)&TokenType, sizeof(TokenType));
	pTokenHeader->m_TokenStreamLength++;

	// Append token message
	//
	USHORT cbToken = CTAIPPacket::GetTokenLength(CTAIP_VERSION_MAJOR, TokenType);
	if (cbData < cbToken)
	{
		return ERROR_INSUFFICIENT_BUFFER;
	}

    if (cbToken > 0)
    {
	    SNIPacketAppendData(pSNIPacket, pData, cbToken);
	    pTokenHeader->m_TokenStreamLength += cbToken;
    }

	return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: ProcessTokenCallback(SNI_Packet * pSNIPacket, BYTE verMajor, BYTE tokenType, const BYTE * pDataBuf)
//
// PURPOSE:
//    Call back function when parsing a packet
//    this is where we get IP address by parsing the packet
//
// RETURNS: error code
//
// NOTES:
//
DWORD __cdecl ProcessTokenCallback(void * lpProv, BYTE verMajor, BYTE tokenType, const BYTE * pDataBuf, USHORT cbData)
{
    CrTrAdditionalInfoProtocol *pProv = (CrTrAdditionalInfoProtocol *)lpProv;

    switch (tokenType)
    {
	    case CrTrAdditionalInfoProtocol::CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv4: 
        case CrTrAdditionalInfoProtocol::CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv6:
		    pProv->SetClientAddress(pDataBuf, cbData, tokenType);
            break;
        case CrTrAdditionalInfoProtocol::CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_FromSecurityProxy:
		    pProv->SetFromSecurityProxy(tokenType);
            break;    
	}

	return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::ProcessSNIPacket
//
// PURPOSE:
//    Unwrapping the packet
//
// RETURNS: error code
//
// NOTES:
//
DWORD CrTrAdditionalInfoProtocol::CTAIPPacket::ProcessSNIPacket(SNI_Packet * pSNIPacket, SNI_Conn * pConn, CrTrAdditionalInfoProtocol *pProv)
{
	DWORD cbBuf;
	BYTE * pBuf;
	TokenStreamHead * pTokenStreamHeader = nullptr;

	// get CTAIP stream header
	//
	SNIPacketGetData(pSNIPacket, &pBuf, &cbBuf);

	// *(USHORT *)pBuf has the offset value to CTAIP data, we need to make sure that cbBuf is big
	// enough to hold TDS data + 2 bytes offset + CTAIP header
	//
	if (( *(USHORT *)pBuf + (DWORD)sizeof(CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead) ) > cbBuf)
	{
		return ERROR_INSUFFICIENT_BUFFER;
	}

	// put CTAIP header to pTokenStreamHeader
	//
	pTokenStreamHeader = (TokenStreamHead *)(pBuf + *(USHORT *)pBuf);

	// check if buf size is big enough for CTAIP packet
	//
	if (( *(USHORT *)pBuf + sizeof(CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead) + (DWORD)pTokenStreamHeader->m_TokenStreamLength) 
			> cbBuf)
	{
		return ERROR_INSUFFICIENT_BUFFER;
	}

	// Read token stream
	//
	DWORD err = CTAIPT_Token_Parser::ProcessTokenStream((BYTE*)pTokenStreamHeader, ProcessTokenCallback, pProv);
	if (err != ERROR_SUCCESS)
	{
		return err;
	}

	// Adjust packet offset and buf size, (removing CTAIP)
	//
	SNIPacketIncrementOffset(pSNIPacket, CrTrAdditionalInfoProtocol::CTAIPPacket::sm_cbPrependData);
	SNIPacketSetBufferSize(pSNIPacket, *(USHORT *)pBuf - CrTrAdditionalInfoProtocol::CTAIPPacket::sm_cbPrependData);

	return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
// NAME: CrTrAdditionalInfoProtocol::ProcessTokenStream
//
// PURPOSE:
//    Parse the token stream
//
// RETURNS: error code
//
// NOTES:
//
DWORD CTAIPT_Token_Parser::ProcessTokenStream(const BYTE * pTokenStreamBuf, PROCESSCTAIPTOKEN_FN fCallback, void * pProv)
{
	Assert(pTokenStreamBuf != nullptr);
	Assert(pProv != nullptr);

	// For conflicted token type checking
	//
	enum TokenTypeMask : BYTE
	{
		mask_IPv4= 0x01,
		mask_IPv6= 0x02,
	};
	BYTE maskTokens = 0;

	const CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead * pTokenStreamHeader = 
		(const CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead *)pTokenStreamBuf;

	// check if version match
	//
	if (pTokenStreamHeader->m_VersionMajor > CTAIP_VERSION_MAJOR)
	{
		return ERROR_VERSION_PARSE_ERROR;
	}

	// total stream length
	//
	USHORT cbTokenStreamLeft = pTokenStreamHeader->m_TokenStreamLength;

	// skip token stream header
	//
	const BYTE *pDataBuf = pTokenStreamBuf + sizeof(CrTrAdditionalInfoProtocol::CTAIPPacket::TokenStreamHead);

	// start reading token stream
	//
	while (cbTokenStreamLeft > 0)
	{
		BYTE tokenType = *pDataBuf++;
		cbTokenStreamLeft--;

		USHORT cbToken = CTAIPT_Token_Parser::GetTokenLength(pTokenStreamHeader->m_VersionMajor, tokenType);
		if (cbToken == 0)
		{
			// Got unknown token or zero length token. If minor version is newer, then it is unknown token.
			//
			if (pTokenStreamHeader->m_VersionMinor > CTAIP_VERSION_MINOR)
            {         
    			// the client side CTAIP version is newer, stop parsing and return ERROR_SUCCESS
    			//
    			return ERROR_SUCCESS;
            }
		}

		// check if token stream length in the header matches with actual stream length
		//
		if (cbToken > cbTokenStreamLeft)
		{
			return ERROR_INVALID_DATA;
		}

		// Mark token type bit for conflicted token types checking
		//
		switch(tokenType)
		{
			case CrTrAdditionalInfoProtocol::CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv4:
				maskTokens |= mask_IPv4;
				break;
			case CrTrAdditionalInfoProtocol::CTAIPPacket::CTAIPTokenType::CTAIPPacketToken_IPv6:
				maskTokens |= mask_IPv6;
				break;
			default:
				// Skip any irrelevant token type.
				//
				break;
		}

		// Check if both IPv4 and IPv4 presented
		//
		if ( (maskTokens & mask_IPv4) && (maskTokens & mask_IPv6) )
		{
			return ERROR_REPARSE_ATTRIBUTE_CONFLICT;
		}

		// let callback function to handle the CTAIP info
		//
		if (fCallback != nullptr)
		{
			DWORD dwErr = (*fCallback)(pProv, pTokenStreamHeader->m_VersionMajor, tokenType, pDataBuf, cbToken);
			if (dwErr != ERROR_SUCCESS)
			{
				return dwErr;
			}
		}

		// adjust length and position to read next token
		//
		cbTokenStreamLeft -= cbToken;
		pDataBuf += cbToken;
	}

	return ERROR_SUCCESS;
}
