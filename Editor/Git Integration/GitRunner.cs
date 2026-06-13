using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
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
        /// Gets or sets whether a Git network command (fetch, pull, push) is currently executing.
        /// </summary>
        public static bool IsRunningNetworkCommand { get; set; }

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
            await RunCommandAsync("status --porcelain -u", line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    count++;
                }
            });
            return count;
        }

        /// <summary>
        /// Gets the list of raw porcelain git status lines representing modified/untracked files.
        /// </summary>
        public static async Task<System.Collections.Generic.List<string>> GetChangedFilesAsync()
        {
            var list = new System.Collections.Generic.List<string>();
            if (!HasGitRepository()) return list;

            await RunCommandAsync("status --porcelain -u", line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    list.Add(line.Trim());
                }
            });
            return list;
        }

        /// <summary>
        /// Unquotes a file path if it starts and ends with double quotes.
        /// </summary>
        public static string UnquotePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return string.Empty;
            p = p.Trim();
            if (p.StartsWith("\"") && p.EndsWith("\"") && p.Length >= 2)
            {
                p = p.Substring(1, p.Length - 2);
            }
            return p.Trim();
        }

        /// <summary>
        /// Extracts the clean file path from a git status porcelain line.
        /// </summary>
        public static string GetFilePathFromLine(string fileLine)
        {
            if (fileLine.Length < 3) return string.Empty;
            string prefix = fileLine.Substring(0, 2);
            string path = fileLine.Substring(2).Trim();
            
            path = UnquotePath(path);
            string cleanPath = path;
            if (prefix.Contains("R")) // Renamed "old -> new"
            {
                int arrow = path.IndexOf("->");
                if (arrow != -1)
                {
                    cleanPath = path.Substring(arrow + 2).Trim();
                    cleanPath = UnquotePath(cleanPath);
                }
            }
            return cleanPath;
        }

        /// <summary>
        /// Extracts all paths (e.g. old and new for a rename) from a git status porcelain line.
        /// </summary>
        public static List<string> GetPathsFromLine(string fileLine)
        {
            var paths = new List<string>();
            if (fileLine.Length < 3) return paths;
            string prefix = fileLine.Substring(0, 2);
            string path = fileLine.Substring(2).Trim();
            
            path = UnquotePath(path);
            if (prefix.Contains("R")) // Renamed "old -> new"
            {
                int arrow = path.IndexOf("->");
                if (arrow != -1)
                {
                    string oldPath = path.Substring(0, arrow).Trim();
                    string newPath = path.Substring(arrow + 2).Trim();
                    paths.Add(UnquotePath(oldPath));
                    paths.Add(UnquotePath(newPath));
                }
                else
                {
                    paths.Add(path);
                }
            }
            else
            {
                paths.Add(path);
            }
            return paths;
        }

        /// <summary>
        /// Checks if the path refers to a directory.
        /// </summary>
        public static bool IsDirectoryPath(string basePath)
        {
            string repoRoot = FindRepositoryRoot();
            string fullPath = Path.Combine(repoRoot, basePath).Replace("\\", "/");
            if (Directory.Exists(fullPath)) return true;

            if (basePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || 
                basePath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(Path.GetExtension(basePath)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all parent directory paths for a given path, up to Assets or Packages.
        /// </summary>
        public static List<string> GetParentDirectories(string path)
        {
            var parents = new List<string>();
            string dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir))
            {
                dir = dir.Replace("\\", "/");
                if (dir == "Assets" || dir == "Packages" || dir == "") break;
                parents.Add(dir);
                dir = Path.GetDirectoryName(dir);
            }
            return parents;
        }

        /// <summary>
        /// Deletes a file or directory from disk, using AssetDatabase if it's within the project.
        /// </summary>
        public static void DeleteFileOrDirectory(string path)
        {
            string repoRoot = FindRepositoryRoot();
            string fullPath = Path.Combine(repoRoot, path).Replace("\\", "/");
            
            string projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
            if (fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relativeAssetPath = fullPath.Substring(projectRoot.Length).TrimStart('/');
                if (relativeAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || 
                    relativeAssetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    if (AssetDatabase.DeleteAsset(relativeAssetPath))
                    {
                        return;
                    }
                }
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                if (File.Exists(fullPath + ".meta"))
                {
                    File.Delete(fullPath + ".meta");
                }
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                if (File.Exists(fullPath + ".meta"))
                {
                    File.Delete(fullPath + ".meta");
                }
            }
        }

        /// <summary>
        /// Filters and deduplicates git status porcelain lines.
        /// </summary>
        public static List<string> FilterAndDeduplicateChangedFiles(List<string> rawLines)
        {
            var cleanPathsSeen = new HashSet<string>();
            var filtered = new List<string>();

            foreach (var line in rawLines)
            {
                if (line.Length < 3) continue;
                string cleanPath = GetFilePathFromLine(line);
                if (string.IsNullOrEmpty(cleanPath)) continue;

                if (cleanPath.EndsWith("/") || cleanPath.EndsWith("\\")) continue;

                string baseCleanPath = cleanPath;
                if (cleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    baseCleanPath = cleanPath.Substring(0, cleanPath.Length - 5);
                }

                if (IsDirectoryPath(baseCleanPath)) continue;

                if (!cleanPathsSeen.Contains(baseCleanPath))
                {
                    cleanPathsSeen.Add(baseCleanPath);
                    
                    if (cleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        string prefix = line.Substring(0, 2);
                        filtered.Add($"{prefix} {baseCleanPath}");
                    }
                    else
                    {
                        filtered.Add(line);
                    }
                }
            }

            return filtered;
        }

        /// <summary>
        /// Discards changes for a specific clean path.
        /// </summary>
        private static async Task DiscardChangesForCleanPathAsync(string cleanPath, List<string> rawChangedFileLines)
        {
            var rawLinesToDiscard = new List<string>();
            foreach (var rawLine in rawChangedFileLines)
            {
                string rawCleanPath = GetFilePathFromLine(rawLine);
                string baseCleanPath = rawCleanPath;
                if (rawCleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    baseCleanPath = rawCleanPath.Substring(0, rawCleanPath.Length - 5);
                }

                if (baseCleanPath == cleanPath)
                {
                    rawLinesToDiscard.Add(rawLine);
                }
            }

            var parents = GetParentDirectories(cleanPath);
            foreach (var parent in parents)
            {
                string parentMeta = parent + ".meta";
                
                bool isParentMetaChanged = false;
                string parentMetaRawLine = null;
                foreach (var rawLine in rawChangedFileLines)
                {
                    string rawCleanPath = GetFilePathFromLine(rawLine);
                    if (rawCleanPath == parentMeta)
                    {
                        isParentMetaChanged = true;
                        parentMetaRawLine = rawLine;
                        break;
                    }
                }

                if (isParentMetaChanged)
                {
                    bool hasOtherChanges = false;
                    foreach (var rawLine in rawChangedFileLines)
                    {
                        string rawCleanPath = GetFilePathFromLine(rawLine);
                        
                        string baseClean = rawCleanPath;
                        if (rawCleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        {
                            baseClean = rawCleanPath.Substring(0, rawCleanPath.Length - 5);
                        }
                        if (baseClean == cleanPath) continue;

                        if (baseClean == parent || baseClean.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            string ext = Path.GetExtension(baseClean);
                            if (string.IsNullOrEmpty(ext)) continue;
                            
                            hasOtherChanges = true;
                            break;
                        }
                    }

                    if (!hasOtherChanges && parentMetaRawLine != null)
                    {
                        rawLinesToDiscard.Add(parentMetaRawLine);
                    }
                }
            }

            foreach (var fileLine in rawLinesToDiscard)
            {
                string prefix = fileLine.Substring(0, 2);
                var paths = GetPathsFromLine(fileLine);
                foreach (var path in paths)
                {
                    if (prefix.Contains("?"))
                    {
                        DeleteFileOrDirectory(path);
                    }
                    else
                    {
                        await RunCommandAsync($"reset HEAD -- \"{path}\"", null, null);
                        int exitCode = await RunCommandAsync($"checkout HEAD -- \"{path}\"", null, null);
                        if (exitCode != 0)
                        {
                            DeleteFileOrDirectory(path);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Discards changes for a collection of selected clean paths.
        /// </summary>
        public static async Task DiscardChangesAsync(IEnumerable<string> selectedCleanPaths, List<string> rawChangedFileLines)
        {
            foreach (var cleanPath in selectedCleanPaths)
            {
                await DiscardChangesForCleanPathAsync(cleanPath, rawChangedFileLines);
            }
        }

        /// <summary>
        /// Stages and commits changes for selected paths with the given message.
        /// </summary>
        public static async Task<bool> CommitChangesAsync(
            IEnumerable<string> selectedCleanPaths, 
            List<string> rawChangedFileLines, 
            string commitMessage, 
            Action<string> onOutput, 
            Action<string> onError)
        {
            onOutput?.Invoke("> git reset");
            await RunCommandAsync("reset", onOutput, onError);

            var pathsToStage = new List<string>();
            var selectedSet = new HashSet<string>(selectedCleanPaths);
            foreach (var rawLine in rawChangedFileLines)
            {
                string rawCleanPath = GetFilePathFromLine(rawLine);
                string baseCleanPath = rawCleanPath;
                if (rawCleanPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    baseCleanPath = rawCleanPath.Substring(0, rawCleanPath.Length - 5);
                }

                if (selectedSet.Contains(baseCleanPath))
                {
                    pathsToStage.AddRange(GetPathsFromLine(rawLine));
                }
            }

            var parentMetasToStage = new HashSet<string>();
            foreach (var path in pathsToStage)
            {
                var parents = GetParentDirectories(path);
                foreach (var parent in parents)
                {
                    string parentMeta = parent + ".meta";
                    foreach (var rawLine in rawChangedFileLines)
                    {
                        string rawCleanPath = GetFilePathFromLine(rawLine);
                        if (rawCleanPath == parentMeta)
                        {
                            parentMetasToStage.Add(parentMeta);
                            break;
                        }
                    }
                }
            }
            pathsToStage.AddRange(parentMetasToStage);

            if (pathsToStage.Count == 0)
            {
                onError?.Invoke("Error: No files selected to commit.");
                return false;
            }

            string addArgs = "add -- " + string.Join(" ", pathsToStage.Select(p => $"\"{p}\""));
            onOutput?.Invoke($"> git {addArgs}");
            int addExit = await RunCommandAsync(addArgs, onOutput, onError);
            
            if (addExit == 0)
            {
                string escapedMessage = commitMessage.Replace("\"", "\\\"");
                onOutput?.Invoke($"> git commit -m \"{escapedMessage}\"");
                int commitExit = await RunCommandAsync($"commit -m \"{escapedMessage}\"", onOutput, onError);
                
                if (commitExit == 0)
                {
                    onOutput?.Invoke("Commit successful.");
                    return true;
                }
                else
                {
                    onError?.Invoke($"Commit failed with exit code {commitExit}");
                    return false;
                }
            }
            else
            {
                onError?.Invoke($"Stage failed with exit code {addExit}");
                return false;
            }
        }
    }
}
