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
            if (InOutOfProcHelper.InProc)
            {
                Type smiLinkType = Type.GetType("Microsoft.SqlServer.Server.InProcLink, SqlAccess, PublicKeyToken=89845dcd8080cc91");

                if (smiLinkType == null)
                {
                    Debug.Assert(false, "could not get InProcLink type");
                    throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server.
                }

                System.Reflection.FieldInfo instanceField = GetStaticField(smiLinkType, "Instance");
                if (instanceField != null)
                {
                    _smiLink = (SmiLink)GetValue(instanceField);
                }
                else
                {
                    Debug.Assert(false, "could not get InProcLink.Instance");
                    throw SQL.ContextUnavailableOutOfProc();    // Must not be a valid version of Sql Server.
                }

                System.Reflection.FieldInfo buildVersionField = GetStaticField(smiLinkType, "BuildVersion");
                if (buildVersionField != null)
                {
                    uint buildVersion = (uint)GetValue(buildVersionField);

                    byte majorVersion = (byte)(buildVersion >> 24);
                    byte minorVersion = (byte)((buildVersion >> 16) & 0xff);
                    short buildNum = (short)(buildVersion & 0xffff);
                    _serverVersion = string.Format(null, "{0:00}.{1:00}.{2:0000}", majorVersion, (short)minorVersion, buildNum);
                }
                else
                {
                    _serverVersion = string.Empty;  // default value if nothing exists.
                }
                
                _negotiatedSmiVersion = _smiLink.NegotiateVersion(SmiLink.InterfaceVersion);
                bool isSupportedVersion = false;
                for (int i = 0; !isSupportedVersion && i < _supportedSmiVersions.Length; i++)
                {
                    if (_supportedSmiVersions[i] == _negotiatedSmiVersion)
                    {
                        isSupportedVersion = true;
                    }
                }

                // Disconnect if we didn't get a supported version!!
                if (!isSupportedVersion)
                {
                    _smiLink = null;
                }

                _eventSinkForGetCurrentContext = new SmiEventSink_Default();
            }
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
