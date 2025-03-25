// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Versioning;
using System.Security.Permissions;
using Interop.Windows.Kernel32;
using Microsoft.Data.Common;

#nullable enable

namespace Microsoft.Data.SqlClient.Diagnostics
{
    [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
    internal sealed partial class SqlClientMetrics
    {
        private const string PerformanceCounterCategoryName = ".NET Data Provider for SqlServer";
        private const string PerformanceCounterCategoryHelp = "Counters for Microsoft.Data.SqlClient";
        private const int CounterInstanceNameMaxLength = 127;

        private PerformanceCounter? _hardConnectsPerSecond;
        private PerformanceCounter? _hardDisconnectsPerSecond;
        private PerformanceCounter? _softConnectsPerSecond;
        private PerformanceCounter? _softDisconnectsPerSecond;
        private PerformanceCounter? _numberOfNonPooledConnections;
        private PerformanceCounter? _numberOfPooledConnections;
        private PerformanceCounter? _numberOfActiveConnectionPoolGroups;
        private PerformanceCounter? _numberOfInactiveConnectionPoolGroups;
        private PerformanceCounter? _numberOfActiveConnectionPools;
        private PerformanceCounter? _numberOfInactiveConnectionPools;
        private PerformanceCounter? _numberOfActiveConnections;
        private PerformanceCounter? _numberOfFreeConnections;
        private PerformanceCounter? _numberOfStasisConnections;
        private PerformanceCounter? _numberOfReclaimedConnections;

        private static PerformanceCounter? CreatePerformanceCounter(string categoryName, string instanceName, string counterName, PerformanceCounterType counterType)
        {
            PerformanceCounter? instance = null;

            try
            {
                instance = new PerformanceCounter();
                instance.CategoryName = categoryName;
                instance.CounterName = counterName;
                instance.InstanceName = instanceName;
                instance.InstanceLifetime = PerformanceCounterInstanceLifetime.Process;
                instance.ReadOnly = false;
                instance.RawValue = 0;  // make sure we start out at zero
            }
            catch (InvalidOperationException e)
            {
                ADP.TraceExceptionWithoutRethrow(e);
            }

            return instance;
        }

        [FileIOPermission(SecurityAction.Assert, Unrestricted = true)]
        private static string? GetAssemblyName()
        {
            // First try GetEntryAssembly name, then AppDomain.FriendlyName.
            Assembly? assembly = Assembly.GetEntryAssembly();
            AssemblyName? name = assembly.GetName();

            return name?.Name;
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

        [PerformanceCounterPermission(SecurityAction.Assert, PermissionAccess = PerformanceCounterPermissionAccess.Write,
            MachineName = ".", CategoryName = PerformanceCounterCategoryName)]
        private void EnablePerformanceCounters()
        {
            AppDomain.CurrentDomain.DomainUnload += UnloadEventHandler;
            AppDomain.CurrentDomain.ProcessExit += ExitEventHandler;
            AppDomain.CurrentDomain.UnhandledException += ExceptionEventHandler;

            string instanceName = GetInstanceName();

            // level 0-3: hard connects/disconnects, plus basic pool/pool entry statistics

            _hardConnectsPerSecond = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "HardConnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
            _hardDisconnectsPerSecond = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "HardDisconnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
            _numberOfNonPooledConnections = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfNonPooledConnections", PerformanceCounterType.NumberOfItems32);
            _numberOfPooledConnections = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfPooledConnections", PerformanceCounterType.NumberOfItems32);
            _numberOfActiveConnectionPoolGroups = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfActiveConnectionPoolGroups", PerformanceCounterType.NumberOfItems32);
            _numberOfInactiveConnectionPoolGroups = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfInactiveConnectionPoolGroups", PerformanceCounterType.NumberOfItems32);
            _numberOfActiveConnectionPools = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfActiveConnectionPools", PerformanceCounterType.NumberOfItems32);
            _numberOfInactiveConnectionPools = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfInactiveConnectionPools", PerformanceCounterType.NumberOfItems32);
            _numberOfStasisConnections = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfStasisConnections", PerformanceCounterType.NumberOfItems32);
            _numberOfReclaimedConnections = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfReclaimedConnections", PerformanceCounterType.NumberOfItems32);

            TraceSwitch perfCtrSwitch = new TraceSwitch("ConnectionPoolPerformanceCounterDetail", "level of detail to track with connection pool performance counters");
            if (TraceLevel.Verbose == perfCtrSwitch.Level)
            {
                _softConnectsPerSecond = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "SoftConnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
                _softDisconnectsPerSecond = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "SoftDisconnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond32);
                _numberOfActiveConnections = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfActiveConnections", PerformanceCounterType.NumberOfItems32);
                _numberOfFreeConnections = CreatePerformanceCounter(PerformanceCounterCategoryName, instanceName, "NumberOfFreeConnections", PerformanceCounterType.NumberOfItems32);
            }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private void RemovePerformanceCounters()
        {
            // ExceptionEventHandler with IsTerminating may be called before
            // the Connection Close is called or the variables are initialized
            _hardConnectsPerSecond?.RemoveInstance();
            _hardDisconnectsPerSecond?.RemoveInstance();
            _softConnectsPerSecond?.RemoveInstance();
            _softDisconnectsPerSecond?.RemoveInstance();
            _numberOfNonPooledConnections?.RemoveInstance();
            _numberOfPooledConnections?.RemoveInstance();
            _numberOfActiveConnectionPoolGroups?.RemoveInstance();
            _numberOfInactiveConnectionPoolGroups?.RemoveInstance();
            _numberOfActiveConnectionPools?.RemoveInstance();
            _numberOfInactiveConnectionPools?.RemoveInstance();
            _numberOfActiveConnections?.RemoveInstance();
            _numberOfFreeConnections?.RemoveInstance();
            _numberOfStasisConnections?.RemoveInstance();
            _numberOfReclaimedConnections?.RemoveInstance();
        }

        [PrePrepareMethod]
        private void ExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (e != null && e.IsTerminating)
            {
                RemovePerformanceCounters();
            }
        }

        [PrePrepareMethod]
        private void ExitEventHandler(object sender, EventArgs e)
        {
            RemovePerformanceCounters();
        }

        [PrePrepareMethod]
        private void UnloadEventHandler(object sender, EventArgs e)
        {
            RemovePerformanceCounters();
        }
    }
}
