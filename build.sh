#!/bin/bash
# ===========================================================
#    SSH Tunnel Manager -- Build Script (Linux/Ubuntu)
# ===========================================================

# Màu sắc terminal
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

echo ""
echo -e "${CYAN}=========================================================="
echo "   SSH Tunnel Manager -- Build Script (Linux)"
echo -e "==========================================================${NC}"
echo ""

# ── Kiểm tra .NET SDK ─────────────────────────────────────
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}  [LOI] Chua cai .NET 8 SDK!${NC}"
    echo ""
    echo "  Cai dat bang lenh sau:"
    echo ""
    echo "    # Them Microsoft package repository"
    echo "    wget https://packages.microsoft.com/config/ubuntu/\$(lsb_release -rs)/packages-microsoft-prod.deb"
    echo "    sudo dpkg -i packages-microsoft-prod.deb"
    echo "    rm packages-microsoft-prod.deb"
    echo ""
    echo "    # Cai .NET 8 SDK"
    echo "    sudo apt update"
    echo "    sudo apt install -y dotnet-sdk-8.0"
    echo ""
    echo "  Sau khi cai xong, chay lai script nay."
    echo ""
    exit 1
fi

# Kiểm tra version .NET >= 8
DOTNET_VERSION=$(dotnet --version 2>/dev/null | cut -d. -f1)
if [ "$DOTNET_VERSION" -lt 8 ] 2>/dev/null; then
    echo -e "${YELLOW}  [CANH BAO] Dang dung .NET $DOTNET_VERSION, can .NET 8+${NC}"
    echo "  Tai tai: https://dotnet.microsoft.com/download/dotnet/8.0"
    echo ""
fi

echo -e "${GREEN}  [OK] .NET SDK san sang: $(dotnet --version)${NC}"
echo ""

# ── Build ─────────────────────────────────────────────────
echo "  [1/3] Dang restore packages..."
dotnet restore > /dev/null 2>&1

echo "  [2/3] Dang build file binary..."
dotnet publish -c Release -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true \
    -o publish/

BUILD_STATUS=$?

if [ $BUILD_STATUS -ne 0 ]; then
    echo ""
    echo -e "${RED}  [LOI] Build that bai!${NC}"
    echo "  Chay lai voi verbose de xem chi tiet loi:"
    echo "  dotnet publish -c Release -r linux-x64 --self-contained true -v detailed"
    echo ""
    exit 1
fi

echo "  [3/3] Build hoan tat!"
echo ""

# ── Cấp quyền thực thi ───────────────────────────────────
chmod +x publish/SshTunnelManager
echo -e "${GREEN}  [OK] Da cap quyen thuc thi cho SshTunnelManager${NC}"

# ── Kiểm tra file cần thiết ──────────────────────────────
echo ""
echo -e "${CYAN}=========================================================="
echo "                   BUILD THANH CONG"
echo -e "==========================================================${NC}"
echo ""
echo "  Output: publish/SshTunnelManager"
echo ""

# Kiểm tra file .pem
if [ -f "publish/default_vps.pem" ]; then
    echo -e "${GREEN}  [OK] default_vps.pem da co san trong publish/${NC}"
    # Tự động fix permission nếu sai
    PERM=$(stat -c "%a" publish/default_vps.pem 2>/dev/null)
    if [ "$PERM" != "600" ]; then
        chmod 600 publish/default_vps.pem
        echo -e "${YELLOW}  [FIX] Da tu dong chmod 600 default_vps.pem${NC}"
    fi
else
    echo -e "${YELLOW}  [!] THIEU: default_vps.pem chua co trong publish/${NC}"
    echo "      - Neu co file .ppk (Windows), convert bang lenh:"
    echo "        sudo apt install putty-tools"
    echo "        puttygen default_vps.ppk -O private-openssh -o publish/default_vps.pem"
    echo "        chmod 600 publish/default_vps.pem"
    echo "      - Neu nhan file .pem tu nguoi quan ly:"
    echo "        cp /duong/dan/default_vps.pem publish/"
    echo "        chmod 600 publish/default_vps.pem"
fi

# Kiểm tra ssh có sẵn không
if command -v ssh &> /dev/null; then
    echo -e "${GREEN}  [OK] OpenSSH client san sang: $(ssh -V 2>&1)${NC}"
else
    echo -e "${YELLOW}  [!] THIEU: openssh-client chua duoc cai${NC}"
    echo "      Cai bang lenh: sudo apt install openssh-client"
fi

# ── Hướng dẫn setup ──────────────────────────────────────
echo ""
echo -e "${CYAN}=========================================================="
echo "   HUONG DAN SETUP SAU KHI BUILD"
echo -e "==========================================================${NC}"
echo ""
echo "  BUOC 1 - Chuan bi thu muc publish/ gom du 2 file:"
echo "    publish/"
echo "    +-- SshTunnelManager      (vua build xong)"
echo "    +-- default_vps.pem       (file key AWS, chmod 600)"
echo ""
echo "  BUOC 2 - Bat OpenSSH Server tren May B (neu May B la Linux):"
echo "    sudo systemctl enable --now sshd"
echo "    sudo ufw allow 22"
echo ""
echo "  BUOC 3 - Cau hinh VPS (chi can lam 1 lan duy nhat):"
echo ""
echo "  --- BUOC 3.1: Fix quyen file .pem ---"
echo "    chmod 600 publish/default_vps.pem"
echo ""
echo "  --- BUOC 3.2: SSH vao VPS ---"
echo "    ssh -i publish/default_vps.pem ubuntu@<IP_VPS>"
echo ""
echo "    Nhap 'yes' neu duoc hoi xac nhan ket noi lan dau."
echo ""
echo "  --- BUOC 3.3: Chinh sua file cau hinh SSH tren VPS ---"
echo "    sudo nano /etc/ssh/sshd_config"
echo ""
echo "    Tim va them vao cuoi file 2 dong sau:"
echo "      GatewayPorts yes"
echo "      AllowTcpForwarding yes"
echo ""
echo "    Luu file: nhan Ctrl+O, Enter, roi Ctrl+X de thoat."
echo ""
echo "  --- BUOC 3.4: Khoi dong lai SSH tren VPS ---"
echo "    sudo systemctl restart sshd"
echo "    sudo systemctl status sshd"
echo "    # Phai thay 'active (running)'"
echo ""
echo "  --- BUOC 3.5: Mo port firewall tren VPS ---"
echo "    sudo ufw allow 10000:19999/tcp"
echo "    sudo ufw status"
echo "    # Neu thay inactive -> firewall dang tat, khong can lam gi"
echo ""
echo "  --- BUOC 3.6: Cache host key VPS (lan dau tien) ---"
echo "    ssh -i publish/default_vps.pem ubuntu@<IP_VPS>"
echo "    # Nhap 'yes' khi hoi -> gõ exit de thoat"
echo "    # Lan sau app se tu dong ket noi, khong hoi nua"
echo ""
echo "  BUOC 4 - Chay app:"
echo "    May B (may dich): ./SshTunnelManager  -> Role B -> Start"
echo "    May A (may ban):  ./SshTunnelManager  -> Role A -> Start"
echo "    Ca 2 may phai nhap CUNG Session ID"
echo ""
echo "  BUOC 5 - Ket noi SSH tu May A:"
echo "    ssh <username_may_b>@127.0.0.1 -p <port_trong_app_muc_4>"
echo ""
echo "  LUU Y BAO MAT:"
echo "    - KHONG upload file .pem len GitHub"
echo "    - Chi gui file key qua kenh rieng (Zalo, USB)"
echo "    - Them *.pem vao .gitignore"
echo ""
echo -e "${CYAN}==========================================================${NC}"
echo ""
