using System.Diagnostics;

namespace SshTunnelManager;

// ============================================================
//  SshProcess — tương đương PlinkProcess nhưng dùng ssh native
//  API giữ nguyên: TunnelName, IsRunning, Start(), Stop(), Dispose()
// ============================================================

public class SshProcess : IDisposable
{
    public string TunnelName { get; }
    public bool IsRunning => _process is { HasExited: false };

    private Process? _process;
    private readonly string _arguments;
    private readonly string _sshPath;
    private bool _disposed;

    public SshProcess(string tunnelName, string sshPath, string arguments)
    {
        TunnelName = tunnelName;
        _sshPath   = sshPath;
        _arguments = arguments;
    }

    public bool Start()
    {
        Stop(); // kill any stale process first
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = _sshPath,
                Arguments              = _arguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            _process = new Process { StartInfo = psi };
            _process.OutputDataReceived += (_, e) => { if (e.Data != null) Logger.Tunnel($"[{TunnelName}] {e.Data}"); };
            _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) Logger.Warn($"[{TunnelName}] STDERR: {e.Data}"); };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            Logger.Success($"[{TunnelName}] ssh started (PID {_process.Id}) → {_arguments}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"[{TunnelName}] Failed to start ssh: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        if (_process == null) return;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(2000);
                Logger.Info($"[{TunnelName}] ssh stopped (PID {_process.Id})");
            }
        }
        catch (Exception ex) { Logger.Warn($"[{TunnelName}] Stop error: {ex.Message}"); }
        finally { _process.Dispose(); _process = null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

// ============================================================
//  SshWrapper — build ssh command-line args cho Linux
//  Tương đương PlinkWrapper nhưng dùng OpenSSH syntax
// ============================================================

public static class SshWrapper
{
    private const string DefaultSshPath = "/usr/bin/ssh";

    /// <summary>
    /// Build ssh args cho Machine B — Reverse Tunnel:
    ///   ssh -o StrictHostKeyChecking=no -o ServerAliveInterval=15
    ///       -o ExitOnForwardFailure=yes
    ///       -i /path/to/key.pem
    ///       -R VpsPort:localhost:RemotePort
    ///       -N user@VPS -p Port
    /// </summary>
    public static string BuildReverseArgs(VpsConfig vps, TunnelConfig tunnel)
    {
        var auth = BuildAuth(vps);
        return $"-o StrictHostKeyChecking=no " +
               $"-o ServerAliveInterval=15 " +
               $"-o ServerAliveCountMax=3 " +
               $"-o ExitOnForwardFailure=yes " +
               $"{auth} " +
               $"-R {tunnel.VpsPort}:localhost:{tunnel.RemotePort} " +
               $"-N " +
               $"-p {vps.Port} " +
               $"{vps.Username}@{vps.Host}";
    }

    /// <summary>
    /// Build ssh args cho Machine A — Forward Tunnel:
    ///   ssh -o StrictHostKeyChecking=no -o ServerAliveInterval=15
    ///       -i /path/to/key.pem
    ///       -L LocalPort:localhost:VpsPort
    ///       -N user@VPS -p Port
    /// </summary>
    public static string BuildForwardArgs(VpsConfig vps, TunnelConfig tunnel)
    {
        var auth = BuildAuth(vps);
        return $"-o StrictHostKeyChecking=no " +
               $"-o ServerAliveInterval=15 " +
               $"-o ServerAliveCountMax=3 " +
               $"-o ExitOnForwardFailure=yes " +
               $"{auth} " +
               $"-L {tunnel.LocalPort}:localhost:{tunnel.VpsPort} " +
               $"-N " +
               $"-p {vps.Port} " +
               $"{vps.Username}@{vps.Host}";
    }

    private static string BuildAuth(VpsConfig vps)
    {
        // Linux: ưu tiên dùng .pem key file
        // Nếu SshKeyFile trỏ đến .ppk thì tìm file .pem cùng tên
        if (!string.IsNullOrWhiteSpace(vps.SshKeyFile))
        {
            var keyFile = ResolvePemPath(vps.SshKeyFile);
            if (File.Exists(keyFile))
            {
                EnsureKeyPermissions(keyFile);
                return $"-i \"{keyFile}\"";
            }
            Logger.Warn($"Key file không tìm thấy: {keyFile}");
        }

        if (!string.IsNullOrWhiteSpace(vps.Password))
        {
            // ssh không hỗ trợ pass qua argument — cần sshpass
            if (IsSshpassAvailable())
                return ""; // SshProcess sẽ dùng sshpass ở tầng trên
            Logger.Warn("Password auth cần 'sshpass'. Cài: sudo apt install sshpass");
        }

        return ""; // dùng ssh-agent hoặc default key (~/.ssh/id_rsa)
    }

    /// <summary>
    /// Nếu config trỏ đến .ppk (Windows format), tự động tìm file .pem cùng thư mục.
    /// Ví dụ: default_vps.ppk → default_vps.pem
    /// </summary>
    private static string ResolvePemPath(string keyFile)
    {
        if (keyFile.EndsWith(".ppk", StringComparison.OrdinalIgnoreCase))
        {
            var pemPath = Path.ChangeExtension(keyFile, ".pem");
            if (File.Exists(pemPath)) return pemPath;

            // Thử tìm trong cùng thư mục với executable
            var appDir  = Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory();
            var pemName = Path.ChangeExtension(Path.GetFileName(keyFile), ".pem");
            return Path.Combine(appDir, pemName);
        }
        return keyFile;
    }

    /// <summary>
    /// Đảm bảo file .pem có permission 600 — ssh sẽ từ chối nếu quá mở.
    /// </summary>
    private static void EnsureKeyPermissions(string keyFile)
    {
        try
        {
            // chmod 600 keyFile
            var chmod = Process.Start(new ProcessStartInfo
            {
                FileName               = "chmod",
                Arguments              = $"600 \"{keyFile}\"",
                UseShellExecute        = false,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            });
            chmod?.WaitForExit(2000);
        }
        catch
        {
            // Không critical — ssh sẽ tự báo lỗi nếu permission sai
        }
    }

    /// <summary>Kiểm tra sshpass có cài không (cần cho password auth).</summary>
    private static bool IsSshpassAvailable()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName              = "which",
                Arguments             = "sshpass",
                UseShellExecute       = false,
                RedirectStandardOutput = true,
                CreateNoWindow        = true,
            });
            p?.WaitForExit(1000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Tìm đường dẫn ssh trên hệ thống.
    /// Thử /usr/bin/ssh trước, sau đó tìm trong PATH.
    /// </summary>
    public static string FindSshPath()
    {
        // Thử đường dẫn mặc định trước
        if (File.Exists(DefaultSshPath)) return DefaultSshPath;

        // Tìm trong PATH
        var envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in envPath.Split(Path.PathSeparator))
        {
            var full = Path.Combine(dir, "ssh");
            if (File.Exists(full)) return full;
        }

        return "ssh"; // fallback — để OS tự resolve
    }

    /// <summary>
    /// Kiểm tra ssh có sẵn và chạy được không.
    /// Tương đương ValidatePlinkPath() của PlinkWrapper.
    /// </summary>
    public static bool ValidateSshPath()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName               = FindSshPath(),
                Arguments              = "-V",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            });
            p?.WaitForExit(3000);
            return p?.ExitCode is 0 or 1; // ssh -V trả về exit code 1 nhưng vẫn in version
        }
        catch { return false; }
    }

    /// <summary>
    /// Kiểm tra file .pem có sẵn không.
    /// Tương đương kiểm tra .ppk trên Windows.
    /// </summary>
    public static bool ValidatePemKey(VpsConfig vps)
    {
        if (string.IsNullOrWhiteSpace(vps.SshKeyFile)) return false;
        var pemPath = ResolvePemPath(vps.SshKeyFile);
        return File.Exists(pemPath);
    }
}
