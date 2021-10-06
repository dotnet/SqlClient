// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Server
{
    internal partial class SmiEventSink_Default : SmiEventSink
    {
        private SqlErrorCollection _errors;
        private SqlErrorCollection _warnings;

        virtual internal string ServerVersion
        {
            get
            {
                return null;
            }
        }

        internal SmiEventSink_Default()
        {
        }
    }
}

