// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DPStressHarness//Microsoft.Data.SqlClient.Stress
{
    class Program
    {
        private static bool s_debugMode = false;
        static void Main(string[] args)
        {
            Init(args);
            Run();
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

        public static void Run()
        {
            if (TestFinder.AssemblyName == null)
            {
                s_mode = RunMode.Help;
            }
            switch (s_mode)
            {
                case RunMode.RunAll:
                    RunStress();
                    break;

                case RunMode.RunVerify:
                    RunVerify();
                    break;

                case RunMode.ExitWithError:
                    ExitWithError();
                    break;

                case RunMode.Help:
                    PrintHelp();
                    break;
            }
        }

        private static void PrintHelp()
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
        }

        private static void PrintConfigSummary()
        {
            const int border = 140;
            Console.WriteLine(new string('#', border));
            Console.WriteLine($"\t AssemblyName:\t{TestFinder.AssemblyName}");
            Console.WriteLine($"\t Run mode:\t{Enum.GetName(typeof(RunMode), s_mode)}");
            foreach (KeyValuePair<string, string> item in TestMetrics.Overrides) Console.WriteLine($"\t Override:\t{item.Key} = {item.Value}");
            foreach (string item in TestMetrics.SelectedTests) Console.WriteLine($"\t Test:\t{item}");
            Console.WriteLine($"\t Duration:\t{TestMetrics.StressDuration} second(s)");
            Console.WriteLine($"\t Threads No.:\t{TestMetrics.StressThreads}");
            Console.WriteLine($"\t Debug mode:\t{s_debugMode}");
            Console.WriteLine($"\t Exception threshold:\t{TestMetrics.ExceptionThreshold}");
            Console.WriteLine($"\t Random seed:\t{TestMetrics.RandomSeed}");
            Console.WriteLine($"\t Filter:\t{TestMetrics.Filter}");
            Console.WriteLine($"\t Deadlock detection enabled:\t{DeadlockDetection.IsEnabled}");
            Console.WriteLine(new string('#', border));
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
            if (!s_debugMode)
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
    }
}
