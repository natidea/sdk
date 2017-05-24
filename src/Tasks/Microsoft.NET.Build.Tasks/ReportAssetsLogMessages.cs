// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.Common;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Report Log Messages in the assets file to MSBuild and raise them as 
    /// DiagnosticMessage items that can be consumed downstream (e.g. by the
    /// dependency node in the solution explorer)
    /// </summary>
    public sealed class ReportAssetsLogMessages : TaskBase
    {
        private LockFile _lockFile;
        private readonly HashSet<NuGetLogCode> _warnAsErrorCodes = new HashSet<NuGetLogCode>();
        private readonly HashSet<NuGetLogCode> _noWarnCodes = new HashSet<NuGetLogCode>();
        private readonly static string[] _separators = new string[] { ",", ";" };

        #region Outputs

        // Only output is 'DiagnosticMessages' which is in the base class TaskBase

        #endregion

        #region Inputs

        /// <summary>
        /// The assets file to process
        /// </summary>
        [Required]
        public string ProjectAssetsFile
        {
            get; set;
        }

        /// <summary>
        /// Indicate that warnings should always be treated as errors
        /// </summary>
        public bool TreatWarningsAsErrors
        {
            get; set;
        }

        /// <summary>
        /// Comma separated list of codes for diagnostics that should 
        /// always be treated as errors
        /// </summary>
        public string WarnAsError
        {
            get; set;
        }

        /// <summary>
        /// Comma separated list of codes for diagnostics that should be 
        /// filtered out of the final results
        /// </summary>
        public string NoWarn
        {
            get; set;
        }

        #endregion

        public ReportAssetsLogMessages()
        {
        }

        #region Test Support

        internal ReportAssetsLogMessages(LockFile lockFile, ILog logger)
            : base(logger)
        {
            _lockFile = lockFile;
        }

        #endregion

        private LockFile LockFile
        {
            get
            {
                if (_lockFile == null)
                {
                    _lockFile = new LockFileCache(BuildEngine4).GetLockFile(ProjectAssetsFile);
                }

                return _lockFile;
            }
        }

        protected override void ExecuteCore()
        {
            InitializeCodeSets();

            foreach (var message in LockFile.LogMessages)
            {
                AddMessage(message);
            }
        }

        private void InitializeCodeSets()
        {
            AddNuGetCodesToSet(WarnAsError, _warnAsErrorCodes);
            AddNuGetCodesToSet(NoWarn, _noWarnCodes);
        }

        private void AddMessage(IAssetsLogMessage message)
        {
            if (_noWarnCodes.Contains(message.Code))
            {
                return;
            }

            var logToMsBuild = true;
            var targetGraphs = message.GetTargetGraphs(LockFile);

            targetGraphs = targetGraphs.Any() ? targetGraphs : new LockFileTarget[] { null };

            foreach (var target in targetGraphs)
            {
                var targetLib = message.LibraryId == null ? null : target?.GetTargetLibrary(message.LibraryId);

                Diagnostics.Add(
                    message.Code.ToString(),
                    message.Message,
                    message.FilePath,
                    ComputeSeverity(message),
                    message.StartLineNumber,
                    message.StartColumnNumber,
                    message.EndLineNumber,
                    message.EndColumnNumber,
                    target?.Name,
                    targetLib == null ? null : $"{targetLib.Name}/{targetLib.Version.ToNormalizedString()}",
                    logToMsBuild);

                logToMsBuild = false; // only write first instance of this diagnostic to msbuild
            }
        }

        private DiagnosticMessageSeverity ComputeSeverity(IAssetsLogMessage message)
        {
            var severity = FromLogLevel(message.Level);
            if (TreatWarningsAsErrors && severity == DiagnosticMessageSeverity.Warning)
            {
                severity = DiagnosticMessageSeverity.Error;
            }
            else
            {
                severity = _warnAsErrorCodes.Contains(message.Code) ? DiagnosticMessageSeverity.Error : severity;
            }
            return severity;
        }

        private static DiagnosticMessageSeverity FromLogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return DiagnosticMessageSeverity.Error;

                case LogLevel.Warning:
                    return DiagnosticMessageSeverity.Warning;

                case LogLevel.Debug:
                case LogLevel.Verbose:
                case LogLevel.Information:
                case LogLevel.Minimal:
                default:
                    return DiagnosticMessageSeverity.Info;
            }
        }

        private static void AddNuGetCodesToSet(string codes, HashSet<NuGetLogCode> codeSet)
        {
            if (!string.IsNullOrWhiteSpace(codes))
            {
                codes.Split(_separators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(code => ParseNuGetCode(code))
                    .Where(code => code != NuGetLogCode.Undefined)
                    .ToList().ForEach(code => codeSet.Add(code));
            }
        }

        private static NuGetLogCode ParseNuGetCode(string code) => 
            Enum.TryParse(code.Trim(), true, out NuGetLogCode nugetLogCode) 
            ? nugetLogCode 
            : NuGetLogCode.Undefined;
    }
}
