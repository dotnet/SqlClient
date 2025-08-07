// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Data.SqlClient.StressTests.Runner;

namespace Microsoft.Data.SqlClient.StressTests.TestBase
{
    public abstract class TestBase
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

        private TestAttributeBase _attr;
        private string _variationSuffix = "";

        protected MethodInfo _testMethod;

        protected Type _type;

        protected List<MethodInfo> _setupMethods;

        protected List<MethodInfo> _cleanupMethods;

        protected delegate void TestMethodDelegate(object t);

        public TestBase(TestAttributeBase attr,
                        MethodInfo testMethodInfo,
                        Type type,
                        List<MethodInfo> setupMethods,
                        List<MethodInfo> cleanupMethods)
        {
            _attr = attr;
            _testMethod = testMethodInfo;
            _type = type;
            _setupMethods = setupMethods;
            _cleanupMethods = cleanupMethods;
        }

        public string Title
        {
            get { return _attr.Title + _variationSuffix; }
        }

        public string Description
        {
            get { return _attr.Description; }
        }

        public string Category
        {
            get { return _attr.Category; }
        }

        public TestPriority Priority
        {
            get { return _attr.Priority; }
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

        public abstract void Run();

        protected void ExecuteSetupPhase(object targetInstance)
        {
            if (_setupMethods != null)
            {
                foreach (MethodInfo setupMthd in _setupMethods)
                {
                    setupMthd.Invoke(targetInstance, null);
                }
            }
        }

        protected void ExecuteCleanupPhase(object targetInstance)
        {
            if (_cleanupMethods != null)
            {
                foreach (MethodInfo cleanupMethod in _cleanupMethods)
                {
                    cleanupMethod.Invoke(targetInstance, null);
                }
            }
        }

        protected void SetVariations(object targetInstance)
        {
            FieldInfo[] fields = targetInstance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

            foreach (FieldInfo fi in fields)
            {
                TestVariationAttribute[] attrs = (TestVariationAttribute[])fi.GetCustomAttributes(typeof(TestVariationAttribute), false);

                foreach (TestVariationAttribute testVarAttr in attrs)
                {
                    foreach (string specifiedVariation in TestMetrics.Variations)
                    {
                        if (specifiedVariation.Equals(testVarAttr.VariationName))
                        {
                            fi.SetValue(targetInstance, testVarAttr.VariationValue);
                            _variationSuffix += "_" + testVarAttr.VariationName;
                            break;
                        }
                    }
                }
            }
        }

        protected TestMethodDelegate CreateTestMethodDelegate()
        {
            return new TestMethodDelegate((instance) => _testMethod.Invoke(instance, null));
        }

        protected void LogTestFailure(string exceptionData)
        {
            Console.WriteLine("{0}: Failed", this.Title);
            Console.WriteLine(exceptionData);

            Logger logger = new Logger(TestMetrics.RunLabel, false, TestMetrics.Milestone, TestMetrics.Branch);
            logger.AddTest(this.Title);
            logger.AddTestMetric("Test Assembly", _testMethod.Module.FullyQualifiedName, null);
            logger.AddTestException(exceptionData);
            logger.Save();
        }

        protected void LogStandardMetrics(Logger logger)
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
