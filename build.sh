#!/bin/bash
# ===========================================================
#    SSH Tunnel Manager -- Build Script (Linux/Ubuntu)
# ===========================================================

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo ""
echo -e "${CYAN}=========================================================="
echo "   SSH Tunnel Manager -- Build Script (Linux)"
echo -e "==========================================================${NC}"
echo ""

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}  [LOI] Chua cai .NET 8 SDK!${NC}"
    echo ""
    echo "  Cai dat bang lenh sau:"
    echo "    wget https://dot.net/v1/dotnet-install.sh"
    echo "    chmod +x dotnet-install.sh"
    echo "    ./dotnet-install.sh --channel 8.0"
    echo "    echo 'export PATH=\$PATH:\$HOME/.dotnet' >> ~/.bashrc"
    echo "    source ~/.bashrc"
    echo ""
    exit 1
fi

DOTNET_VERSION=$(dotnet --version 2>/dev/null | cut -d. -f1)
if [ "$DOTNET_VERSION" -lt 8 ] 2>/dev/null; then
    echo -e "${YELLOW}  [CANH BAO] Dang dung .NET $DOTNET_VERSION, can .NET 8+${NC}"
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
    -p:PublishReadyToRun=false \
    -o publish/linux/

BUILD_STATUS=$?

if [ $BUILD_STATUS -ne 0 ]; then
    echo ""
    echo -e "${RED}  [LOI] Build that bai!${NC}"
    echo "  Chay lai voi verbose:"
    echo "  dotnet publish -c Release -r linux-x64 --self-contained true -v detailed"
    echo ""
    exit 1
fi

echo "  [3/3] Build hoan tat!"
echo ""

# ── Cấp quyền thực thi ───────────────────────────────────
chmod +x publish/linux/SshTunnelManager
echo -e "${GREEN}  [OK] Da cap quyen thuc thi cho SshTunnelManager${NC}"

# ── Xóa file không cần thiết trên Linux ──────────────────
rm -f publish/linux/*.exe
rm -f publish/linux/*.pdb
rm -f publish/linux/*.ppk
rm -f publish/linux/*.ppk.enc
echo -e "${GREEN}  [OK] Da xoa cac file Windows khong can thiet${NC}"

# ── Copy run.sh vào publish/linux/ ───────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cp "$SCRIPT_DIR/run.sh" publish/linux/
chmod +x publish/linux/run.sh
echo -e "${GREEN}  [OK] Da copy run.sh vao publish/linux/${NC}"

# ── Kiểm tra file .pem ───────────────────────────────────
echo ""
echo -e "${CYAN}=========================================================="
echo "                   BUILD THANH CONG"
echo -e "==========================================================${NC}"
echo ""
echo "  Output: publish/linux/SshTunnelManager"
echo ""

if [ -f "publish/linux/default_vps.pem" ]; then
    echo -e "${GREEN}  [OK] default_vps.pem da co san trong publish/linux/${NC}"
    PERM=$(stat -c "%a" publish/linux/default_vps.pem 2>/dev/null)
    if [ "$PERM" != "600" ]; then
        chmod 600 publish/linux/default_vps.pem
        echo -e "${YELLOW}  [FIX] Da tu dong chmod 600 default_vps.pem${NC}"
    fi
else
    echo -e "${YELLOW}  [!] THIEU: default_vps.pem chua co trong publish/linux/${NC}"
    echo "      Copy file key vao:"
    echo "      cp /duong/dan/default_vps.pem publish/linux/"
    echo "      chmod 600 publish/linux/default_vps.pem"    
fi

if command -v ssh &> /dev/null; then
    echo -e "${GREEN}  [OK] OpenSSH client san sang${NC}"
else
    echo -e "${YELLOW}  [!] THIEU: openssh-client${NC}"
    echo "      sudo apt install openssh-client"
fi

echo ""
echo "  Thu muc phan phoi Linux:"
echo "    publish/linux/"
echo "    +-- SshTunnelManager    (binary)"
echo "    +-- default_vps.pem     (file key, can copy thu cong)"
echo "    +-- run.sh              (launcher)"
echo ""
echo "  Chay app:"
echo "    cd publish/linux && ./run.sh"
echo ""
echo -e "${CYAN}==========================================================${NC}"
echo ""
