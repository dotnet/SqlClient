// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Xunit
{
    [Flags]
    public enum RuntimeStressTestModes
    {
        // Disable on any stress test mode or on checked runtime.
        // Can't be ~0 as that would include CheckedRuntime flag, which would break the case
        // where you want to disable in all (including release stress test).
        Any = 0,

        // JitStress, JitStressRegs, JitMinOpts and TailcallStress enable
        // various modes in the JIT that cause us to exercise more code paths,
        // and generate different kinds of code
        JitStress = 1, // COMPlus_JitStress is set.
        JitStressRegs = 1 << 1, // COMPlus_JitStressRegs is set.
        JitMinOpts = 1 << 2, // COMPlus_JITMinOpts is set.
        TailcallStress = 1 << 3, // COMPlus_TailcallStress is set.

        // ZapDisable says to not use NGEN or ReadyToRun images.
        // This means we JIT everything.
        ZapDisable = 1 << 4, // COMPlus_ZapDisable is set.

        // GCStress3 forces a GC at various locations, typically transitions
        // to/from the VM from managed code. 
        GCStress3 = 1 << 5,  // COMPlus_GCStress includes mode 0x3.

        // GCStressC forces a GC at every JIT-generated code instruction,
        // including in NGEN/ReadyToRun code.
        GCStressC = 1 << 6, // COMPlus_GCStress includes mode 0xC.
        CheckedRuntime = 1 << 7, // Disable only when running on checked runtime.
        AnyGCStress = GCStress3 | GCStressC // Disable when any GCStress is exercised.
    }
}
