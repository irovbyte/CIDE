using System.Diagnostics;
using System.Text;

namespace CIDE.Services;

public static class CompileService
{
    public static async Task RunCompilationAsync(string profile, string activeFilePath, string workspacePath, Action<string> onOutput)
    {
        onOutput($"[{DateTime.Now:HH:mm:ss}] Запуск профиля: {profile}\n");

        if (profile == "Один файл (C/C++)")
        {
            if (string.IsNullOrEmpty(activeFilePath)
                || (!activeFilePath.EndsWith(".c", StringComparison.OrdinalIgnoreCase)
                    && !activeFilePath.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase)))
            {
                onOutput("Ошибка: Выберите файл .c или .cpp для компиляции.\n");
                return;
            }

            var compiler = activeFilePath.EndsWith(".c", StringComparison.OrdinalIgnoreCase) ? "gcc.exe" : "g++.exe";
            var compilerPath = Path.Combine(ToolchainService.MinGWDir, "bin", compiler);

            if (!File.Exists(compilerPath))
            {
                onOutput($"Ошибка: Компилятор {compiler} не найден. Проверьте установку.\n");
                compilerPath = compiler;
            }

            var outputExe = Path.Combine(Path.GetDirectoryName(activeFilePath) ?? workspacePath, Path.GetFileNameWithoutExtension(activeFilePath) + ".exe");

            var args = $"-Wall -g \"{activeFilePath}\" -o \"{outputExe}\"";
            onOutput($"$ {compilerPath} {args}\n");

            var result = await ExecuteProcessAsync(compilerPath, args, Path.GetDirectoryName(activeFilePath) ?? workspacePath, onOutput);
            if (result == 0 && File.Exists(outputExe))
            {
                onOutput($"\n[{DateTime.Now:HH:mm:ss}] Успешно собрано. Запуск {outputExe}...\n");
                _ = await ExecuteProcessAsync(outputExe, "", Path.GetDirectoryName(activeFilePath) ?? workspacePath, onOutput);
            }
        }
        else if (profile == "Makefile (C/C++)")
        {
            var makePath = Path.Combine(ToolchainService.MinGWDir, "bin", "mingw32-make.exe");
            onOutput($"$ {makePath}\n");
            _ = await ExecuteProcessAsync(makePath, "", workspacePath, onOutput);
        }
        else if (profile == "Проект C#")
        {
            onOutput("$ dotnet run\n");
            _ = await ExecuteProcessAsync("dotnet", "run", workspacePath, onOutput);
        }
        else
        {
            onOutput("Профиль не поддерживается.\n");
        }
    }

    private static async Task<int> ExecuteProcessAsync(string fileName, string args, string workingDir, Action<string> onOutput)
    {
        try
        {
            using var process = new Process
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

            process.OutputDataReceived += (s, e) => { if (e.Data != null) { onOutput(e.Data + "\n"); } };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) { onOutput(e.Data + "\n"); } };

            _ = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            onOutput($"\nПроцесс завершился с кодом {process.ExitCode}.\n");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            onOutput($"\nОшибка запуска процесса: {ex.Message}\n");
            return -1;
        }
    }
}
