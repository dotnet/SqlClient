// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Data.SqlClient.StressTests.Runner;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    public sealed class StressTest
    {
        #region Constants

        private const string MetricNameTestAssembly = "Test Assembly";
        private const string MetricNameTestImprovement = "Improvement";
        private const string MetricNameTestOwner = "Owner";
        private const string MetricNameTestCategory = "Category";
        private const string MetricNameTestPriority = "Priority";
        private const string MetricNameApplicationName = "Application Name";
        private const string MetricNameTargetAssemblyName = "Target Assembly Name";
        private const string MetricNamePeakWorkingSet = "Peak Working Set";
        private const string MetricNameWorkingSet = "Working Set";
        private const string MetricNamePrivateBytes = "Private Bytes";

        #endregion

        private delegate void TestMethodDelegate(object t);

        private StressTestAttribute _attr;
        private List<MethodInfo> _cleanupMethods;
        private object _targetInstance;
        private TestMethodDelegate _tmd;
        private List<MethodInfo> _setupMethods;
        private MethodInfo _testMethod;
        private Type _type;
        private string _variationSuffix = string.Empty;

        // TODO: MethodInfo objects below can have associated delegates to improve
        // runtime performance.
        private MethodInfo _globalSetupMethod;
        private MethodInfo _globalCleanupMethod;

        public delegate void ExceptionHandler(Exception e);

        /// <summary>
        /// Cache the global exception handler method reference. It is
        /// recommended not to actually use this reference to call the
        /// method. Use the delegate instead.
        /// </summary>
        private MethodInfo _globalExceptionHandlerMethod;

        /// <summary>
        /// Create a delegate to call global exception handler method.
        /// Use this delegate to call test assembly's exception handler.
        /// </summary>
        private ExceptionHandler _globalExceptionHandlerDelegate;

        public StressTest(
            StressTestAttribute attr,
            MethodInfo testMethodInfo,
            MethodInfo globalSetupMethod,
            MethodInfo globalCleanupMethod,
            Type type,
            List<MethodInfo> setupMethods,
            List<MethodInfo> cleanupMethods,
            MethodInfo globalExceptionHandlerMethod)
        {
            _attr = attr;
            _cleanupMethods = cleanupMethods;
            _globalCleanupMethod = globalCleanupMethod;
            _globalExceptionHandlerMethod = globalExceptionHandlerMethod;
            _globalSetupMethod = globalSetupMethod;
            _setupMethods = setupMethods;
            _testMethod = testMethodInfo;
            _type = type;
        }

        #region Properties

        public string Category => _attr.Category;

        public string Description => _attr.Description;

        public TestPriority Priority => _attr.Priority;

        public string Title => _attr.Title + _variationSuffix;

        public int Weight => _attr.Weight;

        #endregion

        #region Public Methods

        public StressTest Clone()
        {
            StressTest t = new StressTest(_attr, this._testMethod, this._globalSetupMethod, this._globalCleanupMethod, this._type, this._setupMethods, this._cleanupMethods, this._globalExceptionHandlerMethod);
            return t;
        }

        public List<string> GetVariations()
        {
            FieldInfo[] fields = _type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

            List<string> variations = new List<string>(10);
            foreach (FieldInfo fi in fields)
            {
                TestVariationAttribute[] attrs = (TestVariationAttribute[])fi.GetCustomAttributes(typeof(TestVariationAttribute), false);

                foreach (TestVariationAttribute testVarAttr in attrs)
                {
                    if (!variations.Contains(testVarAttr.VariationName))
                    {
                        variations.Add(testVarAttr.VariationName);
                    }
                }
            }

            return variations;
        }

        /// <summary>
        /// Provide an opportunity to handle the exception
        /// </summary>
        /// <param name="e"></param>
        public void HandleException(Exception e)
        {
            if (null != _globalExceptionHandlerDelegate)
            {
                _globalExceptionHandlerDelegate(e);
            }
        }

        /// <summary>
        /// Execute the test method(s)
        /// </summary>
        public void Run() =>
            _tmd(_targetInstance);

        /// <summary>
        /// Run any per-thread cleanup for the test
        /// </summary>
        public void RunCleanup()
        {
            if (_cleanupMethods != null)
            {
                foreach (MethodInfo cleanupMethod in _cleanupMethods)
                {
                    cleanupMethod.Invoke(_targetInstance, null);
                }
            }
        }

        /// <summary>
        /// Run final global cleanup for the test assembly. Could be used to release resources or for reporting, etc.
        /// </summary>
        public void RunGlobalCleanup()
        {
            if (null != _globalCleanupMethod)
            {
                _globalCleanupMethod.Invoke(_targetInstance, null);
            }
        }

        /// <summary>
        /// Perform any global initialization for the test assembly. For example, make the connection to the database, load a workspace, etc.
        /// </summary>
        public void RunGlobalSetup()
        {
            if (null == _targetInstance)
            {
                InitTargetInstance();
            }

            if (null != _globalSetupMethod)
            {
                _globalSetupMethod.Invoke(_targetInstance, null);
            }
        }

        /// <summary>
        /// Run any per-thread setup needed
        /// </summary>
        public void RunSetup()
        {
            // create an instance of the class that defines the test method.
            if (null == _targetInstance)
            {
                InitTargetInstance();
            }
            _tmd = new TestMethodDelegate(instance => _testMethod.Invoke(instance, null));

            // Set variation fields on the target instance
            FieldInfo[] fields = _targetInstance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

            foreach (FieldInfo fi in fields)
            {
                TestVariationAttribute[] attrs = (TestVariationAttribute[])fi.GetCustomAttributes(typeof(TestVariationAttribute), false);

                foreach (TestVariationAttribute testVarAttr in attrs)
                {
                    foreach (string specifiedVariation in TestMetrics.Variations)
                    {
                        if (specifiedVariation.Equals(testVarAttr.VariationName))
                        {
                            fi.SetValue(_targetInstance, testVarAttr.VariationValue);
                            _variationSuffix += "_" + testVarAttr.VariationName;
                            break;
                        }
                    }
                }
            }

            // Execute the setup phase for this thread.
            if (_setupMethods != null)
            {
                foreach (MethodInfo setupMthd in _setupMethods)
                {
                    setupMthd.Invoke(_targetInstance, null);
                }
            }
        }

        #endregion

        private void InitTargetInstance()
        {
            _targetInstance = _type.GetConstructor(Type.EmptyTypes).Invoke(null);

            // Create a delegate for exception handling on _targetInstance
            if (_globalExceptionHandlerMethod != null)
            {
                _globalExceptionHandlerDelegate = (ExceptionHandler)_globalExceptionHandlerMethod.CreateDelegate(
                    typeof(ExceptionHandler),
                    _targetInstance
                    );
            }
        }

        // @TODO: I'm not sure how these methods were being used before ....

        private void LogTestFailure(string exceptionData)
        {
            Console.WriteLine("{0}: Failed", this.Title);
            Console.WriteLine(exceptionData);

            Logger logger = new Logger(TestMetrics.RunLabel, false, TestMetrics.Milestone, TestMetrics.Branch);
            logger.AddTest(this.Title);
            logger.AddTestMetric("Test Assembly", _testMethod.Module.FullyQualifiedName, null);
            logger.AddTestException(exceptionData);
            logger.Save();
        }

        private void LogStandardMetrics(Logger logger)
        {
            logger.AddTestMetric(MetricNameTestAssembly, _testMethod.Module.FullyQualifiedName, null);
            logger.AddTestMetric(MetricNameTestImprovement, _attr.Improvement, null);
            logger.AddTestMetric(MetricNameTestOwner, _attr.Owner, null);
            logger.AddTestMetric(MetricNameTestCategory, _attr.Category, null);
            logger.AddTestMetric(MetricNameTestPriority, _attr.Priority.ToString(), null);
            logger.AddTestMetric(MetricNameApplicationName, _attr.Improvement, null);

            if (TestMetrics.TargetAssembly != null)
            {
                logger.AddTestMetric(MetricNameTargetAssemblyName, (new AssemblyName(TestMetrics.TargetAssembly.FullName)).Name, null);
            }

            logger.AddTestMetric(MetricNamePeakWorkingSet, string.Format("{0}", TestMetrics.PeakWorkingSet), "bytes");
            logger.AddTestMetric(MetricNameWorkingSet, string.Format("{0}", TestMetrics.WorkingSet), "bytes");
            logger.AddTestMetric(MetricNamePrivateBytes, string.Format("{0}", TestMetrics.PrivateBytes), "bytes");
        }
    }
}
