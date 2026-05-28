using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;

namespace CIDE.Services;

public static class CompileService
{
    private static Process? t_currentProcess;
    private static SshCommand? t_currentSshCommand;

    public static void KillCurrentProcess()
    {
        if (t_currentProcess != null && !t_currentProcess.HasExited)
        {
            try
            { t_currentProcess.Kill(true); }
            catch { }
        }
        if (t_currentSshCommand != null)
        {
            try
            { t_currentSshCommand.CancelAsync(); }
            catch { }
        }
    }

    public static async Task RunCompilationAsync(BuildProfile? profile, Action<string> onOutput, string projectName = "")
    {
        if (profile == null)
        {
            onOutput("Ошибка: Профиль сборки не выбран.\n");
            return;
        }

        onOutput($"[{DateTime.Now:HH:mm:ss}] Запуск: {profile.DisplayName}\n");

        if (profile.Type is BuildProfileType.Makefile or BuildProfileType.SingleFileC or BuildProfileType.SingleFileCpp)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await ExecuteSshCommandAsync(profile, onOutput, projectName);
                return;
            }
            var compiler = profile.Type == BuildProfileType.SingleFileCpp ? "g++" : (profile.Type == BuildProfileType.Makefile ? "make" : "gcc");
            var args = profile.Type == BuildProfileType.Makefile
                ? $"-f \"{profile.TargetPath}\""
                : $"-Wall -g \"{profile.TargetPath}\" -o \"{Path.Combine(profile.WorkingDirectory, Path.GetFileNameWithoutExtension(profile.TargetPath))}\"";
            onOutput($"$ {compiler} {args}\n");
            _ = await ExecuteProcessAsync(compiler, args, profile.WorkingDirectory, onOutput);
            return;
        }
        if (profile.Type == BuildProfileType.DotNetProject)
        {
            var args = $"run --project \"{profile.TargetPath}\"";
            onOutput($"$ dotnet {args}\n");
            _ = await ExecuteProcessAsync("dotnet", args, profile.WorkingDirectory, onOutput);
        }
        else if (profile.Type is BuildProfileType.Python)
        {
            var args = $"\"{profile.TargetPath}\"";
            onOutput($"$ python {args}\n");
            _ = await ExecuteProcessAsync("python", args, profile.WorkingDirectory, onOutput);
        }
        else if (profile.Type is BuildProfileType.Bash)
        {
            var args = $"\"{profile.TargetPath}\"";
            onOutput($"$ bash {args}\n");
            _ = await ExecuteProcessAsync("bash", args, profile.WorkingDirectory, onOutput);
        }
        else
        {
            onOutput($"Тип '{profile.Type}' не поддерживается для авто-сборки. Используйте терминал.\n");
        }
    }

    private static async Task ExecuteSshCommandAsync(BuildProfile profile, Action<string> onOutput, string projectName)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            projectName = "default";
        }

        var remoteDir = $"/home/cide/shadow/{projectName}";
        var fileName = Path.GetFileName(profile.TargetPath);
        string command;
        if (profile.Type == BuildProfileType.Makefile)
        {
            command = $"cd {remoteDir} && make -f {fileName}";
        }
        else
        {
            var compiler = profile.Type == BuildProfileType.SingleFileCpp ? "g++" : "gcc";
            var outName = Path.GetFileNameWithoutExtension(fileName);
            command = $"cd {remoteDir} && {compiler} -Wall -g {fileName} -o {outName} && ./{outName}";
        }

        onOutput($"[CIDEL Engine] $ {command}\n");

        await Task.Run(() =>
        {
            try
            {
                var conn = CidelEngineService.Instance.LocalEngineConnection;
                var method = new PasswordAuthenticationMethod(conn.Username, conn.Password);
                var connectionInfo = new ConnectionInfo(conn.Host, conn.Port, conn.Username, method);
                using var client = new SshClient(connectionInfo);
                client.Connect();

                t_currentSshCommand = client.CreateCommand(command);
                var asyncResult = t_currentSshCommand.BeginExecute();

                using var reader = new StreamReader(t_currentSshCommand.OutputStream);
                using var errReader = new StreamReader(t_currentSshCommand.ExtendedOutputStream);
                while (!asyncResult.IsCompleted)
                {
                    while (!reader.EndOfStream)
                    {
                        onOutput(reader.ReadLine() + "\n");
                    }

                    while (!errReader.EndOfStream)
                    {
                        onOutput(errReader.ReadLine() + "\n");
                    }

                    Thread.Sleep(50);
                }
                onOutput(reader.ReadToEnd());
                onOutput(errReader.ReadToEnd());

                _ = t_currentSshCommand.EndExecute(asyncResult);
                onOutput($"\nПроцесс завершился с кодом {t_currentSshCommand.ExitStatus}.\n");
                client.Disconnect();
            }
            catch (Exception ex)
            {
                onOutput($"\n❌ Ошибка SSH: {ex.Message}\nПроверьте, запущен ли CIDEL Engine.\n");
            }
            finally
            {
                t_currentSshCommand = null;
            }
        });
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
            onOutput($"\n❌ Ошибка запуска процесса: {ex.Message}\n");
            t_currentProcess?.Dispose();
            t_currentProcess = null;
            return -1;
        }
    }
}
