// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Reflection;

using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.Test.Stress
{
    class Program
    {
        private static bool s_debugMode = false;
        private static bool s_console = false;
        private static IEnumerable<TestBase> s_tests;
        private static StressEngine s_eng;

        static int Main(string[] args)
        {
            var assemblyOption = new Option<string>("--assembly")
            {
                Description = "The name of the assembly containing the tests (loaded via Assembly.Load).",
                Required = true
            };

            var overrideOption = new Option<string[]>("--override")
            {
                Description = "Override a test property. Format: name=value",
                AllowMultipleArgumentsPerToken = true
            };

            var variationOption = new Option<string[]>("--variation")
            {
                Description = "Add a test variation.",
                AllowMultipleArgumentsPerToken = true
            };

            var testOption = new Option<string>("--test") { Description = "Run specific test(s), semicolon-separated." };
            var durationOption = new Option<int?>("--duration") { Description = "Duration of the test in seconds." };
            var threadsOption = new Option<int?>("--threads") { Description = "Number of threads to use." };
            var consoleOption = new Option<bool>("--console") { Description = "Emit all output to the console." };
            var debugOption = new Option<bool>("--debug") { Description = "Print process ID and wait for debugger attach." };
            var exceptionThresholdOption = new Option<int?>("--exception-threshold") { Description = "Limit on exceptions before test halts." };
            var monitorEnabledOption = new Option<bool?>("--monitor-enabled") { Description = "Enable monitoring." };
            var randomSeedOption = new Option<int?>("--random-seed") { Description = "Seed for the random number generator." };
            var filterOption = new Option<string>("--filter") { Description = "Run tests matching the given filter. Example: TestType=Query,Update;IsServerTest=True" };
            var printMethodNameOption = new Option<bool>("--print-method-name") { Description = "Print test method names to console." };
            var deadlockDetectionOption = new Option<bool>("--deadlock-detection") { Description = "Enable deadlock detection." };

            var rootCommand = new RootCommand("SqlClient Stress Test Runner")
            {
                assemblyOption,
                overrideOption,
                variationOption,
                testOption,
                durationOption,
                threadsOption,
                consoleOption,
                debugOption,
                exceptionThresholdOption,
                monitorEnabledOption,
                randomSeedOption,
                filterOption,
                printMethodNameOption,
                deadlockDetectionOption
            };

            rootCommand.SetAction((ParseResult result) =>
            {
                // Assembly
                TestFinder.AssemblyName = new AssemblyName(result.GetValue(assemblyOption));

                // Overrides (format: name=value)
                var overrides = result.GetValue(overrideOption);
                if (overrides != null)
                {
                    foreach (var item in overrides)
                    {
                        var eqIndex = item.IndexOf('=');
                        if (eqIndex <= 0)
                        {
                            Console.Error.WriteLine($"Error: --override value '{item}' must be in name=value format.");
                            Environment.Exit(1);
                        }
                        TestMetrics.Overrides.Add(item.Substring(0, eqIndex), item.Substring(eqIndex + 1));
                    }
                }

                // Variations
                var variations = result.GetValue(variationOption);
                if (variations != null)
                {
                    foreach (var v in variations)
                    {
                        TestMetrics.Variations.Add(v);
                    }
                }

                // Test
                var test = result.GetValue(testOption);
                if (test != null)
                {
                    TestMetrics.SelectedTests.AddRange(test.Split(';'));
                }

                // Duration
                var duration = result.GetValue(durationOption);
                if (duration.HasValue)
                {
                    TestMetrics.StressDuration = duration.Value;
                }

                // Threads
                var threads = result.GetValue(threadsOption);
                if (threads.HasValue)
                {
                    TestMetrics.StressThreads = threads.Value;
                }

                // Console
                s_console = result.GetValue(consoleOption);

                // Debug
                s_debugMode = result.GetValue(debugOption);
                if (s_debugMode)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                    else
                    {
                        Console.WriteLine("Current PID: {0}, attach the debugger and press Enter to continue the execution...", System.Diagnostics.Process.GetCurrentProcess().Id);
                        Console.ReadLine();
                    }
                }

                // Exception threshold
                var threshold = result.GetValue(exceptionThresholdOption);
                if (threshold.HasValue)
                {
                    TestMetrics.ExceptionThreshold = threshold.Value;
                }

                // Monitor enabled
                var monitor = result.GetValue(monitorEnabledOption);
                if (monitor.HasValue)
                {
                    TestMetrics.MonitorEnabled = monitor.Value;
                }

                // Random seed
                var seed = result.GetValue(randomSeedOption);
                if (seed.HasValue)
                {
                    TestMetrics.RandomSeed = seed.Value;
                }

                // Filter
                var filter = result.GetValue(filterOption);
                if (filter != null)
                {
                    TestMetrics.Filter = filter;
                }

                // Print method name
                TestMetrics.PrintMethodName = result.GetValue(printMethodNameOption);

                // Deadlock detection
                if (result.GetValue(deadlockDetectionOption))
                {
                    DeadlockDetection.Enable();
                }

                // Print config, load tests, and run
                PrintConfigSummary();

                Console.WriteLine("Assembly Found for the Assembly Name " + TestFinder.AssemblyName);
                s_tests = TestFinder.GetTests(Assembly.Load(TestFinder.AssemblyName));
                s_eng = new StressEngine(TestMetrics.StressThreads, TestMetrics.StressDuration, s_tests, TestMetrics.RandomSeed);
            });

            var parseResult = rootCommand.Parse(args);
            int exitCode = parseResult.Invoke();
            if (exitCode != 0 || s_eng == null)
            {
                return exitCode;
            }

            return Run();
        }

        private static void PrintConfigSummary()
        {
            string border = new('#', 80);

            Console.WriteLine(border);
            Console.WriteLine($"MDS Version:         {GetMdsVersion()}");
            Console.WriteLine($"Test Assembly Name:  {TestFinder.AssemblyName}");
            foreach (var item in TestMetrics.Overrides)
            {
                Console.WriteLine($"Override:            {item.Key} = {item.Value}");
            }
            foreach (var item in TestMetrics.SelectedTests)
            {
                Console.WriteLine($"Test:                {item}");
            }
            Console.WriteLine($"Duration:            {TestMetrics.StressDuration} second(s)");
            Console.WriteLine($"Threads No.:         {TestMetrics.StressThreads}");
            Console.WriteLine($"Emit to console:     {s_console}");
            Console.WriteLine($"Debug mode:          {s_debugMode}");
            Console.WriteLine($"Exception threshold: {TestMetrics.ExceptionThreshold}");
            Console.WriteLine($"Random seed:         {TestMetrics.RandomSeed}");
            Console.WriteLine($"Filter:              {TestMetrics.Filter}");
            Console.WriteLine($"Deadlock detection:  {DeadlockDetection.IsEnabled}");
            Console.WriteLine(border);
        }

        private static int Run()
        {
            if (!s_console)
            {
                try
                {
                    TextWriter logOut = LogManager.Instance.GetLog("MDSStressTest-" + Environment.Version
                                                                   + "-[" + Environment.OSVersion + "]-"
                                                                   + DateTime.Now.ToString("MMMM dd yyyy @HHmmssFFF"));
                    Console.SetOut(logOut);
                    PrintConfigSummary();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Cannot open log file for writing!");
                    Console.WriteLine(e);
                }
            }
            return s_eng.Run();
        }

        private static string GetMdsVersion()
        {
            // MDS captures its NuGet package version at build-time, so pull
            // it out and return it.
            //
            // See:  tools/targets/GenerateThisAssemblyCs.targets
            //
            var assembly = typeof(SqlConnection).Assembly;
            var type = assembly.GetType("Microsoft.Data.SqlClient.ThisAssembly");
            if (type is null)
            {
                return "<unknown>";
            }

            // Look for the PackageVersion field, falling back to FileVersion.
            var field = type.GetField(
                "PackageVersion",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? type.GetField(
                "FileVersion",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (field is null)
            {
                return "<unknown>";
            }

            return (string)field.GetValue(null) ?? "<unknown>";
        }
    }
}
