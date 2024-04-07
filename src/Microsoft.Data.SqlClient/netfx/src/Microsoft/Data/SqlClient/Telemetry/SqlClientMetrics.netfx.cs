// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Versioning;
using System.Security.Permissions;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient.Telemetry
{
    [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
    internal sealed partial class SqlClientMetrics
    {
        private const string PerformanceCounterCategoryName = ".NET Data Provider for SqlServer";
        private const string PerformanceCounterCategoryHelp = "Counters for Microsoft.Data.SqlClient";

        private const int PerformanceCounterInstanceNameMaxLength = 127;

        private static string s_assemblyName = GetAssemblyName();
        private static string s_instanceName = GetInstanceName();

        private PerformanceCounter _hardConnectsPerSecond;
        private PerformanceCounter _hardDisconnectsPerSecond;

        private PerformanceCounter _softConnectsPerSecond;
        private PerformanceCounter _softDisconnectsPerSecond;

        private PerformanceCounter _numberOfNonPooledConnections;
        private PerformanceCounter _numberOfPooledConnections;

        private PerformanceCounter _numberOfActiveConnectionPoolGroups;
        private PerformanceCounter _numberOfInactiveConnectionPoolGroups;

        private PerformanceCounter _numberOfActiveConnectionPools;
        private PerformanceCounter _numberOfInactiveConnectionPools;

        private PerformanceCounter _numberOfActiveConnections;
        private PerformanceCounter _numberOfFreeConnections;

        private PerformanceCounter _numberOfStasisConnections;
        private PerformanceCounter _numberOfReclaimedConnections;

        private void InitializePlatformSpecificMetrics()
        {
            AppDomain.CurrentDomain.DomainUnload += UnloadEventHandler;
            AppDomain.CurrentDomain.ProcessExit += ExitEventHandler;
            AppDomain.CurrentDomain.UnhandledException += ExceptionEventHandler;

            _hardConnectsPerSecond = CreatePerformanceCounter("HardConnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond64);
            _hardDisconnectsPerSecond = CreatePerformanceCounter("HardDisconnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond64);

            _softConnectsPerSecond = CreatePerformanceCounter("SoftConnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond64);
            _softDisconnectsPerSecond = CreatePerformanceCounter("SoftDisconnectsPerSecond", PerformanceCounterType.RateOfCountsPerSecond64);

            _numberOfNonPooledConnections = CreatePerformanceCounter("NumberOfNonPooledConnections", PerformanceCounterType.NumberOfItems64);
            _numberOfPooledConnections = CreatePerformanceCounter("NumberOfPooledConnections", PerformanceCounterType.NumberOfItems64);

            _numberOfActiveConnectionPoolGroups = CreatePerformanceCounter("NumberOfActiveConnectionPoolGroups", PerformanceCounterType.NumberOfItems64);
            _numberOfInactiveConnectionPoolGroups = CreatePerformanceCounter("NumberOfInactiveConnectionPoolGroups", PerformanceCounterType.NumberOfItems64);

            _numberOfActiveConnectionPools = CreatePerformanceCounter("NumberOfActiveConnectionPools", PerformanceCounterType.NumberOfItems64);
            _numberOfInactiveConnectionPools = CreatePerformanceCounter("NumberOfInactiveConnectionPools", PerformanceCounterType.NumberOfItems64);

            _numberOfActiveConnections = CreatePerformanceCounter("NumberOfActiveConnections", PerformanceCounterType.NumberOfItems64);
            _numberOfFreeConnections = CreatePerformanceCounter("NumberOfFreeConnections", PerformanceCounterType.NumberOfItems64);

            _numberOfStasisConnections = CreatePerformanceCounter("NumberOfStasisConnections", PerformanceCounterType.NumberOfItems64);
            _numberOfReclaimedConnections = CreatePerformanceCounter("NumberOfReclaimedConnections", PerformanceCounterType.NumberOfItems64);
        }

        private void IncrementPlatformSpecificMetric(string metricName, in TagList tagList) { }

        private void DecrementPlatformSpecificMetric(string metricName, in TagList tagList) { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private void DisposePlatformSpecificMetrics()
        {
            _hardConnectsPerSecond?.Dispose();
            _hardDisconnectsPerSecond?.Dispose();

            _softConnectsPerSecond?.Dispose();
            _softDisconnectsPerSecond?.Dispose();

            _numberOfNonPooledConnections?.Dispose();
            _numberOfPooledConnections?.Dispose();

            _numberOfActiveConnectionPoolGroups?.Dispose();
            _numberOfInactiveConnectionPoolGroups?.Dispose();

            _numberOfActiveConnectionPools?.Dispose();
            _numberOfInactiveConnectionPools?.Dispose();

            _numberOfActiveConnections?.Dispose();
            _numberOfFreeConnections?.Dispose();

            _numberOfStasisConnections?.Dispose();
            _numberOfReclaimedConnections?.Dispose();
        }

        [FileIOPermission(SecurityAction.Assert, Unrestricted = true)]
        private static string GetAssemblyName()
            => Assembly.GetExecutingAssembly()?.GetName()?.Name
            ?? AppDomain.CurrentDomain?.FriendlyName;

        // SxS: this method uses GetCurrentProcessId to construct the instance name.
        // TODO: VSDD 534795 - remove the Resource* attributes if you do not use GetCurrentProcessId after the fix
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        private static string GetInstanceName()
        {
            // TODO: If you do not use GetCurrentProcessId after fixing VSDD 534795, please remove Resource* attributes from this method
            int pid = Microsoft.Data.Common.SafeNativeMethods.GetCurrentProcessId();

            // SQLBUDT #366157 -there are several characters which have special meaning
            // to PERFMON.  They recommend that we translate them as shown below, to 
            // prevent problems.

            string result = string.Format(null, "{0}[{1}]", s_assemblyName, pid);
            result = result.Replace('(', '[').Replace(')', ']').Replace('#', '_').Replace('/', '_').Replace('\\', '_');

            // SQLBUVSTS #94625 - counter instance name cannot be greater than 127
            if (result.Length > PerformanceCounterInstanceNameMaxLength)
            {
                // Replacing the middle part with "[...]"
                // For example: if path is c:\long_path\very_(Ax200)_long__path\perftest.exe and process ID is 1234 than the resulted instance name will be: 
                // c:\long_path\very_(AxM)[...](AxN)_long__path\perftest.exe[1234]
                // while M and N are adjusted to make each part before and after the [...] = 61 (making the total = 61 + 5 + 61 = 127)
                const string insertString = "[...]";
                int firstPartLength = (PerformanceCounterInstanceNameMaxLength - insertString.Length) / 2;
                int lastPartLength = PerformanceCounterInstanceNameMaxLength - firstPartLength - insertString.Length;
                result = string.Format(null, "{0}{1}{2}",
                    result.Substring(0, firstPartLength),
                    insertString,
                    result.Substring(result.Length - lastPartLength, lastPartLength));

                Debug.Assert(result.Length == PerformanceCounterInstanceNameMaxLength,
                    string.Format(null, "wrong calculation of the instance name: expected {0}, actual: {1}", PerformanceCounterInstanceNameMaxLength, result.Length));
            }

            return result;
        }

        private PerformanceCounter CreatePerformanceCounter(string counterName, PerformanceCounterType counterType)
        {
            try
            {
                return new PerformanceCounter()
                {
                    CategoryName = PerformanceCounterCategoryName,
                    CounterName = counterName,
                    InstanceName = s_instanceName,
                    InstanceLifetime = PerformanceCounterInstanceLifetime.Process,
                    ReadOnly = false,
                    RawValue = 0
                };
            }
            catch(InvalidOperationException e)
            {
                ADP.TraceExceptionWithoutRethrow(e);
                return null;
            }
        }

        [PrePrepareMethod]
        void ExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if ((null != e) && e.IsTerminating)
            {
                Dispose();
            }
        }

        [PrePrepareMethod]
        void ExitEventHandler(object sender, EventArgs e)
        {
            Dispose();
        }

        [PrePrepareMethod]
        void UnloadEventHandler(object sender, EventArgs e)
        {
            Dispose();
        }
    }
}
