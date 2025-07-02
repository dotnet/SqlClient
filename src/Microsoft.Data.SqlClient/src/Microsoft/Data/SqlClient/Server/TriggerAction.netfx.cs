// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

namespace Microsoft.Data.SqlClient.Server
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/TriggerAction/*' />
    public enum TriggerAction
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Invalid/*' />
        Invalid = 0, // Not actually a valid type: not persisted.
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Insert/*' />
        Insert = 1,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Update/*' />
        Update = 2,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Delete/*' />
        Delete = 3,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateTable/*' />
        CreateTable = 21,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterTable/*' />
        AlterTable = 22,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropTable/*' />
        DropTable = 23,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateIndex/*' />
        CreateIndex = 24,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterIndex/*' />
        AlterIndex = 25,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropIndex/*' />
        DropIndex = 26,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateSecurityExpression/*' />
        CreateSecurityExpression = 31,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropSecurityExpression/*' />
        DropSecurityExpression = 33,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateSynonym/*' />
        CreateSynonym = 34,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropSynonym/*' />
        DropSynonym = 36,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateView/*' />
        CreateView = 41,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterView/*' />
        AlterView = 42,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropView/*' />
        DropView = 43,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateProcedure/*' />
        CreateProcedure = 51,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterProcedure/*' />
        AlterProcedure = 52,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropProcedure/*' />
        DropProcedure = 53,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateFunction/*' />
        CreateFunction = 61,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterFunction/*' />
        AlterFunction = 62,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropFunction/*' />
        DropFunction = 63,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateTrigger/*' />
        CreateTrigger = 71,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterTrigger/*' />
        AlterTrigger = 72,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropTrigger/*' />
        DropTrigger = 73,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateEventNotification/*' />
        CreateEventNotification = 74,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropEventNotification/*' />
        DropEventNotification = 76,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateType/*' />
        CreateType = 91,
        
        //	AlterType = 92,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropType/*' />
        DropType = 93,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateAssembly/*' />
        CreateAssembly = 101,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterAssembly/*' />
        AlterAssembly = 102,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropAssembly/*' />
        DropAssembly = 103,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateUser/*' />
        CreateUser = 131,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterUser/*' />
        AlterUser = 132,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropUser/*' />
        DropUser = 133,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateRole/*' />
        CreateRole = 134,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterRole/*' />
        AlterRole = 135,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropRole/*' />
        DropRole = 136,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateAppRole/*' />
        CreateAppRole = 137,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterAppRole/*' />
        AlterAppRole = 138,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropAppRole/*' />
        DropAppRole = 139,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateSchema/*' />
        CreateSchema = 141,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterSchema/*' />
        AlterSchema = 142,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropSchema/*' />
        DropSchema = 143,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateLogin/*' />
        CreateLogin = 144,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterLogin/*' />
        AlterLogin = 145,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropLogin/*' />
        DropLogin = 146,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateMsgType/*' />
        CreateMsgType = 151,
        
        // AlterMsgType = 152
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropMsgType/*' />
        DropMsgType = 153,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateContract/*' />
        CreateContract = 154,
        
        // AlterContract = 155
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropContract/*' />
        DropContract = 156,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateQueue/*' />
        CreateQueue = 157,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterQueue/*' />
        AlterQueue = 158,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropQueue/*' />
        DropQueue = 159,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateService/*' />
        CreateService = 161,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterService/*' />
        AlterService = 162,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropService/*' />
        DropService = 163,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateRoute/*' />
        CreateRoute = 164,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterRoute/*' />
        AlterRoute = 165,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropRoute/*' />
        DropRoute = 166,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/GrantStatement/*' />
        GrantStatement = 167,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DenyStatement/*' />
        DenyStatement = 168,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/RevokeStatement/*' />
        RevokeStatement = 169,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/GrantObject/*' />
        GrantObject = 170,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DenyObject/*' />
        DenyObject = 171,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/RevokeObject/*' />
        RevokeObject = 172,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateBinding/*' />
        CreateBinding = 174,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterBinding/*' />
        AlterBinding = 175,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropBinding/*' />
        DropBinding = 176,
        
        // CreateXmlSchema = 177
        // AlterXmlSchema = 178
        // DropXmlSchema = 179
        // CreateHttpEndpoint = 181
        // AlterHttpEndpoint = 182
        // DropHttpEndpoint = 183
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreatePartitionFunction/*' />
        CreatePartitionFunction = 191,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterPartitionFunction/*' />
        AlterPartitionFunction = 192,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropPartitionFunction/*' />
        DropPartitionFunction = 193,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreatePartitionScheme/*' />
        CreatePartitionScheme = 194,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterPartitionScheme/*' />
        AlterPartitionScheme = 195,
        
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient.Server/TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropPartitionScheme/*' />
        DropPartitionScheme = 196,
        
        // CreateDatabase = 201
        // AlterDatabase = 202
        // DropDatabase = 203
        
        // 1000-1999 is reserved for SqlTrace
        // TraceStart = 1000
        // TraceEnd = 1999
    }
}

#endif
