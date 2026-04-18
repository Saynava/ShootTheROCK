#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;

public static class ShootTheRockGitCheckpoint
{
    private const string DefaultBranch = "main";
    private const int MaxChangedFilesInMessage = 24;

    [MenuItem("Tools/Shoot the ROCK/Save Everything + GitHub Checkpoint")]
    public static void SaveEverythingAndGitCheckpointMenu()
    {
        RunCheckpoint();
    }

    private static void RunCheckpoint()
    {
        string projectRoot = GetProjectRoot();
        if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
        {
            EditorUtility.DisplayDialog("Shoot the ROCK Git Checkpoint", "Project root not found.", "OK");
            return;
        }

        GitCommandResult repoCheck = RunGit("rev-parse --is-inside-work-tree", projectRoot);
        if (!repoCheck.Success)
        {
            EditorUtility.DisplayDialog("Shoot the ROCK Git Checkpoint", "This Unity project is not inside a git repository yet.", "OK");
            return;
        }

        string userName = RunGit("config --get user.name", projectRoot).StdOut.Trim();
        string userEmail = RunGit("config --get user.email", projectRoot).StdOut.Trim();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(userEmail))
        {
            EditorUtility.DisplayDialog("Shoot the ROCK Git Checkpoint", "Git user.name or user.email is missing. Set git author info first.", "OK");
            return;
        }

        ShootTheRockSceneSync.SaveCurrentCheckpointState();

        GitCommandResult statusResult = RunGit("status --short", projectRoot);
        if (!statusResult.Success)
        {
            ShowGitFailure("Failed to read git status.", statusResult);
            return;
        }

        List<string> changedFiles = ParseChangedFiles(statusResult.StdOut);
        if (changedFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("Shoot the ROCK Git Checkpoint", "No file changes detected, nothing to commit.", "OK");
            return;
        }

        GitCommandResult addResult = RunGit("add -A", projectRoot);
        if (!addResult.Success)
        {
            ShowGitFailure("Failed to stage files.", addResult);
            return;
        }

        string commitMessage = BuildCommitMessage(changedFiles);
        string tempCommitMessagePath = Path.Combine(projectRoot, ".git", "SHOOTTHEROCK_COMMITMSG.txt");
        File.WriteAllText(tempCommitMessagePath, commitMessage, Encoding.UTF8);

        try
        {
            GitCommandResult commitResult = RunGit($"commit -F \"{tempCommitMessagePath}\"", projectRoot);
            if (!commitResult.Success)
            {
                if (ContainsNothingToCommit(commitResult))
                {
                    EditorUtility.DisplayDialog("Shoot the ROCK Git Checkpoint", "No staged changes remained after save/checkpoint, so no commit was created.", "OK");
                    return;
                }

                ShowGitFailure("Failed to create commit.", commitResult);
                return;
            }

            GitCommandResult pushResult = RunGit($"push origin {DefaultBranch}", projectRoot);
            if (!pushResult.Success)
            {
                ShowGitFailure("Commit was created locally, but push failed.", pushResult);
                return;
            }

            string commitHash = RunGit("rev-parse --short HEAD", projectRoot).StdOut.Trim();
            string changedSummary = string.Join("\n", changedFiles.GetRange(0, Math.Min(changedFiles.Count, 8)).ToArray());
            if (changedFiles.Count > 8)
                changedSummary += $"\n... +{changedFiles.Count - 8} more";

            EditorUtility.DisplayDialog(
                "Shoot the ROCK Git Checkpoint",
                $"Checkpoint created and pushed.\n\nCommit: {commitHash}\nBranch: {DefaultBranch}\n\nChanged files:\n{changedSummary}",
                "OK");
        }
        finally
        {
            if (File.Exists(tempCommitMessagePath))
                File.Delete(tempCommitMessagePath);
        }
    }

    private static string BuildCommitMessage(List<string> changedFiles)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Checkpoint: save everything and push");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine("Workflow: Save Everything + GitHub Checkpoint");
        builder.AppendLine("Restore path: Tools > Shoot the ROCK > Restore Saved State");
        builder.AppendLine("Included state files:");
        builder.AppendLine("- Assets/_Game/State/ShootTheRockSceneStateSnapshot.md");
        builder.AppendLine("- Assets/_Game/State/ShootTheRockSceneStateSnapshot.flat.txt");
        builder.AppendLine("- Assets/_Game/State/ShootTheRockSceneStateChanges.md");
        builder.AppendLine("- Assets/_Game/State/ShootTheRockProjectStateSummary.md");
        builder.AppendLine("- Assets/_Game/State/ShootTheRockRestoreSnapshot.json");
        builder.AppendLine();
        builder.AppendLine("Changed files:");

        int limit = Math.Min(changedFiles.Count, MaxChangedFilesInMessage);
        for (int i = 0; i < limit; i++)
            builder.AppendLine($"- {changedFiles[i]}");

        if (changedFiles.Count > limit)
            builder.AppendLine($"- ... and {changedFiles.Count - limit} more files");

        return builder.ToString();
    }

    private static List<string> ParseChangedFiles(string gitStatusOutput)
    {
        List<string> changedFiles = new List<string>();
        if (string.IsNullOrWhiteSpace(gitStatusOutput))
            return changedFiles;

        string[] lines = gitStatusOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.Length < 4)
                continue;

            string path = line.Substring(3).Trim();
            changedFiles.Add(line.Substring(0, 2).Trim() + " " + path);
        }

        return changedFiles;
    }

    private static bool ContainsNothingToCommit(GitCommandResult result)
    {
        string combined = (result.StdOut + "\n" + result.StdErr).ToLowerInvariant();
        return combined.Contains("nothing to commit") || combined.Contains("no changes added to commit");
    }

    private static void ShowGitFailure(string intro, GitCommandResult result)
    {
        string details = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
        if (details.Length > 700)
            details = details.Substring(0, 700) + "...";

        EditorUtility.DisplayDialog("Shoot the ROCK Git Checkpoint", intro + "\n\n" + details, "OK");
        UnityEngine.Debug.LogError(intro + "\n" + result.StdOut + "\n" + result.StdErr);
    }

    private static string GetProjectRoot()
    {
        return Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
    }

    private static GitCommandResult RunGit(string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using (Process process = Process.Start(startInfo))
        {
            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new GitCommandResult(process.ExitCode, stdOut, stdErr);
        }
    }

    private readonly struct GitCommandResult
    {
        public readonly int ExitCode;
        public readonly string StdOut;
        public readonly string StdErr;

        public bool Success => ExitCode == 0;

        public GitCommandResult(int exitCode, string stdOut, string stdErr)
        {
            ExitCode = exitCode;
            StdOut = stdOut ?? string.Empty;
            StdErr = stdErr ?? string.Empty;
        }
    }
}
#endif
