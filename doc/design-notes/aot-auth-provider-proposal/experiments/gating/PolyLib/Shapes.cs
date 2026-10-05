using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
namespace PolyLib
{
    public static class Shapes
    {
        // A: plain feature switch, default true
        [FeatureSwitchDefinition("PolyLib.A")]
        internal static bool SwitchA => AppContext.TryGetSwitch("PolyLib.A", out bool v) ? v : true;
        public static string RunA() => SwitchA ? ReflectA() : "A-off";

        // B: switch + separate guard property
        [FeatureSwitchDefinition("PolyLib.B")]
        internal static bool SwitchB => AppContext.TryGetSwitch("PolyLib.B", out bool v) ? v : true;
        [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
        [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
        internal static bool GuardB => SwitchB;
        public static string RunB() => GuardB ? ReflectB() : "B-off";

        // C: both attributes on one property
        [FeatureSwitchDefinition("PolyLib.C")]
        [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
        [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
        internal static bool SwitchC => AppContext.TryGetSwitch("PolyLib.C", out bool v) ? v : true;
        public static string RunC() => SwitchC ? ReflectC() : "C-off";

        // E: shape B + ILLink.Substitutions.xml stub keyed on IsDynamicCodeSupported=false (net8 AOT fallback)
        [FeatureSwitchDefinition("PolyLib.E")]
        internal static bool SwitchE => AppContext.TryGetSwitch("PolyLib.E", out bool v) ? v : true;
        [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
        [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
        internal static bool GuardE => SwitchE;
        public static string RunE() => GuardE ? ReflectE() : "E-off";

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectA()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "POLY_MARKER_A");
                Activator.CreateInstance(asm.GetType("X." + "POLY_MARKER_A")!);
            }
            catch (Exception) { }
            return "POLY_MARKER_A";
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectB()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "POLY_MARKER_B");
                Activator.CreateInstance(asm.GetType("X." + "POLY_MARKER_B")!);
            }
            catch (Exception) { }
            return "POLY_MARKER_B";
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectC()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "POLY_MARKER_C");
                Activator.CreateInstance(asm.GetType("X." + "POLY_MARKER_C")!);
            }
            catch (Exception) { }
            return "POLY_MARKER_C";
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        [RequiresUnreferencedCode("reflection")]
        [RequiresDynamicCode("reflection")]
        private static string ReflectE()
        {
            try
            {
                var asm = Assembly.Load("Does.Not.Exist." + "POLY_MARKER_E");
                Activator.CreateInstance(asm.GetType("X." + "POLY_MARKER_E")!);
            }
            catch (Exception) { }
            return "POLY_MARKER_E";
        }
    }
}
