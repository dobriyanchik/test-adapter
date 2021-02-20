﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Unicorn.Taf.Core.Engine;

namespace Unicorn.TestAdapter
{
    [ExtensionUri(ExecutorUriString)]
    public class UnicrornTestExecutor : ITestExecutor
    {
        public const string ExecutorUriString = "executor://UnicornTestExecutor/v1";

        private const string Prefix = "Unicorn Adapter: ";

        private const string RunStarting = Prefix + "Test run starting";
        private const string RunComplete = Prefix + "Test run complete";

        public static readonly Uri ExecutorUri = new Uri(ExecutorUriString);

        private bool m_cancelled;

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            if (string.IsNullOrEmpty(runContext.SolutionDirectory))
            {
                frameworkHandle.SendMessage(TestMessageLevel.Informational, Prefix + "test run outside of Visual studio is not supported yet. Exiting...");
                return;
            }

            frameworkHandle.SendMessage(TestMessageLevel.Informational, RunStarting);
            m_cancelled = false;

            var runDir = ExecutorUtilities.PrepareRunDirectory(runContext.SolutionDirectory);
            ExecutorUtilities.CopyDeploymentItems(runContext, runDir, frameworkHandle);

            foreach (var source in sources)
            {
                ExecutorUtilities.CopySourceFilesToRunDir(Path.GetDirectoryName(source), runDir);
                LaunchOutcome outcome = null;

                try
                {
                    using (var isolation = new UnicornAppDomainIsolation<IsolatedTestsRunner>(runDir))
                    {
                        Environment.CurrentDirectory = runDir;
                        var newSource = source.Replace(Path.GetDirectoryName(source), runDir);

                        outcome = isolation.Instance.RunTests(source);
                    }
                }
                catch (Exception ex)
                {
                    frameworkHandle.SendMessage(TestMessageLevel.Error, Prefix + $"error running tests ({ex.ToString()})");
                }
            }

            frameworkHandle.SendMessage(TestMessageLevel.Informational, RunComplete);
        }

        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            if (string.IsNullOrEmpty(runContext.SolutionDirectory))
            {
                frameworkHandle.SendMessage(TestMessageLevel.Informational, Prefix + "test run outside of Visual studio is not supported yet. Exiting...");
                return;
            }

            var sources = tests.Select(t => t.Source).Distinct();

            frameworkHandle.SendMessage(TestMessageLevel.Informational, RunStarting);
            m_cancelled = false;

            var runDir = ExecutorUtilities.PrepareRunDirectory(runContext.SolutionDirectory);
            ExecutorUtilities.CopyDeploymentItems(runContext, runDir, frameworkHandle);

            foreach (var source in sources)
            {
                ExecutorUtilities.CopySourceFilesToRunDir(Path.GetDirectoryName(source), runDir);

                LaunchOutcome outcome = null;
                var succeeded = false;

                try
                {
                    using (var loader = new UnicornAppDomainIsolation<IsolatedTestsRunner>(runDir))
                    {
                        Environment.CurrentDirectory = runDir;
                        var newSource = source.Replace(Path.GetDirectoryName(source), runDir);

                        outcome = loader.Instance.RunTests(newSource, tests.Select(t => t.FullyQualifiedName).ToArray());

                        foreach (TestCase test in tests)
                        {
                            if (!outcome.RunInitialized)
                            {
                                FailTest(test, outcome.RunnerException, frameworkHandle);
                            }
                            else
                            {
                                var unicornOutcome = outcome.SuitesOutcomes.SelectMany(so => so.TestsOutcomes).First(to => to.FullMethodName.Equals(test.FullyQualifiedName));
                                var testResult = GetTestResultFromOutcome(unicornOutcome, test);
                                frameworkHandle.RecordResult(testResult);
                            }
                        }

                        succeeded = true;
                    }
                }
                catch (Exception ex)
                {
                    if (succeeded)
                    {
                        frameworkHandle.SendMessage(TestMessageLevel.Warning, ex.ToString());
                    }
                    else
                    {
                        foreach (TestCase test in tests)
                        {
                            FailTest(test, ex, frameworkHandle);
                        }
                    }
                }
            }

            frameworkHandle.SendMessage(TestMessageLevel.Informational, RunComplete);
        }

        public void Cancel() =>
            m_cancelled = true;

        private void FailTest(TestCase test, Exception ex, IFrameworkHandle frameworkHandle)
        {
            var testResult = new TestResult(test)
            {
                ComputerName = Environment.MachineName,
                Outcome = TestOutcome.Failed,
                ErrorMessage = ex.Message,
                ErrorStackTrace = ex.StackTrace
            };

            frameworkHandle.RecordResult(testResult);
        }

        private TestResult GetTestResultFromOutcome(Taf.Core.Testing.TestOutcome outcome, TestCase testCase)
        {
            var testResult = new TestResult(testCase);
            testResult.ComputerName = Environment.MachineName;

            switch (outcome.Result)
            {
                case Taf.Core.Testing.Status.Passed:
                    testResult.Outcome = TestOutcome.Passed;
                    testResult.Duration = outcome.ExecutionTime;
                    break;
                case Taf.Core.Testing.Status.Failed:
                    testResult.Outcome = TestOutcome.Failed;
                    testResult.ErrorMessage = outcome.Exception.Message;
                    testResult.ErrorStackTrace = outcome.Exception.StackTrace;
                    testResult.Duration = outcome.ExecutionTime;
                    break;
                case Taf.Core.Testing.Status.Skipped:
                    testResult.Outcome = TestOutcome.Skipped;
                    break;
                default:
                    testResult.Outcome = TestOutcome.None;
                    break;
            }

            return testResult;
        }
    }
}
