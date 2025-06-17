// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Server
{
    // Class for implementing a record object that could take advantage of the
    // environment available to a particular protocol level (such as storing data 
    // in native structures for in-proc data access).  Includes methods to send 
    // the record to a context pipe (useful for in-proc scenarios).
    internal abstract class SmiRecordBuffer : SmiTypedGetterSetter
    {
        // SmiRecordBuffer defaults both CanGet and CanSet to true to support
        //  already-shipped SMIV3 record buffer classes. Sub-classes are free to override.
        internal override bool CanGet => true;

        internal override bool CanSet => true;
    }
}
