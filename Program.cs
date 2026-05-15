using System.Runtime.InteropServices;

namespace SshTunnelManager;

// ============================================================
//  Program — Entry point, detect OS rồi gọi đúng nhánh
// ============================================================

class Program
{
    static void Main(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            WindowsApp.Run(args);
        else
            LinuxApp.Run(args);
    }
}
