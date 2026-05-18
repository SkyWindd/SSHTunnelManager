@echo off
chcp 65001 >nul

echo.
echo ==========================================================
echo    SSH Tunnel Manager -- Build Script (Windows)
echo ==========================================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] .NET 8 SDK is not installed!
    echo.
    echo  Download here: https://dotnet.microsoft.com/download/dotnet/8.0
    echo  Choose: .NET 8.0 SDK - Windows x64 Installer
    echo  After installation, restart CMD and run this file again.
    echo.
    pause
    exit /b 1
)

echo  [OK] .NET SDK is ready
echo.
echo  [1/3] Restoring packages...
dotnet restore >nul 2>&1

echo  [2/3] Building .exe file...
dotnet publish -c Release -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -o publish\windows\

if errorlevel 1 (
    echo  [ERROR] Build failed!
    pause
    exit /b 1
)

echo  [3/3] Build completed!
echo.
echo ==========================================================
echo                    BUILD SUCCESSFUL
echo ==========================================================
echo.
echo  Output: publish\windows\SshTunnelManager.exe
echo.

if exist publish\windows\plink.exe (
    echo  [OK] plink.exe already exists in publish\windows\
) else (
    echo  [!] MISSING: plink.exe was not found in publish\windows\
    echo      Download from: https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html
    echo      Copy it into: publish\windows\
)

if exist publish\windows\default_vps.ppk (
    echo  [OK] default_vps.ppk already exists in publish\windows\
) else (
    echo  [!] MISSING: default_vps.ppk was not found in publish\windows\
    echo      Copy your AWS .ppk key into: publish\windows\
    echo      Rename it to: default_vps.ppk
)

echo.
echo ==========================================================
echo   POST-BUILD SETUP GUIDE
echo ==========================================================
echo.

echo  STEP 1 - Prepare publish\windows\ with these 3 files:
echo    publish\windows\
echo    +-- SshTunnelManager.exe   (generated after build)
echo    +-- plink.exe              (downloaded from putty.org)
echo    +-- default_vps.ppk        (AWS private key)
echo.

echo  STEP 2 - Enable OpenSSH Server on the target PC (Machine B):
echo    Open PowerShell as Administrator and run:
echo    ^> Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
echo    ^> Start-Service sshd
echo    ^> Set-Service -Name sshd -StartupType Automatic
echo.

echo  STEP 3 - Configure VPS (only needed once)
echo.

echo  --- STEP 3.1: SSH into the VPS ---
echo  On Machine A, open PowerShell or CMD and run:
echo.
echo    ssh -i "path\to\default_vps.pem" ubuntu@13.229.239.111
echo.
echo    Type "yes" if asked to confirm the first connection.
echo.

echo  --- STEP 3.2: Configure .pem file permissions ---
echo    1. Right-click default_vps.pem -^> Properties
echo    2. Open Security tab -^> Advanced
echo    3. Click Disable inheritance
echo    4. Select "Remove all inherited permissions"
echo    5. Click Add -^> Select a principal
echo    6. Enter your current Windows username
echo    7. Enable "Full control"
echo    8. Remove groups: Users, Everyone, Authenticated Users
echo    9. Keep only: your current account and SYSTEM (if present)
echo.

echo  --- STEP 3.3: Edit SSH configuration on the VPS ---
echo    sudo nano /etc/ssh/sshd_config
echo.
echo    Add these lines at the end of the file:
echo      GatewayPorts yes
echo      AllowTcpForwarding yes
echo.
echo    Save with: Ctrl+O, Enter, Ctrl+X
echo.

echo  --- STEP 3.4: Restart SSH and open VPS firewall ---
echo    sudo systemctl restart sshd
echo    sudo ufw allow 10000:19999/tcp
echo.

echo  --- STEP 3.5: Cache VPS host key ---
echo    publish\windows\plink.exe -ssh ubuntu@13.229.239.111 -P 22 -i "publish\windows\default_vps.ppk"
echo.
echo    Type "y" if prompted, then type exit to close.
echo.

echo  STEP 4 - Run the app:
echo    Machine B (target PC): run SshTunnelManager.exe - Role B - Start
echo    Machine A (controller): run SshTunnelManager.exe - Role A - Start
echo    Both machines must enter the SAME Session ID
echo.

echo  STEP 5 - Connect through SSH:
echo    Open PuTTY
echo    Host: 127.0.0.1
echo    Port: (shown in the app under section [4])
echo.
echo    Login using the Windows username/password of Machine B
echo.

echo  SECURITY NOTES:
echo    - DO NOT upload .ppk files to GitHub
echo    - DO NOT upload .pem files to GitHub
echo    - Only share key files with trusted people
echo    - Add *.ppk and *.pem to .gitignore
echo.

echo ==========================================================
echo.
pause