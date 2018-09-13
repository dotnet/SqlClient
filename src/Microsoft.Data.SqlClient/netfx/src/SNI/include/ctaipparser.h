//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: ctaipparser.hpp
// @Owner: zlin
//
// Purpose:
//     CTAIP Parse
//
// Notes:
//          
//****************************************************************************

#pragma once

#define DECLARE_CTAIP_VERSION(major_version) \
	static USHORT GetTokenLength_##major_version(BYTE tokenType);

#define BEGIN_SET_CTAIPTOKEN_LENGTH(major_version, base_version)  \
	USHORT CTAIPT_Token_Parser::GetTokenLength_##major_version(BYTE tokenType)  \
	{ \
	GETTOKENLENGTH_FN fGetLen = GetTokenLength_##base_version;   \
		switch(tokenType) \
		{ 

#define SET_CTAIPTOKENTYPE_LENGTH(token_type, token_length)  \
		case CrTrAdditionalInfoProtocol::CTAIPPacket::CTAIPTokenType::token_type: \
			return token_length;   \
			break;
	
#define END_SET_CTAIPTOKEN_LENGTH()  \
		default:  \
			break;  \
		}    \
		return (*fGetLen)(tokenType); \
	}

#define BEGIN_ENABLE_CTAIP_VERSION()  \
	CTAIPT_Token_Parser::GETTOKENLENGTH_FN CTAIPT_Token_Parser::GetParser(BYTE verMajor)  \
	{ \
		switch(verMajor) \
		{

#define  ENABLE_CTAIP_VERSION(major_version)   \
		case major_version:  \
			return GetTokenLength_##major_version; \
			break;

#define END_ENABLE_CTAIP_VERSION()  \
		default: \
			break; \
		}  \
		return nullptr; \
	}


typedef DWORD (__cdecl * PROCESSCTAIPTOKEN_FN)(void * pProv, BYTE verMajor, BYTE tokenType, const BYTE * pDataBuf, USHORT cbData);

class CTAIPT_Token_Parser
{
typedef USHORT (*GETTOKENLENGTH_FN) (BYTE tokenType);
		
public:
	DECLARE_CTAIP_VERSION(1);

public:
	// No token in version 0 
	static USHORT GetTokenLength_0(BYTE tokenType)
	{
		return 0;
	}

	// Retrieve a token length based on major version
	//
	static USHORT GetTokenLength(BYTE verMajor, BYTE tokenType)
	{
		// Get parser function for specific major version
		//
		GETTOKENLENGTH_FN fGet = GetParser(verMajor);
		if (fGet != nullptr)
		{
			return (*fGet)(tokenType);
		}

		return 0;
	}

	static DWORD ProcessTokenStream( __in const BYTE * pTokenStreamBuf, PROCESSCTAIPTOKEN_FN fCallback, void * pProv);

private:
	static GETTOKENLENGTH_FN GetParser(BYTE verMajor);
};

