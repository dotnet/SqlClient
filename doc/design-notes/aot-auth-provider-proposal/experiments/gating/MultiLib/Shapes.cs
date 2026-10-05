using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
namespace MultiLib
{
    public static class Shapes
    {
        [FeatureSwitchDefinition("MultiLib.A")]
        internal static bool SwitchA => AppContext.TryGetSwitch("MultiLib.A", out bool v) ? v : true;
        public static string RunA() => SwitchA ? ReflectA() : "A-off";

        [FeatureSwitchDefinition("MultiLib.B")]
        internal static bool SwitchB => AppContext.TryGetSwitch("MultiLib.B", out bool v) ? v : true;
        [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
        [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
        internal static bool GuardB => SwitchB;
        public static string RunB() => GuardB ? ReflectB() : "B-off";

        [FeatureSwitchDefinition("MultiLib.C")]
        [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
        [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
        internal static bool SwitchC => AppContext.TryGetSwitch("MultiLib.C", out bool v) ? v : true;
        public static string RunC() => SwitchC ? ReflectC() : "C-off";

#if NET
        [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
        internal static bool GuardD => System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
        public static string RunD() => GuardD ? ReflectD() : "D-off";
#else
        public static string RunD() => "D-n/a(netstandard2.0 asset)";
#endif

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectA()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "MULTI_MARKER_A");
                Activator.CreateInstance(asm.GetType("X." + "MULTI_MARKER_A")!);
            }
            catch (Exception) { }
            return "MULTI_MARKER_A";
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectB()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "MULTI_MARKER_B");
                Activator.CreateInstance(asm.GetType("X." + "MULTI_MARKER_B")!);
            }
            catch (Exception) { }
            return "MULTI_MARKER_B";
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectC()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "MULTI_MARKER_C");
                Activator.CreateInstance(asm.GetType("X." + "MULTI_MARKER_C")!);
            }
            catch (Exception) { }
            return "MULTI_MARKER_C";
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectD()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "MULTI_MARKER_D");
                Activator.CreateInstance(asm.GetType("X." + "MULTI_MARKER_D")!);
            }
            catch (Exception) { }
            return "MULTI_MARKER_D";
        }
    }
}
