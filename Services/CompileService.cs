using System.Diagnostics;
using System.Text;

namespace CIDE.Services;

public static class CompileService
{
    private static Process? t_currentProcess;

    public static void KillCurrentProcess()
    {
        if (t_currentProcess != null && !t_currentProcess.HasExited)
        {
            try
            { t_currentProcess.Kill(true); }
            catch { }
        }
    }

    public static async Task RunCompilationAsync(string projectType, string activeFilePath, string workspacePath, Action<string> onOutput)
    {
        onOutput($"[{DateTime.Now:HH:mm:ss}] Запуск: {projectType}\n");

        if (projectType == "Makefile")
        {
            var makePath = Path.Combine(ToolchainService.MinGWDir, "bin", "mingw32-make.exe");
            if (!File.Exists(makePath))
            {
                makePath = "mingw32-make.exe";
            }

            onOutput($"$ {makePath}\n");
            var result = await ExecuteProcessAsync(makePath, "", workspacePath, onOutput);
            if (result == 0)
            {
                onOutput($"\n[{DateTime.Now:HH:mm:ss}] Сборка завершена.\n");
            }
        }
        else if (projectType is "C Project" or "C++ Project" or "C File" or "C++ File")
        {
            var isCpp = projectType.Contains("C++");
            var compiler = isCpp ? "g++.exe" : "gcc.exe";
            var compilerPath = Path.Combine(ToolchainService.MinGWDir, "bin", compiler);
            if (!File.Exists(compilerPath))
            {
                compilerPath = compiler;
            }

            var targetFiles = "";
            var outputExe = OperatingSystem.IsWindows() ? "app.exe" : "app";
            var workingDir = workspacePath;

            if (projectType.Contains("Project") && !string.IsNullOrEmpty(workspacePath) && Directory.Exists(workspacePath))
            {
                var ext = isCpp ? "*.cpp" : "*.c";
                var files = Directory.GetFiles(workspacePath, ext, SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    onOutput($"Ошибка: Исходные файлы не найдены в {workspacePath}\n");
                    return;
                }
                targetFiles = string.Join(" ", files.Select(f => $"\"{f}\""));
                var binDir = Path.Combine(workspacePath, "bin");
                if (!Directory.Exists(binDir))
                {
                    _ = Directory.CreateDirectory(binDir);
                }

                outputExe = Path.Combine(binDir, OperatingSystem.IsWindows() ? "app.exe" : "app");
            }
            else
            {
                if (string.IsNullOrEmpty(activeFilePath) || (!activeFilePath.EndsWith(".c", StringComparison.OrdinalIgnoreCase) && !activeFilePath.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase)))
                {
                    onOutput("Ошибка: Откройте файл .c или .cpp для компиляции.\n");
                    return;
                }
                targetFiles = $"\"{activeFilePath}\"";
                workingDir = Path.GetDirectoryName(activeFilePath) ?? workspacePath;
                outputExe = Path.Combine(workingDir, Path.GetFileNameWithoutExtension(activeFilePath) + (OperatingSystem.IsWindows() ? ".exe" : ""));
            }

            var args = $"-Wall -g {targetFiles} -o \"{outputExe}\"";
            onOutput($"$ {compiler} {args}\n");

            var result = await ExecuteProcessAsync(compilerPath, args, workingDir, onOutput);
            if (result == 0 && File.Exists(outputExe))
            {
                onOutput($"\n[{DateTime.Now:HH:mm:ss}] Успешно собрано. Запуск {Path.GetFileName(outputExe)}...\n");
                _ = await ExecuteProcessAsync(outputExe, "", workingDir, onOutput);
            }
        }
        else
        {
            onOutput($"Тип '{projectType}' не поддерживается для авто-сборки. Используйте терминал.\n");
        }
    }

    private static async Task<int> ExecuteProcessAsync(string fileName, string args, string workingDir, Action<string> onOutput)
    {
        try
        {
            t_currentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            t_currentProcess.OutputDataReceived += (s, e) => { if (e.Data != null) { onOutput(e.Data + "\n"); } };
            t_currentProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) { onOutput(e.Data + "\n"); } };

            _ = t_currentProcess.Start();
            t_currentProcess.BeginOutputReadLine();
            t_currentProcess.BeginErrorReadLine();

            await t_currentProcess.WaitForExitAsync();
            var code = t_currentProcess.ExitCode;
            onOutput($"\nПроцесс завершился с кодом {code}.\n");
            t_currentProcess.Dispose();
            t_currentProcess = null;
            return code;
        }
        catch (Exception ex)
        {
            onOutput($"\nОшибка запуска процесса: {ex.Message}\n");
            t_currentProcess?.Dispose();
            t_currentProcess = null;
            return -1;
        }
    }
}
