// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// Extensions for errors in SNI.
    /// </summary>
    internal static class ErrorExtensions
    {
        /// <summary>
        /// Convers the SNIError to SNIErrorDetails.
        /// </summary>
        /// <param name="sniError"></param>
        /// <returns></returns>
        private static SNIErrorDetails ToSniErrorDetails(this SNIError sniError)
            => new SNIErrorDetails
            {
                sniErrorNumber = sniError.sniError,
                errorMessage = sniError.errorMessage,
                nativeError = sniError.nativeError,
                provider = (int)sniError.provider,
                lineNumber = sniError.lineNumber,
                function = sniError.function,
                exception = sniError.exception
            };

        /// <summary>
        /// Converts an SNI error to SqlError.
        /// </summary>
        /// <param name="sniError"></param>
        /// <param name="sniContext"></param>
        /// <param name="serverName">The server name being connected to.</param>
        /// <returns></returns>
        public static SqlError ToSqlError(this SNIError sniError, SniContext sniContext, string serverName)
        {
            SNIErrorDetails details = sniError.ToSniErrorDetails();

            if (details.sniErrorNumber != 0)
            {
                // handle special SNI error codes that are converted into exception which is not a SqlException.
                switch (details.sniErrorNumber)
                {
                    case (int)SNINativeMethodWrapper.SniSpecialErrors.MultiSubnetFailoverWithMoreThan64IPs:
                        // Connecting with the MultiSubnetFailover connection option to a SQL Server instance configured with more than 64 IP addresses is not supported.
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.ProcessSNIError|ERR|ADV> Connecting with the MultiSubnetFailover connection option to a SQL Server instance configured with more than 64 IP addresses is not supported.");
                        throw SQL.MultiSubnetFailoverWithMoreThan64IPs();

                    case (int)SNINativeMethodWrapper.SniSpecialErrors.MultiSubnetFailoverWithInstanceSpecified:
                        // Connecting to a named SQL Server instance using the MultiSubnetFailover connection option is not supported.
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.ProcessSNIError|ERR|ADV> Connecting to a named SQL Server instance using the MultiSubnetFailover connection option is not supported.");
                        throw SQL.MultiSubnetFailoverWithInstanceSpecified();

                    case (int)SNINativeMethodWrapper.SniSpecialErrors.MultiSubnetFailoverWithNonTcpProtocol:
                        // Connecting to a SQL Server instance using the MultiSubnetFailover connection option is only supported when using the TCP protocol.
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.ProcessSNIError|ERR|ADV> Connecting to a SQL Server instance using the MultiSubnetFailover connection option is only supported when using the TCP protocol.");
                        throw SQL.MultiSubnetFailoverWithNonTcpProtocol();
                        // continue building SqlError instance
                }
            }

            SqlClientEventSource.Log.TryAdvancedTraceEvent("< sc.TdsParser.ProcessSNIError |ERR|ADV > Error message Detail: {0}", details.errorMessage);

            Debug.Assert(!string.IsNullOrEmpty(details.errorMessage) || details.sniErrorNumber != 0, "Empty error message received from SNI");
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.ProcessSNIError |ERR|ADV > Empty error message received from SNI. Error Message = {0}, SNI Error Number ={1}", details.errorMessage, details.sniErrorNumber);

            string sqlContextInfo = StringsHelper.GetResourceString(sniContext.ToString());
            string providerRid = string.Format("SNI_PN{0}", details.provider);
            string providerName = StringsHelper.GetResourceString(providerRid);
            Debug.Assert(!string.IsNullOrEmpty(providerName), $"invalid providerResourceId '{providerRid}'");
            uint win32ErrorCode = details.nativeError;
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.ProcessSNIError |ERR|ADV > SNI Native Error Code = {0}", win32ErrorCode);

            string errorMessage = details.errorMessage;
            if (details.sniErrorNumber == 0)
            {
                // Provider error. The message from provider is preceeded with non-localizable info from SNI
                // strip provider info from SNI
                //
                int iColon = errorMessage.IndexOf(':');
                Debug.Assert(0 <= iColon, "':' character missing in sni errorMessage");
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.ProcessSNIError |ERR|ADV > ':' character missing in sni errorMessage. Error Message index of ':' = {0}", iColon);
                Debug.Assert(errorMessage.Length > iColon + 1 && errorMessage[iColon + 1] == ' ', "Expecting a space after the ':' character");
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.TdsParser.ProcessSNIError |ERR|ADV > Expecting a space after the ':' character. Error Message Length = {0}", errorMessage.Length);
                // extract the message excluding the colon and trailing cr/lf chars
                if (0 <= iColon)
                {
                    int len = errorMessage.Length;
                    len -= Environment.NewLine.Length; // exclude newline sequence
                    iColon += 2;  // skip over ": " sequence
                    len -= iColon;
                    /*
                        The error message should come back in the following format: "TCP Provider: MESSAGE TEXT"
                        If the message is received on a Win9x OS, the error message will not contain MESSAGE TEXT
                        If we get an error message with no message text, just return the entire message otherwise
                        return just the message text.
                    */
                    if (len > 0)
                    {
                        errorMessage = errorMessage.Substring(iColon, len);
                    }
                }
            }
            else
            {

                // SNI error. Append additional error message info if available and hasn't been included.
                string sniLookupMessage = SQL.GetSNIErrorMessage((int)details.sniErrorNumber);
                errorMessage = (string.IsNullOrEmpty(errorMessage) || errorMessage.Contains(sniLookupMessage))
                                ? sniLookupMessage
                                : (sniLookupMessage + ": " + errorMessage);
            }
            errorMessage = string.Format("{0} (provider: {1}, error: {2} - {3})",
                sqlContextInfo, providerName, (int)details.sniErrorNumber, errorMessage);

            SqlClientEventSource.Log.TryAdvancedTraceErrorEvent("<sc.TdsParser.ProcessSNIError |ERR|ADV > SNI Error Message. Native Error = {0}, Line Number ={1}, Function ={2}, Exception ={3}, Server = {4}",
                (int)details.nativeError, (int)details.lineNumber, details.function, details.exception, serverName);

            return new SqlError(infoNumber: (int)details.nativeError, errorState: 0x00, TdsEnums.FATAL_ERROR_CLASS, serverName,
                errorMessage, details.function, (int)details.lineNumber, win32ErrorCode: details.nativeError, details.exception);
        }


        public static SqlError ProviderToSqlError(this DataSource datasource, uint nativeError, uint sniErrorCode, string errorMessage, SniContext sniContext, string serverName)
        {
            return new SNIError(datasource.ResolveProvider(), nativeError, sniErrorCode, errorMessage).ToSqlError(sniContext, serverName);
        }

        public static SqlError ProviderToSqlError(this DataSource datasource, uint sniErrorCode, Exception sniException, SniContext sniContext, string serverName, uint nativeErrorCode = 0)
        {
            return new SNIError(datasource.ResolveProvider(), sniErrorCode, sniException, nativeErrorCode).ToSqlError(sniContext, serverName);
        }

        public static SqlError ToSqlError(this SNIProviders provider, uint nativeError, uint sniErrorCode, string errorMessage, SniContext sniContext, string serverName)
        {
            return new SNIError(provider, nativeError, sniErrorCode, errorMessage).ToSqlError(sniContext, serverName);
        }

        public static SqlError CreateSqlError(this SNIProviders provider, uint sniErrorCode, Exception sniException, SniContext sniContext, string serverName, uint nativeErrorCode = 0)
        {
            return new SNIError(provider, sniErrorCode, sniException, nativeErrorCode).ToSqlError(sniContext, serverName);
        }

        /// <summary>
        /// Resolves the SNIProviders from Datasource.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static SNIProviders ResolveProvider(this DataSource dataSource)
        {
            return dataSource.ResolvedProtocol switch
            {
                DataSource.Protocol.TCP => SNIProviders.TCP_PROV,
                _ => throw new NotImplementedException($"{dataSource.ResolvedProtocol.ToString()} provider error handling is not supported"),
            };
        }
    }
}
