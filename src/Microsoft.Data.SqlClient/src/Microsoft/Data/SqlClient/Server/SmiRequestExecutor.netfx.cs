// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Data;
using System.Transactions;

namespace Microsoft.Data.SqlClient.Server
{
    // @TODO: This class is abstract but nothing inherits from it. Code paths that use it are likely very dead.
    internal abstract class SmiRequestExecutor : SmiTypedGetterSetter
    {
        public virtual void Close(SmiEventSink eventSink)
        {
            // Adding as of V3

            // Implement body with throw because there are only a couple of ways to get to this code:
            //  1) Client is calling this method even though the server negotiated for V2- and hasn't implemented V3 yet.
            //  2) Server didn't implement V3 on some interface, but negotiated V3+.
            throw Common.ADP.InternalError(Common.ADP.InternalErrorCode.UnimplementedSMIMethod);
        }
        
        internal virtual SmiEventStream Execute(
            SmiConnection connection,            // Assigned connection
            long transactionId,                  // Assigned transaction
            Transaction associatedTransaction,   // SysTx transaction associated with request, if any.
            CommandBehavior behavior,            // CommandBehavior,   
            SmiExecuteType executeType           // Type of execute called (NonQuery/Pipe/Reader/Row, etc)
        )
        {
            // Adding as of V210

            // Implement body with throw because there are only a couple of ways to get to this code:
            //  1) Client is calling this method even though the server negotiated for V200- and hasn't implemented V210 yet.
            //  2) Server didn't implement V210 on some interface, but negotiated V210+.
            throw Common.ADP.InternalError(Common.ADP.InternalErrorCode.UnimplementedSMIMethod);
        }
        
        internal virtual SmiEventStream Execute(
            SmiConnection connection,                     // Assigned connection
            long transactionId,                  // Assigned transaction
            CommandBehavior behavior,                       // CommandBehavior,   
            SmiExecuteType executeType                     // Type of execute called (NonQuery/Pipe/Reader/Row, etc)
        )
        {
            // Obsoleting as of V210

            // Implement body with throw because there are only a couple of ways to get to this code:
            //  1) Client is calling this method even though the server negotiated for V210+ (and doesn't implement it).
            //  2) Server doesn't implement this method, but negotiated V200-.
            throw Common.ADP.InternalError(Common.ADP.InternalErrorCode.UnimplementedSMIMethod);
        }

        // RequestExecutor only supports setting parameter values, not getting
        internal override bool CanGet => false;

        internal override bool CanSet => true;

        // SmiRequestExecutor and it's subclasses should NOT override Getters from SmiTypedGetterSetter
        //  Calls against those methods on a Request Executor are not allowed.

        // Set DEFAULT bit for parameter
        internal abstract void SetDefault(int ordinal);

        // SmiRequestExecutor subclasses must implement all Setters from SmiTypedGetterSetter
        //  SmiRequestExecutor itself does not need to implement these, since it inherits the default implementation from 
        //      SmiTypedGetterSetter
        
        
    }
}

#endif
