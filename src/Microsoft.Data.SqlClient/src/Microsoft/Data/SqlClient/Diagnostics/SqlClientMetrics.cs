// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;

#if NET
using System.Threading;
#else
#if _WINDOWS
using Interop.Windows.Kernel32;
#endif
using Microsoft.Data.Common;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Versioning;
using System.Security.Permissions;
#endif

#nullable enable

namespace Microsoft.Data.SqlClient.Diagnostics
{
#if NETFRAMEWORK
    [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
#endif
    internal sealed partial class SqlClientMetrics
    {
#if NETFRAMEWORK
        private const string PerformanceCounterCategoryName = ".NET Data Provider for SqlServer";
        private const string PerformanceCounterCategoryHelp = "Counters for Microsoft.Data.SqlClient";
        private const int CounterInstanceNameMaxLength = 127;
#endif

        private readonly SqlClientEventSource _eventSource;
        // The names of the below variables must match between .NET Framework and .NET Core.
#if NET
        private PollingCounter? _activeHardConnectionsCounter;
        private long _activeHardConnections = 0;
        private IncrementingPollingCounter? _hardConnectsPerSecondCounter;
        private long _hardConnectsRate = 0;
        private IncrementingPollingCounter? _hardDisconnectsPerSecondCounter;
        private long _hardDisconnectsRate = 0;

        private PollingCounter? _activeSoftConnectionsCounter;
        private long _activeSoftConnections = 0;
        private IncrementingPollingCounter? _softConnectsPerSecondCounter;
        private long _softConnectsRate = 0;
        private IncrementingPollingCounter? _softDisconnectsPerSecondCounter;
        private long _softDisconnectsRate = 0;

        private PollingCounter? _numberOfNonPooledConnectionsCounter;
        private long _nonPooledConnections = 0;
        private PollingCounter? _numberOfPooledConnectionsCounter;
        private long _pooledConnections = 0;

        private PollingCounter? _numberOfActiveConnectionPoolGroupsCounter;
        private long _activeConnectionPoolGroups = 0;
        private PollingCounter? _numberOfInactiveConnectionPoolGroupsCounter;
        private long _inactiveConnectionPoolGroups = 0;

        private PollingCounter? _numberOfActiveConnectionPoolsCounter;
        private long _activeConnectionPools = 0;
        private PollingCounter? _numberOfInactiveConnectionPoolsCounter;
        private long _inactiveConnectionPools = 0;

        private PollingCounter? _numberOfActiveConnectionsCounter;
        private long _activeConnections = 0;
        private PollingCounter? _numberOfFreeConnectionsCounter;
        private long _freeConnections = 0;
        private PollingCounter? _numberOfStasisConnectionsCounter;
        private long _stasisConnections = 0;
        private IncrementingPollingCounter? _numberOfReclaimedConnectionsCounter;
        private long _reclaimedConnections = 0;
#else
        private PerformanceCounter? _hardConnectsRate;
        private PerformanceCounter? _hardDisconnectsRate;
        private PerformanceCounter? _softConnectsRate;
        private PerformanceCounter? _softDisconnectsRate;
        private PerformanceCounter? _nonPooledConnections;
        private PerformanceCounter? _pooledConnections;
        private PerformanceCounter? _activeConnectionPoolGroups;
        private PerformanceCounter? _inactiveConnectionPoolGroups;
        private PerformanceCounter? _activeConnectionPools;
        private PerformanceCounter? _inactiveConnectionPools;
        private PerformanceCounter? _activeConnections;
        private PerformanceCounter? _freeConnections;
        private PerformanceCounter? _stasisConnections;
        private PerformanceCounter? _reclaimedConnections;

        private string? _instanceName;
#endif

        public SqlClientMetrics(SqlClientEventSource eventSource, bool enableMetrics)
        {
            _eventSource = eventSource;

#if NETFRAMEWORK
            // On .NET Framework, metrics are exposed as performance counters and are always enabled.
            // On .NET Core, metrics are exposed as EventCounters, and require explicit enablement.
            EnablePerformanceCounters();
#else
            if (enableMetrics)
            {
                EnableEventCounters();
            }
#endif
        }

#if NET
        private static void IncrementPlatformSpecificCounter(ref long counter)
            => Interlocked.Increment(ref counter);

        private static void DecrementPlatformSpecificCounter(ref long counter)
            => Interlocked.Decrement(ref counter);
#else
        // .NET Framework doesn't strictly require the PerformanceCounter parameter to be passed as a ref, but doing
        // so means that IncrementPlatformSpecificCounter and DecrementPlatformSpecificCounter can be called in identical
        // ways between .NET Framework and .NET Core.
        private static void IncrementPlatformSpecificCounter(ref PerformanceCounter? counter)
            => counter?.Increment();

        private static void DecrementPlatformSpecificCounter(ref PerformanceCounter? counter)
            => counter?.Decrement();
#endif

        /// <summary>
        /// The number of actual connections that are being made to servers
        /// </summary>
        internal void HardConnectRequest()
        {
#if NET
            IncrementPlatformSpecificCounter(ref _activeHardConnections);
#endif
            IncrementPlatformSpecificCounter(ref _hardConnectsRate);
        }

        /// <summary>
        /// The number of actual disconnects that are being made to servers
        /// </summary>
        internal void HardDisconnectRequest()
        {
#if NET
            DecrementPlatformSpecificCounter(ref _activeHardConnections);
#endif
            IncrementPlatformSpecificCounter(ref _hardDisconnectsRate);
        }

        /// <summary>
        /// The number of connections we get from the pool
        /// </summary>
        internal void SoftConnectRequest()
        {
#if NET
            IncrementPlatformSpecificCounter(ref _activeSoftConnections);
#endif
            IncrementPlatformSpecificCounter(ref _softConnectsRate);
        }

        /// <summary>
        /// The number of connections we return to the pool
        /// </summary>
        internal void SoftDisconnectRequest()
        {
#if NET
            DecrementPlatformSpecificCounter(ref _activeSoftConnections);
#endif
            IncrementPlatformSpecificCounter(ref _softDisconnectsRate);
        }

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        internal void EnterNonPooledConnection()
        {
            IncrementPlatformSpecificCounter(ref _nonPooledConnections);
        }

        /// <summary>
        /// The number of connections that are not using connection pooling
        /// </summary>
        internal void ExitNonPooledConnection()
        {
            DecrementPlatformSpecificCounter(ref _nonPooledConnections);
        }

        /// <summary>
        /// The number of connections that are managed by the connection pool
        /// </summary>
        internal void EnterPooledConnection()
        {
            IncrementPlatformSpecificCounter(ref _pooledConnections);
        }

        /// <summary>
        /// The number of connections that are managed by the connection pool
        /// </summary>
        internal void ExitPooledConnection()
        {
            DecrementPlatformSpecificCounter(ref _pooledConnections);
        }

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        internal void EnterActiveConnectionPoolGroup()
        {
            IncrementPlatformSpecificCounter(ref _activeConnectionPoolGroups);
        }

        /// <summary>
        /// The number of unique connection strings
        /// </summary>
        internal void ExitActiveConnectionPoolGroup()
        {
            DecrementPlatformSpecificCounter(ref _activeConnectionPoolGroups);
        }

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        internal void EnterInactiveConnectionPoolGroup()
        {
            IncrementPlatformSpecificCounter(ref _inactiveConnectionPoolGroups);
        }

        /// <summary>
        /// The number of unique connection strings waiting for pruning
        /// </summary>
        internal void ExitInactiveConnectionPoolGroup()
        {
            DecrementPlatformSpecificCounter(ref _inactiveConnectionPoolGroups);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void EnterActiveConnectionPool()
        {
            IncrementPlatformSpecificCounter(ref _activeConnectionPools);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void ExitActiveConnectionPool()
        {
            DecrementPlatformSpecificCounter(ref _activeConnectionPools);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void EnterInactiveConnectionPool()
        {
            IncrementPlatformSpecificCounter(ref _inactiveConnectionPools);
        }

        /// <summary>
        /// The number of connection pools
        /// </summary>
        internal void ExitInactiveConnectionPool()
        {
            DecrementPlatformSpecificCounter(ref _inactiveConnectionPools);
        }

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        internal void EnterActiveConnection()
        {
            IncrementPlatformSpecificCounter(ref _activeConnections);
        }

        /// <summary>
        /// The number of connections currently in-use
        /// </summary>
        internal void ExitActiveConnection()
        {
            DecrementPlatformSpecificCounter(ref _activeConnections);
        }

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        internal void EnterFreeConnection()
        {
            IncrementPlatformSpecificCounter(ref _freeConnections);
        }

        /// <summary>
        /// The number of connections currently available for use
        /// </summary>
        internal void ExitFreeConnection()
        {
            DecrementPlatformSpecificCounter(ref _freeConnections);
        }

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        internal void EnterStasisConnection()
        {
            IncrementPlatformSpecificCounter(ref _stasisConnections);
        }

        /// <summary>
        /// The number of connections currently waiting to be made ready for use
        /// </summary>
        internal void ExitStasisConnection()
        {
            DecrementPlatformSpecificCounter(ref _stasisConnections);
        }

        /// <summary>
        ///  The number of connections we reclaim from GC'd external connections
        /// </summary>
        internal void ReclaimedConnectionRequest()
        {
            IncrementPlatformSpecificCounter(ref _reclaimedConnections);
        }

#if NET
        public void EnableEventCounters()
        {
            _activeHardConnectionsCounter ??= new PollingCounter("active-hard-connections", _eventSource, () => _activeHardConnections)
            {
                DisplayName = "Actual active connections currently made to servers",
                DisplayUnits = "count"
            };

            _hardConnectsPerSecondCounter ??= new IncrementingPollingCounter("hard-connects", _eventSource, () => _hardConnectsRate)
            {
                DisplayName = "Actual connection rate to servers",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _hardDisconnectsPerSecondCounter ??= new IncrementingPollingCounter("hard-disconnects", _eventSource, () => _hardDisconnectsRate)
            {
                DisplayName = "Actual disconnection rate from servers",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _activeSoftConnectionsCounter ??= new PollingCounter("active-soft-connects", _eventSource, () => _activeSoftConnections)
            {
                DisplayName = "Active connections retrieved from the connection pool",
                DisplayUnits = "count"
            };

            _softConnectsPerSecondCounter ??= new IncrementingPollingCounter("soft-connects", _eventSource, () => _softConnectsRate)
            {
                DisplayName = "Rate of connections retrieved from the connection pool",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _softDisconnectsPerSecondCounter ??= new IncrementingPollingCounter("soft-disconnects", _eventSource, () => _softDisconnectsRate)
            {
                DisplayName = "Rate of connections returned to the connection pool",
                DisplayUnits = "count / sec",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };

            _numberOfNonPooledConnectionsCounter ??= new PollingCounter("number-of-non-pooled-connections", _eventSource, () => _nonPooledConnections)
            {
                DisplayName = "Number of connections not using connection pooling",
                DisplayUnits = "count"
            };

            _numberOfPooledConnectionsCounter ??= new PollingCounter("number-of-pooled-connections", _eventSource, () => _pooledConnections)
            {
                DisplayName = "Number of connections managed by the connection pool",
                DisplayUnits = "count"
            };

            _numberOfActiveConnectionPoolGroupsCounter ??= new PollingCounter("number-of-active-connection-pool-groups", _eventSource, () => _activeConnectionPoolGroups)
            {
                DisplayName = "Number of active unique connection strings",
                DisplayUnits = "count"
            };

            _numberOfInactiveConnectionPoolGroupsCounter ??= new PollingCounter("number-of-inactive-connection-pool-groups", _eventSource, () => _inactiveConnectionPoolGroups)
            {
                DisplayName = "Number of unique connection strings waiting for pruning",
                DisplayUnits = "count"
            };

            _numberOfActiveConnectionPoolsCounter ??= new PollingCounter("number-of-active-connection-pools", _eventSource, () => _activeConnectionPools)
            {
                DisplayName = "Number of active connection pools",
                DisplayUnits = "count"
            };

            _numberOfInactiveConnectionPoolsCounter ??= new PollingCounter("number-of-inactive-connection-pools", _eventSource, () => _inactiveConnectionPools)
            {
                DisplayName = "Number of inactive connection pools",
                DisplayUnits = "count"
            };

            _numberOfActiveConnectionsCounter ??= new PollingCounter("number-of-active-connections", _eventSource, () => _activeConnections)
            {
                DisplayName = "Number of active connections",
                DisplayUnits = "count"
            };

            _numberOfFreeConnectionsCounter ??= new PollingCounter("number-of-free-connections", _eventSource, () => _freeConnections)
            {
                DisplayName = "Number of ready connections in the connection pool",
                DisplayUnits = "count"
            };

            _numberOfStasisConnectionsCounter ??= new PollingCounter("number-of-stasis-connections", _eventSource, () => _stasisConnections)
            {
                DisplayName = "Number of connections currently waiting to be ready",
                DisplayUnits = "count"
            };

            _numberOfReclaimedConnectionsCounter ??= new IncrementingPollingCounter("number-of-reclaimed-connections", _eventSource, () => _reclaimedConnections)
            {
                DisplayName = "Number of reclaimed connections from GC",
                DisplayUnits = "count",
                DisplayRateTimeScale = TimeSpan.FromSeconds(1)
            };
        }
#else
        [PerformanceCounterPermission(SecurityAction.Assert, PermissionAccess = PerformanceCounterPermissionAccess.Write,
            MachineName = ".", CategoryName = PerformanceCounterCategoryName)]
        private void EnablePerformanceCounters()
        {
            AppDomain.CurrentDomain.DomainUnload += ExitOrUnloadEventHandler;
            AppDomain.CurrentDomain.ProcessExit += ExitOrUnloadEventHandler;
            AppDomain.CurrentDomain.UnhandledException += ExceptionEventHandler;

            // level 0-3: hard connects/disconnects, plus basic pool/pool entry statistics
            _hardConnectsRate = CreatePerformanceCounter("HardConnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
            _hardDisconnectsRate = CreatePerformanceCounter("HardDisconnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
            _nonPooledConnections = CreatePerformanceCounter("NumberOfNonPooledConnections", PerformanceCounterType.NumberOfItems32);
            _pooledConnections = CreatePerformanceCounter("NumberOfPooledConnections", PerformanceCounterType.NumberOfItems32);
            _activeConnectionPoolGroups = CreatePerformanceCounter("NumberOfActiveConnectionPoolGroups", PerformanceCounterType.NumberOfItems32);
            _inactiveConnectionPoolGroups = CreatePerformanceCounter("NumberOfInactiveConnectionPoolGroups", PerformanceCounterType.NumberOfItems32);
            _activeConnectionPools = CreatePerformanceCounter("NumberOfActiveConnectionPools", PerformanceCounterType.NumberOfItems32);
            _inactiveConnectionPools = CreatePerformanceCounter("NumberOfInactiveConnectionPools", PerformanceCounterType.NumberOfItems32);
            _stasisConnections = CreatePerformanceCounter("NumberOfStasisConnections", PerformanceCounterType.NumberOfItems32);
            _reclaimedConnections = CreatePerformanceCounter("NumberOfReclaimedConnections", PerformanceCounterType.NumberOfItems32);

            TraceSwitch perfCtrSwitch = new TraceSwitch("ConnectionPoolPerformanceCounterDetail", "level of detail to track with connection pool performance counters");
            if (TraceLevel.Verbose == perfCtrSwitch.Level)
            {
                _softConnectsRate = CreatePerformanceCounter("SoftConnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
                _softDisconnectsRate = CreatePerformanceCounter("SoftDisconnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
                _activeConnections = CreatePerformanceCounter("NumberOfActiveConnections", PerformanceCounterType.NumberOfItems32);
                _freeConnections = CreatePerformanceCounter("NumberOfFreeConnections", PerformanceCounterType.NumberOfItems32);
            }
        }

        [PrePrepareMethod]
        private void ExitOrUnloadEventHandler(object sender, EventArgs e)
        {
            RemovePerformanceCounters();
        }

        [PrePrepareMethod]
        private void ExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (e != null && e.IsTerminating)
            {
                RemovePerformanceCounters();
            }
        }

        private void RemovePerformanceCounters()
        {
            // ExceptionEventHandler with IsTerminating may be called before
            // the Connection Close is called or the variables are initialized
            _hardConnectsRate?.RemoveInstance();
            _hardDisconnectsRate?.RemoveInstance();
            _softConnectsRate?.RemoveInstance();
            _softDisconnectsRate?.RemoveInstance();
            _nonPooledConnections?.RemoveInstance();
            _pooledConnections?.RemoveInstance();
            _activeConnectionPoolGroups?.RemoveInstance();
            _inactiveConnectionPoolGroups?.RemoveInstance();
            _activeConnectionPools?.RemoveInstance();
            _inactiveConnectionPools?.RemoveInstance();
            _activeConnections?.RemoveInstance();
            _freeConnections?.RemoveInstance();
            _stasisConnections?.RemoveInstance();
            _reclaimedConnections?.RemoveInstance();
        }

        private PerformanceCounter? CreatePerformanceCounter(string counterName, PerformanceCounterType counterType)
        {
            _instanceName ??= GetInstanceName();
            try
            {
                PerformanceCounter instance = new();
                instance.CategoryName = PerformanceCounterCategoryName;
                instance.CounterName = counterName;
                instance.InstanceName = _instanceName;
                instance.InstanceLifetime = PerformanceCounterInstanceLifetime.Process;
                instance.ReadOnly = false;
                instance.RawValue = 0;  // make sure we start out at zero

                return instance;
            }
            catch (InvalidOperationException e)
            {
                ADP.TraceExceptionWithoutRethrow(e);

                return null;
            }
        }

        // SxS: this method uses GetCurrentProcessId to construct the instance name.
        // TODO: VSDD 534795 - remove the Resource* attributes if you do not use GetCurrentProcessId after the fix
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        private static string GetInstanceName()
        {
            string result;
            string? instanceName = GetAssemblyName(); // instance perfcounter name

            if (string.IsNullOrEmpty(instanceName))
            {
                instanceName = AppDomain.CurrentDomain?.FriendlyName;
            }

            // TODO: If you do not use GetCurrentProcessId after fixing VSDD 534795, please remove Resource* attributes from this method
            int pid = Kernel32Safe.GetCurrentProcessId();

            // SQLBUDT #366157 -there are several characters which have special meaning
            // to PERFMON.  They recommend that we translate them as shown below, to 
            // prevent problems.

            result = string.Format(null, "{0}[{1}]", instanceName, pid);
            result = result.Replace('(', '[').Replace(')', ']').Replace('#', '_').Replace('/', '_').Replace('\\', '_');

            // SQLBUVSTS #94625 - counter instance name cannot be greater than 127
            if (result.Length > CounterInstanceNameMaxLength)
            {
                // Replacing the middle part with "[...]"
                // For example: if path is c:\long_path\very_(Ax200)_long__path\perftest.exe and process ID is 1234 than the resulted instance name will be: 
                // c:\long_path\very_(AxM)[...](AxN)_long__path\perftest.exe[1234]
                // while M and N are adjusted to make each part before and after the [...] = 61 (making the total = 61 + 5 + 61 = 127)
                const string insertString = "[...]";
                int firstPartLength = (CounterInstanceNameMaxLength - insertString.Length) / 2;
                int lastPartLength = CounterInstanceNameMaxLength - firstPartLength - insertString.Length;
                result = string.Format(null, "{0}{1}{2}",
                    result.Substring(0, firstPartLength),
                    insertString,
                    result.Substring(result.Length - lastPartLength, lastPartLength));

                Debug.Assert(result.Length == CounterInstanceNameMaxLength,
                    string.Format(null, "wrong calculation of the instance name: expected {0}, actual: {1}", CounterInstanceNameMaxLength, result.Length));
            }

            return result;
        }

        [FileIOPermission(SecurityAction.Assert, Unrestricted = true)]
        private static string? GetAssemblyName()
        {
            // First try GetEntryAssembly name, then AppDomain.FriendlyName.
            Assembly? assembly = Assembly.GetEntryAssembly();
            AssemblyName? name = assembly?.GetName();

            return name?.Name;
        }
#endif
    }
}
