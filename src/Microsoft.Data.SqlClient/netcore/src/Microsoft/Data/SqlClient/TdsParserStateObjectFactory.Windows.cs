// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClient
{
    internal sealed class TdsParserStateObjectFactory
    {
        public static readonly TdsParserStateObjectFactory Singleton = new TdsParserStateObjectFactory();

        /* Managed SNI can be enabled on Windows by setting any of the below two environment variables to 'True':
         * Microsoft.Data.SqlClient.UseManagedSNIOnWindows (Supported to respect namespace format)
         * Microsoft_Data_SqlClient_UseManagedSNIOnWindows (Supported for Azure Pipelines)
        **/
        private static Lazy<bool> useManagedSNIOnWindows = new Lazy<bool>(
            () => bool.TrueString.Equals(Environment.GetEnvironmentVariable("Microsoft.Data.SqlClient.UseManagedSNIOnWindows"),
                                        StringComparison.InvariantCultureIgnoreCase) ||
                  bool.TrueString.Equals(Environment.GetEnvironmentVariable("Microsoft_Data_SqlClient_UseManagedSNIOnWindows"),
                                        StringComparison.InvariantCultureIgnoreCase)
        );
        public static bool UseManagedSNI => useManagedSNIOnWindows.Value;

        public EncryptionOptions EncryptionOptions
        {
            get
            {
                return UseManagedSNI ? SNI.SNILoadHandle.SingletonInstance.Options : SNILoadHandle.SingletonInstance.Options;
            }
        }

        public uint SNIStatus
        {
            get
            {
                return UseManagedSNI ? SNI.SNILoadHandle.SingletonInstance.Status : SNILoadHandle.SingletonInstance.Status;
            }
        }

        public TdsParserStateObject CreateTdsParserStateObject(TdsParser parser)
        {
            if (UseManagedSNI)
            {
                return new TdsParserStateObjectManaged(parser);
            }
            else
            {
                return new TdsParserStateObjectNative(parser);
            }
        }

        internal TdsParserStateObject CreateSessionObject(TdsParser tdsParser, TdsParserStateObject _pMarsPhysicalConObj, bool v)
        {
            if (TdsParserStateObjectFactory.UseManagedSNI)
            {
                return new TdsParserStateObjectManaged(tdsParser, _pMarsPhysicalConObj, true);
            }
            else
            {
                return new TdsParserStateObjectNative(tdsParser, _pMarsPhysicalConObj, true);
            }
        }
    }
}
