using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

/// <summary>
/// Reproducible Linux development build entry point for menus and -executeMethod.
/// </summary>
public static class StandaloneBuildAutomation
{
    public const string OutputDirectory = "Builds/Linux";
    public const string StagingDirectory = "Builds/Staging/Linux";
    public const string BackupDirectory = "Builds/Backup/Linux";
    public const string LogDirectory = "Builds/Logs";
    public const string ExecutableName = "TacticalPrototype.x86_64";
    public const string BatchMethodName = "StandaloneBuildAutomation.BuildLinuxDevelopmentBatch";
    public const string PackagingStatusFileName = "linux-packaging-status.txt";
    public const string RuntimeSmokeStatusFileName = "linux-runtime-smoke-status.txt";
    public const string RuntimeSmokeBatchMethodName = "StandaloneBuildAutomation.RunLinuxRuntimeSmokeBatch";
    public const int RuntimeSmokeTimeoutMilliseconds = 20000;

    private const int ExecutePermission = 1;
    private const int TerminationSignal = 15;
    private const int TerminationGraceMilliseconds = 3000;

    private static readonly string[] FcitxEnvironmentVariables =
    {
        "SDL_IM_MODULE",
        "QT_IM_MODULE",
        "XMODIFIERS",
    };

    private static readonly string[] RequiredRuntimeLogMarkers =
    {
        "Mono path[0] =",
        "Run started (seed=",
        "Map scene loaded — MapView will rebuild on OnEnable.",
    };

    private static readonly string[] CrashLogMarkers =
    {
        "Segmentation fault",
        "SIGSEGV",
        "Native Crash Reporting",
        "Crash!!!",
    };

    [DllImport("libc", SetLastError = true)]
    private static extern int access(string path, int mode);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int processId, int signal);

    public sealed class RuntimeSmokeResult
    {
        public RuntimeSmokeResult(
            string status,
            string diagnostic,
            string logPath,
            bool reachedTimeout,
            int? exitCode)
        {
            Status = status;
            Diagnostic = diagnostic;
            LogPath = logPath;
            ReachedTimeout = reachedTimeout;
            ExitCode = exitCode;
        }

        public string Status { get; }
        public string Diagnostic { get; }
        public string LogPath { get; }
        public bool ReachedTimeout { get; }
        public int? ExitCode { get; }
        public bool Succeeded => Status == "Passed";
    }

    public static string AbsoluteOutputDirectory => GetAbsoluteProjectPath(OutputDirectory);

    public static string AbsoluteStagingDirectory => GetAbsoluteProjectPath(StagingDirectory);

    public static string AbsoluteBackupDirectory => GetAbsoluteProjectPath(BackupDirectory);

    public static string AbsoluteLogDirectory => GetAbsoluteProjectPath(LogDirectory);

    public static string AbsoluteExecutablePath => Path.Combine(
        AbsoluteOutputDirectory,
        ExecutableName);

    public static string AbsoluteStagingExecutablePath => Path.Combine(
        AbsoluteStagingDirectory,
        ExecutableName);

    public static string AbsolutePackagingStatusPath => Path.Combine(
        AbsoluteLogDirectory,
        PackagingStatusFileName);

    public static string AbsoluteRuntimeSmokeStatusPath => Path.Combine(
        AbsoluteLogDirectory,
        RuntimeSmokeStatusFileName);

    public static BuildPlayerOptions CreateBuildPlayerOptions()
    {
        return new BuildPlayerOptions
        {
            scenes = ProjectBaselineValidator.GetRequiredScenePaths(),
            locationPathName = AbsoluteStagingExecutablePath,
            target = BuildTarget.StandaloneLinux64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.Development |
                      BuildOptions.StrictMode |
                      BuildOptions.DetailedBuildReport |
                      BuildOptions.CleanBuildCache,
        };
    }

    /// <summary>
    /// Pure configuration guard used by the build entry point and lightweight tests.
    /// </summary>
    public static IReadOnlyList<string> CollectConfigurationErrors(BuildPlayerOptions options)
    {
        var errors = new List<string>();
        string[] expectedScenes = ProjectBaselineValidator.GetRequiredScenePaths();

        if (options.target != BuildTarget.StandaloneLinux64)
            errors.Add("Build target must be StandaloneLinux64.");
        if (options.targetGroup != BuildTargetGroup.Standalone)
            errors.Add("Build target group must be Standalone.");
        if (options.scenes == null || !options.scenes.SequenceEqual(expectedScenes))
            errors.Add("Build scenes must exactly match the validated project scene order.");

        string configuredOutput;
        try
        {
            configuredOutput = Path.GetFullPath(options.locationPathName ?? string.Empty);
        }
        catch (Exception exception)
        {
            errors.Add($"Build output path is invalid: {exception.Message}");
            configuredOutput = string.Empty;
        }

        if (!string.Equals(configuredOutput, AbsoluteStagingExecutablePath, StringComparison.Ordinal))
            errors.Add($"Build output must use isolated staging path '{AbsoluteStagingExecutablePath}'.");

        RequireOption(options.options, BuildOptions.Development, errors);
        RequireOption(options.options, BuildOptions.StrictMode, errors);
        RequireOption(options.options, BuildOptions.DetailedBuildReport, errors);
        RequireOption(options.options, BuildOptions.CleanBuildCache, errors);
        return errors;
    }

    [MenuItem("Tools/TacticalRogue/Build/Linux Development")]
    public static void BuildLinuxDevelopmentFromMenu()
    {
        BuildLinuxDevelopment();
    }

    /// <summary>
    /// CLI entry point: Unity -batchmode -quit -projectPath .
    /// -executeMethod StandaloneBuildAutomation.BuildLinuxDevelopmentBatch
    /// </summary>
    public static void BuildLinuxDevelopmentBatch()
    {
        BuildLinuxDevelopment();
    }

    [MenuItem("Tools/TacticalRogue/Smoke/Linux Runtime")]
    public static void RunLinuxRuntimeSmokeFromMenu()
    {
        RunLinuxRuntimeSmokeOrThrow();
    }

    /// <summary>CLI entry point for -executeMethod. This does not rebuild the player.</summary>
    public static void RunLinuxRuntimeSmokeBatch()
    {
        RunLinuxRuntimeSmokeOrThrow();
    }

    public static RuntimeSmokeResult RunLinuxRuntimeSmoke(
        int timeoutMilliseconds = RuntimeSmokeTimeoutMilliseconds)
    {
        if (timeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));

        IReadOnlyList<string> artifactErrors = CollectArtifactErrors(AbsoluteOutputDirectory);
        if (artifactErrors.Count > 0)
        {
            var invalidPackaging = new RuntimeSmokeResult(
                "Failed",
                "Runtime smoke requires valid packaging: " + string.Join(" | ", artifactErrors),
                null,
                false,
                null);
            WriteRuntimeSmokeStatus(invalidPackaging);
            return invalidPackaging;
        }

        Directory.CreateDirectory(AbsoluteLogDirectory);
        string logPath = CreateRuntimeSmokeLogPath();
        ProcessStartInfo startInfo = CreateRuntimeSmokeStartInfo(logPath);
        Process process = null;
        bool reachedTimeout = false;
        int? exitCode = null;

        try
        {
            process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Process.Start returned no Linux player process.");

            bool exited = process.WaitForExit(timeoutMilliseconds);
            if (exited)
            {
                exitCode = process.ExitCode;
            }
            else
            {
                reachedTimeout = true;
                TerminateProcessSafely(process);
                if (process.HasExited)
                    exitCode = process.ExitCode;
            }

            string logContents = File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
            RuntimeSmokeResult result = ClassifyRuntimeSmoke(
                logContents,
                reachedTimeout,
                exitCode,
                logPath);
            WriteRuntimeSmokeStatus(result);
            return result;
        }
        catch (Exception exception)
        {
            var failed = new RuntimeSmokeResult(
                "Failed",
                $"Runtime smoke execution failed: {exception.Message}",
                logPath,
                reachedTimeout,
                exitCode);
            WriteRuntimeSmokeStatus(failed);
            return failed;
        }
        finally
        {
            if (process != null)
            {
                if (!process.HasExited)
                    TerminateProcessSafely(process);
                process.Dispose();
            }
        }
    }

    public static ProcessStartInfo CreateRuntimeSmokeStartInfo(string absoluteLogPath)
    {
        if (!Path.IsPathRooted(absoluteLogPath))
            throw new ArgumentException("Runtime smoke log path must be absolute.", nameof(absoluteLogPath));

        var startInfo = new ProcessStartInfo
        {
            FileName = AbsoluteExecutablePath,
            WorkingDirectory = AbsoluteOutputDirectory,
            Arguments = $"-batchmode -nographics -logFile \"{absoluteLogPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Environment isolation for the diagnosed SDL_Fcitx_Init crash. This does not alter game behavior.
        foreach (string variableName in FcitxEnvironmentVariables)
            startInfo.Environment.Remove(variableName);
        return startInfo;
    }

    public static RuntimeSmokeResult ClassifyRuntimeSmoke(
        string logContents,
        bool reachedTimeout,
        int? exitCode,
        string logPath = null)
    {
        logContents = logContents ?? string.Empty;
        string crashMarker = CrashLogMarkers.FirstOrDefault(marker => Contains(logContents, marker));
        if (crashMarker != null)
        {
            return new RuntimeSmokeResult(
                "Failed",
                $"Crash marker found in player log: {crashMarker}",
                logPath,
                reachedTimeout,
                exitCode);
        }

        if (exitCode == 139)
        {
            return new RuntimeSmokeResult(
                "Failed",
                "Player reported crash exit code 139.",
                logPath,
                reachedTimeout,
                exitCode);
        }

        if (reachedTimeout && exitCode != 0 && exitCode != 143)
        {
            return new RuntimeSmokeResult(
                "Failed",
                $"Player termination after controlled timeout returned unexpected code {FormatExitCode(exitCode)}; expected 0 or 143.",
                logPath,
                true,
                exitCode);
        }

        if (!reachedTimeout && exitCode != 0)
        {
            return new RuntimeSmokeResult(
                "Failed",
                $"Player exited unexpectedly before timeout with code {FormatExitCode(exitCode)}.",
                logPath,
                false,
                exitCode);
        }

        string[] missingMarkers = RequiredRuntimeLogMarkers
            .Where(marker => !Contains(logContents, marker))
            .ToArray();
        if (missingMarkers.Length > 0)
        {
            return new RuntimeSmokeResult(
                "Failed",
                "Player log is missing required markers: " + string.Join(" | ", missingMarkers),
                logPath,
                reachedTimeout,
                exitCode);
        }

        string completion = reachedTimeout
            ? "Player remained alive until the controlled timeout."
            : "Player exited cleanly after initialization.";
        return new RuntimeSmokeResult("Passed", completion, logPath, reachedTimeout, exitCode);
    }

    public static string CreateRuntimeSmokeLogPath(DateTime? utcTimestamp = null)
    {
        Directory.CreateDirectory(AbsoluteLogDirectory);
        string timestamp = (utcTimestamp ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyyMMdd-HHmmssfff");
        return CreateUniquePath(AbsoluteLogDirectory, $"linux-runtime-smoke-{timestamp}", ".log");
    }

    public static BuildReport BuildLinuxDevelopment()
    {
        BuildPlayerOptions options = CreateBuildPlayerOptions();
        IReadOnlyList<string> errors = CollectConfigurationErrors(options);
        if (errors.Count > 0)
            throw new BuildFailedException(FormatErrors(errors));

        // ProjectBaselineValidator runs once through IPreprocessBuildWithReport.
        // Only staging is cleared; the last promoted build and logs remain intact.
        RecreateStagingDirectory();
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        catch (Exception exception)
        {
            WritePackagingStatus("Failed", exception.Message);
            throw;
        }

        if (report == null)
        {
            WritePackagingStatus("Failed", "BuildPipeline returned no BuildReport.");
            throw new BuildFailedException("Linux development build returned no BuildReport.");
        }
        if (report.summary.result != BuildResult.Succeeded)
        {
            WritePackagingStatus(
                "Failed",
                $"BuildResult={report.summary.result}; Errors={report.summary.totalErrors}");
            throw new BuildFailedException(
                $"Linux development build failed: result={report.summary.result}, " +
                $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}.");
        }

        var artifactErrors = CollectArtifactErrors(AbsoluteStagingDirectory);
        if (artifactErrors.Count > 0)
        {
            WritePackagingStatus("Failed", string.Join(" | ", artifactErrors));
            throw new BuildFailedException(FormatErrors(artifactErrors));
        }

        try
        {
            artifactErrors = PromoteValidatedStagingBuild(
                AbsoluteStagingDirectory,
                AbsoluteOutputDirectory,
                AbsoluteBackupDirectory,
                path => CollectArtifactErrors(path),
                AbsoluteLogDirectory);
        }
        catch (Exception exception)
        {
            WritePackagingStatus("PromotionFailed", exception.Message);
            throw;
        }

        if (artifactErrors.Count > 0)
        {
            WritePackagingStatus("PromotionInvalid", string.Join(" | ", artifactErrors));
            throw new BuildFailedException(FormatErrors(artifactErrors));
        }

        WritePackagingStatus(
            "Succeeded",
            $"Output={AbsoluteExecutablePath}; Size={report.summary.totalSize}; Warnings={report.summary.totalWarnings}");

        Debug.Log(
            $"Linux packaging succeeded: {AbsoluteExecutablePath} " +
            $"({report.summary.totalSize} bytes, {report.summary.totalTime}). Runtime smoke was not run.");
        return report;
    }

    public static IReadOnlyList<string> CollectArtifactErrors(
        string buildDirectory,
        Func<string, bool> executableCheck = null)
    {
        var errors = new List<string>();
        string executablePath = Path.Combine(buildDirectory, ExecutableName);
        if (!File.Exists(executablePath))
        {
            errors.Add($"Expected executable is missing: {executablePath}");
        }
        else
        {
            if (new FileInfo(executablePath).Length == 0)
                errors.Add($"Expected executable is empty: {executablePath}");
            Func<string, bool> check = executableCheck ?? IsExecutableOnCurrentHost;
            if (!check(executablePath))
                errors.Add($"Expected executable does not have Linux execute permission: {executablePath}");
        }

        string unityPlayerPath = Path.Combine(buildDirectory, "UnityPlayer.so");
        RequireNonEmptyFile(unityPlayerPath, "Unity runtime", errors);

        string dataDirectory = Path.Combine(
            buildDirectory,
            Path.GetFileNameWithoutExtension(ExecutableName) + "_Data");
        if (!Directory.Exists(dataDirectory))
        {
            errors.Add($"Expected player data directory is missing: {dataDirectory}");
            return errors;
        }

        RequireNonEmptyFile(Path.Combine(dataDirectory, "boot.config"), "player data file", errors);
        RequireNonEmptyFile(Path.Combine(dataDirectory, "globalgamemanagers"), "player data file", errors);
        RequireNonEmptyFile(Path.Combine(dataDirectory, "level0"), "player data file", errors);
        RequireNonEmptyFile(Path.Combine(dataDirectory, "ScriptingAssemblies.json"), "player data file", errors);
        return errors;
    }

    /// <summary>
    /// Promotes staging, validates the promoted files, and only then removes the previous build backup.
    /// Any promotion exception or post-promotion validation error restores the previous build.
    /// </summary>
    public static IReadOnlyList<string> PromoteValidatedStagingBuild(
        string stagingDirectory,
        string outputDirectory,
        string backupDirectory,
        Func<string, IReadOnlyList<string>> validatePromotedBuild,
        string logDirectory = null,
        Action<string, string> promoteAction = null)
    {
        if (!Directory.Exists(stagingDirectory))
            throw new DirectoryNotFoundException($"Staging build is missing: {stagingDirectory}");
        if (validatePromotedBuild == null)
            throw new ArgumentNullException(nameof(validatePromotedBuild));

        if (Directory.Exists(backupDirectory))
        {
            if (!Directory.Exists(outputDirectory))
                Directory.Move(backupDirectory, outputDirectory);
            else
                Directory.Delete(backupDirectory, true);
        }

        bool previousBuildMoved = false;
        if (Directory.Exists(outputDirectory))
        {
            PreservePlayerSmokeLog(
                outputDirectory,
                logDirectory ?? Path.Combine(Path.GetDirectoryName(outputDirectory), "Logs"));
            Directory.CreateDirectory(Path.GetDirectoryName(backupDirectory));
            Directory.Move(outputDirectory, backupDirectory);
            previousBuildMoved = true;
        }

        try
        {
            (promoteAction ?? Directory.Move)(stagingDirectory, outputDirectory);
            IReadOnlyList<string> validationErrors =
                validatePromotedBuild(outputDirectory) ?? new[] { "Promoted build validation returned no result." };
            if (validationErrors.Count > 0)
            {
                RestorePreviousBuild(outputDirectory, backupDirectory, previousBuildMoved);
                return validationErrors;
            }
        }
        catch
        {
            RestorePreviousBuild(outputDirectory, backupDirectory, previousBuildMoved);
            throw;
        }

        if (Directory.Exists(backupDirectory))
            Directory.Delete(backupDirectory, true);
        return Array.Empty<string>();
    }

    /// <summary>Copies an existing smoke log outside replaceable build output without overwriting evidence.</summary>
    public static string PreservePlayerSmokeLog(
        string outputDirectory,
        string logDirectory,
        DateTime? utcTimestamp = null)
    {
        string sourcePath = Path.Combine(outputDirectory, "player-smoke.log");
        if (!File.Exists(sourcePath))
            return null;

        Directory.CreateDirectory(logDirectory);
        string timestamp = (utcTimestamp ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyyMMdd-HHmmssfff");
        string destinationPath = Path.Combine(logDirectory, $"player-smoke-{timestamp}.log");
        int suffix = 1;
        while (File.Exists(destinationPath))
        {
            destinationPath = Path.Combine(logDirectory, $"player-smoke-{timestamp}-{suffix}.log");
            suffix++;
        }

        File.Copy(sourcePath, destinationPath, false);
        return destinationPath;
    }

    private static void RecreateStagingDirectory()
    {
        if (Directory.Exists(AbsoluteStagingDirectory))
            Directory.Delete(AbsoluteStagingDirectory, true);
        Directory.CreateDirectory(AbsoluteStagingDirectory);
        Directory.CreateDirectory(AbsoluteLogDirectory);
    }

    private static void RestorePreviousBuild(
        string outputDirectory,
        string backupDirectory,
        bool previousBuildMoved)
    {
        if (Directory.Exists(outputDirectory))
            Directory.Delete(outputDirectory, true);
        if (previousBuildMoved && Directory.Exists(backupDirectory))
            Directory.Move(backupDirectory, outputDirectory);
    }

    private static void RunLinuxRuntimeSmokeOrThrow()
    {
        RuntimeSmokeResult result = RunLinuxRuntimeSmoke();
        if (!result.Succeeded)
            throw new BuildFailedException(result.Diagnostic);

        Debug.Log($"Linux runtime smoke passed. {result.Diagnostic} Log: {result.LogPath}");
    }

    private static void TerminateProcessSafely(Process process)
    {
        if (process.HasExited)
            return;

        if (Application.platform == RuntimePlatform.LinuxEditor)
        {
            SendSignal(process.Id, TerminationSignal);
            if (process.WaitForExit(TerminationGraceMilliseconds))
                return;
        }

        process.Kill();
        if (!process.WaitForExit(TerminationGraceMilliseconds))
            throw new InvalidOperationException($"Linux player process {process.Id} did not terminate.");
    }

    private static void RequireOption(
        BuildOptions actual,
        BuildOptions required,
        ICollection<string> errors)
    {
        if ((actual & required) == 0)
            errors.Add($"Build option {required} must be enabled.");
    }

    private static string FormatErrors(IReadOnlyList<string> errors)
    {
        return "Linux development build validation failed:\n- " + string.Join("\n- ", errors);
    }

    private static string GetAbsoluteProjectPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }

    private static bool IsExecutableOnCurrentHost(string path)
    {
        return Application.platform != RuntimePlatform.LinuxEditor || access(path, ExecutePermission) == 0;
    }

    private static void RequireNonEmptyFile(
        string path,
        string label,
        ICollection<string> errors)
    {
        if (!File.Exists(path))
            errors.Add($"Expected {label} is missing: {path}");
        else if (new FileInfo(path).Length == 0)
            errors.Add($"Expected {label} is empty: {path}");
    }

    private static void WritePackagingStatus(
        string packagingStatus,
        string details)
    {
        Directory.CreateDirectory(AbsoluteLogDirectory);
        File.WriteAllText(
            AbsolutePackagingStatusPath,
            $"PackagingStatus={packagingStatus}{Environment.NewLine}" +
            $"Details={details}{Environment.NewLine}");
    }

    private static void WriteRuntimeSmokeStatus(RuntimeSmokeResult result)
    {
        Directory.CreateDirectory(AbsoluteLogDirectory);
        File.WriteAllText(
            AbsoluteRuntimeSmokeStatusPath,
            $"RuntimeSmokeStatus={result.Status}{Environment.NewLine}" +
            $"ReachedTimeout={result.ReachedTimeout}{Environment.NewLine}" +
            $"ExitCode={FormatExitCode(result.ExitCode)}{Environment.NewLine}" +
            $"LogPath={result.LogPath ?? string.Empty}{Environment.NewLine}" +
            $"Diagnostic={result.Diagnostic}{Environment.NewLine}");
    }

    private static string CreateUniquePath(
        string directory,
        string fileNameWithoutExtension,
        string extension)
    {
        string path = Path.Combine(directory, fileNameWithoutExtension + extension);
        int suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{fileNameWithoutExtension}-{suffix}{extension}");
            suffix++;
        }
        return path;
    }

    private static bool Contains(string value, string marker)
    {
        return value.IndexOf(marker, StringComparison.Ordinal) >= 0;
    }

    private static string FormatExitCode(int? exitCode)
    {
        return exitCode.HasValue ? exitCode.Value.ToString() : "NotAvailable";
    }
}
