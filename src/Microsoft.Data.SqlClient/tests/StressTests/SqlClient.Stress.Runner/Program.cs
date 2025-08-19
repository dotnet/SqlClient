// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Microsoft.Data.SqlClient;

namespace DPStressHarness//Microsoft.Data.SqlClient.Stress
{
    class Program
    {
        private static bool s_debugMode = false;
        static int Main(string[] args)
        {
            Init(args);
            return Run();
        }

        public enum RunMode
        {
            RunAll,
            RunVerify,
            Help,
            ExitWithError
        };

        private static RunMode s_mode = RunMode.RunAll;
        private static IEnumerable<TestBase> s_tests;
        private static StressEngine s_eng;
        private static string s_error;
        private static bool s_console = false;

        public static void Init(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-a":
                        string assemblyName = args[++i];
                        TestFinder.AssemblyName = new AssemblyName(assemblyName);
                        break;

                    case "-all":
                        s_mode = RunMode.RunAll;
                        break;

                    case "-override":
                        TestMetrics.Overrides.Add(args[++i], args[++i]);
                        break;

                    case "-variation":
                        TestMetrics.Variations.Add(args[++i]);
                        break;

                    case "-test":
                        TestMetrics.SelectedTests.AddRange(args[++i].Split(';'));
                        break;

                    case "-duration":
                        TestMetrics.StressDuration = int.Parse(args[++i]);
                        break;

                    case "-threads":
                        TestMetrics.StressThreads = int.Parse(args[++i]);
                        break;

                    case "-verify":
                        s_mode = RunMode.RunVerify;
                        break;

                    case "-console":
                        s_console = true;
                        break;

                    case "-debug":
                        s_debugMode = true;
                        if (System.Diagnostics.Debugger.IsAttached)
                        {
                            System.Diagnostics.Debugger.Break();
                        }
                        else
                        {
                            Console.WriteLine("Current PID: {0}, attach the debugger and press Enter to continue the execution...", System.Diagnostics.Process.GetCurrentProcess().Id);
                            Console.ReadLine();
                        }
                        break;

                    case "-exceptionThreshold":
                        TestMetrics.ExceptionThreshold = int.Parse(args[++i]);
                        break;

                    case "-monitorenabled":
                        TestMetrics.MonitorEnabled = bool.Parse(args[++i]);
                        break;

                    case "-randomSeed":
                        TestMetrics.RandomSeed = int.Parse(args[++i]);
                        break;

                    case "-filter":
                        TestMetrics.Filter = args[++i];
                        break;

                    case "-printMethodName":
                        TestMetrics.PrintMethodName = true;
                        break;

                    case "-deadlockdetection":
                        if (bool.Parse(args[++i]))
                        {
                            DeadlockDetection.Enable();
                        }
                        break;

                    default:
                        s_mode = RunMode.Help;
                        break;
                }
            }

            PrintConfigSummary();

            if (TestFinder.AssemblyName != null)
            {
                Console.WriteLine("Assembly Found for the Assembly Name " + TestFinder.AssemblyName);

                // get and load all the tests
                s_tests = TestFinder.GetTests(Assembly.Load(TestFinder.AssemblyName));

                // instantiate the stress engine
                s_eng = new StressEngine(TestMetrics.StressThreads, TestMetrics.StressDuration, s_tests, TestMetrics.RandomSeed);
            }
            else
            {
                Program.s_error = string.Format("Assembly {0} cannot be found.", TestFinder.AssemblyName);
                s_mode = RunMode.ExitWithError;
            }
        }

        public static int Run()
        {
            if (TestFinder.AssemblyName == null)
            {
                s_mode = RunMode.Help;
            }
            switch (s_mode)
            {
                case RunMode.RunAll:
                    return RunStress();

                case RunMode.RunVerify:
                    return RunVerify();

                case RunMode.ExitWithError:
                    return ExitWithError();

                case RunMode.Help:
                default:
                    return PrintHelp();
            }
        }

        private static int PrintHelp()
        {
            Console.WriteLine("stresstest.exe [-a <module name>] <arguments>");
            Console.WriteLine();
            Console.WriteLine("   -a <module name> should specify path to the assembly containing the tests.");
            Console.WriteLine();
            Console.WriteLine("Supported options are:");
            Console.WriteLine();
            Console.WriteLine("   -all                        Run all tests - best for debugging, not perf measurements.");
            Console.WriteLine();
            Console.WriteLine("   -verify                     Run in functional verification mode.");
            Console.WriteLine();
            Console.WriteLine("   -duration <n>               Duration of the test in seconds.");
            Console.WriteLine();
            Console.WriteLine("   -threads <n>                Number of threads to use.");
            Console.WriteLine();
            Console.WriteLine("   -override <name> <value>    Override the value of a test property.");
            Console.WriteLine();
            Console.WriteLine("   -test <name1;name2>         Run specific test(s).");
            Console.WriteLine();
            Console.WriteLine("   -console                    Emit all output to the console.");
            Console.WriteLine();
            Console.WriteLine("   -debug                      Print process ID in the beginning and wait for Enter (to give your time to attach the debugger).");
            Console.WriteLine();
            Console.WriteLine("   -exceptionThreshold <n>     An optional limit on exceptions which will be caught. When reached, test will halt.");
            Console.WriteLine();
            Console.WriteLine("   -monitorenabled             True or False to enable monitoring. Default is false");
            Console.WriteLine();
            Console.WriteLine("   -randomSeed                 Enables setting of the random number generator used internally.  This serves both the purpose");
            Console.WriteLine("                               of helping to improve reproducibility and making it deterministic from Chess's perspective");
            Console.WriteLine("                               for a given schedule. Default is " + TestMetrics.RandomSeed + ".");
            Console.WriteLine();
            Console.WriteLine("   -filter                     Run tests whose stress test attributes match the given filter. Filter is not applied if attribute");
            Console.WriteLine("                               does not implement ITestAttributeFilter. Example: -filter TestType=Query,Update;IsServerTest=True ");
            Console.WriteLine();
            Console.WriteLine("   -printMethodName            Print tests' title in console window");
            Console.WriteLine();
            Console.WriteLine("   -deadlockdetection          True or False to enable deadlock detection. Default is false");
            Console.WriteLine();

            return 1;
        }

        private static void PrintConfigSummary()
        {
            string border = new('#', 80);

            Console.WriteLine(border);
            Console.WriteLine($"MDS Version:         {GetMdsVersion()}");
            Console.WriteLine($"Test Assembly Name:  {TestFinder.AssemblyName}");
            Console.WriteLine($"Run mode:            {Enum.GetName(typeof(RunMode), s_mode)}");
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

        private static int ExitWithError()
        {
            Environment.FailFast("Exit with error(s).");
            return 1;
        }

        private static int RunVerify()
        {
            throw new NotImplementedException();
        }

        private static int RunStress()
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
            var type = assembly.GetType("System.ThisAssembly");
            if (type is null)
            {
                return "<unknown>";
            }

            // Look for the NuGetPackageVersion field, which is available in
            // newer MDS packages.
            var field = type.GetField(
                "NuGetPackageVersion",
                BindingFlags.NonPublic | BindingFlags.Static);

            // If not present, use the older assembly file version field.
            if (field is null)
            {
                field = type.GetField(
                    "InformationalVersion",
                    BindingFlags.NonPublic | BindingFlags.Static);
            }

            if (field is null)
            {
                return "<unknown>";
            }

            return (string)field.GetValue(null) ?? "<unknown>";
        }
    }
}
