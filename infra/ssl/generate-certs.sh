#!/bin/bash
set -e

# Отключаем конвертацию путей MSYS2 (критично для Git Bash на Windows!)
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL="*"

CERT_DIR="./certs"
CA_DIR="$CERT_DIR/ca"
SERVICES_DIR="$CERT_DIR/services"
CERT_PASSWORD="vp_secret"

SERVICES=(
    "minio.vp.local"
    "rabbitmq.vp.local"
    "fileservice.vp.local"
    "lessonservice.vp.local"
    "webapp.vp.local"
    "nginx.vp.local"
)

echo "=== [1/3] Cleaning old certs ==="
rm -rf "$CERT_DIR"
mkdir -p "$CA_DIR" "$SERVICES_DIR"

echo "=== [2/3] Creating Root CA ==="
openssl genrsa -out "$CA_DIR/ca.key" 4096 2>/dev/null
openssl req -new -x509 -days 3650 \
    -key "$CA_DIR/ca.key" \
    -out "$CA_DIR/ca.crt" \
    -subj "/C=RU/ST=Local/L=Dev/O=VideoPlatform/CN=VideoPlatform Root CA" 2>/dev/null
echo "   OK: Root CA created"

echo "=== [3/3] Generating service certificates ==="
for SERVICE in "${SERVICES[@]}"; do
    echo "   -> $SERVICE"
    SERVICE_DIR="$SERVICES_DIR/$SERVICE"
    mkdir -p "$SERVICE_DIR"
    SHORT_NAME=$(echo $SERVICE | cut -d. -f1)

    cat > "$SERVICE_DIR/openssl.cnf" <<INNER
[req]
default_bits = 2048
prompt = no
default_md = sha256
distinguished_name = dn
req_extensions = v3_req

[dn]
C = RU
ST = Local
L = Dev
O = VideoPlatform
OU = Development
CN = $SERVICE

[v3_req]
basicConstraints = CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth, clientAuth
subjectAltName = @alt_names

[alt_names]
DNS.1 = $SERVICE
DNS.2 = $SHORT_NAME
DNS.3 = localhost
IP.1 = 127.0.0.1
INNER

    openssl genrsa -out "$SERVICE_DIR/$SERVICE.key" 2048 2>/dev/null
    openssl req -new -key "$SERVICE_DIR/$SERVICE.key" -out "$SERVICE_DIR/$SERVICE.csr" -config "$SERVICE_DIR/openssl.cnf" 2>/dev/null
    openssl x509 -req -days 365 \
        -in "$SERVICE_DIR/$SERVICE.csr" \
        -CA "$CA_DIR/ca.crt" \
        -CAkey "$CA_DIR/ca.key" \
        -CAcreateserial \
        -out "$SERVICE_DIR/$SERVICE.crt" \
        -extensions v3_req \
        -extfile "$SERVICE_DIR/openssl.cnf" 2>/dev/null
    openssl pkcs12 -export \
        -in "$SERVICE_DIR/$SERVICE.crt" \
        -inkey "$SERVICE_DIR/$SERVICE.key" \
        -out "$SERVICE_DIR/$SERVICE.pfx" \
        -passout pass:$CERT_PASSWORD 2>/dev/null

    echo "      OK"
done

echo ""
echo "============================================================"
echo "[SUCCESS] All certificates generated!"
echo "============================================================"
echo ""
echo "Location: $CERT_DIR"
echo ""
echo "Structure:"
for SERVICE in "${SERVICES[@]}"; do
    echo "  +-- $SERVICE/"
done
echo ""
echo "PFX password: $CERT_PASSWORD"
echo ""
echo "[IMPORTANT] Install Root CA to Trusted Root store:"
echo "  Double-click: $CA_DIR/ca.crt"
echo ""