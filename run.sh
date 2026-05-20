#!/bin/bash
# ===========================================================
#    SSH Tunnel Manager — Launcher (Linux/Ubuntu)
#    User chỉ cần: chmod +x run.sh && ./run.sh
# ===========================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP="$SCRIPT_DIR/SshTunnelManager"
PEM="$SCRIPT_DIR/default_vps.pem"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# ── Tự động bật SSH Server nếu chưa chạy ─────────────────
ensure_ssh_server() {
    # Kiểm tra openssh-server đã cài chưa
    if ! command -v sshd &>/dev/null; then
        echo -e "${YELLOW}  [SSH] Chua cai openssh-server, dang cai...${NC}"
        sudo apt-get install -y openssh-server -qq
    fi

    # Kiểm tra SSH đang chạy chưa
    if ! sudo service ssh status 2>/dev/null | grep -q "running"; then
        echo -e "${YELLOW}  [SSH] SSH server chua chay, dang bat...${NC}"
        sudo service ssh start
        sleep 1
        if sudo service ssh status 2>/dev/null | grep -q "running"; then
            echo -e "${GREEN}  [SSH] SSH server da bat thanh cong${NC}"
        else
            echo -e "${YELLOW}  [SSH] Khong bat duoc SSH server tu dong.${NC}"
            echo "        Chay thu cong: sudo service ssh start"
        fi
    fi
}

ensure_ssh_server

# ── Kiểm tra binary ───────────────────────────────────────
if [ ! -f "$APP" ]; then
    echo -e "${RED}  [LOI] Khong tim thay file SshTunnelManager!${NC}"
    echo ""
    echo "  Dam bao thu muc nay co du 3 file:"
    echo "    SshTunnelManager      <- file chinh"
    echo "    default_vps.pem       <- file key VPS"
    echo "    run.sh                <- file nay"
    exit 1
fi

# ── Fix permission file .pem ──────────────────────────────
if [ -f "$PEM" ]; then
    PERM=$(stat -c "%a" "$PEM" 2>/dev/null)
    if [ "$PERM" != "600" ]; then
        chmod 600 "$PEM"
        echo -e "${YELLOW}  [FIX] Da tu dong chmod 600 default_vps.pem${NC}"
    fi
else
    echo -e "${YELLOW}  [CANH BAO] Khong tim thay default_vps.pem${NC}"
    echo "  App van chay duoc nhung khong ket noi duoc VPS."
    echo "  Dat file default_vps.pem vao cung thu muc voi run.sh."
    echo ""
fi

# ── Cấp quyền thực thi nếu thiếu ─────────────────────────
if [ ! -x "$APP" ]; then
    chmod +x "$APP"
fi

# ── Chạy app ─────────────────────────────────────────────
exec "$APP" "$@"
