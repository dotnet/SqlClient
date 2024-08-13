// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClientX.Tds.State;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests.Util
{
    internal class TestTdsEventListener : ITdsEventListener
    {
        private bool _fireInfoMessageEvents = false;

        public bool FireInfoMessageEventOnUserErrors
        {
            get => _fireInfoMessageEvents;
            set => _fireInfoMessageEvents = value;
        }

        public void OnInfoMessage(SqlInfoMessageEventArgs sqlInfoMessageEventArgs, out bool notified)
        {
            notified = false;
            // Do nothing - consume info events.
        }
    }
}
