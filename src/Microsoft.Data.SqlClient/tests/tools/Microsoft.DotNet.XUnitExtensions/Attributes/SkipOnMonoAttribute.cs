using System;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions.Attributes
{
    [TraitDiscoverer("Microsoft.DotNet.XUnitExtensions.SkipOnMonoDiscoverer", "Microsoft.DotNet.XUnitExtensions")]
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class SkipOnMonoAttribute : Attribute, ITraitAttribute
    {
        internal SkipOnMonoAttribute() { }
        public SkipOnMonoAttribute(string reason, TestPlatforms testPlatforms = TestPlatforms.Any) { }
    }
}
