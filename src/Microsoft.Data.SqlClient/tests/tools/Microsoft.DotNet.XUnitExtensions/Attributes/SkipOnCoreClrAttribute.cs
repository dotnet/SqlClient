using System;
using Xunit.Sdk;

namespace Xunit
{
    [TraitDiscoverer("Microsoft.DotNet.XUnitExtensions.SkipOnCoreClrDiscoverer", "Microsoft.DotNet.XUnitExtensions")]
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class SkipOnCoreClrAttribute : Attribute, ITraitAttribute
    {
        internal SkipOnCoreClrAttribute() { }

        public SkipOnCoreClrAttribute(string reason, TestPlatforms testPlatforms) { }
        public SkipOnCoreClrAttribute(string reason, RuntimeStressTestModes testMode) { }
        public SkipOnCoreClrAttribute(string reason, TestPlatforms testPlatforms, RuntimeStressTestModes testMode) { }
        public SkipOnCoreClrAttribute(string reason) { }
    }
}
