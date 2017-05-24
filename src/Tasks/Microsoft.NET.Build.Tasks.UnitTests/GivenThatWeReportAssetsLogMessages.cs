// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using NuGet.Common;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Microsoft.NET.Build.Tasks.UnitTests.LockFileSnippets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeReportAssetsLogMessages
    {
        [Fact]
        public void ItReportsDiagnosticsWithMinimumData()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning")
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            task.DiagnosticMessages.Should().HaveCount(1);
            log.Messages.Should().HaveCount(1);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new object[] { new string[0] })]
        public void ItReportsZeroDiagnosticsWithNoLogs(string [] logsJson)
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(logsJson);

            var task = GetExecutedTaskFromContents(lockFileContent, log);
            
            task.DiagnosticMessages.Should().BeEmpty();
            log.Messages.Should().BeEmpty();
        }

        [Fact]
        public void ItReportsDiagnosticsMetadataWithLogs()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Error, "Sample error",
                        filePath: "path/to/project.csproj",
                        libraryId: "LibA",
                        targetGraphs: new string[]{ ".NETCoreApp,Version=v1.0" }),
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Warning, "Sample warning",
                        libraryId: "LibB",
                        targetGraphs: new string[]{ ".NETCoreApp,Version=v1.0" })
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            log.Messages.Should().HaveCount(2);
            task.DiagnosticMessages.Should().HaveCount(2);

            Action<string,string,string> checkMetadata = (key, val1, val2) => {
                task.DiagnosticMessages
                    .Select(item => item.GetMetadata(key))
                    .Should().Contain(new string[] { val1, val2 });
            };

            checkMetadata(MetadataKeys.DiagnosticCode, "NU1000", "NU1001");
            checkMetadata(MetadataKeys.Severity, "Error", "Warning");
            checkMetadata(MetadataKeys.Message, "Sample error", "Sample warning");
            checkMetadata(MetadataKeys.FilePath, "", "path/to/project.csproj");
            checkMetadata(MetadataKeys.ParentTarget, ".NETCoreApp,Version=v1.0", ".NETCoreApp,Version=v1.0");
            checkMetadata(MetadataKeys.ParentPackage, "LibA/1.2.3", "LibB/1.2.3");
        }

        [Theory]
        [InlineData(null, null, "", "")]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, null, ".NETCoreApp,Version=v1.0", "")]
        [InlineData(null, "LibA", "", "")]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, "LibA", ".NETCoreApp,Version=v1.0", "LibA/1.2.3")]
        public void ItReportsDiagnosticsWithAllTargetLibraryCases(string[] targetGraphs, string libraryId, string expectedTarget, string expectedPackage)
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning", 
                        filePath: "path/to/project.csproj",
                        libraryId: libraryId,
                        targetGraphs: targetGraphs)
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            log.Messages.Should().HaveCount(1);
            task.DiagnosticMessages.Should().HaveCount(1);
            var item = task.DiagnosticMessages.First();

            item.GetMetadata(MetadataKeys.ParentTarget).Should().Be(expectedTarget);
            item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(expectedPackage);
        }

        [Fact]
        public void ItHandlesInfoLogLevels()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Information, "Sample message"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Minimal, "Sample message"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Verbose, "Sample message"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Debug, "Sample message"),
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            log.Messages.Should().HaveCount(4);
            task.DiagnosticMessages.Should().HaveCount(4);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.Severity))
                    .Should().OnlyContain(s => s == "Info");
        }

        [Theory]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0", ".NETFramework,Version=v4.6.1" }, "LibA")]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, "LibA")]
        public void ItHandlesMultiTFMScenarios(string[] targetGraphs, string libraryId)
        {
            var log = new MockLog();
            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETFramework,Version=v4.6.1", TargetLibA, TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] {
                    ProjectGroup, NETCoreGroup, NET461Group
                },
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning",
                        filePath: "path/to/project.csproj",
                        libraryId: libraryId,
                        targetGraphs: targetGraphs)
                }
            );            

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            // a diagnostic for each target graph...
            task.DiagnosticMessages.Should().HaveCount(targetGraphs.Length);

            // ...but only one is logged
            log.Messages.Should().HaveCount(1);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.ParentTarget))
                    .Should().Contain(targetGraphs);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.ParentPackage))
                    .Should().OnlyContain(v => v.StartsWith(libraryId));
        }

        [Fact]
        public void ItSkipsInvalidEntries()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Error, "Sample error that will be invalid"),
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Warning, "Sample warning"),
                }
            );
            lockFileContent = lockFileContent.Replace("NU1000", "CA1000");

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            log.Messages.Should().HaveCount(1);
            task.DiagnosticMessages.Should().HaveCount(1);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.DiagnosticCode))
                    .Should().OnlyContain(v => v == "NU1001");
        }

        [Fact]
        public void ItCanReportWarningsAsErrors()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Error, "1000 error"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "1000 warning"), // warning -> error
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Error, "1001 error"),
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Warning, "1001 warning"),
                    CreateLog(NuGetLogCode.NU1002, LogLevel.Information, "1002 info"), // info -> error
                    CreateLog(NuGetLogCode.NU1100, LogLevel.Information, "1100 info"),
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log, warnAsError: "NU1000;NU1002");

            log.Messages.Should().HaveCount(6);
            log.Messages.Where(m => m.StartsWith("[ERROR]:")).Should().HaveCount(4);

            task.DiagnosticMessages.Should().HaveCount(6);

            task.DiagnosticMessages
                .Where(item => item.HasMetadataValue(MetadataKeys.Severity, "Error"))
                .Should().HaveCount(4);

            task.DiagnosticMessages
                .Where(item => item.HasMetadataValue(MetadataKeys.DiagnosticCode, "NU1000"))
                .Select(item => item.GetMetadata(MetadataKeys.Severity))
                .Should().OnlyContain(v => v == "Error");

            task.DiagnosticMessages
                .Where(item => item.HasMetadataValue(MetadataKeys.DiagnosticCode, "NU1002"))
                .Select(item => item.GetMetadata(MetadataKeys.Severity))
                .Should().OnlyContain(v => v == "Error");

            task.DiagnosticMessages
                .Where(item => item.HasMetadataValue(MetadataKeys.DiagnosticCode, "NU1001"))
                .Where(item => item.HasMetadataValue(MetadataKeys.Severity, "Warning"))
                .Should().HaveCount(1);

            task.DiagnosticMessages
                .Where(item => item.HasMetadataValue(MetadataKeys.DiagnosticCode, "NU1100"))
                .Select(item => item.GetMetadata(MetadataKeys.Severity))
                .Should().OnlyContain(v => v == "Info");
        }


        [Fact]
        public void ItSuppressesNoWarnIssues()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Error, "1000 error"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "1000 warning"),
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Error, "1001 error"),
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Warning, "1001 warning"),
                    CreateLog(NuGetLogCode.NU1002, LogLevel.Information, "1002 info"),
                    CreateLog(NuGetLogCode.NU1100, LogLevel.Information, "1100 info"),
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log, noWarn: "NU1000;NU1002");

            log.Messages.Should().HaveCount(3);
            task.DiagnosticMessages.Should().HaveCount(3);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.DiagnosticCode))
                    .Should().Contain(new string[] { "NU1001", "NU1100" });
        }

        public static IEnumerable<object[]> InvalidCodes
        {
            get
            {
                return new[]
                {
                    new object[] {"", null},
                    new object[] {"  ", null},
                    new object[] {";", null},
                    new object[] {"abc!@#;", null},
                    new object[] {",1000;", null},
                    new object[] {"abc!@#;NU1100", new string[] { "NU1100" }},
                    new object[] {"NU1101, NU1102;NU1103,abcd", new string[] { "NU1101", "NU1102", "NU1103" }},
                    new object[] {"NU1101,NU999", new string[] { "NU1101" }},
                    new object[] {"NU1101,CS2002", new string[] { "NU1101" }},
                };
            }
        }

        [Theory]
        [MemberData(nameof(InvalidCodes))]
        public void ItSkipsInvalidWarnAsErrorCodes(string warnAsError, string [] expectedErrors)
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1100, LogLevel.Warning, "Sample warning"),
                    CreateLog(NuGetLogCode.NU1101, LogLevel.Warning, "Sample warning"),
                    CreateLog(NuGetLogCode.NU1102, LogLevel.Warning, "Sample warning"),
                    CreateLog(NuGetLogCode.NU1103, LogLevel.Warning, "Sample warning"),
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log, warnAsError: warnAsError);

            log.Messages.Should().HaveCount(4);
            task.DiagnosticMessages.Should().HaveCount(4);

            var errors = task.DiagnosticMessages
                .Where(item => item.HasMetadataValue(MetadataKeys.Severity, "Error"));

            expectedErrors = expectedErrors ?? new string[0];
            errors.Should().HaveCount(expectedErrors.Length);            

            if (expectedErrors.Length > 0)
            {
                errors
                    .Select(item => item.GetMetadata(MetadataKeys.DiagnosticCode))
                    .Should().Contain(expectedErrors);
            }
            else
            {
                errors.Should().BeEmpty();
            }
        }

        [Theory]
        [MemberData(nameof(InvalidCodes))]
        public void ItSkipsInvalidNoWarnCodes(string noWarn, string[] expectedSuppressed)
        {
            var log = new MockLog();
            string[] logMessages = new string[] {
                    CreateLog(NuGetLogCode.NU1100, LogLevel.Warning, "Sample warning"),
                    CreateLog(NuGetLogCode.NU1101, LogLevel.Warning, "Sample warning"),
                    CreateLog(NuGetLogCode.NU1102, LogLevel.Warning, "Sample warning"),
                    CreateLog(NuGetLogCode.NU1103, LogLevel.Warning, "Sample warning"),
                };

            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: logMessages
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log, noWarn: noWarn);

            expectedSuppressed = expectedSuppressed ?? new string[0];
            var initialCount = logMessages.Length;
            var finalCount = initialCount - expectedSuppressed.Length;

            log.Messages.Should().HaveCount(finalCount);
            task.DiagnosticMessages.Should().HaveCount(finalCount);

            if (expectedSuppressed.Length > 0)
            {
                task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.DiagnosticCode))
                    .Should().NotContain(expectedSuppressed);
            }
            else
            {
                task.DiagnosticMessages.Should().HaveCount(initialCount);
            }
        }

        private static string CreateDefaultLockFileSnippet(string[] logs = null) => 
            CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] {
                    ProjectGroup, NETCoreGroup
                },
                logs: logs
            );

        private ReportAssetsLogMessages GetExecutedTaskFromContents(string lockFileContents, MockLog logger,
            string warnAsError = null, string noWarn = null)
        {
            var lockFile = TestLockFiles.CreateLockFile(lockFileContents);
            return GetExecutedTask(lockFile, logger, warnAsError, noWarn);
        }

        private ReportAssetsLogMessages GetExecutedTask(LockFile lockFile, MockLog logger, 
            string warnAsError = null, string noWarn = null)
        {
            var task = new ReportAssetsLogMessages(lockFile, logger)
            {
                ProjectAssetsFile = lockFile.Path,
                WarnAsError = warnAsError,
                NoWarn = noWarn
            };

            task.Execute().Should().BeTrue();

            return task;
        }
    }
}
