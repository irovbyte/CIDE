namespace CIDE.Models;

public class ServerLoadStats
{
    public double CpuLoadPercentage { get; set; }
    public double RamUsedMegabytes { get; set; }
    public double RamTotalMegabytes { get; set; }
    public double SwapUsedMegabytes { get; set; }
    public double SwapTotalMegabytes { get; set; }
}
