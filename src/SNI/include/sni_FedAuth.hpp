//****************************************************************************
//              Copyright (c) Microsoft Corporation.
//
// @File: SNI_FedAuth.hpp
// Purpose:
//	SNI interface for generating tokens for federated authentication,
//	implemented by calling underlying libraries like ADAL.
//
// Notes:
//          
// @EndHeader@
//****************************************************************************

#ifndef _SNI_FEDAUTH_HPP_
#define _SNI_FEDAUTH_HPP_

#include "adalerr.h"
#include "adal.h"

enum class AdalOption
{
    // Validation checks if this end-point is an AAD endpoint.
    // By default, it is On.
    // If you are sure, that it is an AAD endpoint then you can switch this Off (ADAL_OPTION_ENDPOINT_VALIDATION_SERVICE=ADAL_DISALLOW).
    // If you are sure that it is not an AAD endpoint, then you need switch this Off (ADAL_OPTION_ENDPOINT_VALIDATION_SERVICE=ADAL_DISALLOW).
    // If you get this endpoint from not trusted source (came from server or read from unprotected file), leave this option On (ADAL_OPTION_ENDPOINT_VALIDATION_SERVICE=ADAL_ALLOW).
    EndpointValidationService = 1,

    // If this options is disabled then in interactive mode web browser do not support cookies.
    // It means that SSO process will not work, but user will not be able to make authentication for different user. Internet kiosk scenario.
    // If this options is enabled, web site could use cookie to authenticate the user. SSO process will work.
    Cookies = 2,

    // if ADAL_OPTION_SSL_ERROR == ADAL_DISALLOW(default), then we if we have any SSL error we stop working.
    // if ADAL_OPTION_SSL_ERROR == ADAL_ALLOW, then we continue to work if there is an ssl-error.
    // CAUTION: allowing SSL-errors (ADAL_OPTION_SSL_ERROR == ADAL_ALLOW) make sense only for debugging purpose.
    //          by default we disallow SSL-errors.
    //          for final product, you need disallow SSL-errors (ADAL_OPTION_SSL_ERROR == ADAL_DISALLOW or ADAL_OPTION_SSL_ERROR == ADAL_DEFAULT)
    SslError = 4,

    // if ADAL_OPTION_FORCE_PROMPT == ADAL_DISALLOW(default), ADAL will use stored cookies and cache for the token.
    // if ADAL_OPTION_FORCE_PROMPT == ADAL_ALLOW, then new requests will not use cache, but they will update cache. Interactive flow will prompt the user for credentials again.
    ForcePrompt = 8,

    // Note: This option skipped to 32 because aoClientAssertion took the 16 spot over in the AuthOptions in AuthenticationContext.Enums.h.
    // if ADAL_OPTION_INTERNET_OPTION_END_BROWSER_SESSION == ADAL_ALLOW (default), the INTERNET_OPTION_END_BROWSER_SESSION option will be set in WebUIController::Start()
    // if ADAL_OPTION_INTERNET_OPTION_END_BROWSER_SESSION == ADAL_DISALLOW, the INTERNET_OPTION_END_BROWSER_SESSION will not be set in WebUIController::Start() allowing session to carry over
    ADAL_OPTION_INTERNET_OPTION_END_BROWSER_SESSION = 32,

    // if ADAL_OPTION_USE_WAM == ADAL_ALLOW(default), ADAL will use Web Account Manager (starting Windows 10 TH2) to obtain tokens.
    // if ADAL_OPTION_USE_WAM == ADAL_DISALLOW, ADAL will not use Web Account Manager (even on Windows 10 TH2 and beyond) and will communicate with AAD directly.
    ADAL_OPTION_USE_WAM = 64
};

enum class AdalOptionValue
{
    // disallows the option
    Disallow = 0,

    // allows the option
    Allow = 1,

    // resets option to default state.
    Default = 2
};

enum class AccountType
{
    // Default. Asks ADAL to discover, what mode it uses. 
    AutoDetect = 0,

    // Users' accounts managed by STS (AAD or other). 
    // STS must support OAuth2 protocol.
    Managed = 1,

    // Users' accounts managed by federated IdP.
    // IdP must support WS-trust protocol.
    Federated = 2
};

class ErrorCategory
{
public:
    // Success
    static const DWORD Success = 0;

    // Username/password wrong
    static const DWORD InvalidGrant = 1;

    // Tranisent error need retry
    static const DWORD TransientError = 2;

    // Other error
    static const DWORD OtherError = 3;
};

class ADALState
{
public:
    static const DWORD Success = 0;
    static const DWORD Default = 1;
    static const DWORD ADALCoInitializeEx = 2;
    static const DWORD ADALCreateAuthenticationContextNoUI = 3;
    static const DWORD ADALSetOption = 4;
    static const DWORD ADALAcquireToken = 5;
    static const DWORD ADALUseUsernamePassword = 6;
    static const DWORD ADALUseWindowsIntegrated = 7;
    static const DWORD ADALGetAccessToken = 8;
    static const DWORD ADALGetAccessTokenLength = 9;
    static const DWORD ADALGetErrorDescription = 10;
    static const DWORD ADALGetErrorDescriptionLength = 11;
    static const DWORD ADALGetRequestStatus = 12;
    static const DWORD ADALGetErrorCode = 13;
    static const DWORD ADALGetErrorCodeLength = 14;
    static const DWORD ADALDeleteRequest = 15;
    static const DWORD ADALReleaseAuthenticationContext = 16;
    static const DWORD ADALGetRequestStatusForAcquireToken = 17;
    static const DWORD ADALGetRequestStatusForUsernamePassword = 18;
    static const DWORD ADALGetRequestStatusForWindowsIntegrated = 19;
    static const DWORD ADALGetAccessTokenExpirationTime = 20;
    static const DWORD ADALSetOptionUseWam = 21;
};

typedef HADALCONTEXT(__stdcall *PFADALCreateAuthenticationContextNoUI) (LPCWSTR, LPCWSTR);
typedef BOOL(__stdcall *PFADALSetOption) (HADALCONTEXT, AdalOption, AdalOptionValue);
typedef HADALREQUEST(__stdcall *PFADALAcquireToken) (HADALCONTEXT, LPCWSTR, GUID*);
typedef DWORD(__stdcall *PFADALGetRequestStatus)(HADALREQUEST);
typedef BOOL(__stdcall *PFADALUseUsernamePassword) (HADALREQUEST, LPCWSTR, LPCWSTR);
typedef BOOL(__stdcall *PFADALUseWindowsAuthentication) (HADALREQUEST);
typedef DWORD(__stdcall *PFADALGetAccessToken) (HADALREQUEST, LPWSTR, LPDWORD);
typedef DWORD(__stdcall *PFADALGetErrorDescription) (HADALREQUEST, LPWSTR, LPDWORD);
typedef DWORD(__stdcall *PFADALGetErrorCode) (HADALREQUEST, LPWSTR, LPDWORD);
typedef BOOL(__stdcall *PFADALDeleteRequest)(HADALREQUEST);
typedef BOOL(__stdcall *PFADALReleaseAuthenticationContext)(HADALCONTEXT);
typedef DWORD(__stdcall *PFADALGetAccessTokenExpirationTime) (HADALREQUEST, LPSYSTEMTIME);

struct ADALFunctionTable
{
    HMODULE hDll;
    PFADALCreateAuthenticationContextNoUI ADALCreateAuthenticationContextNoUI;
    PFADALSetOption ADALSetOption;
    PFADALAcquireToken ADALAcquireToken;
    PFADALGetRequestStatus ADALGetRequestStatus;
    PFADALUseUsernamePassword ADALUseUsernamePassword;
    PFADALUseWindowsAuthentication ADALUseWindowsAuthentication;
    PFADALGetAccessToken ADALGetAccessToken;
    PFADALGetErrorDescription ADALGetErrorDescription;
    PFADALGetErrorCode ADALGetErrorCode;
    PFADALDeleteRequest ADALDeleteRequest;
    PFADALReleaseAuthenticationContext ADALReleaseAuthenticationContext;
    PFADALGetAccessTokenExpirationTime ADALGetAccessTokenExpirationTime;
};

extern "C" struct ADALFunctionTable g_ADAL;

extern "C" DWORD SNISecADALInitialize();
extern "C" DWORD SNISecADALGetAccessToken( __in LPCWSTR userName,
                                           __in LPCWSTR password,
                                           __in LPCWSTR stsURL,
                                           __in LPCWSTR resource,
                                           __in GUID& correlationId,
                                           __in LPCWSTR clientId,
                                           __in const bool& fWindowsIntegrated,
                                           __deref_opt_out_bcount(*pcbToken) LPWSTR *ppbToken,
                                           __out_opt DWORD& cbToken,
                                           __deref_opt_out_ecount(*pcsErrorDescription + 1) LPWSTR *ppsErrorDescription,
                                           __out_opt DWORD& csErrorDescription,
                                           __out DWORD& adalStatus,
                                           __out DWORD& state,
                                           __out_opt _FILETIME& fileTime);

#define LOAD_ADAL_FUNCTION(type, name) \
    g_ADAL.##name = (type)GetProcAddress(g_ADAL.hDll, #name); \
        if (nullptr == g_ADAL.name) \
        { \
            dwError = GetLastError(); \
            BidTrace1(ERROR_TAG _T("GetProcAddress(hAdalDll, ") _T(#name) _T("failed: %u{WINERR}.\n"), dwError); \
            SNI_SET_LAST_ERROR(INVALID_PROV, SNIE_61, dwError); \
            goto Exit; \
        }

// ADAL functions: ADALGetAccessToken, ADALGetErrorCode and ADALGetErrorDescription returns ERROR_INSUFFICIENT_BUFFE when call it first time
// the out parameter length contains new length. 
#define CHECK_ADAL_FUNCTION_RETURN_STATUS_FORLENGTH(statusToCheck, functionName) \
    if (ERROR_INSUFFICIENT_BUFFER != (statusToCheck)) \
    { \
        BidTrace1(ERROR_TAG _T(#functionName) _T(" got the unexpected status for the length: %u.\n"), (statusToCheck)); \
        goto Exit; \
    }

#define CHECK_MEM_ALLOCATION(pointerToCheck, functionName) \
    if (nullptr == pointerToCheck && ERROR_SUCCESS == status) \
    { \
        status = ERROR_OUTOFMEMORY; \
        BidTrace1(ERROR_TAG _T("Failed to allocate memory for the function ") _T(#functionName) _T(" status : %u{WINERR}.\n"), status); \
        goto Exit; \
     }

#define CHECK_ADAL_FUNCTION_RETURN_STATUS_BOOL(isSuccessful, functionName) \
    if (!(isSuccessful)) \
    { \
        if (ERROR_SUCCESS == status) \
        { \
            status = GetLastError(); \
        } \
        BidTrace1(ERROR_TAG _T(#functionName) _T(" returned FALSE. Current status: %u{WINERR}.\n"), status); \
     }

#endif
