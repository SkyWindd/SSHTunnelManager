using System.Net.Sockets;

namespace SshTunnelManager;

/// <summary>
/// Tương đương TunnelMonitor.cs (Windows) nhưng dùng SshProcess thay PlinkProcess.
/// Quản lý các SshProcess, monitor liveness, auto-reconnect khi tunnel chết.
/// </summary>
public class LinuxTunnelMonitor : IDisposable
{
    public bool IsRunning { get; private set; }

    private readonly AppConfig _cfg;
    private readonly string _sshPath;
    private readonly List<LinuxManagedTunnel> _tunnels = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private bool _disposed;

    public LinuxTunnelMonitor(AppConfig cfg)
    {
        _cfg     = cfg;
        _sshPath = SshWrapper.FindSshPath();
    }

    // ---- Public API ----

    public void StartAll()
    {
        if (IsRunning) return;

        Logger.Info("LinuxTunnelMonitor: starting all tunnels...");
        _cts = new CancellationTokenSource();

        foreach (var t in _cfg.Tunnels)
        {
            var args = _cfg.Role == MachineRole.MachineB
                ? SshWrapper.BuildReverseArgs(_cfg.Vps, t)
                : SshWrapper.BuildForwardArgs(_cfg.Vps, t);

            var proc = new SshProcess(t.Name, _sshPath, args);
            _tunnels.Add(new LinuxManagedTunnel(t, proc));
            proc.Start();
        }

        IsRunning    = true;
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
    }

    public void StopAll()
    {
        if (!IsRunning) return;
        Logger.Info("LinuxTunnelMonitor: stopping all tunnels...");
        _cts?.Cancel();
        _monitorTask?.Wait(5000);

        foreach (var mt in _tunnels) mt.Process.Stop();
        _tunnels.Clear();
        IsRunning = false;
    }

    public void PrintStatus()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  Role: {_cfg.Role}   AutoReconnect: {_cfg.AutoReconnect}");
        Console.WriteLine($"  VPS : {_cfg.Vps.Username}@{_cfg.Vps.Host}:{_cfg.Vps.Port}");
        Console.WriteLine($"  SSH : {_sshPath}");
        Console.WriteLine();
        Console.WriteLine($"  {"Tunnel",-12} {"Type",-8} {"Ports",-28} {"Status",-10} {"Reconnects"}");
        Console.WriteLine("  " + new string('─', 72));
        Console.ResetColor();

        foreach (var mt in _tunnels)
        {
            var portInfo = _cfg.Role == MachineRole.MachineB
                ? $"VPS:{mt.Config.VpsPort} ← local:{mt.Config.RemotePort}"
                : $"local:{mt.Config.LocalPort} → VPS:{mt.Config.VpsPort}";

            var status = mt.Process.IsRunning ? "●  UP" : "○  DOWN";
            var color  = mt.Process.IsRunning ? ConsoleColor.Green : ConsoleColor.Red;

            Console.Write($"  {mt.Config.Name,-12} {mt.Config.Type,-8} {portInfo,-28} ");
            Console.ForegroundColor = color;
            Console.Write($"{status,-10}");
            Console.ResetColor();
            Console.WriteLine($" {mt.ReconnectCount}x");
        }
        Console.WriteLine();
    }

    // ---- Internal heartbeat loop ----

    private async Task MonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_cfg.HeartbeatIntervalSec * 1000, ct).ContinueWith(_ => { });
            if (ct.IsCancellationRequested) break;

            foreach (var mt in _tunnels)
            {
                if (mt.Process.IsRunning)
                {
                    // Port-level liveness check cho MachineA (forward tunnel)
                    if (_cfg.Role == MachineRole.MachineA && !IsPortOpen("127.0.0.1", mt.Config.LocalPort))
                    {
                        Logger.Warn($"[{mt.Config.Name}] Forward port {mt.Config.LocalPort} not responding — treating as dead.");
                        mt.Process.Stop();
                    }
                    else continue;
                }

                if (!_cfg.AutoReconnect) continue;

                Logger.Warn($"[{mt.Config.Name}] Tunnel is down. Reconnecting in {_cfg.ReconnectDelaySec}s...");
                await Task.Delay(_cfg.ReconnectDelaySec * 1000, ct).ContinueWith(_ => { });
                if (ct.IsCancellationRequested) break;

                mt.ReconnectCount++;
                mt.Process.Start();
            }
        }
    }

    private static bool IsPortOpen(string host, int port, int timeoutMs = 1000)
    {
        try
        {
            using var tcp = new TcpClient();
            var result  = tcp.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(timeoutMs);
            if (success) tcp.EndConnect(result);
            return success;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAll();
        _cts?.Dispose();
    }

    private class LinuxManagedTunnel(TunnelConfig config, SshProcess process)
    {
        public TunnelConfig Config { get; } = config;
        public SshProcess   Process { get; } = process;
        public int ReconnectCount { get; set; }
    }
}
