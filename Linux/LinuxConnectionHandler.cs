using System.Diagnostics;

namespace SshTunnelManager;

/// <summary>
/// Tương đương ConnectionHandler.cs (Windows) nhưng dành cho Linux.
/// Bỏ LaunchPutty / LaunchRdp — thay bằng hướng dẫn dùng ssh command trực tiếp.
/// Giữ nguyên: AddCustomTunnel, RemoveTunnel, PrintTunnelUsageGuide.
/// </summary>
public static class LinuxConnectionHandler
{
    // ---- Launch helpers ----

    /// <summary>
    /// Linux không dùng PuTTY — in ra ssh command để user tự chạy trong terminal.
    /// </summary>
    public static void PrintSshCommand(AppConfig cfg)
    {
        var ssh = cfg.Tunnels.FirstOrDefault(t => t.Type == ConnectionType.SSH);
        if (ssh == null) { Logger.Warn("No SSH tunnel configured."); return; }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n  Chạy lệnh sau trong terminal để SSH vào Máy B:");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\n    ssh <username_may_b>@127.0.0.1 -p {ssh.LocalPort}");
        Console.ResetColor();
        Console.WriteLine("\n  Thay <username_may_b> bằng username thực của Máy B.");

        // Hỏi có muốn tự động mở terminal và chạy không
        Console.Write("\n  Tự động mở terminal và chạy lệnh? (y/N): ");
        if (Console.ReadLine()?.Trim().ToLower() != "y") return;

        Console.Write("  Nhập username Máy B: ");
        var username = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(username)) { Logger.Warn("Username không được để trống."); return; }

        LaunchSshInTerminal(username, ssh.LocalPort);
    }

    /// <summary>
    /// Linux không dùng mstsc — in ra hướng dẫn dùng VNC hoặc Remmina.
    /// </summary>
    public static void PrintRdpGuide(AppConfig cfg)
    {
        var rdp = cfg.Tunnels.FirstOrDefault(t => t.Type == ConnectionType.RDP);
        if (rdp == null) { Logger.Warn("No RDP tunnel configured."); return; }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n  Kết nối Remote Desktop từ Linux:");
        Console.ResetColor();
        Console.WriteLine($"\n  Cách 1 — Remmina (GUI, phổ biến nhất):");
        Console.WriteLine($"    1. Cài: sudo apt install remmina");
        Console.WriteLine($"    2. Mở Remmina → New connection");
        Console.WriteLine($"    3. Protocol: RDP");
        Console.WriteLine($"    4. Server: 127.0.0.1:{rdp.LocalPort}");
        Console.WriteLine($"    5. Nhập username/password của Máy B → Connect");
        Console.WriteLine($"\n  Cách 2 — xfreerdp (command line):");
        Console.WriteLine($"    sudo apt install freerdp2-x11");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"    xfreerdp /v:127.0.0.1:{rdp.LocalPort} /u:<username_may_b>");
        Console.ResetColor();
        Console.WriteLine($"\n  Lưu ý: Máy B phải bật Remote Desktop (Windows Pro/Enterprise).");
        Console.WriteLine($"         Nếu Máy B dùng Windows Home → dùng VNC thay thế.");
    }

    /// <summary>
    /// Mở terminal và chạy ssh command tự động.
    /// Thử lần lượt các terminal phổ biến trên Ubuntu.
    /// </summary>
    private static void LaunchSshInTerminal(string username, int port)
    {
        var sshCmd = $"ssh {username}@127.0.0.1 -p {port}";

        // Danh sách terminal phổ biến trên Ubuntu, thử theo thứ tự
        var terminals = new[]
        {
            ("gnome-terminal", $"-- bash -c \"{sshCmd}; exec bash\""),
            ("xterm",          $"-e \"{sshCmd}\""),
            ("konsole",        $"-e bash -c \"{sshCmd}; exec bash\""),
            ("xfce4-terminal", $"-e \"{sshCmd}\""),
            ("lxterminal",     $"-e \"{sshCmd}\""),
        };

        foreach (var (terminal, args) in terminals)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName        = terminal,
                    Arguments       = args,
                    UseShellExecute = false,
                });
                if (p != null)
                {
                    Logger.Success($"Đã mở {terminal} với lệnh: {sshCmd}");
                    return;
                }
            }
            catch { /* terminal này không có, thử cái tiếp theo */ }
        }

        Logger.Warn("Không tìm thấy terminal. Hãy tự mở terminal và chạy lệnh trên.");
    }

    // ---- Custom port management (giữ nguyên logic từ Windows) ----

    public static void AddCustomTunnel(AppConfig cfg)
    {
        Console.WriteLine("\n--- Add Custom Port Forward ---");
        Console.Write("  Tunnel name           : ");
        var name = Console.ReadLine()?.Trim() ?? "custom";

        Console.Write("  Local port (MachineA) : ");
        if (!int.TryParse(Console.ReadLine(), out var local)) { Logger.Warn("Invalid port."); return; }

        Console.Write("  VPS relay port        : ");
        if (!int.TryParse(Console.ReadLine(), out var vps)) { Logger.Warn("Invalid port."); return; }

        Console.Write("  Remote port (MachineB): ");
        if (!int.TryParse(Console.ReadLine(), out var remote)) { Logger.Warn("Invalid port."); return; }

        cfg.Tunnels.Add(new TunnelConfig
        {
            Name       = name,
            Type       = ConnectionType.Custom,
            LocalPort  = local,
            VpsPort    = vps,
            RemotePort = remote,
        });
        ConfigManager.Save(cfg);
        Logger.Success($"Custom tunnel '{name}' added. Restart tunnels to apply.");
    }

    public static void RemoveTunnel(AppConfig cfg)
    {
        if (cfg.Tunnels.Count == 0) { Console.WriteLine("No tunnels configured."); return; }

        Console.WriteLine("\n--- Remove Tunnel ---");
        for (int i = 0; i < cfg.Tunnels.Count; i++)
            Console.WriteLine($"  [{i + 1}] {cfg.Tunnels[i].Name}");

        Console.Write("Select number to remove (0 = cancel): ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > cfg.Tunnels.Count) return;

        var removed = cfg.Tunnels[idx - 1];
        cfg.Tunnels.RemoveAt(idx - 1);
        ConfigManager.Save(cfg);
        Logger.Success($"Tunnel '{removed.Name}' removed.");
    }

    // ---- Usage guide ----

    public static void PrintTunnelUsageGuide(AppConfig cfg)
    {
        Console.Clear();
        var sep = "  " + new string('═', 65);

        // ── Header ──────────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine(sep);
        Console.WriteLine("  ║          HƯỚNG DẪN SỬ DỤNG SSH TUNNEL MANAGER              ║");
        Console.WriteLine(sep);
        Console.ResetColor();

        // ── Session info ─────────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  📋 Session ID : \"{cfg.SessionId}\"");
        Console.WriteLine($"  🖥  Vai trò    : {(cfg.Role == MachineRole.MachineA ? "Máy A — CLIENT (kết nối vào máy bạn bè)" : "Máy B — SERVER (máy đích, được kết nối vào)")}");
        Console.WriteLine($"  🌐 VPS        : {cfg.Vps.Username}@{cfg.Vps.Host}");
        Console.WriteLine($"  🐧 Hệ điều hành: Linux");
        Console.ResetColor();

        if (cfg.Role == MachineRole.MachineB)
            PrintGuideForMachineB(cfg);
        else
            PrintGuideForMachineA(cfg);

        // ── Troubleshooting ───────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n  " + new string('─', 65));
        Console.WriteLine("  ❓ XỬ LÝ SỰ CỐ THƯỜNG GẶP (Linux)");
        Console.WriteLine("  " + new string('─', 65));
        Console.ResetColor();
        Console.WriteLine("  • Tunnel DOWN liên tục         → Kiểm tra internet, VPS có hoạt động không");
        Console.WriteLine("  • 'connect_to localhost failed' → Máy B chưa bật SSH Server:");
        Console.WriteLine("      Linux:   sudo service ssh start");
        Console.WriteLine("      Windows: Start-Service sshd (PowerShell Admin)");
        Console.WriteLine("  • 'Connection refused'          → Tunnel chưa UP, chờ vài giây rồi thử lại");
        Console.WriteLine("  • 'Permission denied'           → Sai username/password hoặc sai file .pem");
        Console.WriteLine("  • 'Bad permissions'             → Chạy: chmod 600 default_vps.pem");
        Console.WriteLine("  • 'Host key verification'       → Chạy: ssh-keygen -R 127.0.0.1");
        Console.WriteLine("  • CRLF error khi chạy .sh      → Chạy: sed -i 's/\r//' run.sh build.sh");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Nhấn Enter để quay lại menu...");
        Console.ResetColor();
        Console.ReadLine();
    }

    private static void PrintGuideForMachineB(AppConfig cfg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  " + new string('─', 65));
        Console.WriteLine("  ✅ NHIỆM VỤ CỦA MÁY B (máy này)");
        Console.WriteLine("  " + new string('─', 65));
        Console.ResetColor();
        Console.WriteLine("\n  Máy B đẩy Reverse Tunnel lên VPS để Máy A có thể kết nối vào.");
        Console.WriteLine("  Bạn CHỈ CẦN giữ app này đang chạy — không cần làm gì thêm.\n");

        Console.WriteLine("  Các cổng đang mở trên VPS cho session này:");
        foreach (var t in cfg.Tunnels)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"\n    [{t.Name}] ");
            Console.ResetColor();
            Console.WriteLine($"Máy này (port {t.RemotePort})  →  VPS relay port {t.VpsPort}");
            if (t.Type == ConnectionType.SSH)
                Console.WriteLine($"           Máy A sẽ SSH vào cổng này để điều khiển máy bạn");
            else if (t.Type == ConnectionType.RDP)
                Console.WriteLine($"           Máy A sẽ Remote Desktop / VNC vào cổng này");
            else
                Console.WriteLine($"           Máy A sẽ kết nối ứng dụng tùy chỉnh vào cổng này");
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  ⚠  YÊU CẦU TRÊN MÁY B (Linux):");
        Console.ResetColor();
        Console.WriteLine("    • OpenSSH Server phải đang chạy:");
        Console.WriteLine("      Kiểm tra: sudo systemctl status sshd");
        Console.WriteLine("      Bật:      sudo systemctl enable --now sshd");
        Console.WriteLine("    • Firewall phải cho phép port 22:");
        Console.WriteLine("      sudo ufw allow 22");
        Console.WriteLine("    • File key .pem phải có permission 600:");
        Console.WriteLine("      chmod 600 default_vps.pem");
        Console.WriteLine("\n  ℹ  Chia sẻ thông tin sau cho người dùng Máy A:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"    Session ID : {cfg.SessionId}");
        Console.WriteLine($"    Username   : (username Linux của máy này — chạy: whoami)");
        Console.WriteLine($"    Password   : (password Linux của máy này)");
        Console.ResetColor();
    }

    private static void PrintGuideForMachineA(AppConfig cfg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n  " + new string('─', 65));
        Console.WriteLine("  ✅ CÁCH KẾT NỐI TỪ MÁY A (máy này — Linux)");
        Console.WriteLine("  " + new string('─', 65));
        Console.ResetColor();
        Console.WriteLine("\n  Các cổng local dưới đây được forward xuyên VPS tới Máy B:\n");

        foreach (var t in cfg.Tunnels)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  ┌─── [{t.Name}] " + new string('─', 48 - t.Name.Length) + "┐");
            Console.ResetColor();
            Console.WriteLine($"  │  Luồng: localhost:{t.LocalPort}  →  VPS:{t.VpsPort}  →  MáyB:{t.RemotePort}");

            if (t.Type == ConnectionType.SSH)
            {
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Cách kết nối bằng ssh:");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  │    ssh <username_may_b>@127.0.0.1 -p {t.LocalPort}");
                Console.ResetColor();
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Hoặc chọn [5] → [1] trong menu để mở terminal tự động");
            }
            else if (t.Type == ConnectionType.RDP)
            {
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Cách kết nối bằng Remmina (GUI):");
                Console.WriteLine($"  │    Protocol: RDP  |  Server: 127.0.0.1:{t.LocalPort}");
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Hoặc dùng xfreerdp (command line):");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  │    xfreerdp /v:127.0.0.1:{t.LocalPort} /u:<username_may_b>");
                Console.ResetColor();
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Hoặc chọn [5] → [2] trong menu để xem hướng dẫn đầy đủ");
            }
            else
            {
                Console.WriteLine($"  │");
                Console.WriteLine($"  │  Kết nối ứng dụng bất kỳ tới: localhost:{t.LocalPort}");
                Console.WriteLine($"  │  Ví dụ VNC Viewer:");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"  │    vncviewer 127.0.0.1:{t.LocalPort}");
                Console.ResetColor();
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  └" + new string('─', 62) + "┘");
            Console.ResetColor();
            Console.WriteLine();
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ⚠  ĐIỀU KIỆN ĐỂ KẾT NỐI THÀNH CÔNG:");
        Console.ResetColor();
        Console.WriteLine("    • Máy B phải đang chạy app này với Role = B, Tunnel RUNNING");
        Console.WriteLine("    • Máy A (máy này) phải đang RUNNING (đã start tunnel)");
        Console.WriteLine("    • Cả 2 máy phải dùng cùng Session ID: \"" + cfg.SessionId + "\"");
        Console.WriteLine("    • Máy B phải có internet (dù khác mạng, khác IP đều được)");
    }
}
