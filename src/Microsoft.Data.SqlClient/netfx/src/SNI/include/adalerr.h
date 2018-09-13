//------------------------------------------------------------------------------
// Microsoft Azure Active Directory
// 
// File:        adalerr.h
// Copyright:   Copyright (c) Microsoft Corporation
// Notice:      Microsoft Confidential. For internal use only.
//------------------------------------------------------------------------------
#pragma once

// BEGIN: Http errors (do not add anything in this block, there is space)
//
//  Values are 32 bit values laid out as follows:
//
//   3 3 2 2 2 2 2 2 2 2 2 2 1 1 1 1 1 1 1 1 1 1
//   1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0
//  +---+-+-+-----------------------+-------------------------------+
//  |Sev|C|R|     Facility          |               Code            |
//  +---+-+-+-----------------------+-------------------------------+
//
//  where
//
//      Sev - is the severity code
//
//          00 - Success
//          01 - Informational
//          10 - Warning
//          11 - Error
//
//      C - is the Customer code flag
//
//      R - is a reserved bit
//
//      Facility - is the facility code
//
//      Code - is the facility's status code
//
//
// Define the facility codes
//
#define FACILITY_ADAL_URLMON             0xAA7
#define FACILITY_ADAL_UNEXPECTED         0xAAA
#define FACILITY_ADAL_TOKENBROKER        0xAAB
#define FACILITY_ADAL_SYSTEM             0xAA5
#define FACILITY_ADAL_SERVER             0xAA9
#define FACILITY_ADAL_SERIALIZATION      0xAA4
#define FACILITY_ADAL_PROTOCOL           0xAA2
#define FACILITY_ADAL_JSON               0xAA6
#define FACILITY_ADAL_INTERNET           0xAA8
#define FACILITY_ADAL_HTTP               0xAA3
#define FACILITY_ADAL_DEVELOPER          0xAA1


//
// Define the severity codes
//
#define SEVERITY_ADAL_WARNING            0x2
#define SEVERITY_ADAL_SUCCESS            0x0
#define SEVERITY_ADAL_INFO               0x1
#define SEVERITY_ADAL_ERROR              0x3


//
// MessageId: INFO_HTTP_STATUS_CONTINUE
//
// MessageText:
//
// The request can be continued.
//
#define INFO_HTTP_STATUS_CONTINUE        ((DWORD)0x4AA30064L)

//
// MessageId: INFO_HTTP_STATUS_SWITCH_PROTOCOLS
//
// MessageText:
//
// The server has switched protocols in an upgrade header.
//
#define INFO_HTTP_STATUS_SWITCH_PROTOCOLS ((DWORD)0x4AA30065L)

//
// MessageId: INFO_HTTP_STATUS_1STCLASS_MAX
//
// MessageText:
//
// Unknown HTTP information code.
//
#define INFO_HTTP_STATUS_1STCLASS_MAX    ((DWORD)0x4AA30066L)

//
// MessageId: SUCCESS_HTTP_STATUS_OK
//
// MessageText:
//
// The request completed successfully.
//
#define SUCCESS_HTTP_STATUS_OK           ((DWORD)0x0AA300C8L)

//
// MessageId: SUCCESS_HTTP_STATUS_CREATED
//
// MessageText:
//
// The request has been fulfilled and resulted in the creation of a new resource.
//
#define SUCCESS_HTTP_STATUS_CREATED      ((DWORD)0x0AA300C9L)

//
// MessageId: SUCCESS_HTTP_STATUS_ACCEPTED
//
// MessageText:
//
// The request has been accepted for processing, but the processing has not been completed.
//
#define SUCCESS_HTTP_STATUS_ACCEPTED     ((DWORD)0x0AA300CAL)

//
// MessageId: SUCCESS_HTTP_STATUS_PARTIAL
//
// MessageText:
//
// The returned meta information in the entity-header is not the definitive set available from the origin server.
//
#define SUCCESS_HTTP_STATUS_PARTIAL      ((DWORD)0x0AA300CBL)

//
// MessageId: SUCCESS_HTTP_STATUS_NO_CONTENT
//
// MessageText:
//
// The server has fulfilled the request, but there is no new information to send back.
//
#define SUCCESS_HTTP_STATUS_NO_CONTENT   ((DWORD)0x0AA300CCL)

//
// MessageId: SUCCESS_HTTP_STATUS_RESET_CONTENT
//
// MessageText:
//
// The request has been completed, and the client program should reset the document view that caused the request to be sent to allow the user to easily initiate another input action.
//
#define SUCCESS_HTTP_STATUS_RESET_CONTENT ((DWORD)0x0AA300CDL)

//
// MessageId: SUCCESS_HTTP_STATUS_PARTIAL_CONTENT
//
// MessageText:
//
// The server has fulfilled the partial GET request for the resource.
//
#define SUCCESS_HTTP_STATUS_PARTIAL_CONTENT ((DWORD)0x0AA300CEL)

//
// MessageId: SUCCESS_HTTP_STATUS_2NDCLASS_MAX
//
// MessageText:
//
// Unknown HTTP success code.
//
#define SUCCESS_HTTP_STATUS_2NDCLASS_MAX ((DWORD)0x0AA300CFL)

//
// MessageId: ERROR_HTTP_STATUS_AMBIGUOUS
//
// MessageText:
//
// The server couldn't decide what to return.
//
#define ERROR_HTTP_STATUS_AMBIGUOUS      ((DWORD)0xCAA3012CL)

//
// MessageId: ERROR_HTTP_STATUS_MOVED
//
// MessageText:
//
// The requested resource has been assigned to a new permanent URI (Uniform Resource Identifier), and any future references to this resource should be done using one of the returned URIs.
//
#define ERROR_HTTP_STATUS_MOVED          ((DWORD)0xCAA3012DL)

//
// MessageId: ERROR_HTTP_STATUS_REDIRECT
//
// MessageText:
//
// The requested resource resides temporarily under a different URI (Uniform Resource Identifier).
//
#define ERROR_HTTP_STATUS_REDIRECT       ((DWORD)0xCAA3012EL)

//
// MessageId: ERROR_HTTP_STATUS_REDIRECT_METHOD
//
// MessageText:
//
// The response to the request can be found under a different URI (Uniform Resource Identifier) and should be retrieved using a GET HTTP verb on that resource.
//
#define ERROR_HTTP_STATUS_REDIRECT_METHOD ((DWORD)0xCAA3012FL)

//
// MessageId: ERROR_HTTP_STATUS_NOT_MODIFIED
//
// MessageText:
//
// The requested resource has not been modified.
//
#define ERROR_HTTP_STATUS_NOT_MODIFIED   ((DWORD)0xCAA30130L)

//
// MessageId: ERROR_HTTP_STATUS_USE_PROXY
//
// MessageText:
//
// The requested resource must be accessed through the proxy given by the location field.
//
#define ERROR_HTTP_STATUS_USE_PROXY      ((DWORD)0xCAA30131L)

//
// MessageId: ERROR_HTTP_STATUS_REDIRECT_KEEP_VERB
//
// MessageText:
//
// The redirected request keeps the same HTTP verb. HTTP/1.1 behavior.
//
#define ERROR_HTTP_STATUS_REDIRECT_KEEP_VERB ((DWORD)0xCAA30133L)

//
// MessageId: ERROR_HTTP_STATUS_3RDCLASS_MAX
//
// MessageText:
//
// Unknown HTTP error.
//
#define ERROR_HTTP_STATUS_3RDCLASS_MAX   ((DWORD)0xCAA30134L)

//
// MessageId: ERROR_HTTP_STATUS_BAD_REQUEST
//
// MessageText:
//
// The request could not be processed by the server due to invalid syntax.
//
#define ERROR_HTTP_STATUS_BAD_REQUEST    ((DWORD)0xCAA30190L)

//
// MessageId: ERROR_HTTP_STATUS_DENIED
//
// MessageText:
//
// The requested resource requires user authentication.
//
#define ERROR_HTTP_STATUS_DENIED         ((DWORD)0xCAA30191L)

//
// MessageId: ERROR_HTTP_STATUS_PAYMENT_REQ
//
// MessageText:
//
// Not currently implemented in the HTTP protocol.
//
#define ERROR_HTTP_STATUS_PAYMENT_REQ    ((DWORD)0xCAA30192L)

//
// MessageId: ERROR_HTTP_STATUS_FORBIDDEN
//
// MessageText:
//
// The server understood the request, but is refusing to fulfill it.
//
#define ERROR_HTTP_STATUS_FORBIDDEN      ((DWORD)0xCAA30193L)

//
// MessageId: ERROR_HTTP_STATUS_NOT_FOUND
//
// MessageText:
//
// The server has not found anything matching the requested URI (Uniform Resource Identifier).
//
#define ERROR_HTTP_STATUS_NOT_FOUND      ((DWORD)0xCAA30194L)

//
// MessageId: ERROR_HTTP_STATUS_BAD_METHOD
//
// MessageText:
//
// The HTTP verb used is not allowed.
//
#define ERROR_HTTP_STATUS_BAD_METHOD     ((DWORD)0xCAA30195L)

//
// MessageId: ERROR_HTTP_STATUS_NONE_ACCEPTABLE
//
// MessageText:
//
// No responses acceptable to the client were found.
//
#define ERROR_HTTP_STATUS_NONE_ACCEPTABLE ((DWORD)0xCAA30196L)

//
// MessageId: ERROR_HTTP_STATUS_PROXY_AUTH_REQ
//
// MessageText:
//
// Proxy authentication required.
//
#define ERROR_HTTP_STATUS_PROXY_AUTH_REQ ((DWORD)0xCAA30197L)

//
// MessageId: ERROR_HTTP_STATUS_REQUEST_TIMEOUT
//
// MessageText:
//
// The server timed out waiting for the request.
//
#define ERROR_HTTP_STATUS_REQUEST_TIMEOUT ((DWORD)0xCAA30198L)

//
// MessageId: ERROR_HTTP_STATUS_CONFLICT
//
// MessageText:
//
// The request could not be completed due to a conflict with the current state of the resource. The user should resubmit with more information.
//
#define ERROR_HTTP_STATUS_CONFLICT       ((DWORD)0xCAA30199L)

//
// MessageId: ERROR_HTTP_STATUS_GONE
//
// MessageText:
//
// The requested resource is no longer available at the server, and no forwarding address is known.
//
#define ERROR_HTTP_STATUS_GONE           ((DWORD)0xCAA3019AL)

//
// MessageId: ERROR_HTTP_STATUS_LENGTH_REQUIRED
//
// MessageText:
//
// The server refuses to accept the request without a defined content length.
//
#define ERROR_HTTP_STATUS_LENGTH_REQUIRED ((DWORD)0xCAA3019BL)

//
// MessageId: ERROR_HTTP_STATUS_PRECOND_FAILED
//
// MessageText:
//
// The precondition given in one or more of the request header fields evaluated to false when it was tested on the server.
//
#define ERROR_HTTP_STATUS_PRECOND_FAILED ((DWORD)0xCAA3019CL)

//
// MessageId: ERROR_HTTP_STATUS_REQUEST_TOO_LARGE
//
// MessageText:
//
// The server is refusing to process a request because the request entity is larger than the server is willing or able to process.
//
#define ERROR_HTTP_STATUS_REQUEST_TOO_LARGE ((DWORD)0xCAA3019DL)

//
// MessageId: ERROR_HTTP_STATUS_URI_TOO_LONG
//
// MessageText:
//
// The server is refusing to service the request because the request URI (Uniform Resource Identifier) is longer than the server is willing to interpret.
//
#define ERROR_HTTP_STATUS_URI_TOO_LONG   ((DWORD)0xCAA3019EL)

//
// MessageId: ERROR_HTTP_STATUS_UNSUPPORTED_MEDIA
//
// MessageText:
//
// The server is refusing to service the request because the entity of the request is in a format not supported by the requested resource for the requested method.
//
#define ERROR_HTTP_STATUS_UNSUPPORTED_MEDIA ((DWORD)0xCAA3019FL)

//
// MessageId: ERROR_HTTP_STATUS_4THCLASS_MAX
//
// MessageText:
//
// Unknown HTTP error.
//
#define ERROR_HTTP_STATUS_4THCLASS_MAX   ((DWORD)0xCAA301A0L)

//
// MessageId: ERROR_HTTP_STATUS_RETRY_WITH
//
// MessageText:
//
// The request should be retried after doing the appropriate action.
//
#define ERROR_HTTP_STATUS_RETRY_WITH     ((DWORD)0xCAA301C1L)

//
// MessageId: ERROR_HTTP_STATUS_SERVER_ERROR
//
// MessageText:
//
// The server encountered an unexpected condition that prevented it from fulfilling the request.
//
#define ERROR_HTTP_STATUS_SERVER_ERROR   ((DWORD)0xCAA301F4L)

//
// MessageId: ERROR_HTTP_STATUS_NOT_SUPPORTED
//
// MessageText:
//
// The server does not support the functionality required to fulfill the request.
//
#define ERROR_HTTP_STATUS_NOT_SUPPORTED  ((DWORD)0xCAA301F5L)

//
// MessageId: ERROR_HTTP_STATUS_BAD_GATEWAY
//
// MessageText:
//
// The server, while acting as a gateway or proxy, received an invalid response from the upstream server it accessed in attempting to fulfill the request.
//
#define ERROR_HTTP_STATUS_BAD_GATEWAY    ((DWORD)0xCAA301F6L)

//
// MessageId: ERROR_HTTP_STATUS_SERVICE_UNAVAIL
//
// MessageText:
//
// The service is temporarily overloaded.
//
#define ERROR_HTTP_STATUS_SERVICE_UNAVAIL ((DWORD)0xCAA301F7L)

//
// MessageId: ERROR_HTTP_STATUS_GATEWAY_TIMEOUT
//
// MessageText:
//
// The request was timed out waiting for a gateway.
//
#define ERROR_HTTP_STATUS_GATEWAY_TIMEOUT ((DWORD)0xCAA301F8L)

//
// MessageId: ERROR_HTTP_STATUS_VERSION_NOT_SUP
//
// MessageText:
//
// The server does not support, or refuses to support, the HTTP protocol version that was used in the request message.
//
#define ERROR_HTTP_STATUS_VERSION_NOT_SUP ((DWORD)0xCAA301F9L)

//
// MessageId: ERROR_HTTP_STATUS_5THCLASS_MAX
//
// MessageText:
//
// Unknown HTTP error.
//
#define ERROR_HTTP_STATUS_5THCLASS_MAX   ((DWORD)0xCAA301FAL)

// END: Http errors 
//
// MessageId: ERROR_INET_INVALID_URL
//
// MessageText:
//
// The URL could not be parsed.
//
#define ERROR_INET_INVALID_URL           ((DWORD)0xCAA70001L)

//
// MessageId: ERROR_INET_NO_SESSION
//
// MessageText:
//
// No Internet session was established.
//
#define ERROR_INET_NO_SESSION            ((DWORD)0xCAA70002L)

//
// MessageId: ERROR_INET_CANNOT_CONNECT
//
// MessageText:
//
// The attempt to connect to the Internet has failed.
//
#define ERROR_INET_CANNOT_CONNECT        ((DWORD)0xCAA70003L)

//
// MessageId: ERROR_INET_RESOURCE_NOT_FOUND
//
// MessageText:
//
// The server or proxy was not found.
//
#define ERROR_INET_RESOURCE_NOT_FOUND    ((DWORD)0xCAA70004L)

//
// MessageId: ERROR_INET_OBJECT_NOT_FOUND
//
// MessageText:
//
// The object was not found.
//
#define ERROR_INET_OBJECT_NOT_FOUND      ((DWORD)0xCAA70005L)

//
// MessageId: ERROR_INET_DATA_NOT_AVAILABLE
//
// MessageText:
//
// An Internet connection was established, but the data cannot be retrieved.
//
#define ERROR_INET_DATA_NOT_AVAILABLE    ((DWORD)0xCAA70006L)

//
// MessageId: ERROR_INET_DOWNLOAD_FAILURE
//
// MessageText:
//
// The download has failed (the connection was interrupted).
//
#define ERROR_INET_DOWNLOAD_FAILURE      ((DWORD)0xCAA70007L)

//
// MessageId: ERROR_INET_AUTHENTICATION_REQUIRED
//
// MessageText:
//
// Authentication is needed to access the object.
//
#define ERROR_INET_AUTHENTICATION_REQUIRED ((DWORD)0xCAA70008L)

//
// MessageId: ERROR_INET_NO_VALID_MEDIA
//
// MessageText:
//
// The object is not in one of the acceptable MIME types.
//
#define ERROR_INET_NO_VALID_MEDIA        ((DWORD)0xCAA70009L)

//
// MessageId: ERROR_INET_CONNECTION_TIMEOUT
//
// MessageText:
//
// The Internet connection has timed out.
//
#define ERROR_INET_CONNECTION_TIMEOUT    ((DWORD)0xCAA7000AL)

//
// MessageId: ERROR_INET_INVALID_REQUEST
//
// MessageText:
//
// The request was invalid.
//
#define ERROR_INET_INVALID_REQUEST       ((DWORD)0xCAA7000BL)

//
// MessageId: ERROR_INET_UNKNOWN_PROTOCOL
//
// MessageText:
//
// The protocol is not known and no pluggable protocols have been entered that match.
//
#define ERROR_INET_UNKNOWN_PROTOCOL      ((DWORD)0xCAA7000CL)

//
// MessageId: ERROR_INET_SECURITY_PROBLEM
//
// MessageText:
//
// A security problem was encountered. Please check settings for IE.
//
#define ERROR_INET_SECURITY_PROBLEM      ((DWORD)0xCAA7000DL)

//
// MessageId: ERROR_INET_CANNOT_LOAD_DATA
//
// MessageText:
//
// The object could not be loaded.
//
#define ERROR_INET_CANNOT_LOAD_DATA      ((DWORD)0xCAA7000EL)

//
// MessageId: ERROR_INET_CANNOT_INSTANTIATE_OBJECT
//
// MessageText:
//
// CoCreateInstance failed.
//
#define ERROR_INET_CANNOT_INSTANTIATE_OBJECT ((DWORD)0xCAA7000FL)

//
// MessageId: ERROR_INET_INVALID_CERTIFICATE
//
// MessageText:
//
// Certificate is invalid.
//
#define ERROR_INET_INVALID_CERTIFICATE   ((DWORD)0xCAA70010L)

//
// MessageId: ERROR_INET_REDIRECT_FAILED
//
// MessageText:
//
// Redirect request failed.
//
#define ERROR_INET_REDIRECT_FAILED       ((DWORD)0xCAA70011L)

//
// MessageId: ERROR_INET_REDIRECT_TO_DIR
//
// MessageText:
//
// The request is being redirected to a directory.
//
#define ERROR_INET_REDIRECT_TO_DIR       ((DWORD)0xCAA70012L)

//
// MessageId: ERROR_INET_CANNOT_LOCK_REQUEST
//
// MessageText:
//
// The requested resource could not be locked.
//
#define ERROR_INET_CANNOT_LOCK_REQUEST   ((DWORD)0xCAA70013L)

//
// MessageId: ERROR_INET_CODE_DOWNLOAD_DECLINED
//
// MessageText:
//
// The component download was declined by the user.
//
#define ERROR_INET_CODE_DOWNLOAD_DECLINED ((DWORD)0xCAA70014L)

//
// MessageId: ERROR_INET_RESULT_DISPATCHED
//
// MessageText:
//
// The binding has already been completed and the result has been dispatched, so your abort call has been canceled.
//
#define ERROR_INET_RESULT_DISPATCHED     ((DWORD)0xCAA70015L)

//
// MessageId: ERROR_INET_CANNOT_REPLACE_SFP_FILE
//
// MessageText:
//
// The exact version requested by a component download cannot be found.
//
#define ERROR_INET_CANNOT_REPLACE_SFP_FILE ((DWORD)0xCAA70016L)

//
// Internet API error returns
//
//
// MessageId: ERROR_ADAL_INTERNET_OUT_OF_HANDLES
//
// MessageText:
//
// No more handles could be generated at this time.
//
#define ERROR_ADAL_INTERNET_OUT_OF_HANDLES ((DWORD)0xCAA82EE1L)

//
// MessageId: ERROR_ADAL_INTERNET_TIMEOUT
//
// MessageText:
//
// The request has timed out.
//
#define ERROR_ADAL_INTERNET_TIMEOUT      ((DWORD)0xCAA82EE2L)

//
// MessageId: ERROR_ADAL_INTERNET_EXTENDED_ERROR
//
// MessageText:
//
// An extended error was returned from the server. This is typically a string or buffer containing a verbose error message. Call InternetGetLastResponseInfo to retrieve the error text.
//
#define ERROR_ADAL_INTERNET_EXTENDED_ERROR ((DWORD)0xCAA82EE3L)

//
// MessageId: ERROR_ADAL_INTERNET_INTERNAL_ERROR
//
// MessageText:
//
// An internal error has occurred.
//
#define ERROR_ADAL_INTERNET_INTERNAL_ERROR ((DWORD)0xCAA82EE4L)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_URL
//
// MessageText:
//
// An internal error has occurred.
//
#define ERROR_ADAL_INTERNET_INVALID_URL  ((DWORD)0xCAA82EE5L)

//
// MessageId: ERROR_ADAL_INTERNET_UNRECOGNIZED_SCHEME
//
// MessageText:
//
// The URL scheme could not be recognized, or is not supported.
//
#define ERROR_ADAL_INTERNET_UNRECOGNIZED_SCHEME ((DWORD)0xCAA82EE6L)

//
// MessageId: ERROR_ADAL_INTERNET_NAME_NOT_RESOLVED
//
// MessageText:
//
// The server name could not be resolved.
//
#define ERROR_ADAL_INTERNET_NAME_NOT_RESOLVED ((DWORD)0xCAA82EE7L)

//
// MessageId: ERROR_ADAL_INTERNET_PROTOCOL_NOT_FOUND
//
// MessageText:
//
// The requested protocol could not be located.
//
#define ERROR_ADAL_INTERNET_PROTOCOL_NOT_FOUND ((DWORD)0xCAA82EE8L)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_OPTION
//
// MessageText:
//
// A request to InternetQueryOption or InternetSetOption specified an invalid option value.
//
#define ERROR_ADAL_INTERNET_INVALID_OPTION ((DWORD)0xCAA82EE9L)

//
// MessageId: ERROR_ADAL_INTERNET_BAD_OPTION_LENGTH
//
// MessageText:
//
// The length of an option supplied to InternetQueryOption or InternetSetOption is incorrect for the type of option specified.
//
#define ERROR_ADAL_INTERNET_BAD_OPTION_LENGTH ((DWORD)0xCAA82EEAL)

//
// MessageId: ERROR_ADAL_INTERNET_OPTION_NOT_SETTABLE
//
// MessageText:
//
// The requested option cannot be set, only queried.
//
#define ERROR_ADAL_INTERNET_OPTION_NOT_SETTABLE ((DWORD)0xCAA82EEBL)

//
// MessageId: ERROR_ADAL_INTERNET_SHUTDOWN
//
// MessageText:
//
// Internet API support is being shut down or unloaded.
//
#define ERROR_ADAL_INTERNET_SHUTDOWN     ((DWORD)0xCAA82EECL)

//
// MessageId: ERROR_ADAL_INTERNET_INCORRECT_USER_NAME
//
// MessageText:
//
// Supplied username is not correct.
//
#define ERROR_ADAL_INTERNET_INCORRECT_USER_NAME ((DWORD)0xCAA82EEDL)

//
// MessageId: ERROR_ADAL_INTERNET_INCORRECT_PASSWORD
//
// MessageText:
//
// Supplied password is not correct.
//
#define ERROR_ADAL_INTERNET_INCORRECT_PASSWORD ((DWORD)0xCAA82EEEL)

//
// MessageId: ERROR_ADAL_INTERNET_LOGIN_FAILURE
//
// MessageText:
//
// The request to connect and log on to an server failed.
//
#define ERROR_ADAL_INTERNET_LOGIN_FAILURE ((DWORD)0xCAA82EEFL)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_OPERATION
//
// MessageText:
//
// The requested operation is invalid.
//
#define ERROR_ADAL_INTERNET_INVALID_OPERATION ((DWORD)0xCAA82EF0L)

//
// MessageId: ERROR_ADAL_INTERNET_OPERATION_CANCELLED
//
// MessageText:
//
// The requested operation is cancelled.
//
#define ERROR_ADAL_INTERNET_OPERATION_CANCELLED ((DWORD)0xCAA82EF1L)

//
// MessageId: ERROR_ADAL_INTERNET_INCORRECT_HANDLE_TYPE
//
// MessageText:
//
// The type of handle supplied is incorrect for this operation.
//
#define ERROR_ADAL_INTERNET_INCORRECT_HANDLE_TYPE ((DWORD)0xCAA82EF2L)

//
// MessageId: ERROR_ADAL_INTERNET_INCORRECT_HANDLE_STATE
//
// MessageText:
//
// The requested operation cannot be carried out because the handle supplied is not in the correct state.
//
#define ERROR_ADAL_INTERNET_INCORRECT_HANDLE_STATE ((DWORD)0xCAA82EF3L)

//
// MessageId: ERROR_ADAL_INTERNET_NOT_PROXY_REQUEST
//
// MessageText:
//
// The request cannot be made via a proxy.
//
#define ERROR_ADAL_INTERNET_NOT_PROXY_REQUEST ((DWORD)0xCAA82EF4L)

//
// MessageId: ERROR_ADAL_INTERNET_REGISTRY_VALUE_NOT_FOUND
//
// MessageText:
//
// A required registry value could not be located.
//
#define ERROR_ADAL_INTERNET_REGISTRY_VALUE_NOT_FOUND ((DWORD)0xCAA82EF5L)

//
// MessageId: ERROR_ADAL_INTERNET_BAD_REGISTRY_PARAMETER
//
// MessageText:
//
// A required registry value was located but is an incorrect type or has an invalid value.
//
#define ERROR_ADAL_INTERNET_BAD_REGISTRY_PARAMETER ((DWORD)0xCAA82EF6L)

//
// MessageId: ERROR_ADAL_INTERNET_NO_DIRECT_ACCESS
//
// MessageText:
//
// Direct network access cannot be made at this time.
//
#define ERROR_ADAL_INTERNET_NO_DIRECT_ACCESS ((DWORD)0xCAA82EF7L)

//
// MessageId: ERROR_ADAL_INTERNET_NO_CONTEXT
//
// MessageText:
//
// An asynchronous request could not be made because a zero context value was supplied.
//
#define ERROR_ADAL_INTERNET_NO_CONTEXT   ((DWORD)0xCAA82EF8L)

//
// MessageId: ERROR_ADAL_INTERNET_NO_CALLBACK
//
// MessageText:
//
// An asynchronous request could not be made because a callback function has not been set.
//
#define ERROR_ADAL_INTERNET_NO_CALLBACK  ((DWORD)0xCAA82EF9L)

//
// MessageId: ERROR_ADAL_INTERNET_REQUEST_PENDING
//
// MessageText:
//
// The required operation could not be completed because one or more requests are pending.
//
#define ERROR_ADAL_INTERNET_REQUEST_PENDING ((DWORD)0xCAA82EFAL)

//
// MessageId: ERROR_ADAL_INTERNET_INCORRECT_FORMAT
//
// MessageText:
//
// The format of the request is invalid.
//
#define ERROR_ADAL_INTERNET_INCORRECT_FORMAT ((DWORD)0xCAA82EFBL)

//
// MessageId: ERROR_ADAL_INTERNET_ITEM_NOT_FOUND
//
// MessageText:
//
// The requested item could not be located.
//
#define ERROR_ADAL_INTERNET_ITEM_NOT_FOUND ((DWORD)0xCAA82EFCL)

//
// MessageId: ERROR_ADAL_INTERNET_CANNOT_CONNECT
//
// MessageText:
//
// The attempt to connect to the server failed.
//
#define ERROR_ADAL_INTERNET_CANNOT_CONNECT ((DWORD)0xCAA82EFDL)

//
// MessageId: ERROR_ADAL_INTERNET_CONNECTION_ABORTED
//
// MessageText:
//
// The connection with the server has been terminated.
//
#define ERROR_ADAL_INTERNET_CONNECTION_ABORTED ((DWORD)0xCAA82EFEL)

//
// MessageId: ERROR_ADAL_INTERNET_CONNECTION_RESET
//
// MessageText:
//
// The connection with the server has been reset.
//
#define ERROR_ADAL_INTERNET_CONNECTION_RESET ((DWORD)0xCAA82EFFL)

//
// MessageId: ERROR_ADAL_INTERNET_FORCE_RETRY
//
// MessageText:
//
// The function needs to redo the request.
//
#define ERROR_ADAL_INTERNET_FORCE_RETRY  ((DWORD)0xCAA82F00L)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_PROXY_REQUEST
//
// MessageText:
//
// The request to the proxy was invalid.
//
#define ERROR_ADAL_INTERNET_INVALID_PROXY_REQUEST ((DWORD)0xCAA82F01L)

//
// MessageId: ERROR_ADAL_INTERNET_NEED_UI
//
// MessageText:
//
// A user interface or other blocking operation has been requested.
//
#define ERROR_ADAL_INTERNET_NEED_UI      ((DWORD)0xCAA82F02L)

//
// MessageId: ERROR_ADAL_INTERNET_HANDLE_EXISTS
//
// MessageText:
//
// The request failed because the handle already exists.
//
#define ERROR_ADAL_INTERNET_HANDLE_EXISTS ((DWORD)0xCAA82F04L)

//
// MessageId: ERROR_ADAL_INTERNET_SEC_CERT_DATE_INVALID
//
// MessageText:
//
// SSL certificate date that was received from the server is bad. The certificate is expired.
//
#define ERROR_ADAL_INTERNET_SEC_CERT_DATE_INVALID ((DWORD)0xCAA82F05L)

//
// MessageId: ERROR_ADAL_INTERNET_SEC_CERT_CN_INVALID
//
// MessageText:
//
// SSL certificate common name (host name field) is incorrect-for example, if you entered www.server.com and the common name on the certificate says www.different.com.
//
#define ERROR_ADAL_INTERNET_SEC_CERT_CN_INVALID ((DWORD)0xCAA82F06L)

//
// MessageId: ERROR_ADAL_INTERNET_HTTP_TO_HTTPS_ON_REDIR
//
// MessageText:
//
// The application is moving from a non-SSL to an SSL connection because of a redirect.
//
#define ERROR_ADAL_INTERNET_HTTP_TO_HTTPS_ON_REDIR ((DWORD)0xCAA82F07L)

//
// MessageId: ERROR_ADAL_INTERNET_HTTPS_TO_HTTP_ON_REDIR
//
// MessageText:
//
// The application is moving from an SSL to an non-SSL connection because of a redirect.
//
#define ERROR_ADAL_INTERNET_HTTPS_TO_HTTP_ON_REDIR ((DWORD)0xCAA82F08L)

//
// MessageId: ERROR_ADAL_INTERNET_MIXED_SECURITY
//
// MessageText:
//
// The content is not entirely secure. Some of the content being viewed may have come from unsecured servers.
//
#define ERROR_ADAL_INTERNET_MIXED_SECURITY ((DWORD)0xCAA82F09L)

//
// MessageId: ERROR_ADAL_INTERNET_CHG_POST_IS_NON_SECURE
//
// MessageText:
//
// The application is posting and attempting to change multiple lines of text on a server that is not secure.
//
#define ERROR_ADAL_INTERNET_CHG_POST_IS_NON_SECURE ((DWORD)0xCAA82F0AL)

//
// MessageId: ERROR_ADAL_INTERNET_POST_IS_NON_SECURE
//
// MessageText:
//
// The application is posting data to a server that is not secure.
//
#define ERROR_ADAL_INTERNET_POST_IS_NON_SECURE ((DWORD)0xCAA82F0BL)

//
// MessageId: ERROR_ADAL_INTERNET_CLIENT_AUTH_CERT_NEEDED
//
// MessageText:
//
// The server is requesting client authentication.
//
#define ERROR_ADAL_INTERNET_CLIENT_AUTH_CERT_NEEDED ((DWORD)0xCAA82F0CL)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_CA
//
// MessageText:
//
// The function is unfamiliar with the Certificate Authority that generated the server's certificate.
//
#define ERROR_ADAL_INTERNET_INVALID_CA   ((DWORD)0xCAA82F0DL)

//
// MessageId: ERROR_ADAL_INTERNET_CLIENT_AUTH_NOT_SETUP
//
// MessageText:
//
// Client authorization is not set up on this computer.
//
#define ERROR_ADAL_INTERNET_CLIENT_AUTH_NOT_SETUP ((DWORD)0xCAA82F0EL)

//
// MessageId: ERROR_ADAL_INTERNET_ASYNC_THREAD_FAILED
//
// MessageText:
//
// The application could not start an asynchronous thread.
//
#define ERROR_ADAL_INTERNET_ASYNC_THREAD_FAILED ((DWORD)0xCAA82F0FL)

//
// MessageId: ERROR_ADAL_INTERNET_REDIRECT_SCHEME_CHANGE
//
// MessageText:
//
// The function could not handle the redirection, because the scheme changed (for example, HTTP to FTP).
//
#define ERROR_ADAL_INTERNET_REDIRECT_SCHEME_CHANGE ((DWORD)0xCAA82F10L)

//
// MessageId: ERROR_ADAL_INTERNET_DIALOG_PENDING
//
// MessageText:
//
// Another thread has a password dialog box in progress.
//
#define ERROR_ADAL_INTERNET_DIALOG_PENDING ((DWORD)0xCAA82F11L)

//
// MessageId: ERROR_ADAL_INTERNET_RETRY_DIALOG
//
// MessageText:
//
// The dialog box should be retried.
//
#define ERROR_ADAL_INTERNET_RETRY_DIALOG ((DWORD)0xCAA82F12L)

//
// MessageId: ERROR_ADAL_INTERNET_HTTPS_HTTP_SUBMIT_REDIR
//
// MessageText:
//
// The data being submitted to an SSL connection is being redirected to a non-SSL connection.
//
#define ERROR_ADAL_INTERNET_HTTPS_HTTP_SUBMIT_REDIR ((DWORD)0xCAA82F14L)

//
// MessageId: ERROR_ADAL_INTERNET_INSERT_CDROM
//
// MessageText:
//
// The request requires a CD-ROM to be inserted in the CD-ROM drive to locate the resource requested.
//
#define ERROR_ADAL_INTERNET_INSERT_CDROM ((DWORD)0xCAA82F15L)

//
// MessageId: ERROR_ADAL_INTERNET_FORTEZZA_LOGIN_NEEDED
//
// MessageText:
//
// The requested resource requires Fortezza authentication.
//
#define ERROR_ADAL_INTERNET_FORTEZZA_LOGIN_NEEDED ((DWORD)0xCAA82F16L)

//
// MessageId: ERROR_ADAL_INTERNET_SEC_CERT_ERRORS
//
// MessageText:
//
// The SSL certificate contains errors.
//
#define ERROR_ADAL_INTERNET_SEC_CERT_ERRORS ((DWORD)0xCAA82F17L)

//
// MessageId: ERROR_ADAL_INTERNET_SEC_CERT_NO_REV
//
// MessageText:
//
// The SSL certificate was not revoked.
//
#define ERROR_ADAL_INTERNET_SEC_CERT_NO_REV ((DWORD)0xCAA82F18L)

//
// MessageId: ERROR_ADAL_INTERNET_SEC_CERT_REV_FAILED
//
// MessageText:
//
// Revocation of the SSL certificate failed.
//
#define ERROR_ADAL_INTERNET_SEC_CERT_REV_FAILED ((DWORD)0xCAA82F19L)

//
// MessageId: ERROR_ADAL_INTERNET_CANNOT_CALL_BEFORE_OPEN
//
// MessageText:
//
// Returned by the HttpRequest object if a requested operation cannot be performed before calling the Open method.
//
#define ERROR_ADAL_INTERNET_CANNOT_CALL_BEFORE_OPEN ((DWORD)0xCAA82F44L)

//
// MessageId: ERROR_ADAL_INTERNET_CANNOT_CALL_BEFORE_SEND
//
// MessageText:
//
// Returned by the HttpRequest object if a requested operation cannot be performed before calling the Send method.
//
#define ERROR_ADAL_INTERNET_CANNOT_CALL_BEFORE_SEND ((DWORD)0xCAA82F45L)

//
// MessageId: ERROR_ADAL_INTERNET_CANNOT_CALL_AFTER_SEND
//
// MessageText:
//
// Returned by the HttpRequest object if a requested operation cannot be performed after calling the Send method.
//
#define ERROR_ADAL_INTERNET_CANNOT_CALL_AFTER_SEND ((DWORD)0xCAA82F46L)

//
// MessageId: ERROR_ADAL_INTERNET_CANNOT_CALL_AFTER_OPEN
//
// MessageText:
//
// Returned by the HttpRequest object if a specified option cannot be requested after the Open method has been called.
//
#define ERROR_ADAL_INTERNET_CANNOT_CALL_AFTER_OPEN ((DWORD)0xCAA82F47L)

//
// MessageId: ERROR_ADAL_INTERNET_HEADER_NOT_FOUND
//
// MessageText:
//
// The requested header could not be located.
//
#define ERROR_ADAL_INTERNET_HEADER_NOT_FOUND ((DWORD)0xCAA82F76L)

//
// MessageId: ERROR_ADAL_INTERNET_DOWNLEVEL_SERVER
//
// MessageText:
//
// The server did not return any headers.
//
#define ERROR_ADAL_INTERNET_DOWNLEVEL_SERVER ((DWORD)0xCAA82F77L)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_SERVER_RESPONSE
//
// MessageText:
//
// The server response could not be parsed.
//
#define ERROR_ADAL_INTERNET_INVALID_SERVER_RESPONSE ((DWORD)0xCAA82F78L)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_HEADER
//
// MessageText:
//
// The supplied header is invalid.
//
#define ERROR_ADAL_INTERNET_INVALID_HEADER ((DWORD)0xCAA82F79L)

//
// MessageId: ERROR_ADAL_INTERNET_INVALID_QUERY_REQUEST
//
// MessageText:
//
// The request made to HttpQueryInfo is invalid.
//
#define ERROR_ADAL_INTERNET_INVALID_QUERY_REQUEST ((DWORD)0xCAA82F7AL)

//
// MessageId: ERROR_ADAL_INTERNET_HEADER_ALREADY_EXISTS
//
// MessageText:
//
// The header could not be added because it already exists.
//
#define ERROR_ADAL_INTERNET_HEADER_ALREADY_EXISTS ((DWORD)0xCAA82F7BL)

//
// MessageId: ERROR_ADAL_INTERNET_REDIRECT_FAILED
//
// MessageText:
//
// The redirection failed.
//
#define ERROR_ADAL_INTERNET_REDIRECT_FAILED ((DWORD)0xCAA82F7CL)

//
// MessageId: ERROR_ADAL_INTERNET_SECURE_CHANNEL_ERROR
//
// MessageText:
//
// Indicates that an error occurred having to do with a secure channel.
//
#define ERROR_ADAL_INTERNET_SECURE_CHANNEL_ERROR ((DWORD)0xCAA82F7DL)

//
// MessageId: ERROR_ADAL_INTERNET_NOT_REDIRECTED
//
// MessageText:
//
// The HTTP request was not redirected.
//
#define ERROR_ADAL_INTERNET_NOT_REDIRECTED ((DWORD)0xCAA82F80L)

//
// MessageId: ERROR_ADAL_INTERNET_COOKIE_NEEDS_CONFIRMATION
//
// MessageText:
//
// The HTTP cookie requires confirmation.
//
#define ERROR_ADAL_INTERNET_COOKIE_NEEDS_CONFIRMATION ((DWORD)0xCAA82F81L)

//
// MessageId: ERROR_ADAL_INTERNET_COOKIE_DECLINED
//
// MessageText:
//
// The HTTP cookie was declined by the server.
//
#define ERROR_ADAL_INTERNET_COOKIE_DECLINED ((DWORD)0xCAA82F82L)

//
// MessageId: ERROR_ADAL_INTERNET_BAD_AUTO_PROXY_SCRIPT
//
// MessageText:
//
// An error occurred executing the script code in the Proxy Auto-Configuration (PAC) file.
//
#define ERROR_ADAL_INTERNET_BAD_AUTO_PROXY_SCRIPT ((DWORD)0xCAA82F86L)

//
// MessageId: ERROR_ADAL_INTERNET_UNABLE_TO_DOWNLOAD_SCRIPT
//
// MessageText:
//
// The PAC file cannot be downloaded. For example, the server referenced by the PAC URL may not have been reachable, or the server returned a 404 NOT FOUND response.
//
#define ERROR_ADAL_INTERNET_UNABLE_TO_DOWNLOAD_SCRIPT ((DWORD)0xCAA82F87L)

//
// MessageId: ERROR_ADAL_INTERNET_REDIRECT_NEEDS_CONFIRMATION
//
// MessageText:
//
// The redirection requires user confirmation.
//
#define ERROR_ADAL_INTERNET_REDIRECT_NEEDS_CONFIRMATION ((DWORD)0xCAA82F88L)

//
// MessageId: ERROR_ADAL_INTERNET_SECURE_INVALID_CERT
//
// MessageText:
//
// Indicates that a certificate is invalid.
//
#define ERROR_ADAL_INTERNET_SECURE_INVALID_CERT ((DWORD)0xCAA82F89L)

//
// MessageId: ERROR_ADAL_INTERNET_SECURE_CERT_REVOKED
//
// MessageText:
//
// Indicates that a certificate has been revoked.
//
#define ERROR_ADAL_INTERNET_SECURE_CERT_REVOKED ((DWORD)0xCAA82F8AL)

//
// MessageId: ERROR_ADAL_INTERNET_SECURE_CERT_WRONG_USAGE
//
// MessageText:
//
// Indicates that a certificate is not valid for the requested usage.
//
#define ERROR_ADAL_INTERNET_SECURE_CERT_WRONG_USAGE ((DWORD)0xCAA82F8BL)

//
// MessageId: ERROR_ADAL_INTERNET_SECURE_FAILURE
//
// MessageText:
//
// One or more errors were found in the Secure Sockets Layer (SSL) certificate sent by the server. 
//
#define ERROR_ADAL_INTERNET_SECURE_FAILURE ((DWORD)0xCAA82F8FL)

//
// MessageId: ERROR_ADAL_INTERNET_AUTO_PROXY_SERVICE_ERROR
//
// MessageText:
//
// A proxy for the specified URL cannot be located.
//
#define ERROR_ADAL_INTERNET_AUTO_PROXY_SERVICE_ERROR ((DWORD)0xCAA82F92L)

//
// MessageId: ERROR_ADAL_INTERNET_AUTODETECTION_FAILED
//
// MessageText:
//
// WinHTTP was unable to discover the URL of the Proxy Auto-Configuration (PAC) file.
//
#define ERROR_ADAL_INTERNET_AUTODETECTION_FAILED ((DWORD)0xCAA82F94L)

//
// MessageId: ERROR_ADAL_INTERNET_HEADER_COUNT_EXCEEDED
//
// MessageText:
//
// Larger number of headers were present in a response than WinHTTP could receive.
//
#define ERROR_ADAL_INTERNET_HEADER_COUNT_EXCEEDED ((DWORD)0xCAA82F95L)

//
// MessageId: ERROR_ADAL_INTERNET_HEADER_SIZE_OVERFLOW
//
// MessageText:
//
// The size of headers received exceeds the limit for the request handle.
//
#define ERROR_ADAL_INTERNET_HEADER_SIZE_OVERFLOW ((DWORD)0xCAA82F96L)

//
// MessageId: ERROR_ADAL_INTERNET_CHUNKED_ENCODING_HEADER_SIZE_OVERFLOW
//
// MessageText:
//
// An overflow condition is encountered in the course of parsing chunked encoding.
//
#define ERROR_ADAL_INTERNET_CHUNKED_ENCODING_HEADER_SIZE_OVERFLOW ((DWORD)0xCAA82F97L)

//
// MessageId: ERROR_ADAL_INTERNET_RESPONSE_DRAIN_OVERFLOW
//
// MessageText:
//
// Returned when an incoming response exceeds an internal WinHTTP size limit.
//
#define ERROR_ADAL_INTERNET_RESPONSE_DRAIN_OVERFLOW ((DWORD)0xCAA82F98L)

//
// MessageId: ERROR_ADAL_INTERNET_CLIENT_CERT_NO_PRIVATE_KEY
//
// MessageText:
//
// The context for the SSL client certificate does not have a private key associated with it. The client certificate may have been imported to the computer without the private key.
//
#define ERROR_ADAL_INTERNET_CLIENT_CERT_NO_PRIVATE_KEY ((DWORD)0xCAA82F99L)

//
// MessageId: ERROR_ADAL_INTERNET_CLIENT_CERT_NO_ACCESS_PRIVATE_KEY
//
// MessageText:
//
// The application does not have the required privileges to access the private key associated with the client certificate.
//
#define ERROR_ADAL_INTERNET_CLIENT_CERT_NO_ACCESS_PRIVATE_KEY ((DWORD)0xCAA82F9AL)

//
// MessageId: ERROR_ADAL_UNEXPECTED
//
// MessageText:
//
// An unexpected error has occurred.
//
#define ERROR_ADAL_UNEXPECTED            ((DWORD)0xCAAA0001L)

//
// MessageId: ERROR_ADAL_BASE64_ENCODE_FAILED
//
// MessageText:
//
// Base64 encode failed.
//
#define ERROR_ADAL_BASE64_ENCODE_FAILED  ((DWORD)0xCAAA0002L)

//
// MessageId: ERROR_ADAL_COMPRESSION_FAILED
//
// MessageText:
//
// Compression of the data blob failed.
//
#define ERROR_ADAL_COMPRESSION_FAILED    ((DWORD)0xCAAA0003L)

//
// MessageId: ERROR_ADAL_BASE64_URL_DECODE_FAILED
//
// MessageText:
//
// Base64 URL decode failed.
//
#define ERROR_ADAL_BASE64_URL_DECODE_FAILED ((DWORD)0xCAAA0004L)

//
// MessageId: ERROR_ADAL_HEX_TO_BYTE_CONVERSION_FAILED
//
// MessageText:
//
// Conversion of hex to byte failed.
//
#define ERROR_ADAL_HEX_TO_BYTE_CONVERSION_FAILED ((DWORD)0xCAAA0005L)

//
// MessageId: ERROR_ADAL_CERT_STORE_NOT_OPEN
//
// MessageText:
//
// Certificate store is not opened.
//
#define ERROR_ADAL_CERT_STORE_NOT_OPEN   ((DWORD)0xCAAA0006L)

//
// MessageId: ERROR_ADAL_DEVICE_CERT_NOT_FOUND
//
// MessageText:
//
// Device Certificate not found in the certificate store.
//
#define ERROR_ADAL_DEVICE_CERT_NOT_FOUND ((DWORD)0xCAAA0007L)

//
// MessageId: ERROR_ADAL_SERIALIZED_BLOB_INVALID
//
// MessageText:
//
// Serialization blob is invalid.
//
#define ERROR_ADAL_SERIALIZED_BLOB_INVALID ((DWORD)0xCAA40001L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_UNAUTHORIZED_CLIENT
//
// MessageText:
//
// The client is not authorized to request an authorization code using this method.
//
#define ERROR_ADAL_SERVER_ERROR_UNAUTHORIZED_CLIENT ((DWORD)0xCAA20001L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_INVALID_REQUEST
//
// MessageText:
//
// The request is missing a required parameter, includes an invalid parameter value, includes a parameter more than once, or is otherwise malformed.
//
#define ERROR_ADAL_SERVER_ERROR_INVALID_REQUEST ((DWORD)0xCAA20002L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_INVALID_GRANT
//
// MessageText:
//
// Authorization grant failed for this assertion.
//
#define ERROR_ADAL_SERVER_ERROR_INVALID_GRANT ((DWORD)0xCAA20003L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_ACCESS_DENIED
//
// MessageText:
//
// The resource owner or authorization server denied the request.
//
#define ERROR_ADAL_SERVER_ERROR_ACCESS_DENIED ((DWORD)0xCAA20004L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_TEMPORARILY_UNAVAILABLE
//
// MessageText:
//
// The authorization server is currently unable to handle the request due to a temporary overloading or maintenance of the server.
//
#define ERROR_ADAL_SERVER_ERROR_TEMPORARILY_UNAVAILABLE ((DWORD)0xCAA20005L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_UNSUPPORTED_RESPONSE_TYPE
//
// MessageText:
//
// The authorization server does not support obtaining an authorization code using this method.
//
#define ERROR_ADAL_SERVER_ERROR_UNSUPPORTED_RESPONSE_TYPE ((DWORD)0xCAA20006L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_INVALID_SCOPE
//
// MessageText:
//
// The requested scope is invalid, unknown, or malformed.
//
#define ERROR_ADAL_SERVER_ERROR_INVALID_SCOPE ((DWORD)0xCAA20007L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_RECEIVED
//
// MessageText:
//
// The authorization server encountered an unexpected condition that prevented it from fulfilling the request.
//
#define ERROR_ADAL_SERVER_ERROR_RECEIVED ((DWORD)0xCAA20008L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_INVALID_CLIENT
//
// MessageText:
//
// Client authentication failed (e.g., unknown client, no client authentication included, or unsupported authentication method).
//
#define ERROR_ADAL_SERVER_ERROR_INVALID_CLIENT ((DWORD)0xCAA20009L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_UNSUPPORTED_UNSUPPORTED_GRANT_TYPE
//
// MessageText:
//
// The authorization grant type is not supported by the authorization server.
//
#define ERROR_ADAL_SERVER_ERROR_UNSUPPORTED_UNSUPPORTED_GRANT_TYPE ((DWORD)0xCAA2000AL)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_INVALID_RESOURCE
//
// MessageText:
//
// The resource is invalid due to configuration state or not existing.
//
#define ERROR_ADAL_SERVER_ERROR_INVALID_RESOURCE ((DWORD)0xCAA2000BL)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_INTERACTION_REQUIRED
//
// MessageText:
//
// The request requires user interaction.
//
#define ERROR_ADAL_SERVER_ERROR_INTERACTION_REQUIRED ((DWORD)0xCAA2000CL)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_CODE_UNKNOWN
//
// MessageText:
//
// Server return error code, but it is unknown.
//
#define ERROR_ADAL_SERVER_ERROR_CODE_UNKNOWN ((DWORD)0xCAA20064L)

//
// MessageId: ERROR_ADAL_SERVER_NO_AUTHORIZATION_CODE_FOUND
//
// MessageText:
//
// The response from the server did not have an authorization code.
//
#define ERROR_ADAL_SERVER_NO_AUTHORIZATION_CODE_FOUND ((DWORD)0xCAA90001L)

//
// MessageId: ERROR_ADAL_SERVER_ERROR_SAML_ASSERTION_NOT_FOUND
//
// MessageText:
//
// WSTrust response does not have recognized SAML assertion.
//
#define ERROR_ADAL_SERVER_ERROR_SAML_ASSERTION_NOT_FOUND ((DWORD)0xCAA90002L)

//
// MessageId: ERROR_ADAL_NO_REFRESH_TOKEN
//
// MessageText:
//
// There is no refresh token.
//
#define ERROR_ADAL_NO_REFRESH_TOKEN      ((DWORD)0xCAA90003L)

//
// MessageId: ERROR_ADAL_RENEW_BY_REFRESH_TOKEN
//
// MessageText:
//
// Getting token by refresh token failed.
//
#define ERROR_ADAL_RENEW_BY_REFRESH_TOKEN ((DWORD)0xCAA90004L)

//
// MessageId: ERROR_ADAL_RESOURCE_OWNER_PASSWORD_CREDENTIAL_GRANT_FAIL
//
// MessageText:
//
// Failed to get a token by username/password.
//
#define ERROR_ADAL_RESOURCE_OWNER_PASSWORD_CREDENTIAL_GRANT_FAIL ((DWORD)0xCAA90005L)

//
// MessageId: ERROR_ADAL_WSTRUST_TOKEN_REQUEST_FAIL
//
// MessageText:
//
// It failed to get token by WS-Trust flow.
//
#define ERROR_ADAL_WSTRUST_TOKEN_REQUEST_FAIL ((DWORD)0xCAA90006L)

//
// MessageId: ERROR_ADAL_URL_INVALID
//
// MessageText:
//
// Url is invalid or too long.
//
#define ERROR_ADAL_URL_INVALID           ((DWORD)0xCAA90007L)

//
// MessageId: INFO_ADAL_TOKEN_VALID
//
// MessageText:
//
// Cached token is valid, returning cached token
//
#define INFO_ADAL_TOKEN_VALID            ((DWORD)0x4AA90008L)

//
// MessageId: INFO_ADAL_TOKEN_EXPIRED
//
// MessageText:
//
// Token is expired
//
#define INFO_ADAL_TOKEN_EXPIRED          ((DWORD)0x4AA90009L)

//
// MessageId: INFO_ADAL_REFRESHTOKEN_SUCCESS
//
// MessageText:
//
// Access token was renewed by refresh token
//
#define INFO_ADAL_REFRESHTOKEN_SUCCESS   ((DWORD)0x4AA9000AL)

//
// MessageId: INFO_ADAL_CHANGE_AUTHCODE_TOKEN_SUCCESS
//
// MessageText:
//
// Change authorization code on token completed successfully
//
#define INFO_ADAL_CHANGE_AUTHCODE_TOKEN_SUCCESS ((DWORD)0x4AA9000BL)

//
// MessageId: INFO_ADAL_RESOURCE_OWNER_PASSWORD_CREDENTIAL_GRANT_SUCCESS
//
// MessageText:
//
// Getting token by username/password is completed successfully.
//
#define INFO_ADAL_RESOURCE_OWNER_PASSWORD_CREDENTIAL_GRANT_SUCCESS ((DWORD)0x4AA9000CL)

//
// MessageId: INFO_ADAL_WSTRUST_TOKEN_REQUEST_SUCCESS
//
// MessageText:
//
// WStrust token request is completed successfully
//
#define INFO_ADAL_WSTRUST_TOKEN_REQUEST_SUCCESS ((DWORD)0x4AA9000DL)

//
// MessageId: INFO_ADAL_UI_FLOW_COMPLETE
//
// MessageText:
//
// UI Flow is completed
//
#define INFO_ADAL_UI_FLOW_COMPLETE       ((DWORD)0x4AA9000EL)

//
// MessageId: INFO_ADAL_REQUEST_COMPLETE
//
// MessageText:
//
// Sending request completed successfully
//
#define INFO_ADAL_REQUEST_COMPLETE       ((DWORD)0x4AA9000FL)

//
// MessageId: INFO_ADAL_REQUEST_SENDING
//
// MessageText:
//
// Sending web request
//
#define INFO_ADAL_REQUEST_SENDING        ((DWORD)0x4AA90010L)

//
// MessageId: INFO_ADAL_HEADERS
//
// MessageText:
//
// Information about request headers
//
#define INFO_ADAL_HEADERS                ((DWORD)0x4AA90011L)

//
// MessageId: ERROR_ADAL_SILENT_LOGIN_THREAD_MANUAL_TERMINATED
//
// MessageText:
//
// Background thread for silent login did not terminate on time, so it is manually terminated.
//
#define ERROR_ADAL_SILENT_LOGIN_THREAD_MANUAL_TERMINATED ((DWORD)0xCAA90012L)

//
// MessageId: ERROR_ADAL_FAILED_TO_PARSE_WSTRUST_RESPONSE
//
// MessageText:
//
// It failed to parse WS-Trust(xml format) message.
//
#define ERROR_ADAL_FAILED_TO_PARSE_WSTRUST_RESPONSE ((DWORD)0xCAA90013L)

//
// MessageId: ERROR_ADAL_WSTRUST_REQUEST_SECURITYTOKEN_FAILED
//
// MessageText:
//
// Server WS-Trust response reported fault exception and it failed to get assertion
//
#define ERROR_ADAL_WSTRUST_REQUEST_SECURITYTOKEN_FAILED ((DWORD)0xCAA90014L)

//
// MessageId: ERROR_ADAL_OAUTH_RESPONSE_INVALID
//
// MessageText:
//
// Server returned invalid OAuth response
//
#define ERROR_ADAL_OAUTH_RESPONSE_INVALID ((DWORD)0xCAA90015L)

//
// MessageId: ERROR_ADAL_IDTOKEN_INVALID
//
// MessageText:
//
// User identifier token is not valid Base64 encoded
//
#define ERROR_ADAL_IDTOKEN_INVALID       ((DWORD)0xCAA90016L)

//
// MessageId: ERROR_ADAL_PROTOCOL_NOT_SUPPORTED
//
// MessageText:
//
// Protocol is not supported by the client library.
//
#define ERROR_ADAL_PROTOCOL_NOT_SUPPORTED ((DWORD)0xCAA90017L)

//
// MessageId: ERROR_ADAL_COULDNOT_DISCOVER_USERREALM
//
// MessageText:
//
// Could not discover a user realm.
//
#define ERROR_ADAL_COULDNOT_DISCOVER_USERREALM ((DWORD)0xCAA90018L)

//
// MessageId: ERROR_ADAL_UNEXPECTED_RESPONSE
//
// MessageText:
//
// Unexpected response from the server.
//
#define ERROR_ADAL_UNEXPECTED_RESPONSE   ((DWORD)0xCAA90019L)

//
// MessageId: ERROR_ADAL_USERREALM_NOENDPOINT
//
// MessageText:
//
// No endpoint information in discovery response.
//
#define ERROR_ADAL_USERREALM_NOENDPOINT  ((DWORD)0xCAA9001AL)

//
// MessageId: ERROR_ADAL_USERREALM_RESPONSE_INVALID_JSON
//
// MessageText:
//
// User realm response is invalid json object.
//
#define ERROR_ADAL_USERREALM_RESPONSE_INVALID_JSON ((DWORD)0xCAA9001BL)

//
// MessageId: ERROR_ADAL_USERREALM_RESPONSE_FEDERATION_PROTOCOL_INVALID
//
// MessageText:
//
// User realm response has unknown federation protocol.
//
#define ERROR_ADAL_USERREALM_RESPONSE_FEDERATION_PROTOCOL_INVALID ((DWORD)0xCAA9001CL)

//
// MessageId: ERROR_ADAL_USERREALM_RESPONSE_ACCOUNT_TYPE_INVALID
//
// MessageText:
//
// User realm response has unknown account type.
//
#define ERROR_ADAL_USERREALM_RESPONSE_ACCOUNT_TYPE_INVALID ((DWORD)0xCAA9001DL)

//
// MessageId: ERROR_ADAL_USERREALM_RESPONSE_PARSE_FAILED
//
// MessageText:
//
// User realm response is failed to parse.
//
#define ERROR_ADAL_USERREALM_RESPONSE_PARSE_FAILED ((DWORD)0xCAA9001EL)

//
// MessageId: ERROR_ADAL_INTEGRATED_AUTH_WITHOUT_FEDERATION
//
// MessageText:
//
// Integrated Windows authentication supported only in federation flow.
//
#define ERROR_ADAL_INTEGRATED_AUTH_WITHOUT_FEDERATION ((DWORD)0xCAA9001FL)

//
// MessageId: ERROR_ADAL_WS_TRUST_METADATAURL_NOT_SECURE
//
// MessageText:
//
// Url for WS-Trust metadata exchange endpoint is not secure (https).
//
#define ERROR_ADAL_WS_TRUST_METADATAURL_NOT_SECURE ((DWORD)0xCAA90020L)

//
// MessageId: WARNING_ADAL_WS_TRUST_POLICY_VALIDATION
//
// MessageText:
//
// There is an error occurred during WS-Trust policy validation.
//
#define WARNING_ADAL_WS_TRUST_POLICY_VALIDATION ((DWORD)0x8AA90021L)

//
// MessageId: ERROR_ADAL_COULDNOT_DISCOVER_INTEGRATED_AUTH_ENDPOINT
//
// MessageText:
//
// Could not discover endpoint for Integrate Windows Authentication. Check your ADFS settings. It should support Integrate Widows Authentication for WS-Trust 1.3 or WS-Trust 2005.
//
#define ERROR_ADAL_COULDNOT_DISCOVER_INTEGRATED_AUTH_ENDPOINT ((DWORD)0xCAA90022L)

//
// MessageId: ERROR_ADAL_COULDNOT_DISCOVER_USERNAME_PASSWORD_ENDPOINT
//
// MessageText:
//
// Could not discover endpoint for username/password authentication. Check your ADFS settings. It should support username/password authentication for WS-Trust 1.3 or WS-Trust 2005.
//
#define ERROR_ADAL_COULDNOT_DISCOVER_USERNAME_PASSWORD_ENDPOINT ((DWORD)0xCAA90023L)

//
// MessageId: WARNING_ADAL_WS_TRUST_MEX_NO_ADDRESS
//
// MessageText:
//
// No address element found in port element. Continue the search.
//
#define WARNING_ADAL_WS_TRUST_MEX_NO_ADDRESS ((DWORD)0x8AA90024L)

//
// MessageId: WARNING_ADAL_WS_TRUST_MEX_ADDRESS_INSECURE
//
// MessageText:
//
// Address element is found, but contains insecure Url. Continue the search.
//
#define WARNING_ADAL_WS_TRUST_MEX_ADDRESS_INSECURE ((DWORD)0x8AA90025L)

//
// MessageId: WARNING_ADAL_WS_TRUST_MEX_NO_BINDING_IN_PORT
//
// MessageText:
//
// No 'binding' attribute is found in port element.
//
#define WARNING_ADAL_WS_TRUST_MEX_NO_BINDING_IN_PORT ((DWORD)0x8AA90026L)

//
// MessageId: WARNING_ADAL_WS_TRUST_MEX_NO_SOAP_TRANSPORT
//
// MessageText:
//
// No soap transport found in the element. Continue the search.
//
#define WARNING_ADAL_WS_TRUST_MEX_NO_SOAP_TRANSPORT ((DWORD)0x8AA90027L)

//
// MessageId: WARNING_ADAL_WS_TRUST_MEX_NO_SOAP_ACTION
//
// MessageText:
//
// No soap action found in the element. Continue the search.
//
#define WARNING_ADAL_WS_TRUST_MEX_NO_SOAP_ACTION ((DWORD)0x8AA90028L)

//
// MessageId: WARNING_ADAL_WS_TRUST_MEX_NO_NAME_IN_BINDING
//
// MessageText:
//
// No name attribute in binding element. Continue the search.
//
#define WARNING_ADAL_WS_TRUST_MEX_NO_NAME_IN_BINDING ((DWORD)0x8AA90029L)

//
// MessageId: WARNING_ADAL_WS_TRUST_MEX_NO_URI_IN_REF
//
// MessageText:
//
// No uri attribute in PolicyReference element. Continue the search.
//
#define WARNING_ADAL_WS_TRUST_MEX_NO_URI_IN_REF ((DWORD)0x8AA9002AL)

//
// MessageId: ERROR_ADAL_WS_TRUST_MEX_REQUEST
//
// MessageText:
//
// WS-Trust metadata exchange request failed.
//
#define ERROR_ADAL_WS_TRUST_MEX_REQUEST  ((DWORD)0xCAA9002BL)

//
// MessageId: ERROR_ADAL_FAILED_TO_PARSE_XML
//
// MessageText:
//
// Failed to parse XML blob.
//
#define ERROR_ADAL_FAILED_TO_PARSE_XML   ((DWORD)0xCAA9002CL)

//
// MessageId: ERROR_ADAL_CLIENT_CREDENTIAL_GRANT_FAIL
//
// MessageText:
//
// Failed to get a token by client credential grant.
//
#define ERROR_ADAL_CLIENT_CREDENTIAL_GRANT_FAIL ((DWORD)0xCAA9002DL)

//
// MessageId: INFO_ADAL_CLIENT_CREDENTIAL_GRANT_SUCCESS
//
// MessageText:
//
// Getting token by the client credential grant is completed successfully.
//
#define INFO_ADAL_CLIENT_CREDENTIAL_GRANT_SUCCESS ((DWORD)0x4AA9002EL)

//
// MessageId: ERROR_ADAL_NO_USER_INFO
//
// MessageText:
//
// No user information found.
//
#define ERROR_ADAL_NO_USER_INFO          ((DWORD)0xCAA9002FL)

//
// MessageId: INFO_ADAL_PKEYAUTH_CHALLENGE_RECEIVED
//
// MessageText:
//
// PKeyauth challenge is received.
//
#define INFO_ADAL_PKEYAUTH_CHALLENGE_RECEIVED ((DWORD)0x4AA90030L)

//
// MessageId: INFO_ADAL_NAVIGATE_WITH_PKEYAUTH_CHALLENGE_RESPONSE
//
// MessageText:
//
// Browser starts navigating with pkeyauth challenge response header.
//
#define INFO_ADAL_NAVIGATE_WITH_PKEYAUTH_CHALLENGE_RESPONSE ((DWORD)0x4AA90031L)

//
// MessageId: ERROR_ADAL_PKEYAUTH_CHALLENGE_RESPONSE_EMPTY
//
// MessageText:
//
// Challenge response header is returned empty.
//
#define ERROR_ADAL_PKEYAUTH_CHALLENGE_RESPONSE_EMPTY ((DWORD)0xCAA90032L)

//
// MessageId: INFO_ADAL_PKEYAUTH_SIGNED_WITH_CNG_KEY
//
// MessageText:
//
// PKeyAuth challenge is signed with CNG key.
//
#define INFO_ADAL_PKEYAUTH_SIGNED_WITH_CNG_KEY ((DWORD)0x4AA90033L)

//
// MessageId: INFO_ADAL_PKEYAUTH_SIGNED_WITH_CAPI_KEY
//
// MessageText:
//
// PKeyAuth challenge is signed with CAPI key.
//
#define INFO_ADAL_PKEYAUTH_SIGNED_WITH_CAPI_KEY ((DWORD)0x4AA90034L)

//
// MessageId: INFO_ADAL_PKEYAUTH_SENDING_RESPONSE_FOR_REFRESH_TOKEN
//
// MessageText:
//
// It is sending response for PKeyAuth challenge at refresh token request.
//
#define INFO_ADAL_PKEYAUTH_SENDING_RESPONSE_FOR_REFRESH_TOKEN ((DWORD)0x4AA90035L)

//
// MessageId: ERROR_ADAL_PKEYAUTH_NONCE_EMPTY
//
// MessageText:
//
// Nonce in Pkeyauth challenge is empty.
//
#define ERROR_ADAL_PKEYAUTH_NONCE_EMPTY  ((DWORD)0xCAA90036L)

//
// MessageId: ERROR_ADAL_PKEYAUTH_AUDIENCE_EMPTY
//
// MessageText:
//
// Audience in Pkeyauth challenge is empty.
//
#define ERROR_ADAL_PKEYAUTH_AUDIENCE_EMPTY ((DWORD)0xCAA90037L)

//
// MessageId: ERROR_ADAL_JWT_BEARER_GRANT_FAIL
//
// MessageText:
//
// Failed to get a token by JWT bearer grant.
//
#define ERROR_ADAL_JWT_BEARER_GRANT_FAIL ((DWORD)0xCAA90038L)

//
// MessageId: INFO_ADAL_JWT_BEARER_GRANT_SUCCESS
//
// MessageText:
//
// Getting token by the JWT bearer grant is completed successfully.
//
#define INFO_ADAL_JWT_BEARER_GRANT_SUCCESS ((DWORD)0x4AA90039L)

//
// MessageId: ERROR_ADAL_GET_ACCESS_TOKEN_FROM_STS
//
// MessageText:
//
// Getting access token from sts failed.
//
#define ERROR_ADAL_GET_ACCESS_TOKEN_FROM_STS ((DWORD)0x4AA9003AL)

//
// MessageId: ERROR_ADAL_MSXML_NOT_REGISTERED
//
// MessageText:
//
// It could not initialize MSXML class
//
#define ERROR_ADAL_MSXML_NOT_REGISTERED  ((DWORD)0xCAA50001L)

//
// MessageId: ERROR_ADAL_SYSTEM_ERROR_OCCURRED
//
// MessageText:
//
// System error occurred. Please see Details tab for additional info.
//
#define ERROR_ADAL_SYSTEM_ERROR_OCCURRED ((DWORD)0xCAA50002L)

//
// MessageId: ERROR_ADAL_USER_DOMAIN_STATUS
//
// MessageText:
//
// Failed to determine whether the current user is a domain or local account.
//
#define ERROR_ADAL_USER_DOMAIN_STATUS    ((DWORD)0x8AA50003L)

//
// MessageId: ERROR_ADAL_JSON_SUCCESS
//
// MessageText:
//
// The operation completed successfully.
//
#define ERROR_ADAL_JSON_SUCCESS          ((DWORD)0x4AA60001L)

//
// MessageId: ERROR_ADAL_JSON_OUTOFMEMORY
//
// MessageText:
//
// Out of memory.
//
#define ERROR_ADAL_JSON_OUTOFMEMORY      ((DWORD)0xCAA60002L)

//
// MessageId: ERROR_ADAL_JSON_INVALID_PARAMETER
//
// MessageText:
//
// The parameter is incorrect.
//
#define ERROR_ADAL_JSON_INVALID_PARAMETER ((DWORD)0xCAA60003L)

//
// MessageId: ERROR_ADAL_JSON_INSUFFICIENT_BUFFER
//
// MessageText:
//
// The data area passed to a system call is too small.
//
#define ERROR_ADAL_JSON_INSUFFICIENT_BUFFER ((DWORD)0xCAA60004L)

//
// MessageId: ERROR_ADAL_JSON_NOT_FOUND
//
// MessageText:
//
// Element not found.
//
#define ERROR_ADAL_JSON_NOT_FOUND        ((DWORD)0xCAA60005L)

//
// MessageId: ERROR_ADAL_JSON_INTERNAL
//
// MessageText:
//
// Unexpected Failure.
//
#define ERROR_ADAL_JSON_INTERNAL         ((DWORD)0xCAA60006L)

//
// MessageId: ERROR_ADAL_JSON_MALFORMED
//
// MessageText:
//
// The JSON text being parsed is malformed.
//
#define ERROR_ADAL_JSON_MALFORMED        ((DWORD)0xCAA60007L)

//
// MessageId: ERROR_ADAL_IDTOKEN_JSON_MALFORMED
//
// MessageText:
//
// The id_token JSON text being parsed is malformed.
//
#define ERROR_ADAL_IDTOKEN_JSON_MALFORMED ((DWORD)0xCAA60008L)

//
// MessageId: ERROR_ADAL_ACCOUNT_SWITCH
//
// MessageText:
//
// Token Broker returned status Account Switch.
//
#define ERROR_ADAL_ACCOUNT_SWITCH        ((DWORD)0xCAAB0001L)

//
// MessageId: ERROR_ADAL_USER_CANCEL
//
// MessageText:
//
// Token Broker returned status User Cancel.
//
#define ERROR_ADAL_USER_CANCEL           ((DWORD)0xCAAB0002L)

//
// MessageId: ERROR_ADAL_ACCOUNT_PROVIDER_NOT_AVAILABLE
//
// MessageText:
//
// Token Broker returned status Account provider not available.
//
#define ERROR_ADAL_ACCOUNT_PROVIDER_NOT_AVAILABLE ((DWORD)0xCAAB0003L)

//
// MessageId: ERROR_ADAL_UNKNOWN_TB_STATUS
//
// MessageText:
//
// Token Broker returned unknown status.
//
#define ERROR_ADAL_UNKNOWN_TB_STATUS     ((DWORD)0xCAAB0004L)

//
// MessageId: ERROR_ADAL_NEED_CREDENTIAL
//
// MessageText:
//
// Need user interface to continue.
//
#define ERROR_ADAL_NEED_CREDENTIAL       ((DWORD)0xCAA10001L)

#define ERROR_ADAL_NEED_UI ERROR_ADAL_NEED_CREDENTIAL
//
// MessageId: ERROR_UI_PENDING
//
// MessageText:
//
// User interface is waits for user input.
//
#define ERROR_UI_PENDING                 ((DWORD)0xCAA10002L)

//
// MessageId: ERROR_ADAL_INVALID_HTTP_REQUEST_STATE
//
// MessageText:
//
// There was an error while creating a web browser control.
//
#define ERROR_ADAL_INVALID_HTTP_REQUEST_STATE ((DWORD)0xCAA10003L)

//
// MessageId: ERROR_ADAL_WINHTTP_CONNECTION_MISSING
//
// MessageText:
//
// HTTP request state is invalid and connection is missing
//
#define ERROR_ADAL_WINHTTP_CONNECTION_MISSING ((DWORD)0xCAA10004L)

//
// MessageId: ERROR_ADAL_AUTHORITY_IS_NOT_VALID_URL
//
// MessageText:
//
// The value specified for 'authority' must be Url of the format http(s)://hostname/subpath.
//
#define ERROR_ADAL_AUTHORITY_IS_NOT_VALID_URL ((DWORD)0xCAA10005L)

//
// MessageId: ERROR_ADAL_AUTHORITY_IS_INVALID
//
// MessageText:
//
// The value specified for 'authority' is invalid. It is not in the valid authority list or not discovered.
//
#define ERROR_ADAL_AUTHORITY_IS_INVALID  ((DWORD)0xCAA10006L)

//
// MessageId: ERROR_ADAL_AUTHORITY_EMPTY
//
// MessageText:
//
// The value specified for 'authority' must be non-empty.
//
#define ERROR_ADAL_AUTHORITY_EMPTY       ((DWORD)0xCAA10007L)

//
// MessageId: ERROR_ADAL_AUTHORITY_STATUS_PARAM_IS_NULL
//
// MessageText:
//
// Parameter for status param is null.
//
#define ERROR_ADAL_AUTHORITY_STATUS_PARAM_IS_NULL ((DWORD)0xCAA10008L)

//
// MessageId: ERROR_ADAL_CLIENTID_EMPTY
//
// MessageText:
//
// The value specified for 'clientId' must be non-empty.
//
#define ERROR_ADAL_CLIENTID_EMPTY        ((DWORD)0xCAA10009L)

//
// MessageId: ERROR_ADAL_RESOURCE_EMPTY
//
// MessageText:
//
// The value specified for 'resource' must be non-empty.
//
#define ERROR_ADAL_RESOURCE_EMPTY        ((DWORD)0xCAA1000AL)

//
// MessageId: ERROR_ADAL_REDIRECTURI_EMPTY
//
// MessageText:
//
// The value specified for redirect URI must be non-empty.
//
#define ERROR_ADAL_REDIRECTURI_EMPTY     ((DWORD)0xCAA1000BL)

//
// MessageId: ERROR_ADAL_REDIRECTURI_IS_INVALID
//
// MessageText:
//
// The value specified for redirect URI has invalid format.
//
#define ERROR_ADAL_REDIRECTURI_IS_INVALID ((DWORD)0xCAA1000CL)

//
// MessageId: ERROR_ADAL_REDIRECTURI_FRAGMENT
//
// MessageText:
//
// The value specified for redirect URI should not contain fragment (#fragment).
//
#define ERROR_ADAL_REDIRECTURI_FRAGMENT  ((DWORD)0xCAA1000DL)

//
// MessageId: ERROR_ADAL_HANDLE
//
// MessageText:
//
// The handle is invalid.
//
#define ERROR_ADAL_HANDLE                ((DWORD)0xCAA1000EL)

//
// MessageId: ERROR_ADAL_HANDLE_PROTECTED
//
// MessageText:
//
// The handle specified could not be freed. You don't need to free this handle.
//
#define ERROR_ADAL_HANDLE_PROTECTED      ((DWORD)0xCAA1000FL)

//
// MessageId: ERROR_ADAL_TOKEN_LENGTH
//
// MessageText:
//
// Token length is invalid.
//
#define ERROR_ADAL_TOKEN_LENGTH          ((DWORD)0xCAA10010L)

//
// MessageId: ERROR_ADAL_EXPIRESON
//
// MessageText:
//
// ExpiresOn parameter is invalid.
//
#define ERROR_ADAL_EXPIRESON             ((DWORD)0xCAA10011L)

//
// MessageId: ERROR_ADAL_WEBBROWSER_NOINTERFACE
//
// MessageText:
//
// IE parameter does not implement IWebBrowser2 interface.
//
#define ERROR_ADAL_WEBBROWSER_NOINTERFACE ((DWORD)0xCAA10012L)

//
// MessageId: ERROR_ADAL_DESCRIPTION_LENGTH
//
// MessageText:
//
// Error description length is invalid.
//
#define ERROR_ADAL_DESCRIPTION_LENGTH    ((DWORD)0xCAA10013L)

//
// MessageId: ERROR_ADAL_REFRESH_TOKEN_EMPTY
//
// MessageText:
//
// Refresh token is empty.
//
#define ERROR_ADAL_REFRESH_TOKEN_EMPTY   ((DWORD)0xCAA10014L)

//
// MessageId: ERROR_ADAL_HOST_REQUIREMENTS
//
// MessageText:
//
// Host requirements parameter should not be null.
//
#define ERROR_ADAL_HOST_REQUIREMENTS     ((DWORD)0xCAA10015L)

//
// MessageId: ERROR_ADAL_HOST_REQUIREMENTS_INVALIDSIZE
//
// MessageText:
//
// Invalid field size for the host requirements parameter.
//
#define ERROR_ADAL_HOST_REQUIREMENTS_INVALIDSIZE ((DWORD)0xCAA10016L)

//
// MessageId: ERROR_ADAL_LOG_OPTIONS_NULL
//
// MessageText:
//
// Log options must not be null for setting the log options
//
#define ERROR_ADAL_LOG_OPTIONS_NULL      ((DWORD)0xCAA10017L)

//
// MessageId: ERROR_ADAL_LOG_OPTIONS_INVALIDSIZE
//
// MessageText:
//
// Invalid field size for the log options parameter.
//
#define ERROR_ADAL_LOG_OPTIONS_INVALIDSIZE ((DWORD)0xCAA10018L)

//
// MessageId: ERROR_ADAL_LOG_TYPE_INVALID
//
// MessageText:
//
// Invalid logger type
//
#define ERROR_ADAL_LOG_TYPE_INVALID      ((DWORD)0xCAA10019L)

//
// MessageId: ERROR_ADAL_SERIALIZED_BLOB_EMPTY
//
// MessageText:
//
// Serialized blob is empty.
//
#define ERROR_ADAL_SERIALIZED_BLOB_EMPTY ((DWORD)0xCAA1001AL)

//
// MessageId: ERROR_ADAL_SERIALIZED_BLOB_LENGTH_NULL
//
// MessageText:
//
// Serialized context is null.
//
#define ERROR_ADAL_SERIALIZED_BLOB_LENGTH_NULL ((DWORD)0xCAA1001BL)

//
// MessageId: ERROR_ADAL_SERIALIZED_BUFFER_LENGTH
//
// MessageText:
//
// The buffer for output of the serialization is too small.
//
#define ERROR_ADAL_SERIALIZED_BUFFER_LENGTH ((DWORD)0xCAA1001CL)

//
// MessageId: ERROR_ADAL_WEB_BROWSER_NULL
//
// MessageText:
//
// Pointer on 'WebBrowser' is null.
//
#define ERROR_ADAL_WEB_BROWSER_NULL      ((DWORD)0xCAA1001DL)

//
// MessageId: ERROR_ADAL_NON_STA_THREAD
//
// MessageText:
//
// Current thread is not a single thread apartment (STA) thread. This thread must run with COM initialized with 'COINIT_APARTMENTTHREADED'
//
#define ERROR_ADAL_NON_STA_THREAD        ((DWORD)0xCAA1001EL)

//
// MessageId: ERROR_ADAL_NOT_IMPLEMENTED
//
// MessageText:
//
// Not implemented
//
#define ERROR_ADAL_NOT_IMPLEMENTED       ((DWORD)0xCAA1001FL)

//
// MessageId: ERROR_ADAL_WINHTTP_CONTENT_INVALID
//
// MessageText:
//
// Content is larger than ULONG_MAX
//
#define ERROR_ADAL_WINHTTP_CONTENT_INVALID ((DWORD)0xCAA10020L)

//
// MessageId: ERROR_ADAL_WINHTTP_REQUEST_INVALID
//
// MessageText:
//
// Previous request handle is not closed
//
#define ERROR_ADAL_WINHTTP_REQUEST_INVALID ((DWORD)0xCAA10021L)

//
// MessageId: ERROR_ADAL_WINHTTP_REUSE_NOT_PERMITTED
//
// MessageText:
//
// WebRequest object can be used to make one request
//
#define ERROR_ADAL_WINHTTP_REUSE_NOT_PERMITTED ((DWORD)0xCAA10022L)

//
// MessageId: ERROR_ADAL_WINHTTP_STATUS_NOT_OK
//
// MessageText:
//
// WebRequest status is not 200
//
#define ERROR_ADAL_WINHTTP_STATUS_NOT_OK ((DWORD)0xCAA10023L)

//
// MessageId: ERROR_ADAL_WINHTTP_EXCEEDS_TOTAL_DOWNLOAD_SIZE
//
// MessageText:
//
// WebRequest total download size is exceeding the current limit
//
#define ERROR_ADAL_WINHTTP_EXCEEDS_TOTAL_DOWNLOAD_SIZE ((DWORD)0xCAA10024L)

//
// MessageId: ERROR_ADAL_WINHTTP_INVALID_ASYNC_CALLBACK
//
// MessageText:
//
// WebRequest needs callback for async calls
//
#define ERROR_ADAL_WINHTTP_INVALID_ASYNC_CALLBACK ((DWORD)0xCAA10025L)

//
// MessageId: ERROR_ADAL_JSON_STACK_SIZE_LIMIT
//
// MessageText:
//
// Json nested roots are limited
//
#define ERROR_ADAL_JSON_STACK_SIZE_LIMIT ((DWORD)0xCAA10026L)

//
// MessageId: ERROR_ADAL_WEBREQUEST_ADDITIONAL_HEADERS_INVALID
//
// MessageText:
//
// Additional headers are supposed to have CRLF line ending
//
#define ERROR_ADAL_WEBREQUEST_ADDITIONAL_HEADERS_INVALID ((DWORD)0xCAA10027L)

//
// MessageId: ERROR_ADAL_EVENTREGISTER_PROVIDER_NAME_EMPTY
//
// MessageText:
//
// Registry root name is empty for event log
//
#define ERROR_ADAL_EVENTREGISTER_PROVIDER_NAME_EMPTY ((DWORD)0xCAA10028L)

//
// MessageId: ERROR_ADAL_EVENTREGISTER_MESSAGE_DLL_PATH_EMPTY
//
// MessageText:
//
// Message file path is empty
//
#define ERROR_ADAL_EVENTREGISTER_MESSAGE_DLL_PATH_EMPTY ((DWORD)0xCAA10029L)

//
// MessageId: ERROR_ADAL_NO_UI
//
// MessageText:
//
// No user interface needed to complete request.
//
#define ERROR_ADAL_NO_UI                 ((DWORD)0xCAA1002AL)

//
// MessageId: ERROR_ADAL_INVALID_METADATA
//
// MessageText:
//
// Metadata is invalid.
//
#define ERROR_ADAL_INVALID_METADATA      ((DWORD)0xCAA1002BL)

//
// MessageId: ERROR_ADAL_GUID_GENERATE_FAILED
//
// MessageText:
//
// It failed to generate GUID
//
#define ERROR_ADAL_GUID_GENERATE_FAILED  ((DWORD)0xCAA1002CL)

//
// MessageId: ERROR_OPERATION_PENDING
//
// MessageText:
//
// Operation is pending.
//
#define ERROR_OPERATION_PENDING          ((DWORD)0xCAA1002DL)

//
// MessageId: ERROR_ADAL_OUT_PARAM_IS_NULL
//
// MessageText:
//
// In_out parameter is null. It is required to set the correct length for the output string.
//
#define ERROR_ADAL_OUT_PARAM_IS_NULL     ((DWORD)0xCAA1002EL)

//
// MessageId: ERROR_ENDPOINT_INVALID
//
// MessageText:
//
// Invalid endpoint identifier.
//
#define ERROR_ENDPOINT_INVALID           ((DWORD)0xCAA1002FL)

//
// MessageId: ERROR_AUTH_ENUM_INVALID
//
// MessageText:
//
// Authentication context options is invalid. Only one option in a time allowed to get, multiple allowed to set.
//
#define ERROR_AUTH_ENUM_INVALID          ((DWORD)0xCAA10030L)

//
// MessageId: ERROR_OPTION_VALUE_INVALID
//
// MessageText:
//
// An option value is invalid.
//
#define ERROR_OPTION_VALUE_INVALID       ((DWORD)0xCAA10031L)

//
// MessageId: ERROR_OPTION_VALUE_NULL
//
// MessageText:
//
// An option value is null.
//
#define ERROR_OPTION_VALUE_NULL          ((DWORD)0xCAA10032L)

//
// MessageId: ERROR_ACCOUNT_TYPE_NULL
//
// MessageText:
//
// Account type is null.
//
#define ERROR_ACCOUNT_TYPE_NULL          ((DWORD)0xCAA10033L)

//
// MessageId: ERROR_ACCOUNT_TYPE_INVALID
//
// MessageText:
//
// Account type has invalid value.
//
#define ERROR_ACCOUNT_TYPE_INVALID       ((DWORD)0xCAA10034L)

//
// MessageId: ERROR_USERNAME_EMPTY
//
// MessageText:
//
// User name could not be null or empty.
//
#define ERROR_USERNAME_EMPTY             ((DWORD)0xCAA10035L)

//
// MessageId: ERROR_ADAL_CLIENT_SECRET_LENGTH
//
// MessageText:
//
// Client secret length is null.
//
#define ERROR_ADAL_CLIENT_SECRET_LENGTH  ((DWORD)0xCAA10036L)

//
// MessageId: ERROR_ADAL_NO_CREDENTIAL_NEEDED
//
// MessageText:
//
// We don't need credentials for this flow.
//
#define ERROR_ADAL_NO_CREDENTIAL_NEEDED  ((DWORD)0xCAA10037L)

//
// MessageId: ERROR_ADAL_NO_UI_SUPPORTED
//
// MessageText:
//
// Authentication context doesn't support UI flow.
//
#define ERROR_ADAL_NO_UI_SUPPORTED       ((DWORD)0xCAA10038L)

//
// MessageId: ERROR_ASSERTION_TYPE_INVALID
//
// MessageText:
//
// Invalid assertion type.
//
#define ERROR_ASSERTION_TYPE_INVALID     ((DWORD)0xCAA10039L)

//
// MessageId: ERROR_ADAL_NO_PASSWORD_EXPIRY
//
// MessageText:
//
// Password expiry information is not available with this request.
//
#define ERROR_ADAL_NO_PASSWORD_EXPIRY    ((DWORD)0xCAA1003AL)

//
// MessageId: ERROR_ADAL_FAILED_TO_OPEN_CERTSTORE
//
// MessageText:
//
// Failed to open local user local certificate store.
//
#define ERROR_ADAL_FAILED_TO_OPEN_CERTSTORE ((DWORD)0xCAA1003BL)

//
// MessageId: ERROR_ADAL_FAILED_TO_CLOSE_CERTSTORE
//
// MessageText:
//
// Failed to close local user local certificate store
//
#define ERROR_ADAL_FAILED_TO_CLOSE_CERTSTORE ((DWORD)0xCAA1003CL)

//
// MessageId: ERROR_ADAL_OUT_OF_MEMORY
//
// MessageText:
//
// Failed to allocate memory.
//
#define ERROR_ADAL_OUT_OF_MEMORY         ((DWORD)0xCAA1003DL)

//
// MessageId: ERROR_ADAL_INVALID_THUMBPRINT
//
// MessageText:
//
// Given certificate thumbprint is invalid.
//
#define ERROR_ADAL_INVALID_THUMBPRINT    ((DWORD)0xCAA1003EL)

//
// MessageId: ERROR_ADAL_NULL_SIGN_INPUT_MESSAGE
//
// MessageText:
//
// Sign input message is null.
//
#define ERROR_ADAL_NULL_SIGN_INPUT_MESSAGE ((DWORD)0xCAA1003FL)

//
// MessageId: ERROR_ADAL_SIGNATURE_FAILED
//
// MessageText:
//
// Failed to sign the message.
//
#define ERROR_ADAL_SIGNATURE_FAILED      ((DWORD)0xCAA10040L)

//
// MessageId: ERROR_ADAL_ISSUER_NOT_SET
//
// MessageText:
//
// Certificate issuer is not set.
//
#define ERROR_ADAL_ISSUER_NOT_SET        ((DWORD)0xCAA10041L)

//
// MessageId: ERROR_ADAL_NULL_HASH
//
// MessageText:
//
// Hash input is null.
//
#define ERROR_ADAL_NULL_HASH             ((DWORD)0xCAA10042L)

//
// MessageId: ERROR_ADAL_WAM_NOT_ENABLED
//
// MessageText:
//
// The call is not supported as using Web Account Manager is disabled.
//
#define ERROR_ADAL_WAM_NOT_ENABLED       ((DWORD)0xCAA10043L)

//
// MessageId: ERROR_ADAL_NULL_HWND
//
// MessageText:
//
// HWND input parameter is null.
//
#define ERROR_ADAL_NULL_HWND             ((DWORD)0xCAA10044L)

//
// MessageId: ERROR_ADAL_WAM_ENABLED
//
// MessageText:
//
// The call is not supported as using Web Account Manager is enabled.
//
#define ERROR_ADAL_WAM_ENABLED           ((DWORD)0xCAA10045L)

//
// MessageId: ERROR_ADAL_HOST_EMPTY
//
// MessageText:
//
// The value specified for host argument must be non-empty.
//
#define ERROR_ADAL_HOST_EMPTY            ((DWORD)0xCAA10046L)

