//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: ctaip.hpp
// @Owner: zlin
//
// Purpose:
//     CTAIP provider
// Notes:
//          
//****************************************************************************

#pragma once

#define CTAIP_VERSION_MAJOR 1
#define CTAIP_VERSION_MINOR 2


class CrTrAdditionalInfoProtocol : public SNI_Provider
{
public:
	class CTAIPPacket
	{
		//CTAIPPacket = TokensOffset +  PacketData(i.e. original Login packet)  + TokenStream
		//
	public:
		typedef struct 
		{
			BYTE m_VersionMajor;
			BYTE m_VersionMinor;
			USHORT m_TokenStreamLength; // Data length of the 
		} TokenStreamHead;

		enum CTAIPTokenType: BYTE
		{
			CTAIPPacketToken_IPv4,
			CTAIPPacketToken_IPv6,
			CTAIPPacketToken_FromSecurityProxy,
			CTAIPPacketToken_Maximum,
			CTAIPPacketToken_invalid
		};

	public:
		static const USHORT sm_cbPrependData;

	public:
		static USHORT GetTokenLength(BYTE verMajor, BYTE tokenType);

		static TokenStreamHead * SetTokenStreamHead(__inout SNI_Packet * pSNIPacket);
		static DWORD AppendTokenStream(SNI_Packet * pSNIPacket, TokenStreamHead * pTokenHeader, BYTE TokenType, __in_bcount(cbData) BYTE * pData, __in USHORT cbData);

		static DWORD ProcessSNIPacket(SNI_Packet * pSNIPacket, SNI_Conn * pConn, CrTrAdditionalInfoProtocol *pProv);
	};

public:
	static DWORD Initialize( PSNI_PROVIDER_INFO pInfo );

	DWORD ReadSync(__out SNI_Packet ** ppNewPacket, int timeout);
	DWORD ReadAsync(__inout SNI_Packet ** ppNewPacket, LPVOID pPacketKey);
	DWORD WriteSync(SNI_Packet * pPacket, SNI_ProvInfo * pProvInfo);
	DWORD WriteAsync(SNI_Packet * pPacket, SNI_ProvInfo * pProvInfo);
	DWORD ReadDone(__inout SNI_Packet ** ppPacket, __inout SNI_Packet **ppLeftOver, DWORD dwBytes, DWORD dwError);
	DWORD WriteDone(SNI_Packet ** ppPacket, DWORD dwBytes, DWORD dwError);	
	DWORD Close(__in DWORD dwCloseFlags);
	void Release();

public:
	CrTrAdditionalInfoProtocol(SNI_Conn *pConn);

	DWORD FInit();
    DWORD InitX(__in LPVOID pInfo);

	~CrTrAdditionalInfoProtocol();

    DWORD SetClientAddress(__in_bcount(cbAddress) LPCBYTE prgbAddress, __in ULONG cbAddress, __in BYTE bTokenType);
    DWORD SetFromSecurityProxy(__in BYTE bTokenType);

    DWORD GetClientAddressInfo(__out SNICTAIPAddressInfo* pAddressInfo);

private:
	enum CTAIP_state : WORD
	{
		error_init,
		running,
		error_send,
		error_read
	};

	CTAIP_state m_State;
	
	BYTE m_Err;
	BYTE m_addressTokenType;
	BYTE m_addressDataBuf[16];
    BYTE m_securityProxyTokenType;

	SNICritSec *m_CS;

private:
    DWORD WritePacketData(__in SNI_Packet *pPacket);
	DWORD InitClientAddress(__in SNI_Conn * pSNIConn, __out BYTE& TokenType, __out_bcount(cbOut)PVOID pOut, __in USHORT cbOut);
	DWORD InitClientAddress(__in_bcount(cbAddress) LPCBYTE prgbAddress, __in ULONG cbAddress, __out BYTE& TokenType, __out_bcount(cbOut)PVOID pOut, __in USHORT cbOut);
    
	void SetState(CTAIP_state state, DWORD dwNativeErr);
};

#include "ctaipparser.h"
