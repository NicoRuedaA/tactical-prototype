using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

public sealed class StandaloneBuildAutomationTests
{
    private readonly List<string> _temporaryDirectories = new List<string>();

    [TearDown]
    public void TearDown()
    {
        foreach (string path in _temporaryDirectories)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }

    [Test]
    public void CreateBuildPlayerOptions_UsesValidatedLinuxDevelopmentConfiguration()
    {
        BuildPlayerOptions options = StandaloneBuildAutomation.CreateBuildPlayerOptions();

        Assert.That(options.target, Is.EqualTo(BuildTarget.StandaloneLinux64));
        Assert.That(options.targetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
        Assert.That(options.scenes, Is.EqualTo(ProjectBaselineValidator.GetRequiredScenePaths()));
        Assert.That(
            Path.GetFullPath(options.locationPathName),
            Is.EqualTo(StandaloneBuildAutomation.AbsoluteStagingExecutablePath));
        Assert.That(options.options.HasFlag(BuildOptions.Development), Is.True);
        Assert.That(options.options.HasFlag(BuildOptions.StrictMode), Is.True);
        Assert.That(options.options.HasFlag(BuildOptions.DetailedBuildReport), Is.True);
        Assert.That(options.options.HasFlag(BuildOptions.CleanBuildCache), Is.True);
        Assert.That(StandaloneBuildAutomation.CollectConfigurationErrors(options), Is.Empty);
    }

    [Test]
    public void CollectConfigurationErrors_RejectsWrongTargetScenesOutputAndFlags()
    {
        BuildPlayerOptions options = StandaloneBuildAutomation.CreateBuildPlayerOptions();
        options.target = BuildTarget.WebGL;
        options.targetGroup = BuildTargetGroup.WebGL;
        options.scenes = options.scenes.Reverse().ToArray();
        options.locationPathName = Path.Combine("Builds", "wrong-player");
        options.options = BuildOptions.None;

        var errors = StandaloneBuildAutomation.CollectConfigurationErrors(options);

        Assert.That(errors, Has.Some.Contains("StandaloneLinux64"));
        Assert.That(errors, Has.Some.Contains("target group"));
        Assert.That(errors, Has.Some.Contains("scene order"));
        Assert.That(errors, Has.Some.Contains("Build output"));
        Assert.That(errors, Has.Some.Contains(nameof(BuildOptions.Development)));
        Assert.That(errors, Has.Some.Contains(nameof(BuildOptions.StrictMode)));
        Assert.That(errors, Has.Some.Contains(nameof(BuildOptions.DetailedBuildReport)));
        Assert.That(errors, Has.Some.Contains(nameof(BuildOptions.CleanBuildCache)));
    }

    [Test]
    public void CollectArtifactErrors_RequiresExecutableRuntimeAndEssentialData()
    {
        string buildDirectory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(buildDirectory, "TacticalPrototype_Data"));
        File.WriteAllText(Path.Combine(buildDirectory, StandaloneBuildAutomation.ExecutableName), string.Empty);

        var errors = StandaloneBuildAutomation.CollectArtifactErrors(buildDirectory, _ => false);

        Assert.That(errors, Has.Some.Contains("execute permission"));
        Assert.That(errors, Has.Some.Contains("UnityPlayer.so"));
        Assert.That(errors, Has.Some.Contains("boot.config"));
        Assert.That(errors, Has.Some.Contains("globalgamemanagers"));
        Assert.That(errors, Has.Some.Contains("level0"));
        Assert.That(errors, Has.Some.Contains("ScriptingAssemblies.json"));
    }

    [Test]
    public void CollectArtifactErrors_AcceptsCompletePackagingArtifacts()
    {
        string buildDirectory = CreateTemporaryDirectory();
        string dataDirectory = Path.Combine(buildDirectory, "TacticalPrototype_Data");
        Directory.CreateDirectory(dataDirectory);
        WriteArtifact(buildDirectory, StandaloneBuildAutomation.ExecutableName);
        WriteArtifact(buildDirectory, "UnityPlayer.so");
        WriteArtifact(dataDirectory, "boot.config");
        WriteArtifact(dataDirectory, "globalgamemanagers");
        WriteArtifact(dataDirectory, "level0");
        WriteArtifact(dataDirectory, "ScriptingAssemblies.json");

        Assert.That(
            StandaloneBuildAutomation.CollectArtifactErrors(buildDirectory, _ => true),
            Is.Empty);
    }

    [Test]
    public void PromoteValidatedStagingBuild_ReplacesPreviousBuildAndPreservesSiblingLogs()
    {
        string root = CreateTemporaryDirectory();
        string staging = Path.Combine(root, "Staging", "Linux");
        string output = Path.Combine(root, "Linux");
        string backup = Path.Combine(root, "Backup", "Linux");
        string logs = Path.Combine(root, "Logs");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(staging, "new-build"), "new");
        File.WriteAllText(Path.Combine(output, "old-build"), "old");
        File.WriteAllText(Path.Combine(logs, "build.log"), "evidence");

        StandaloneBuildAutomation.PromoteValidatedStagingBuild(
            staging,
            output,
            backup,
            _ => Array.Empty<string>(),
            logs);

        Assert.That(File.Exists(Path.Combine(output, "new-build")), Is.True);
        Assert.That(File.Exists(Path.Combine(output, "old-build")), Is.False);
        Assert.That(Directory.Exists(staging), Is.False);
        Assert.That(Directory.Exists(backup), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(logs, "build.log")), Is.EqualTo("evidence"));
    }

    [Test]
    public void PromoteValidatedStagingBuild_WhenPromotionFails_RestoresPreviousBuild()
    {
        string root = CreateTemporaryDirectory();
        string staging = Path.Combine(root, "Staging", "Linux");
        string output = Path.Combine(root, "Linux");
        string backup = Path.Combine(root, "Backup", "Linux");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(staging, "new-build"), "new");
        File.WriteAllText(Path.Combine(output, "old-build"), "old");

        Assert.Throws<IOException>(() => StandaloneBuildAutomation.PromoteValidatedStagingBuild(
            staging,
            output,
            backup,
            _ => Array.Empty<string>(),
            Path.Combine(root, "Logs"),
            (_, __) => throw new IOException("Injected promotion failure.")));

        Assert.That(File.ReadAllText(Path.Combine(output, "old-build")), Is.EqualTo("old"));
        Assert.That(File.Exists(Path.Combine(staging, "new-build")), Is.True);
        Assert.That(Directory.Exists(backup), Is.False);
    }

    [Test]
    public void PromoteValidatedStagingBuild_RecoversInterruptedBackupBeforeAttemptingPromotion()
    {
        string root = CreateTemporaryDirectory();
        string staging = Path.Combine(root, "Staging", "Linux");
        string output = Path.Combine(root, "Linux");
        string backup = Path.Combine(root, "Backup", "Linux");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(staging, "new-build"), "new");
        File.WriteAllText(Path.Combine(backup, "old-build"), "old");

        Assert.Throws<IOException>(() => StandaloneBuildAutomation.PromoteValidatedStagingBuild(
            staging,
            output,
            backup,
            _ => Array.Empty<string>(),
            Path.Combine(root, "Logs"),
            (_, __) => throw new IOException("Injected promotion failure.")));

        Assert.That(File.ReadAllText(Path.Combine(output, "old-build")), Is.EqualTo("old"));
        Assert.That(File.Exists(Path.Combine(staging, "new-build")), Is.True);
        Assert.That(Directory.Exists(backup), Is.False);
    }

    [Test]
    public void PromoteValidatedStagingBuild_WhenPostValidationFails_RestoresBackup()
    {
        string root = CreateTemporaryDirectory();
        string staging = Path.Combine(root, "Staging", "Linux");
        string output = Path.Combine(root, "Linux");
        string backup = Path.Combine(root, "Backup", "Linux");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(staging, "new-build"), "new");
        File.WriteAllText(Path.Combine(output, "old-build"), "old");

        IReadOnlyList<string> errors = StandaloneBuildAutomation.PromoteValidatedStagingBuild(
            staging,
            output,
            backup,
            _ => new[] { "Promoted artifacts are invalid." },
            Path.Combine(root, "Logs"));

        Assert.That(errors, Is.EqualTo(new[] { "Promoted artifacts are invalid." }));
        Assert.That(File.ReadAllText(Path.Combine(output, "old-build")), Is.EqualTo("old"));
        Assert.That(File.Exists(Path.Combine(output, "new-build")), Is.False);
        Assert.That(Directory.Exists(backup), Is.False);
    }

    [Test]
    public void PreservePlayerSmokeLog_UsesUniqueNameWithoutRemovingSource()
    {
        string root = CreateTemporaryDirectory();
        string output = Path.Combine(root, "Linux");
        string logs = Path.Combine(root, "Logs");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(output, "player-smoke.log"), "crash evidence");
        var timestamp = new DateTime(2026, 7, 17, 12, 34, 56, 789, DateTimeKind.Utc);
        File.WriteAllText(Path.Combine(logs, "player-smoke-20260717-123456789.log"), "older evidence");

        string preservedPath = StandaloneBuildAutomation.PreservePlayerSmokeLog(output, logs, timestamp);

        Assert.That(Path.GetFileName(preservedPath), Is.EqualTo("player-smoke-20260717-123456789-1.log"));
        Assert.That(File.ReadAllText(preservedPath), Is.EqualTo("crash evidence"));
        Assert.That(File.ReadAllText(Path.Combine(output, "player-smoke.log")), Is.EqualTo("crash evidence"));
        Assert.That(
            File.ReadAllText(Path.Combine(logs, "player-smoke-20260717-123456789.log")),
            Is.EqualTo("older evidence"));
    }

    [Test]
    public void CreateRuntimeSmokeStartInfo_IsHeadlessAndRemovesFcitxEnvironment()
    {
        string logPath = Path.Combine(CreateTemporaryDirectory(), "runtime-smoke.log");

        var startInfo = StandaloneBuildAutomation.CreateRuntimeSmokeStartInfo(logPath);

        Assert.That(startInfo.FileName, Is.EqualTo(StandaloneBuildAutomation.AbsoluteExecutablePath));
        Assert.That(startInfo.WorkingDirectory, Is.EqualTo(StandaloneBuildAutomation.AbsoluteOutputDirectory));
        Assert.That(startInfo.Arguments, Does.Contain("-batchmode"));
        Assert.That(startInfo.Arguments, Does.Contain("-nographics"));
        Assert.That(startInfo.Arguments, Does.Contain(logPath));
        Assert.That(startInfo.Environment.ContainsKey("SDL_IM_MODULE"), Is.False);
        Assert.That(startInfo.Environment.ContainsKey("QT_IM_MODULE"), Is.False);
        Assert.That(startInfo.Environment.ContainsKey("XMODIFIERS"), Is.False);
    }

    [Test]
    public void ClassifyRuntimeSmoke_WithRequiredMarkersAndControlledTimeout_Passes()
    {
        var result = StandaloneBuildAutomation.ClassifyRuntimeSmoke(
            CompleteRuntimeLog(),
            true,
            143,
            "/tmp/runtime-smoke.log");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.ReachedTimeout, Is.True);
        Assert.That(result.Diagnostic, Does.Contain("controlled timeout"));
    }

    [Test]
    public void ClassifyRuntimeSmoke_WithRequiredMarkersAndCleanExit_Passes()
    {
        var result = StandaloneBuildAutomation.ClassifyRuntimeSmoke(
            CompleteRuntimeLog(),
            false,
            0);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.ReachedTimeout, Is.False);
        Assert.That(result.Diagnostic, Does.Contain("exited cleanly"));
    }

    [Test]
    public void ClassifyRuntimeSmoke_RejectsCrashUnexpectedExitAndMissingMarkers()
    {
        var crash = StandaloneBuildAutomation.ClassifyRuntimeSmoke(
            CompleteRuntimeLog() + "\nReceived SIGSEGV",
            true,
            143);
        var unexpectedExit = StandaloneBuildAutomation.ClassifyRuntimeSmoke(
            CompleteRuntimeLog(),
            false,
            139);
        var missingMarker = StandaloneBuildAutomation.ClassifyRuntimeSmoke(
            "Mono path[0] = '/tmp/Managed'\nRun started (seed=1)",
            false,
            0);

        Assert.That(crash.Succeeded, Is.False);
        Assert.That(crash.Diagnostic, Does.Contain("Crash marker"));
        Assert.That(unexpectedExit.Succeeded, Is.False);
        Assert.That(unexpectedExit.Diagnostic, Does.Contain("code 139"));
        Assert.That(missingMarker.Succeeded, Is.False);
        Assert.That(missingMarker.Diagnostic, Does.Contain("Map scene loaded"));
    }

    [Test]
    public void ClassifyRuntimeSmoke_AfterTimeout_RejectsCrashAndUnexpectedTerminationCodes()
    {
        var segfault = StandaloneBuildAutomation.ClassifyRuntimeSmoke(
            CompleteRuntimeLog(),
            true,
            139);
        var unexpectedKill = StandaloneBuildAutomation.ClassifyRuntimeSmoke(
            CompleteRuntimeLog(),
            true,
            137);

        Assert.That(segfault.Succeeded, Is.False);
        Assert.That(segfault.Diagnostic, Does.Contain("crash exit code 139"));
        Assert.That(unexpectedKill.Succeeded, Is.False);
        Assert.That(unexpectedKill.Diagnostic, Does.Contain("expected 0 or 143"));
    }

    private string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tactical-build-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);
        return path;
    }

    private static void WriteArtifact(string directory, string fileName)
    {
        File.WriteAllText(Path.Combine(directory, fileName), "artifact");
    }

    private static string CompleteRuntimeLog()
    {
        return "Mono path[0] = '/tmp/TacticalPrototype_Data/Managed'\n" +
               "Run started (seed=123) with 2 pieces, 8 map nodes\n" +
               "Map scene loaded — MapView will rebuild on OnEnable.";
    }
}
