using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace RexTools.GitIntegration.Editor
{
    /// <summary>
    /// Static helper class for detecting Git repositories and running Git commands asynchronously.
    /// </summary>
    public static class GitRunner
    {
        private static string cachedRepoPath;

        /// <summary>
        /// Finds the closest Git repository root by searching upward from the current project directory.
        /// </summary>
        public static string FindRepositoryRoot()
        {
            if (!string.IsNullOrEmpty(cachedRepoPath)) return cachedRepoPath;

            string currentDir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(currentDir))
            {
                if (Directory.Exists(Path.Combine(currentDir, ".git")))
                {
                    cachedRepoPath = currentDir.Replace("\\", "/");
                    return cachedRepoPath;
                }
                var parent = Directory.GetParent(currentDir);
                currentDir = parent != null ? parent.FullName : null;
            }
            return null;
        }

        /// <summary>
        /// Checks if a Git repository root has been detected.
        /// </summary>
        public static bool HasGitRepository()
        {
            return !string.IsNullOrEmpty(FindRepositoryRoot());
        }

        /// <summary>
        /// Executes a Git command asynchronously on a background thread.
        /// Outputs are marshalled back to the main thread via EditorApplication.delayCall.
        /// </summary>
        public static Task<int> RunCommandAsync(string args, Action<string> onOutputLine, Action<string> onErrorLine = null)
        {
            var tcs = new TaskCompletionSource<int>();
            string repoRoot = FindRepositoryRoot();

            if (string.IsNullOrEmpty(repoRoot))
            {
                onErrorLine?.Invoke("Error: No Git repository root found.");
                tcs.SetResult(-1);
                return tcs.Task;
            }

            Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = args,
                        WorkingDirectory = repoRoot,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    var process = new Process { StartInfo = startInfo };
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            string data = e.Data;
                            EditorApplication.delayCall += () => onOutputLine?.Invoke(data);
                        }
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            string data = e.Data;
                            EditorApplication.delayCall += () => (onErrorLine ?? onOutputLine)?.Invoke(data);
                        }
                    };

                    if (!process.Start())
                    {
                        EditorApplication.delayCall += () => (onErrorLine ?? onOutputLine)?.Invoke("Failed to start process 'git'.");
                        tcs.SetResult(-1);
                        return;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();
                    int exitCode = process.ExitCode;
                    process.Close();

                    EditorApplication.delayCall += () => tcs.SetResult(exitCode);
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    EditorApplication.delayCall += () =>
                    {
                        (onErrorLine ?? onOutputLine)?.Invoke($"Exception executing git: {msg}");
                        tcs.SetResult(-1);
                    };
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Retrieves the current active branch name asynchronously.
        /// </summary>
        public static async Task<string> GetCurrentBranchAsync()
        {
            if (!HasGitRepository()) return "None";
            string branch = "";
            await RunCommandAsync("branch --show-current", line => branch = line.Trim());
            return string.IsNullOrEmpty(branch) ? "Unknown" : branch;
        }

        /// <summary>
        /// Retrieves the count of commits ahead/behind the remote branch.
        /// </summary>
        public static async Task<(int ahead, int behind)> GetSyncCountsAsync()
        {
            if (!HasGitRepository()) return (0, 0);
            string resultLine = "";
            
            // Runs rev-list to get left-right count (ahead tab behind) against the upstream @{u}
            await RunCommandAsync("rev-list --left-right --count HEAD...@{u}", line => resultLine = line.Trim());
            
            if (string.IsNullOrEmpty(resultLine)) return (0, 0);
            
            var parts = resultLine.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int ahead) && int.TryParse(parts[1], out int behind))
            {
                return (ahead, behind);
            }
            return (0, 0);
        }

        /// <summary>
        /// Gets the count of files modified (local uncommitted changes).
        /// </summary>
        public static async Task<int> GetModifiedFilesCountAsync()
        {
            if (!HasGitRepository()) return 0;
            int count = 0;
            await RunCommandAsync("status --porcelain", line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    count++;
                }
            });
            return count;
        }
    }
}
