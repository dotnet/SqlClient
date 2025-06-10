// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Diagnostics;
using System.Security.Permissions;

namespace Microsoft.Data.SqlClient.Server
{
    internal sealed class SmiContextFactory
    {
        public static readonly SmiContextFactory Instance = new SmiContextFactory();

        private readonly SmiLink _smiLink;
        private readonly ulong _negotiatedSmiVersion;
        private readonly string _serverVersion;
        private readonly SmiEventSink_Default _eventSinkForGetCurrentContext;

        internal const ulong Sql2005Version = 100;
        internal const ulong Sql2008Version = 210;
        internal const ulong LatestVersion = Sql2008Version;

        private readonly ulong[] _supportedSmiVersions = { Sql2005Version, Sql2008Version };

        // Used as the key for SmiContext.GetContextValue()
        internal enum ContextKey
        {
            Connection = 0,
            SqlContext = 1
        }

        private SmiContextFactory()
        {
        }

        internal ulong NegotiatedSmiVersion
        {
            get
            {
                if (_smiLink == null)
                {
                    throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server, or not be SqlCLR
                }

                return _negotiatedSmiVersion;
            }
        }

        internal string ServerVersion
        {
            get
            {
                if (_smiLink == null)
                {
                    throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server, or not be SqlCLR
                }

                return _serverVersion;
            }
        }

        internal SmiContext GetCurrentContext()
        {
            if (_smiLink == null)
            {
                throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server, or not be SqlCLR
            }

            object result = _smiLink.GetCurrentContext(_eventSinkForGetCurrentContext);
            _eventSinkForGetCurrentContext.ProcessMessagesAndThrow();

            if (result == null)
            {
                throw SQL.ContextUnavailableWhileInProc();
            }

            Debug.Assert(result is SmiContext, "didn't get SmiContext from GetCurrentContext?");
            return (SmiContext)result;
        }

        [ReflectionPermission(SecurityAction.Assert, MemberAccess = true)]
        private object GetValue(System.Reflection.FieldInfo fieldInfo)
        {
            object result = fieldInfo.GetValue(null);
            return result;
        }

        [ReflectionPermission(SecurityAction.Assert, MemberAccess = true)]
        private System.Reflection.FieldInfo GetStaticField(Type aType, string fieldName)
        {
            System.Reflection.FieldInfo result = aType.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.GetField);
            return result;
        }
    }
}

#endif
