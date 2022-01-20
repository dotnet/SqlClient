// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Server
{
    internal enum EMDEventType
    {
        x_eet_Invalid = 0,    // Not actually a valid type: not persisted.
        x_eet_Insert = 1,    // Insert (eg. on table/view)
        x_eet_Update = 2,    // Update (eg. on table/view)
        x_eet_Delete = 3,    // Delete (eg. on table/view)
        x_eet_Create_Table = 21,
        x_eet_Alter_Table = 22,
        x_eet_Drop_Table = 23,
        x_eet_Create_Index = 24,
        x_eet_Alter_Index = 25,
        x_eet_Drop_Index = 26,
        x_eet_Create_Stats = 27,
        x_eet_Update_Stats = 28,
        x_eet_Drop_Stats = 29,
        x_eet_Create_Secexpr = 31,
        x_eet_Drop_Secexpr = 33,
        x_eet_Create_Synonym = 34,
        x_eet_Drop_Synonym = 36,
        x_eet_Create_View = 41,
        x_eet_Alter_View = 42,
        x_eet_Drop_View = 43,
        x_eet_Create_Procedure = 51,
        x_eet_Alter_Procedure = 52,
        x_eet_Drop_Procedure = 53,
        x_eet_Create_Function = 61,
        x_eet_Alter_Function = 62,
        x_eet_Drop_Function = 63,
        x_eet_Create_Trigger = 71,   // On database/server/table
        x_eet_Alter_Trigger = 72,
        x_eet_Drop_Trigger = 73,
        x_eet_Create_Event_Notification = 74,
        x_eet_Drop_Event_Notification = 76,
        x_eet_Create_Type = 91,
        //  x_eet_Alter_Type        = 92,
        x_eet_Drop_Type = 93,
        x_eet_Create_Assembly = 101,
        x_eet_Alter_Assembly = 102,
        x_eet_Drop_Assembly = 103,
        x_eet_Create_User = 131,
        x_eet_Alter_User = 132,
        x_eet_Drop_User = 133,
        x_eet_Create_Role = 134,
        x_eet_Alter_Role = 135,
        x_eet_Drop_Role = 136,
        x_eet_Create_AppRole = 137,
        x_eet_Alter_AppRole = 138,
        x_eet_Drop_AppRole = 139,
        x_eet_Create_Schema = 141,
        x_eet_Alter_Schema = 142,
        x_eet_Drop_Schema = 143,
        x_eet_Create_Login = 144,
        x_eet_Alter_Login = 145,
        x_eet_Drop_Login = 146,
        x_eet_Create_MsgType = 151,
        x_eet_Alter_MsgType = 152,
        x_eet_Drop_MsgType = 153,
        x_eet_Create_Contract = 154,
        x_eet_Alter_Contract = 155,
        x_eet_Drop_Contract = 156,
        x_eet_Create_Queue = 157,
        x_eet_Alter_Queue = 158,
        x_eet_Drop_Queue = 159,
        x_eet_Create_Service = 161,
        x_eet_Alter_Service = 162,
        x_eet_Drop_Service = 163,
        x_eet_Create_Route = 164,
        x_eet_Alter_Route = 165,
        x_eet_Drop_Route = 166,
        x_eet_Grant_Statement = 167,
        x_eet_Deny_Statement = 168,
        x_eet_Revoke_Statement = 169,
        x_eet_Grant_Object = 170,
        x_eet_Deny_Object = 171,
        x_eet_Revoke_Object = 172,
        x_eet_Activation = 173,
        x_eet_Create_Binding = 174,
        x_eet_Alter_Binding = 175,
        x_eet_Drop_Binding = 176,
        x_eet_Create_XmlSchema = 177,
        x_eet_Alter_XmlSchema = 178,
        x_eet_Drop_XmlSchema = 179,
        x_eet_Create_HttpEndpoint = 181,
        x_eet_Alter_HttpEndpoint = 182,
        x_eet_Drop_HttpEndpoint = 183,
        x_eet_Create_Partition_Function = 191,
        x_eet_Alter_Partition_Function = 192,
        x_eet_Drop_Partition_Function = 193,
        x_eet_Create_Partition_Scheme = 194,
        x_eet_Alter_Partition_Scheme = 195,
        x_eet_Drop_Partition_Scheme = 196,

        x_eet_Create_Database = 201,
        x_eet_Alter_Database = 202,
        x_eet_Drop_Database = 203,

        // 1000 - 1999 is reserved for SQLTrace.
        x_eet_Trace_Start = 1000,
        x_eet_Trace_End = 1999,
        // WHEN ADDING, PLEASE CHECK WITH FILE-OWNER FOR WHICH NUMBERS TO USE.  THANKS!
    };

    /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/TriggerAction/*' />
    public enum TriggerAction
    {
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Invalid/*' />
        Invalid = EMDEventType.x_eet_Invalid,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Insert/*' />
        Insert = EMDEventType.x_eet_Insert,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Update/*' />
        Update = EMDEventType.x_eet_Update,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/Delete/*' />
        Delete = EMDEventType.x_eet_Delete,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateTable/*' />
        CreateTable = EMDEventType.x_eet_Create_Table,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterTable/*' />
        AlterTable = EMDEventType.x_eet_Alter_Table,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropTable/*' />
        DropTable = EMDEventType.x_eet_Drop_Table,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateIndex/*' />
        CreateIndex = EMDEventType.x_eet_Create_Index,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterIndex/*' />
        AlterIndex = EMDEventType.x_eet_Alter_Index,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropIndex/*' />
        DropIndex = EMDEventType.x_eet_Drop_Index,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateSynonym/*' />
        CreateSynonym = EMDEventType.x_eet_Create_Synonym,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropSynonym/*' />
        DropSynonym = EMDEventType.x_eet_Drop_Synonym,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateSecurityExpression/*' />
        CreateSecurityExpression = EMDEventType.x_eet_Create_Secexpr,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropSecurityExpression/*' />
        DropSecurityExpression = EMDEventType.x_eet_Drop_Secexpr,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateView/*' />
        CreateView = EMDEventType.x_eet_Create_View,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterView/*' />
        AlterView = EMDEventType.x_eet_Alter_View,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropView/*' />
        DropView = EMDEventType.x_eet_Drop_View,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateProcedure/*' />
        CreateProcedure = EMDEventType.x_eet_Create_Procedure,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterProcedure/*' />
        AlterProcedure = EMDEventType.x_eet_Alter_Procedure,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropProcedure/*' />
        DropProcedure = EMDEventType.x_eet_Drop_Procedure,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateFunction/*' />
        CreateFunction = EMDEventType.x_eet_Create_Function,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterFunction/*' />
        AlterFunction = EMDEventType.x_eet_Alter_Function,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropFunction/*' />
        DropFunction = EMDEventType.x_eet_Drop_Function,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateTrigger/*' />
        CreateTrigger = EMDEventType.x_eet_Create_Trigger,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterTrigger/*' />
        AlterTrigger = EMDEventType.x_eet_Alter_Trigger,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropTrigger/*' />
        DropTrigger = EMDEventType.x_eet_Drop_Trigger,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateEventNotification/*' />
        CreateEventNotification = EMDEventType.x_eet_Create_Event_Notification,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropEventNotification/*' />
        DropEventNotification = EMDEventType.x_eet_Drop_Event_Notification,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateType/*' />
        CreateType = EMDEventType.x_eet_Create_Type,
        //	Alter_Type = EMDEventType.x_eet_Alter_Type,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropType/*' />
        DropType = EMDEventType.x_eet_Drop_Type,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateAssembly/*' />
        CreateAssembly = EMDEventType.x_eet_Create_Assembly,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterAssembly/*' />
        AlterAssembly = EMDEventType.x_eet_Alter_Assembly,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropAssembly/*' />
        DropAssembly = EMDEventType.x_eet_Drop_Assembly,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateUser/*' />
        CreateUser = EMDEventType.x_eet_Create_User,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterUser/*' />
        AlterUser = EMDEventType.x_eet_Alter_User,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropUser/*' />
        DropUser = EMDEventType.x_eet_Drop_User,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateRole/*' />
        CreateRole = EMDEventType.x_eet_Create_Role,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterRole/*' />
        AlterRole = EMDEventType.x_eet_Alter_Role,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropRole/*' />
        DropRole = EMDEventType.x_eet_Drop_Role,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateAppRole/*' />
        CreateAppRole = EMDEventType.x_eet_Create_AppRole,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterAppRole/*' />
        AlterAppRole = EMDEventType.x_eet_Alter_AppRole,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropAppRole/*' />
        DropAppRole = EMDEventType.x_eet_Drop_AppRole,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateSchema/*' />
        CreateSchema = EMDEventType.x_eet_Create_Schema,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterSchema/*' />
        AlterSchema = EMDEventType.x_eet_Alter_Schema,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropSchema/*' />
        DropSchema = EMDEventType.x_eet_Drop_Schema,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateLogin/*' />
        CreateLogin = EMDEventType.x_eet_Create_Login,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterLogin/*' />
        AlterLogin = EMDEventType.x_eet_Alter_Login,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropLogin/*' />
        DropLogin = EMDEventType.x_eet_Drop_Login,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateMsgType/*' />
        CreateMsgType = EMDEventType.x_eet_Create_MsgType,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropMsgType/*' />
        DropMsgType = EMDEventType.x_eet_Drop_MsgType,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateContract/*' />
        CreateContract = EMDEventType.x_eet_Create_Contract,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropContract/*' />
        DropContract = EMDEventType.x_eet_Drop_Contract,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateQueue/*' />
        CreateQueue = EMDEventType.x_eet_Create_Queue,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterQueue/*' />
        AlterQueue = EMDEventType.x_eet_Alter_Queue,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropQueue/*' />
        DropQueue = EMDEventType.x_eet_Drop_Queue,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateService/*' />
        CreateService = EMDEventType.x_eet_Create_Service,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterService/*' />
        AlterService = EMDEventType.x_eet_Alter_Service,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropService/*' />
        DropService = EMDEventType.x_eet_Drop_Service,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateRoute/*' />
        CreateRoute = EMDEventType.x_eet_Create_Route,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterRoute/*' />
        AlterRoute = EMDEventType.x_eet_Alter_Route,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropRoute/*' />
        DropRoute = EMDEventType.x_eet_Drop_Route,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/GrantStatement/*' />
        GrantStatement = EMDEventType.x_eet_Grant_Statement,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DenyStatement/*' />
        DenyStatement = EMDEventType.x_eet_Deny_Statement,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/RevokeStatement/*' />
        RevokeStatement = EMDEventType.x_eet_Revoke_Statement,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/GrantObject/*' />
        GrantObject = EMDEventType.x_eet_Grant_Object,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DenyObject/*' />
        DenyObject = EMDEventType.x_eet_Deny_Object,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/RevokeObject/*' />
        RevokeObject = EMDEventType.x_eet_Revoke_Object,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreateBinding/*' />
        CreateBinding = EMDEventType.x_eet_Create_Binding,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterBinding/*' />
        AlterBinding = EMDEventType.x_eet_Alter_Binding,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropBinding/*' />
        DropBinding = EMDEventType.x_eet_Drop_Binding,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreatePartitionFunction/*' />
        CreatePartitionFunction = EMDEventType.x_eet_Create_Partition_Function,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterPartitionFunction/*' />
        AlterPartitionFunction = EMDEventType.x_eet_Alter_Partition_Function,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropPartitionFunction/*' />
        DropPartitionFunction = EMDEventType.x_eet_Drop_Partition_Function,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/CreatePartitionScheme/*' />
        CreatePartitionScheme = EMDEventType.x_eet_Create_Partition_Scheme,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/AlterPartitionScheme/*' />
        AlterPartitionScheme = EMDEventType.x_eet_Alter_Partition_Scheme,
        /// <include file='..\..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient.Server\TriggerAction.xml' path='docs/members[@name="TriggerAction"]/DropPartitionScheme/*' />
        DropPartitionScheme = EMDEventType.x_eet_Drop_Partition_Scheme,
    }
}
