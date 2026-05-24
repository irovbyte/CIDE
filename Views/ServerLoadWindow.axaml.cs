using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CIDE.Services;
using CIDE.Models;
using Renci.SshNet;
using System.Text.RegularExpressions;

namespace CIDE.Views;

#pragma warning disable SYSLIB1045 
#pragma warning disable CA1861 

public partial class ServerLoadWindow : Window
{
    private bool _isRunning = true;
    private long _prevIdleTime;
    private long _prevTotalTime;

    public ServerLoadWindow()
    {
        InitializeComponent();
        Closed += (s, e) => _isRunning = false;
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null)
        {
            closeBtn.Click += (s, e) => Close();
        }
        var titleBar = this.FindControl<Avalonia.Controls.Border>("TitleBarBorder");
        if (titleBar != null)
        {
            titleBar.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };
        }
        Task.Run(PollStatsLoop);
    }

    private async Task PollStatsLoop()
    {
        while (_isRunning)
        {
            try
            {
                if (WorkspaceService.CurrentProvider is SshFileSystemProvider sshProvider)
                {
                    var client = sshProvider.GetSshClient();
                    if (client != null && client.IsConnected)
                    {
                        var cmd = client.CreateCommand("cat /proc/stat; echo '---'; cat /proc/meminfo");
                        var result = cmd.Execute();
                        ParseAndApplyStats(result);
                    }
                    else
                    {
                        SetStatus("Нет подключения");
                    }
                }
                else
                {
                    SetStatus("Только для SSH");
                }
            }
            catch (Exception)
            {
                SetStatus("Ошибка");
            }
            await Task.Delay(1000);
        }
    }

    private void SetStatus(string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CpuText.Text = msg;
            RamText.Text = msg;
            CpuProgress.Value = 0;
            RamProgress.Value = 0;
        });
    }

    private void ParseAndApplyStats(string output)
    {
        try
        {
            var parts = output.Split(["---"], StringSplitOptions.None);
            if (parts.Length < 2)
                return;

            var stat = parts[0];
            var meminfo = parts[1];
            var cpuLine = stat.Split('\n')[0];
            var cpuMatch = Regex.Match(cpuLine, @"cpu\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)");
            double cpuUsage = 0;
            if (cpuMatch.Success)
            {
                long user = long.Parse(cpuMatch.Groups[1].Value);
                long nice = long.Parse(cpuMatch.Groups[2].Value);
                long system = long.Parse(cpuMatch.Groups[3].Value);
                long idle = long.Parse(cpuMatch.Groups[4].Value);

                long idleTime = idle;
                long totalTime = user + nice + system + idle;

                if (_prevTotalTime > 0)
                {
                    var totalDelta = totalTime - _prevTotalTime;
                    var idleDelta = idleTime - _prevIdleTime;
                    cpuUsage = (double)(totalDelta - idleDelta) / totalDelta * 100.0;
                }
                _prevTotalTime = totalTime;
                _prevIdleTime = idleTime;
            }
            var memTotalMatch = Regex.Match(meminfo, @"MemTotal:\s+(\d+)");
            var memFreeMatch = Regex.Match(meminfo, @"MemFree:\s+(\d+)");
            var buffersMatch = Regex.Match(meminfo, @"Buffers:\s+(\d+)");
            var cachedMatch = Regex.Match(meminfo, @"Cached:\s+(\d+)");

            double memUsage = 0;
            double memTotalMb = 0;
            double memUsedMb = 0;

            if (memTotalMatch.Success && memFreeMatch.Success)
            {
                long total = long.Parse(memTotalMatch.Groups[1].Value);
                long free = long.Parse(memFreeMatch.Groups[1].Value);
                long buffers = buffersMatch.Success ? long.Parse(buffersMatch.Groups[1].Value) : 0;
                long cached = cachedMatch.Success ? long.Parse(cachedMatch.Groups[1].Value) : 0;

                long used = total - free - buffers - cached;
                memUsage = (double)used / total * 100.0;
                memTotalMb = total / 1024.0;
                memUsedMb = used / 1024.0;
            }

            Dispatcher.UIThread.Post(() =>
            {
                CpuText.Text = $"{cpuUsage:F1} %";
                CpuProgress.Value = double.IsNaN(cpuUsage) ? 0 : Math.Min(100, Math.Max(0, cpuUsage));

                RamText.Text = $"{memUsedMb:F0} MB / {memTotalMb:F0} MB ({memUsage:F1}%)";
                RamProgress.Value = double.IsNaN(memUsage) ? 0 : Math.Min(100, Math.Max(0, memUsage));
            });
        }
        catch { }
    }
}
