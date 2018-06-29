//------------------------------------------------------------------------------
// Microsoft Azure Active Directory
//
// Copyright:   Copyright (c) Microsoft Corporation
// Notice:      Microsoft Confidential. For internal use only.
//------------------------------------------------------------------------------

#pragma once

#include <windef.h>

#include <MsHtmHst.h>
#include "adalerr.h"

#ifndef ADAL_API
#define ADAL_API __declspec( dllimport )
#endif

#ifdef __cplusplus
extern "C"
{
#endif

    DECLARE_HANDLE(HADALCONTEXT);
    DECLARE_HANDLE(HADALREQUEST);

    /// <summary>
    /// The callback function to inform that UI has completed and can be released.
    /// This function is provided in ADALUIUseWebBrowser.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <param name="lpData">data was sent by the application, to get application specific context. ADAL doesn't process this data, only sends to the application back.</param>
    typedef void (CALLBACK* LPADAL_COMPLETION_ROUTINE)(HADALREQUEST hRequest, LPVOID lpData);

    typedef enum ADAL_LOGLEVEL
    {
        ADAL_LOGLEVEL_ERROR     = 0,
        ADAL_LOGLEVEL_WARN      = 1,
        ADAL_LOGLEVEL_INFO      = 2,
        ADAL_LOGLEVEL_VERBOSE   = 3,
    } ADAL_LOGLEVEL;

    /// <summary>
    /// The callback function which is called for logging
    /// Users need to set log option for this and pass their callback
    /// </summary>
    /// <param name="message">Log message</param>
    /// <param name="additionalInformation">Additional info</param>
    /// <param name="logLevel">ADAL_LOGLEVEL enum</param>
    /// <param name="errorCode">ADAL or System error code</param>
    /// <param name="lpData">Application supplied data passed to callback.</param>
    typedef void (CALLBACK* LPADAL_LOG_ROUTINE)( LPCWSTR message, LPCWSTR additionalInformation, ADAL_LOGLEVEL logLevel, DWORD errorCode, LPVOID lpData );

    /// <summary>
    /// Creates a new instance of HADALCONTEXT for interactive flow.
    /// </summary>
    /// <param name="authority">this parameter is a Url of the token issuer</param>
    /// <param name="clientId">the client id registered with the token issuer</param>
    /// <param name="redirectUri">the redirect URI registered with the token issuer</param>
    /// <param name="loginHint">optional. this is the user identifier like email address</param>
    /// <returns>
    /// If the function succeeds, the return value is an HADALCONTEXT.
    /// If the function fails, the return value is NULL, GetLastError can be used to get an ADAL or a system error code.
    /// </returns>
    ADAL_API HADALCONTEXT WINAPI ADALCreateAuthenticationContext( LPCWSTR authority, LPCWSTR clientId, LPCWSTR redirectUri, LPCWSTR loginHint );

    /// <summary>
    /// Creates a new instance of HADALCONTEXT, for non-interactive flow.
    /// </summary>
    /// <param name="authority">this parameter is a Url of the token issuer</param>
    /// <param name="clientId">the client id registered with the token issuer</param>
    /// <returns>
    /// If the function succeeds, the return value is an HADALCONTEXT.
    /// If the function fails, the return value is NULL, GetLastError can be used to get an ADAL or a system error code.
    /// </returns>
    ADAL_API HADALCONTEXT WINAPI ADALCreateAuthenticationContextNoUI( LPCWSTR authority, LPCWSTR clientId );

    /// <summary>
    /// Deserialize an HADALCONTEXT instance that was previously serialized using SerializeAuthenticationContext.
    /// </summary>
    /// <param name="serializedContext">string to put serialized context into</param>
    /// <returns>
    /// If the function succeeds, the return value is an HADALCONTEXT.
    /// The caller must free, this context using ADALReleaseAuthenticationContext, when it is done with context.
    /// If the function fails, the return value is NULL, GetLastError can be used to get an ADAL or a system error code.
    /// </returns>
    ADAL_API HADALCONTEXT WINAPI ADALDeserializeAuthenticationContext( LPCWSTR serializedContext );

    typedef enum ADAL_SERIALIZE
    {
        // there is no protection applied for output string.
        ADAL_SERIALIZE_NOPROTECTION = 0,

        // context is protected per user level, only particular user can open context.
        ADAL_SERIALIZE_PROTECTED_PER_USER = 1
    } ADAL_SERIALIZE;

    /// <summary>
    /// Used to save the HADALCONTEXT instance so that it can used at a later point.
    /// Serialized authentication context is a string in format
    /// [digits]-[base64 string]
    /// Generally we guaranty, that it is a string from ASCII char set.
    /// However we return a Unicode string for interface consistency, and give ability to store in a string based storages.
    /// If the there is a problem with length of this buffer, you could convert this buffer from Unicode to ASCII/ANSI or UTF-8 char set.
    /// And you will get a buffer, which will bet twice shorter, without loosing information.
    /// </summary>
    /// <param name="hContext">ParamDescription</param>
    /// <param name="option">option</param>
    /// <param name="serializedContext">serializedContext</param>
    /// <param name="contextLength">contextLength</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALSerializeAuthenticationContext( HADALCONTEXT hContext, ADAL_SERIALIZE option, LPWSTR serializedContext, LPDWORD contextLength );

    /// <summary>
    /// Returns true, if context was modified since previous serialization.
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>    
    /// <returns>
    /// If the function returns TRUE, then the context was changed since previous serialization, and you need to save this context.
    /// If the function returns FALSE, then the context wasn't changed, since previous serialization or passed invalid handle as the input.
    /// The only reason it can fail, if the input handle for HADALCONTEXT is invalid. You can check this situation by calling GetLastError().
    /// In expectation, that the application will not send invalid context/closed context, library can use this property as a simply
    /// If ADALIsModified(hContext) == TRUE,  then need save.
    /// If ADALIsModified(hContext) == FALSE, then doesn't need save.
    /// </returns>
    ADAL_API BOOL WINAPI ADALIsModified( HADALCONTEXT hContext );

    /// <summary>
    /// Releases the authentication context. The memory for the context will be actually freed only when all request are deleted. Each request has a ref-count logic on context.
    /// You must delete all requests.
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>    
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALReleaseAuthenticationContext( HADALCONTEXT hContext );

    /// <summary>
    /// Gets an access token request from the issuer.
    /// If the token issuer needs user credentials, then status of request will be  ERROR_ADAL_NEED_UI, it informs the caller that application need user interface to continue.
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>    
    /// <param name="resource">Resource identifier, used to request a token</param>
    /// <param name="correlationId">Optional. Could be NULL. This is used for tracing purposes. It allows you to find request in the traces of the server.</param>
    /// <returns>
    /// If the function succeeds, the return value is HADALREQUEST. The caller must free this handle by ADALDeleteRequest.
    /// If the function fails, the return value is NULL.
    /// If the status of the request is ERROR_ADAL_NEED_CREDENTIAL, then application need to ask for user credential. Application could follow web browser flow, WAM UI flow, or non-interactive flow by providing user name/password or using integrated window auth, or using saml assertion.
    /// UI flow: if ADALIsWamUsed returns TRUE, the application must use WAM UI flow, otherwize use web browser flow
    /// Web Browser flow: User needs to specify a web browser control for displaying content or ask ADAL library to create UI for the application for displaying content.
    /// To specify the web browser control the application needs to call ADALUIUseWebBrowser or ADALUICreateHostWindow to ask ADAL library create the window, for rendering content.
    /// WAM UI flow: Use ADALUIUseWAM for UI flow
    /// If authority is invalid, it will return error.
    /// </returns>
    /// <example>
    /// The interaction is:
    /// <code>
    /// User can create context in two ways. ADALCreateAuthenticationContext can use UI flow and non-interactive flow as well. ADALCreateAuthenticationContexNoUI can't use UI flow.
    ///
    /// hContext = ADALCreateAuthenticationContext( authority, clientId, redirectUri, loginHint );
    /// hRequest = ADALAcquireToken(hContext, resource, correlationId);
    /// if (hRequest == NULL)
    /// {
    ///     ADALReleaseAuthenticationContext(hContext);
    ///     FormatMessage(GetLastError());
    ///     return;
    /// }
    /// switch ( ADALGetRequestStatus(hRequest) )
    /// {
    ///     case ERROR_SUCCESS:
    ///         ... now you can get a token ...
    ///         ADALGetAccessToken(hRequest, accessToken, &amp;tokenLength);
    ///         ADALGetAccessTokenExpirationTime( hRequest,  &amp;expiresOn );
    ///
    ///         ... and refresh token as well ...
    ///         ADALGetRefreshToken( ADALGetContext( hRequest ), resource, refreshToken, &amp;tokenLength);
    ///     break;
    ///     case ERROR_ADAL_NEED_CREDENTIAL:
    ///         ... If user's environment supports browser control, they can ask for browser interaction.
    ///
    ///         if (!ADALIsWAMUsed())
    ///         {
    ///             ADALUIUseWebBrowser ( hRequest, webBrowser, &amp;callBackFunctionForUIComplete, lpCallbackData );
    ///             or
    ///             hWnd = ADALUICreateHostWindow ( hRequest, &amp;callBackFunctionForUIComplete, lpCallbackData, hWndParent, lpRect, szWindowName,  dwStyle, dwExStyle, hMenuOrID );
    ///         }
    ///         else
    ///         {
    ///             //Must be called on UI thread
    ///             ADALUIUseWAM( HADALREQUEST hRequest, HWND hWnd, LPADAL_COMPLETION_ROUTINE lpUICompleteCallback, LPVOID lpCallbackData );
    ///         }
    ///
    ///         ... make a message loop or return (let parent message loop work)
    ///         ... User must provide callback function(LPADAL_COMPLETION_ROUTINE) so that UI can call after it is complete. User can provide callback data(LPVOID) as well. Library passes back the callback data without any changes.
    ///         ... lpUICompleteCallback will be called after the UI flow is finished.
    ///
    ///         ... User can call the following sample function to retrieve the status for the request for all the flows.
    ///         checkStatus(hRequest);
    ///
    ///         Non-interactive context does not need message loop. Context can be created by ADALCreateAuthenticationContextNoUI or ADALCreateAuthenticationContext
    ///         Non-interactive flow does not set uicomplete callback. User needs to check the request status.
    ///         User can override endpoints that are required for non-interactive scenario by calling ADALUseEndpoint.
    ///         User can set account types if authority does not have userrealm endpoint such as directly talking to ADFS.
    ///
    ///         ADALUseUsernamePassword( hRequest, username, password );
    ///         checkStatus(hRequest);
    ///         ... or
    ///         ADALUseWindowsAuthentication( hRequest );
    ///         checkStatus(hRequest);
    ///         ... or
    ///         ADALUseSAMLAssertion( hRequest, assertion, assertionType);
    ///         checkStatus(hRequest);
    ///     break;
    ///     default:
    ///         ... or error ...
    ///         ADALGetErrorDescription( hRequest, errorDescription, &amp;errorLength );
    ///         ADALGetErrorCode( hRequest, error, &amp;errorLength );
    ///
    ///         ... you can free request here or later...
    ///     break;
    /// }
    /// ADALDeleteRequest( hRequest );
    ///
    /// ... You can release context here or later...
    /// ADALReleaseAuthenticationContext(hContext);
    ///
    /// static void CALLBACK callBackFunctionForUIComplete(HADALREQUEST hRequest, LPVOID lpData)
    /// {
    ///         ... Any user's data
    ///         MyContext* ctx=(MyContext*)lpData;
    ///         ... User can check the status here
    ///         checkStatus(HADALREQUEST hRequest);
    /// }
    ///
    /// static void checkStatus(HADALREQUEST hRequest)
    /// {
    ///     ... You can call ADALAcquireToken here or later.
    ///     if (ERROR_SUCCESS == ADALGetRequestStatus(hRequest))
    ///     {
    ///         ... Now you can get a token ...
    ///         ADALGetAccessToken(hRequest, accessToken, &amp;tokenLength);
    ///         ADALGetAccessTokenExpirationTime( hRequest,  &amp;expiresOn );
    ///
    ///         ADALGetRefreshToken( ADALGetContext( hRequest ), resource, refreshToken, &amp;tokenLength);
    ///     }
    ///     else
    ///     {
    ///         ... or error ...
    ///         ADALGetErrorDescription( hRequest, errorDescription, &amp;errorLength );
    ///         ADALGetErrorCode( hRequest, error, &amp;errorLength );
    ///     }
    ///
    ///     ... You can free request here or later...
    ///     ADALDeleteRequest( hRequest );
    /// }
    /// </code>
    /// </example>
    ADAL_API HADALREQUEST WINAPI ADALAcquireToken( HADALCONTEXT hContext, LPCWSTR resource, const GUID* correlationId );

    /// <summary>
    /// Returns authentication context associated with request.
    /// Your are not able and you do not need to release this handle.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <returns>
    /// Returns authentication context associated with request.
    /// Your are not able and you do not need to release this handle.
    /// </returns>
    ADAL_API HADALCONTEXT WINAPI ADALGetContext( HADALREQUEST hRequest );

    /// <summary>
    /// Frees the resources used by the HADALREQUEST instance, as well as referenced authentication context, if it is last request in the context.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALDeleteRequest( HADALREQUEST hRequest );

    /// <summary>
    /// Returns request status, see ADALAcquireToken for details.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <returns>
    /// The return value is the status of the request.
    /// The function fails, only if you are sending invalid handle, otherwise you need treat the returning value as a request status.
    /// switch ( ADALGetRequestStatus(hRequest) )
    /// {
    ///     case ERROR_SUCCESS:
    ///         ... now you can get a token ...
    ///         ADALGetAccessToken(hRequest, accessToken, &amp;tokenLength);
    ///         ADALGetAccessTokenExpirationTime( hRequest,  &amp;expiresOn );
    ///
    ///         ... and refresh token as well ...
    ///         ADALGetRefreshToken( ADALGetContext( hRequest ), resource, refreshToken, &amp;tokenLength);
    ///     break;
    ///     case ERROR_ADAL_NEED_CREDENTIAL:
    ///         ADALUIUseWebBrowser ( hRequest, webBrowser, callback, callbackData );
    ///         or
    ///         hWnd = ADALUICreateHostWindow ( hRequest, lpUICompleteCallback, lpCallbackData, hWndParent, lpRect, szWindowName,  dwStyle, dwExStyle, hMenuOrID );
    ///         or 
    ///         //Must be called in UI thread
    ///         ADALUIUseWAM( HADALREQUEST hRequest, HWND hWnd, LPADAL_COMPLETION_ROUTINE lpUICompleteCallback, LPVOID lpCallbackData );
    ///         ... make a message loop or return (let parent message loop work) ...
    ///         ... or
    ///         ADALUseUsernamePassword( hRequest, username, password );
    ///         checkStatus(hRequest);
    ///         ... or
    ///         ADALUseWindowsAuthentication( hRequest );
    ///         checkStatus(hRequest);
    ///         ... or
    ///         ADALUseSAMLAssertion( hRequest, assertion, assertionType);
    ///         checkStatus(hRequest);
    ///         ... or
    ///         ADALUseClientCredential( hRequest );
    ///         checkStatus(hRequest);
    ///     break;
    ///     default:
    ///         ... or error ...
    ///         ADALGetErrorDescription( hRequest, errorDescription, &amp;errorLength );
    ///         ADALGetErrorCode( hRequest, error, &amp;errorLength );
    ///
    ///         ... you can free request here or later...
    ///     break;
    /// }
    ///
    /// static void checkStatus(HADALREQUEST hRequest)
    /// {
    ///     ... You can call ADALAcquireToken here or later.
    ///     if (ERROR_SUCCESS == ADALGetRequestStatus(hRequest))
    ///     {
    ///         ... Now you can get a token ...
    ///         ADALGetAccessToken(hRequest, accessToken, &amp;tokenLength);
    ///         ADALGetAccessTokenExpirationTime( hRequest,  &amp;expiresOn );
    ///
    ///         ADALGetRefreshToken( ADALGetContext( hRequest ), resource, refreshToken, &amp;tokenLength);
    ///     }
    ///     else
    ///     {
    ///         ... or error ...
    ///         ADALGetErrorDescription( hRequest, errorDescription, &amp;errorLength );
    ///         ADALGetErrorCode( hRequest, error, &amp;errorLength );
    ///     }
    ///
    ///     ... You can free request here or later...
    ///     ADALDeleteRequest( hRequest );
    /// }
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetRequestStatus( HADALREQUEST hRequest );

    /// <summary>
    /// Reads the access token from token request. There is no server interaction, just reads data from memory.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <param name="accessToken">the buffer into which the access token is copied into after it is received from the token issuer</param>
    /// <param name="tokenLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetAccessToken(HADALREQUEST hRequest, LPWSTR accessToken, LPDWORD tokenLength);

    /// <summary>
    /// Reads life time of the access token from authentication request. There is no server interaction, just reads data from memory.
    /// </summary>
    /// <param name="hRequest">Instance of the authentication request.</param>
    /// <param name="expiresOn">time up to which the access token is valid. Time is in UTC.</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALGetAccessTokenExpirationTime( HADALREQUEST hRequest,  LPSYSTEMTIME expiresOn );

    /// <summary>
    /// Gets displayable and unique user Id, there is guaranty that this user id is displayable to the users.
    /// As we restricted to choose displayable identifier only, and displayable identifiers are optional this function could fail in providing user id in some cases.
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>
    /// <param name="value">the buffer into which the displayable user id is copied into after it is received from the token issuer</param>
    /// <param name="valueLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetDisplayableUserId( HADALCONTEXT hContext, LPWSTR value, LPDWORD valueLength);

    /// <summary>
    /// Gets unique user Id, there is no guaranty that this user id is displayable to the users, however there is a guaranty that this user id is unique.
    /// As we free to choose non displayable identifier, we have more luck in success to return id to the application.
    /// We also try to be stable here, in sense no matter which claims are returned we return the same id.
    /// In case this information doesn't exists function doesn't return anything.
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>
    /// <param name="value">the buffer into which the unique user id is copied into after it is received from the token issuer</param>
    /// <param name="valueLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetUniqueUserId( HADALCONTEXT hContext, LPWSTR value, LPDWORD valueLength);

    /// <summary>
    /// Gets name of the user
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>
    /// <param name="value">the buffer into which the upn is copied into after it is received from the token issuer</param>
    /// <param name="valueLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetGivenName( HADALCONTEXT hContext, LPWSTR value, LPDWORD valueLength);

    /// <summary>
    /// Gets family name of the user
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>
    /// <param name="value">the buffer into which the upn is copied into after it is received from the token issuer</param>
    /// <param name="valueLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetFamilyName( HADALCONTEXT hContext, LPWSTR value, LPDWORD valueLength);

    /// <summary>
    /// Get's the number of days until the users password expires, if the server sent this data.
    /// </summary>
    /// <param name="hRequest">Instance of the authentication request.</param>
    /// <param name="passwordExpiryDays">The number of days until the users password will no longer be valid.</param>
    /// <returns>
    /// If the function succeeds, the return value TRUE.
    /// If the function fails or no password expiry data is available, the return value is FALSE.
    /// </returns>
    ADAL_API BOOL WINAPI ADALGetPasswordExpiryDays( HADALREQUEST hRequest, DWORD *passwordExpiryDays );

    /// <summary>
    /// Gets the Url at which the user can change their password, if the server sent this information.
    /// </summary>
    /// <param name="hRequest">Instance of the authentication request.</param>
    /// <param name="passwordChangeUrl">The buffer into which the url is copied into after it is received from the token issuer</param>
    /// <param name="passwordChangeUrlLength">This in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the url</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetPasswordChangeUrl( HADALREQUEST hRequest, LPWSTR passwordChangeUrl, LPDWORD passwordChangeUrlLength);

    /// <summary>
    /// Gets TenantId
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>
    /// <param name="value">the buffer into which the upn is copied into after it is received from the token issuer</param>
    /// <param name="valueLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetTenantId( HADALCONTEXT hContext, LPWSTR value, LPDWORD valueLength);


    /// <summary>
    /// Pass to ADALGetIdTokenValue to retrieve the object id of the user from the id_token if one was passed by the server.
    /// </summary>
    #define IDTOKEN_OID_KEY  L"oid"

    /// <summary>
    /// Gets a value associated with the passed key from the id_token if one was returned from the server.
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>
    /// <param name="key">the key to look for in the id_token, NULL terminated</param>
    /// <param name="value">the buffer into which the id_token value is copied into after it is received from the token issuer</param>
    /// <param name="valueLength">this in_out parameter contains the size of the value buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetIdTokenValue( HADALCONTEXT hContext, LPCWSTR key, LPWSTR value, LPDWORD valueLength);

    /// <summary>
    /// This methods returns the error message received from the server.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <param name="errorDescription">the buffer into which the error description is copied</param>
    /// <param name="errorLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the error description</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetErrorDescription( HADALREQUEST hRequest, LPWSTR errorDescription, LPDWORD errorLength );

    /// <summary>
    /// This methods returns the a string error code received from the server.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <param name="error">the buffer into which the error code is copied</param>
    /// <param name="errorLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the error code</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetErrorCode( HADALREQUEST hRequest, LPWSTR error, LPDWORD errorLength );

    /// <summary>
    /// Reads the refresh token from authentication context. There is no server interaction, just reads data from memory.
    /// </summary>
    /// <param name="hContext">instance of the authentication context handle created by the ADALCreateAuthenticationContext method</param>
    /// <param name="resource">Resource for which token is associated. You can send NULL, if you need to get broad refresh token.</param>
    /// <param name="refreshToken">the buffer into which the refresh token is copied into</param>
    /// <param name="tokenLength">this in_out parameter contains the size of the buffer and is updated by the method to indicate the size of the token</param>
    /// <returns>
    /// If the function succeeds, the return value is ERROR_SUCCESS.
    /// If the function returns ERROR_INSUFFICIENT_BUFFER, then tokenLength contains new length (number of chars without ending '\0').
    /// If the function fails, the return value is an ADAL or a system error code.
    /// If you set the resource to Null and receive error that refresh token does not exist, it means you don't have broad refresh token where you can use for multiple resources
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetRefreshToken( HADALCONTEXT hContext, LPCWSTR resource, LPWSTR refreshToken, LPDWORD tokenLength);

    /// <summary>
    /// Sets the refresh token into the HADALCONTEXT instance. There is no server interaction, just reads data from memory.
    /// </summary>
    /// <param name="hContext">Instance of the HADALCONTEXT created by the ADALCreateAuthenticationContext method</param>
    /// <param name="resource">Resource for which token is associated. You can send NULL, if you need to set broad refresh token, which will be used for acquire token for multiple resources. Not every refresh token is broad refresh token. You can get those from ADALGetRefreshToken by setting resource to null.</param>
    /// <param name="refreshToken">the buffer into which the refresh token is copied into after it is received from the token issuer</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetRefreshToken( HADALCONTEXT hContext, LPCWSTR resource, LPCWSTR refreshToken );

    /// <summary>
    /// Set a web browser control as a primary method for displaying content.
    /// If the host window need to be closed earlier, then you need to call this function with
    ///     ADALUIUseWebBrowser(hContext, NULL, NULL, NULL);
    /// ADALUIUseWebBrowser is not supported if ADALIsWAMUsed returns TRUE. Use ADALUIUseWAM in that case.
    ///
    /// Consider to use ADALUICreateHostServiceProvider/ADALUICreateHostUIHandler (see definition for this function for help)
    ///     ADALUICreateHostServiceProvider returns IServiceProvider, which should be used for protection against various threats.
    ///     ADALUICreateHostUIHandler returns IDocHostUIHandler which could be used to improve user experience.
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <param name="lpWebBrowser">Pointer to the web browser control, which implements IWebBrowser2 interfaces.</param>
    /// <param name="lpUICompleteCallback">Callback function, which says that UI is complete and we don't need any UI.</param>
    /// <param name="lpCallbackData">Data which will comeback with callback.</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUIUseWebBrowser( HADALREQUEST hRequest, LPUNKNOWN lpWebBrowser, LPADAL_COMPLETION_ROUTINE lpUICompleteCallback, LPVOID lpCallbackData );

    /// <summary>
    /// Uses Web Account Manager for UI flow.
    /// This is supported starting Windows10 TH2
    /// Use ADALIsWAMUsed to see if ADALUIUseWAM is supported. If ADALIsWAMUsed returns TRUE, then ADALUIUseWAM is supported
    /// The API must be called on UI thread
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <param name="hWnd">Window handle required by Web Account Manager API for the application window.</param>
    /// <param name="lpUICompleteCallback">Callback function, which says that UI is complete and we don't need any UI.</param>
    /// <param name="lpCallbackData">Data which will comeback with callback.</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUIUseWAM( HADALREQUEST hRequest, HWND hWnd, LPADAL_COMPLETION_ROUTINE lpUICompleteCallback, LPVOID lpCallbackData );

    /// <summary>
    /// Sets username and password to use with non-interactive flow
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <param name="username">Username format is john.doe@contoso.com </param>
    /// <param name="password">Password</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUseUsernamePassword( HADALREQUEST hRequest, LPCWSTR username, LPCWSTR password );


    /// <summary>
    /// Sets request to use Windows Integrated Auth (WIA)
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUseWindowsAuthentication( HADALREQUEST hRequest );

    /// <summary>
    /// Enum for SAML Assertion to use V1 or V2. See ADALUseSAMLAssertion().
    /// </summary>
    typedef enum ADAL_SAML_ASSERTION
    {
        // Samlv1 assertion
        ADAL_ASSERTION_SAMLV1 = 0,

        // Samlv2 assertion
        ADAL_ASSERTION_SAMLV2 = 1,
    } ADAL_SAML_ASSERTION;

    /// <summary>
    /// Set a saml assertion and type to use in Non-Interactive flow for requesting a token.
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <param name="assertion">Saml assertion without any encoding</param>
    /// <param name="assertionType">Saml assertion type.</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUseSAMLAssertion( HADALREQUEST hRequest, LPCWSTR assertion, ADAL_SAML_ASSERTION assertionType);

    /// <summary>
    /// Set client id when creating the context and set client secret with ADALSetClientSecret call and then call this method.
    /// ADALUseClientCredential is not supported if ADALIsWAMUsed returns TRUE.
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <returns>
    /// TRUE If the function succeeds, otherwise FALSE.
    /// To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUseClientCredential( HADALREQUEST hRequest );

    /// <summary>
    /// Uses the Client credential and User Token to get a "on-behalf of" token from AAD.
    /// The Client credential can be set by using ADALSetClientSecretUsingCertficateThumbprint().
    /// ADALUseClientCredentialWithUserToken is not supported if ADALIsWAMUsed returns TRUE.
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <param name="userToken">The user token to use in the request</param>
    /// <returns>
    /// TRUE If the function succeeds, otherwise FALSE.
    /// To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUseClientCredentialWithUserToken(HADALREQUEST hRequest, LPCWSTR userToken);

    /// <summary>
    /// Struct for Web Host Requirements
    /// </summary>
    struct ADAL_WEB_HOST_REQUIREMENTS
    {
        // size to inform user about version of the structure.
        DWORD           cbSize;

        // flags IDocHostUIHandler Interface
        DOCHOSTUIFLAG   DocHostUIFlags;

        // flags for DISPID_AMBIENT_DLCONTROL handler.
        DWORD           DLControlFlags;
    };

    /// <summary>
    /// Pointer to struct for Web Host Requirements. See ADALUIGetHostRequirements().
    /// </summary>
    typedef struct ADAL_WEB_HOST_REQUIREMENTS* LPADAL_WEB_HOST_REQUIREMENTS;

    /// <summary>
    /// Get required flags for web browser host. This minimum set of required flags.
    /// </summary>
    /// <param name="hContext">Instance of the authentication context.</param>    
    /// <param name="hostRequirements">Pointer to ADAL_WEB_HOST_REQUIREMENTS struct to hold returned data</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUIGetHostRequirements( HADALCONTEXT hContext, LPADAL_WEB_HOST_REQUIREMENTS hostRequirements );

    /// <summary>
    /// Struct for extended Web Host Requirements
    /// </summary>
    struct ADAL_WEB_HOST_REQUIREMENTS_EX
    {
        // Size to inform user about version of the structure.
        DWORD           cbSize;

        // IServiceProvider Interface
        LPUNKNOWN   lpServiceProvider;
    };

    /// <summary>
    /// Pointer to struct for extended Web Host Requirements. 
    /// </summary>
    typedef struct ADAL_WEB_HOST_REQUIREMENTS_EX* LPADAL_WEB_HOST_REQUIREMENTS_EX;

    /// <summary>
    /// Providing interfaces for extending browser client.
    /// ADAL strongly recommends use this interface.
    /// ADAL users IServiceProvider to protect against different threats. ADAL overrides a set of services on this interface to protect
    /// against invalid certificates, unauthorized download, activeX and other plug-ins.
    /// </summary>
    /// <param name="hContext">Instance of the HADALCONTEXT created by the ADALCreateAuthenticationContext method</param>
    /// <param name="outerObject">
    /// Optional. Could be NULL.
    /// Used for "COM aggregation" see details in below arguments description.
    /// In most of cases it could be NULL, but there could be case when user of the library already have implementation of these interfaces.
    /// In this case, developer needs to apply "COM Aggregation" approach to extend his/her implementation by ADAL's implementation.
    /// </param>
    /// <returns>
    /// If the function fails, the return value is NULL. To get extended error info call GetLastError()
    ///
    /// If the function succeeds, the return value is pointer on IUnknown* interface of object that implements IServicePovider interfaces.
    /// User must call IUnknown::Release to free this object.
    /// (see http://msdn.microsoft.com/en-us/library/cc678965(v=vs.85).aspx ).
    /// Web browser host should provider this interface through the IOleClientSite interface that is supplied by using IOleObject::SetClientSite.
    /// If host application provides own implementation of IServiceProvider, consider to user "COM aggregation"
    /// to extend your implementation by ADAL's implementation. For this purpose you need to provide outerObject.
    /// ADAL's build-in web browser host (ADALUICreateHostWindow) uses this interface automatically.
    /// </returns>
    ADAL_API LPUNKNOWN WINAPI ADALUICreateHostServiceProvider( HADALCONTEXT hContext, LPUNKNOWN outerObject /*= NULL*/ );

    /// <summary>
    /// Providing interfaces for extending browser UI.
    /// This interface used for extending UI:
    ///     - more accurate processing keyboard (disable refresh etc).
    ///     - more accurate context menu processing.
    ///     - working in High DPI application.
    /// </summary>
    /// <param name="hContext">Instance of the HADALCONTEXT created by the ADALCreateAuthenticationContext method</param>
    /// <param name="outerObject">
    /// Optional. Could be NULL.
    /// Used for "COM aggregation" see details in below arguments description.
    /// In most of cases it could be NULL, but there could be case when user of the library already have implementation of these interfaces.
    /// In this case, developer needs to apply "COM Aggregation" approach to extend his/her implementation by ADAL's implementation.
    /// </param>
    /// <returns>
    /// If the function fails, the return value is NULL. To get extended error info call GetLastError()
    ///
    /// If the function succeeds, the return value is pointer on IUnknown* interface of object that implements IDocHostUIHandler interfaces.
    /// User must call IUnknown::Release to free this object.
    /// This object implements following interfaces:
    ///     - IDocHostUIHandler
    ///     - IDocHostUIHandler2
    ///     - IDocHostUIHandlerDispatch - this implementation of the same interfaces but for simplicity use in ATL.
    ///
    /// See also:
    ///     http://msdn.microsoft.com/en-us/library/aa753260(v=vs.90).aspx
    ///     http://msdn.microsoft.com/en-us/library/aa770041(v=vs.85).aspx
    ///     http://msdn.microsoft.com/en-us/library/aa770042(v=vs.85).aspx
    ///
    /// ADAL's build-in web browser host (ADALUICreateHostWindow) uses this interface, and we publish this interface for users convenience.
    ///
    /// </returns>
    /// <remarks>
    /// It is not mandatory for using this interface for the host application, as application could have its own logic to handle this things.
    /// Also application could aggregate this object, if it has its own implementation of this interface.
    ///
    /// ADALUIGetHostRequirements returns DocHostUIFlags using this interface. So, you don't need use ADALUIGetHostRequirements, if you are using this interface (as GetHostInfo return the same value).
    /// </remarks>
    ADAL_API LPUNKNOWN WINAPI ADALUICreateHostUIHandler( HADALCONTEXT hContext, LPUNKNOWN outerObject /*= NULL*/ );

    /// <summary>
    /// Create a popup window as a primary method for displaying content.
    /// 1. You can pass WS_CHILD window style, to create host window as a child control of different window.
    /// 2. You also can create WS_OVERLAPPED or WS_POPUP window. ADAL doesn't define any custom window style.
    /// All window attributes pass directly to CreateWindowEx function.
    /// By default, ADAL creates a top-level window.
    /// So, you able to create any window with any style and subclass this window as well.
    /// This window could be a control on your window.
    /// ADALUICreateHostWindow is not supported if ADALIsWAMUsed returns TRUE. Use ADALUIUseWAM in that case.
    /// </summary>
    /// <param name="hRequest">HADALREQUEST handle created by the ADALAcquireToken function</param>
    /// <param name="lpUICompleteCallback">Callback function, which says that UI is complete and we don't need any UI.</param>
    /// <param name="lpCallbackData">Data which will comeback with callback.</param>
    /// <param name="hWndParent">Optional. Parent window. See CreateWindowEx MSDN documentation.</param>
    /// <param name="lpRect">Optional. Rectangle window. See CreateWindowEx MSDN documentation.</param>
    /// <param name="szWindowName">Optional. Window title/text. See CreateWindowEx MSDN documentation.</param>
    /// <param name="dwStyle">Optional. Window style. See CreateWindowEx MSDN documentation.</param>
    /// <param name="dwExStyle">Optional. Window extended style. See CreateWindowEx MSDN documentation.</param>
    /// <param name="hMenuOrID">Optional. Window menu. See CreateWindowEx MSDN documentation.</param>
    /// <returns>
    /// If the function succeeds, it returns handle to the window
    /// If the function fails, the return value is NULL. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API HWND WINAPI ADALUICreateHostWindow( HADALREQUEST hRequest, LPADAL_COMPLETION_ROUTINE lpUICompleteCallback, LPVOID lpCallbackData, HWND hWndParent, LPRECT lpRect, LPCWSTR szWindowName,  DWORD dwStyle, DWORD dwExStyle, HMENU hMenuOrID );

    /// <summary>
    /// Returns a web browser assigned with context. Useful with ADALUICreateHostWindow.
    /// ADALUIGetWebBrowser is not supported if ADALIsWAMUsed returns TRUE.
    /// </summary>
    /// <param name="hRequest">Instance of the HADALREQUEST created by ADALAcquireToken.</param>
    /// <param name="ppWebBrowser">Pointer to the web browser control, which implements IWebBrowser2 interfaces.</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUIGetWebBrowser( HADALREQUEST hRequest, LPUNKNOWN* ppWebBrowser );

    /// <summary>
    /// Set additional query params and related endpoint to use in request
    /// This string will be appended to the end of the original request
    /// Example: "&amp;Attribute1=Value1&amp;Attribute2=Value2"
    /// If you are sending encoded params, you should set the encodedParams to true.
    /// If you are sending attribute and value pairs and need encoding, you should set the encodedParams to false
    /// </summary>
    /// <param name="hContext">Instance of the authentication context handle.</param>
    /// <param name="additionalQueryParams">additional query params</param>
    /// <param name="encodedParams">Set to true to indicate that additional params are encoded. Set to false if query params are not encoded</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE, GetLastError could be used to get an ADAL or a system error code.
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetAdditionalQueryParams( HADALCONTEXT hContext, LPCWSTR additionalQueryParams, BOOL encodedParams );

    /// <summary>
    /// This appends these headers to existing headers for all requests
    /// Add CRLF ending for each header entry even for a single header line
    /// </summary>
    /// <param name="hContext">Instance of the HADALCONTEXT created by the ADALCreateAuthenticationContext function</param>
    /// <param name="headers">Example: HeaderAtrribute1:HeaderValue1\r\nHeaderAtrribute2:HeaderValue2\r\n </param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetAdditionalHttpHeaders( HADALCONTEXT hContext, LPCWSTR headers );

    /// <summary>
    /// Struct for log options
    /// </summary>
    struct ADAL_LOG_OPTIONS
    {
        // Size to inform user about the version of the structure. Set cbSize to sizeof( ADAL_LOG_OPTIONS)
        DWORD				cbSize;

        // Set TRUE to enable Trace log, which can viewed from output window after setting the level with ATL/MFC Trace tool. Set FALSE to disable.
        BOOL				enableTraceLog;

        // Set TRUE to enable event logger that writes to the event view.
        // If set to FALSE, it removes event logger
        BOOL				enableEventLog;

        // Root name to register event source for reporting to the event view. You need to create registry entries for message file at HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\Application\eventLogRegistryRootName
        LPCWSTR				eventLogRegistryRootName;

        // If provided, it enables callBack logger. Logger will also call this function for logging beside other enabled loggers
        // If it is null, callback loggers will be removed
        LPADAL_LOG_ROUTINE	lpLogCallback;

        // Optional context, which will be pass back to callback function
        LPVOID				lpData;

        // Set logging level for all loggers.
        // Level Error will report only errors
        // Level Warning will report errors and warning messages
        // Level Info will report info and above
        // Level Verbose will report all of the messages
        ADAL_LOGLEVEL			level;
    };

    /// <summary>
    /// pointer to struct for log options. See ADALSetLogOptions().
    /// </summary>
    typedef struct ADAL_LOG_OPTIONS* LPADAL_LOG_OPTIONS;

    /// <summary>
    /// Set log options to enable/disable trace logger, enable/disable event logger, and attach callback for custom logging
    /// If you provide callback function, it will call your method for logging in addition to other enabled loggers
    /// In order to use the event logger, you need to register message file and supported types in the registry:
    ///  Registry Info
    /// EventMessageFile	REG_EXPAND_SZ	[PathToDll]\adal.dll
    /// TypesSupported		REG_DWORD		mask EVENTLOG_ERROR_TYPE | EVENTLOG_WARNING_TYPE | EVENTLOG_INFORMATION_TYPE
    ///
    /// Example registry file:
    ///	Windows Registry Editor Version 5.00
    ///
    /// [HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\EventLog\Application\YourRegistryRootName]
    /// "TypesSupported"=dword:00000007
    /// "EventMessageFile"="[PathToDll]\adal.dll"
    ///
    /// Event log message files are cached in the event view. If you register wrong file, you need to create with different rootname or restart event
    /// viewer to use new message file. If you don't see the error descriptions in the event view, message file registration is not correct.
    /// System error codes are mapped to a custom Adal error code and message is displayed in the details section for Event Log.
    /// </summary>
    /// <param name="logOptions">Log Options struct</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetLogOptions( LPADAL_LOG_OPTIONS logOptions );

    /// <summary>
    /// Customize behavior of silent logon process.
    /// </summary>
    /// <param name="hContext">Authorization context</param>
    /// <param name="timeOutMiliSeconds">if equals to 0, then we disable silent logon process.
    /// Non zero value indicates number of milliseconds could be spend on total detection process.
    /// This value used to control redirect time spend on redirection between endpoints.</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetSilentLogonOptions( HADALCONTEXT hContext, DWORD timeOutMiliSeconds );

    /// <summary>
    /// Enum that describes all possible endpoints which could be used in ADAL
    /// </summary>
    typedef enum ADAL_ENDPOINT
    {
        // Authorization end point.
        ADAL_ENDPOINT_AUTHORIZATION = 0,

        // Token issuance end point.
        ADAL_ENDPOINT_TOKEN = 1,

        // WS-Federation metadata exchange end point (MEX).
        ADAL_ENDPOINT_WS_METADATA = 2,

        // WS-Trust endpoint for Windows Integrated authentication.
        ADAL_ENDPOINT_WS_WINDOWS_AUTHENTICATION = 3,

        // WS-Trust endpoint for username/password authentication.
        ADAL_ENDPOINT_WS_USERNAME_PASSWORD = 4,
    } ADAL_ENDPOINT;

    /// <summary>
    /// This is an optional API used to override endpoints, which used by ADAL.
    /// </summary>
    /// <param name="hContext">Authorization context</param>
    /// <param name="endPoint">Endpoint used, which needs to be overridden.</param>
    /// <param name="endPointUrl">Url of an endpoint</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALUseEndpoint( HADALCONTEXT hContext, ADAL_ENDPOINT endPoint, LPCWSTR endPointUrl );


    /// <summary>
    /// Struct for ADAL options
    /// </summary>
    typedef enum ADAL_OPTION
    {
        // Validation checks if this end-point is an AAD endpoint.
        // By default, it is On.
        // If you are sure, that it is an AAD endpoint then you can switch this Off (ADAL_OPTION_ENDPOINT_VALIDATION_SERVICE=ADAL_DISALLOW).
        // If you are sure that it is not an AAD endpoint, then you need switch this Off (ADAL_OPTION_ENDPOINT_VALIDATION_SERVICE=ADAL_DISALLOW).
        // If you get this endpoint from not trusted source (came from server or read from unprotected file), leave this option On (ADAL_OPTION_ENDPOINT_VALIDATION_SERVICE=ADAL_ALLOW).
        ADAL_OPTION_ENDPOINT_VALIDATION_SERVICE = 1,

        // If this options is disabled then in interactive mode web browser do not support cookies.
        // It means that SSO process will not work, but user will not be able to make authentication for different user. Internet kiosk scenario.
        // If this options is enabled, web site could use cookie to authenticate the user. SSO process will work.
        ADAL_OPTION_COOKIES = 2,

        // if ADAL_OPTION_SSL_ERROR == ADAL_DISALLOW(default), then we if we have any SSL error we stop working.
        // if ADAL_OPTION_SSL_ERROR == ADAL_ALLOW, then we continue to work if there is an ssl-error.
        // CAUTION: allowing SSL-errors (ADAL_OPTION_SSL_ERROR == ADAL_ALLOW) make sense only for debugging purpose.
        //          by default we disallow SSL-errors.
        //          for final product, you need disallow SSL-errors (ADAL_OPTION_SSL_ERROR == ADAL_DISALLOW or ADAL_OPTION_SSL_ERROR == ADAL_DEFAULT)
        ADAL_OPTION_SSL_ERROR = 4,

        // if ADAL_OPTION_FORCE_PROMPT == ADAL_DISALLOW(default), ADAL will use stored cookies and cache for the token.
        // if ADAL_OPTION_FORCE_PROMPT == ADAL_ALLOW, then new requests will not use cache, but they will update cache. Interactive flow will prompt the user for credentials again.
        ADAL_OPTION_FORCE_PROMPT = 8,

        // Note: This option skipped to 32 because aoClientAssertion took the 16 spot over in the AuthOptions in AuthenticationContext.Enums.h.
        // if ADAL_OPTION_INTERNET_OPTION_END_BROWSER_SESSION == ADAL_ALLOW (default), the INTERNET_OPTION_END_BROWSER_SESSION option will be set in WebUIController::Start()
        // if ADAL_OPTION_INTERNET_OPTION_END_BROWSER_SESSION == ADAL_DISALLOW, the INTERNET_OPTION_END_BROWSER_SESSION will not be set in WebUIController::Start() allowing session to carry over
        ADAL_OPTION_INTERNET_OPTION_END_BROWSER_SESSION = 32,

        // if ADAL_OPTION_USE_WAM == ADAL_ALLOW(default), ADAL will use Web Account Manager (starting Windows 10 TH2) to obtain tokens.
        // if ADAL_OPTION_USE_WAM == ADAL_DISALLOW, ADAL will not use Web Account Manager (even on Windows 10 TH2 and beyond) and will communicate with AAD directly.
        ADAL_OPTION_USE_WAM = 64

    } ADAL_OPTION;

    /// <summary>
    /// Struct for ADAL option values
    /// </summary>
    typedef enum ADAL_OPTION_VALUE
    {
        // disallows the option
        ADAL_DISALLOW = 0,

        // allows the option
        ADAL_ALLOW = 1,

        // resets option to default state.
        ADAL_DEFAULT = 2
    } ADAL_OPTION_VALUE;

    /// <summary>
    /// Sets the ADAL options for the given context.
    /// </summary>
    /// <param name="hContext">Authorization context</param>
    /// <param name="option">Option(s) to set</param>
    /// <param name="value">Value to set to option(s)</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetOption( HADALCONTEXT hContext, ADAL_OPTION option, ADAL_OPTION_VALUE value);

    /// <summary>
    /// Gets the ADAL options for the given context.
    /// </summary>
    /// <param name="hContext">Authorization context</param>
    /// <param name="option">Option to get</param>
    /// <param name="value">Pointer to value struct to receive option(s)</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALGetOption( HADALCONTEXT hContext, ADAL_OPTION option, ADAL_OPTION_VALUE* value);

    /// <summary>
    /// Enum for ADAL account type
    /// </summary>
    typedef enum ADAL_ACCOUNT_TYPE
    {
        // Default. Asks ADAL to discover, what mode it uses.
        ADAL_ACCOUNT_TYPE_AUTO_DETECT = 0,

        // Users' accounts managed by STS (AAD or other).
        // STS must support OAuth2 protocol.
        ADAL_ACCOUNT_TYPE_MANAGED = 1,

        // Users' accounts managed by federated IdP.
        // IdP must support WS-trust protocol.
        ADAL_ACCOUNT_TYPE_FEDERATED = 2
    } ADAL_ACCOUNT_TYPE;

    /// <summary>
    /// Sets the ADAL account type for the given context.
    /// </summary>
    /// <param name="hContext">Authorization context</param>
    /// <param name="accountType">Account type to set</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetAccountType( HADALCONTEXT hContext, ADAL_ACCOUNT_TYPE accountType);

    /// <summary>
    /// Gets the ADAL account type for the given context.
    /// </summary>
    /// <param name="hContext">Authorization context</param>
    /// <param name="accountType">Pointer to account type to hold the returned account type</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALGetAccountType( HADALCONTEXT hContext, ADAL_ACCOUNT_TYPE* accountType);

    /// <summary>
    /// Sets the client secret for the given context
    /// </summary>
    /// <param name="hContext">Authorization context</param>
    /// <param name="clientSecret">Client Secret</param>
    /// <returns>
    /// If the function succeeds, the return value is TRUE.
    /// If the function fails, the return value is FALSE. To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetClientSecret( HADALCONTEXT hContext, LPCWSTR clientSecret );

    /// <summary>
    /// Enum indicating the flags for the Certificate Store
    /// </summary>
    typedef enum ADAL_CERT_STORE_TYPE
    {
        ADAL_CERT_STORE_TYPE_USER = CERT_SYSTEM_STORE_CURRENT_USER,
        ADAL_CERT_STORE_TYPE_MACHINE = CERT_SYSTEM_STORE_LOCAL_MACHINE
    } ADAL_CERT_STORE_TYPE;

    /// <summary>
    /// Sets the Client Secret to a self signed JWS token prepared with the Certificate loaded based on the thumbprint
    /// </summary>
    /// <param name="hContext"> The handle to the ADAL Context </param>
    /// <param name="certThumbprint">The thumbprint of the certificate</param>
    /// <param name="certStoreFlags">The location where to find the certificate</param>
    /// <returns>
    /// TRUE If the function succeeds, otherwise FALSE.
    /// To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetClientAssertionUsingCertificateThumbprint(HADALCONTEXT hContext, LPCWSTR certThumbprint, ADAL_CERT_STORE_TYPE certStoreFlags);

    /// <summary>
    /// Gets the Client Secret 
    /// </summary>
    /// <param name="hContext">
    /// The handle to the ADAL Context
    /// </param>
    /// <param name="clientSecret">
    /// The buffer into which the client secret is copied in to
    /// </param>
    /// <param name="clientSecretLength">
    /// This in_out parameter contains the size of the clientSecret buffer and is updated by the method to indicate the size of the client secret
    /// </param>
    /// <returns>
    /// TRUE If the function succeeds, otherwise FALSE.
    /// To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALGetClientSecret( HADALCONTEXT hContext, LPWSTR clientSecret, LPDWORD clientSecretLength );

    /// <summary>
    /// Gets the Formal Authority (issuer) from the authority in the passed in context.
    /// </summary>
    /// <param name="hContext">
    /// The handle to the ADAL Context
    /// </param>
    /// <param name="formalAuthorityUrl">
    /// The issuer url as found from the .well-known/openid-configuration endpoint of the authority in the authentication context.
    /// </param>
    /// <param name="formalAuthorityUrlLength">
    /// The length of the string passed in to the formalAuthorityUrl parameter
    /// </param>
    /// <returns>
    /// TRUE If the function succeeds and the formal authority in the formalAuthority parameter,
    ///  otherwise FALSE and an empty string in the formalAuthority parameter.
    /// To get extended error info call GetLastError()
    /// </returns>
    ADAL_API DWORD WINAPI ADALGetFormalAuthority( HADALCONTEXT hContext, LPWSTR formalAuthorityUrl, LPDWORD formalAuthorityUrlLength );

    /// <summary>
    /// Tells the calling application if Web Accounts Manager(WAM) will be used to obtain the tokens.
    /// WAM functionality exists starting Windows 10 TH2 (Threshold 2)
    /// </summary>
    /// <param name="hContext">
    /// The handle to the ADAL Context. The application can call ADALSetOption with ADAL_OPTION_USE_WAM to overide the default behavior/
    /// The ADAL_OPTION_USE_WAM flag is taken into consideration by the ADALIsWAMUsed API
    /// </param>
    /// <returns>
    /// TRUE if Web Accounts Manager(WAM) is supported and will be used, otherwise FALSE
    /// </returns>
    ADAL_API BOOL WINAPI ADALIsWAMUsed( HADALCONTEXT hContext );

    /// <summary>
    /// Sets the Client Secret to a self signed JWS token prepared with the certificate context that is passed in to the API.
    /// </summary>
    /// <param name="hContext">
    /// The handle to the ADAL Context
    /// </param>
    /// <param name="certThumbprint">
    /// The thumbprint of the certificate
    /// </param>
    /// <param name="pcCertContext">
    /// PCCERT_CONTEXT pointer to the certificate context.
    /// </param>
    /// <returns>
    /// TRUE If the function succeeds, otherwise FALSE.
    /// To get extended error info call GetLastError()
    /// </returns>
    ADAL_API BOOL WINAPI ADALSetClientAssertionUsingCertificateContext(HADALCONTEXT hContext, LPCWSTR certThumbprint, const CERT_CONTEXT* pcCertContext);

#ifdef __cplusplus
}
#endif