// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

// This diagnostic harness includes tests that mutate PROCESS-GLOBAL state (e.g.
// ThreadPool.SetMin/MaxThreads in the starvation experiments) and heavy soak
// tests. Running collections in parallel lets those perturb unrelated tests
// (starving their connections), so we run serially - matching the container
// runner, which uses `xunit.console.exe -parallel none`.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
