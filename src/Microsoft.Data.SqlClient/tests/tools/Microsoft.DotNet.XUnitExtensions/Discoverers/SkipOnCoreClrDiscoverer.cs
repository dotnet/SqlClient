// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions
{
    public class SkipOnCoreClrDiscoverer : ITraitDiscoverer
    {
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            if (!SkipOnMonoDiscoverer.IsMonoRuntime)
            {
                TestPlatforms testPlatforms = TestPlatforms.Any;
                RuntimeStressTestModes stressMode = RuntimeStressTestModes.Any;
                foreach (object arg in traitAttribute.GetConstructorArguments().Skip(1)) // We skip the first one as it is the reason
                {
                    if (arg is TestPlatforms tp)
                    {
                        testPlatforms = tp;
                    }
                    else if (arg is RuntimeStressTestModes rstm)
                    {
                        stressMode = rstm;
                    }
                }

                if (DiscovererHelpers.TestPlatformApplies(testPlatforms) && StressModeApplies(stressMode))
                {
                    if (IsCheckedRuntime() || (IsRuntimeStressTesting && !stressMode.HasFlag(RuntimeStressTestModes.CheckedRuntime)))
                    {
                        return new[] { new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing) };
                    }
                }
            }

            return Array.Empty<KeyValuePair<string, string>>();
        }

        // Order here matters as some env variables may appear in multiple modes
        private static bool StressModeApplies(RuntimeStressTestModes stressMode) =>
            stressMode == RuntimeStressTestModes.Any ||
            (stressMode.HasFlag(RuntimeStressTestModes.GCStress3) && IsGCStress3) ||
            (stressMode.HasFlag(RuntimeStressTestModes.GCStressC) && IsGCStressC) ||
            (stressMode.HasFlag(RuntimeStressTestModes.ZapDisable) && IsZapDisable) ||
            (stressMode.HasFlag(RuntimeStressTestModes.TailcallStress) && IsTailCallStress) ||
            (stressMode.HasFlag(RuntimeStressTestModes.JitStressRegs) && IsJitStressRegs) ||
            (stressMode.HasFlag(RuntimeStressTestModes.JitStress) && IsJitStress) ||
            (stressMode.HasFlag(RuntimeStressTestModes.JitMinOpts) && IsJitMinOpts) ||
            stressMode == RuntimeStressTestModes.CheckedRuntime; // if checked runtime is the only flag, all stress modes apply.

        // Order here matters as some env variables may appear in multiple modes
        private static bool IsRuntimeStressTesting =>
            IsGCStress3 ||
            IsGCStressC ||
            IsZapDisable ||
            IsTailCallStress ||
            IsJitStressRegs ||
            IsJitStress ||
            IsJitMinOpts;

        private static string GetEnvironmentVariableValue(string name) => Environment.GetEnvironmentVariable(name) ?? "0";

        private static bool IsJitStress => !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStress"), "0", StringComparison.InvariantCulture);

        private static bool IsJitStressRegs => !string.Equals(GetEnvironmentVariableValue("COMPlus_JitStressRegs"), "0", StringComparison.InvariantCulture);

        private static bool IsJitMinOpts => string.Equals(GetEnvironmentVariableValue("COMPlus_JITMinOpts"), "1", StringComparison.InvariantCulture);

        private static bool IsTailCallStress => string.Equals(GetEnvironmentVariableValue("COMPlus_TailcallStress"), "1", StringComparison.InvariantCulture);

        private static bool IsZapDisable => string.Equals(GetEnvironmentVariableValue("COMPlus_ZapDisable"), "1", StringComparison.InvariantCulture);

        private static bool IsGCStress3 => CompareGCStressModeAsLower(GetEnvironmentVariableValue("COMPlus_GCStress"), "0x3", "3");

        private static bool IsGCStressC => CompareGCStressModeAsLower(GetEnvironmentVariableValue("COMPlus_GCStress"), "0xC", "C");

        private static bool IsCheckedRuntime()
        {
            Assembly assembly = typeof(string).Assembly;
            AssemblyConfigurationAttribute assemblyConfigurationAttribute = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();

            return assemblyConfigurationAttribute != null &&
                string.Equals(assemblyConfigurationAttribute.Configuration, "Checked", StringComparison.InvariantCulture);
        }

        private static bool CompareGCStressModeAsLower(string value, string first, string second)
        {
            value = value.ToLowerInvariant();
            return string.Equals(value, first.ToLowerInvariant(), StringComparison.InvariantCulture) ||
                string.Equals(value, second.ToLowerInvariant(), StringComparison.InvariantCulture) ||
                string.Equals(value, "0xf", StringComparison.InvariantCulture) ||
                string.Equals(value, "f", StringComparison.InvariantCulture);
        }
    }
}
