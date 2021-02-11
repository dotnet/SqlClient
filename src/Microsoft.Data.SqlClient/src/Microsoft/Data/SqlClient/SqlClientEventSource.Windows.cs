// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Microsoft.Data.SqlClient
{
    internal partial class SqlClientEventSource : EventSource
    {
        private bool _traceLoggingProviderEnabled = false;

        /// <summary>
        /// Captures application flow traces from native networking implementation
        /// </summary>
        private const EventCommand SNINativeTrace = (EventCommand)8192;

        /// <summary>
        /// Captures scope trace events from native networking implementation
        /// </summary>
        private const EventCommand SNINativeScope = (EventCommand)16384;

        /// <summary>
        /// Disables all event tracing in native networking implementation
        /// </summary>
        private const EventCommand SNINativeDisable = (EventCommand)32768;

        protected override void OnEventCommand(EventCommandEventArgs e)
        {
            // Internally, EventListener.EnableEvents sends an event command, with a reserved value of 0, -2, or -3.
            // When a command is sent via EnableEvents or SendCommand, check if it is a user-defined value
            // to enable or disable event tracing in sni.dll.
            // If registration fails, all write and unregister commands will be a no-op.

            // If managed networking is enabled, don't call native wrapper methods
#if NETCOREAPP || NETSTANDARD
            if (AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", out bool isEnabled) && isEnabled)
            {
                return;
            }
#endif
            // Only register the provider if it's not already registered. Registering a provider that is already
            // registered can lead to unpredictable behaviour.
            if (!_traceLoggingProviderEnabled && e.Command > 0 && (e.Command & (SNINativeTrace | SNINativeScope)) != 0)
            {
                int eventKeyword = (int)(e.Command & (SNINativeTrace | SNINativeScope));
                _traceLoggingProviderEnabled = SNINativeMethodWrapper.RegisterTraceProvider(eventKeyword);
                Debug.Assert(_traceLoggingProviderEnabled, "Failed to enable TraceLogging provider.");
            }
            else if (_traceLoggingProviderEnabled && (e.Command == SNINativeDisable))
            {
                // Only unregister the provider if it's currently registered.
                SNINativeMethodWrapper.UnregisterTraceProvider();
                _traceLoggingProviderEnabled = false;
            }
        }
    }
}
