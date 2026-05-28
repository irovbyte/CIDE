using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CIDE.Models;

namespace CIDE.Services;

public static class ProjectContextService
{
    public static Task<List<BuildProfile>> ScanWorkspaceAsync(string? workspacePath, string? activeFilePath)
    {
        return Task.Run(() =>
        {
            var profiles = new List<BuildProfile>();
            if (!string.IsNullOrWhiteSpace(activeFilePath))
            {
                var ext = Path.GetExtension(activeFilePath).ToLowerInvariant();
                if (ext == ".c")
                {
                    profiles.Add(new BuildProfile
                    {
                        DisplayName = "Компилировать текущий файл (C)",
                        Type = BuildProfileType.SingleFileC,
                        TargetPath = activeFilePath,
                        WorkingDirectory = Path.GetDirectoryName(activeFilePath) ?? ""
                    });
                }
                else if (ext is ".cpp" or ".cxx" or ".cc")
                {
                    profiles.Add(new BuildProfile
                    {
                        DisplayName = "Компилировать текущий файл (C++)",
                        Type = BuildProfileType.SingleFileCpp,
                        TargetPath = activeFilePath,
                        WorkingDirectory = Path.GetDirectoryName(activeFilePath) ?? ""
                    });
                }
                else if (ext == ".py")
                {
                    profiles.Add(new BuildProfile
                    {
                        DisplayName = "Запустить Python скрипт",
                        Type = BuildProfileType.Python,
                        TargetPath = activeFilePath,
                        WorkingDirectory = Path.GetDirectoryName(activeFilePath) ?? ""
                    });
                }
                else if (ext is ".sh" or ".bash")
                {
                    profiles.Add(new BuildProfile
                    {
                        DisplayName = "Запустить Bash скрипт",
                        Type = BuildProfileType.Bash,
                        TargetPath = activeFilePath,
                        WorkingDirectory = Path.GetDirectoryName(activeFilePath) ?? ""
                    });
                }
            }
            if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            {
                return profiles;
            }
            try
            {
                var dirInfo = new DirectoryInfo(workspacePath);
                SearchForProjects(dirInfo, workspacePath, profiles, 0, 3);
            }
            catch (Exception)
            {
            }

            return profiles;
        });
    }

    private static void SearchForProjects(DirectoryInfo currentDir, string workspacePath, List<BuildProfile> profiles, int currentDepth, int maxDepth)
    {
        if (currentDepth > maxDepth)
        {
            return;
        }

        if (currentDir.Name.StartsWith('.') || currentDir.Name == "node_modules" || currentDir.Name == "bin" || currentDir.Name == "obj")
        {
            return;
        }

        try
        {
            var makefiles = currentDir.GetFiles("Makefile").Concat(currentDir.GetFiles("makefile"));
            foreach (var mf in makefiles)
            {
                var relPath = Path.GetRelativePath(workspacePath, mf.FullName);
                profiles.Add(new BuildProfile
                {
                    DisplayName = $"Make: {relPath}",
                    Type = BuildProfileType.Makefile,
                    TargetPath = mf.FullName,
                    WorkingDirectory = currentDir.FullName
                });
            }
            var csprojFiles = currentDir.GetFiles("*.csproj");
            foreach (var proj in csprojFiles)
            {
                profiles.Add(new BuildProfile
                {
                    DisplayName = $"Run: {proj.Name}",
                    Type = BuildProfileType.DotNetProject,
                    TargetPath = proj.FullName,
                    WorkingDirectory = currentDir.FullName
                });
            }
            foreach (var subDir in currentDir.GetDirectories())
            {
                SearchForProjects(subDir, workspacePath, profiles, currentDepth + 1, maxDepth);
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
