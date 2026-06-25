# generate-certs.ps1
$ErrorActionPreference = "Stop"
$CertDir = ".\certs"
$CaDir = "$CertDir\ca"
$ServicesDir = "$CertDir\services"
$Services = @(
    "minio.vp.local",
    "rabbitmq.vp.local",
    "fileservice.vp.local",
    "lessonservice.vp.local",
    "webapp.vp.local",
    "nginx.vp.local"
)
$CertPassword = "vp_secret"

# 1. Проверяем openssl
$OpenSSL = Get-Command openssl -ErrorAction SilentlyContinue
if (-not $OpenSSL) {
    Write-Host "📦 OpenSSL не найден. Устанавливаю через winget..." -ForegroundColor Yellow
    winget install -e --id shininglight.openssl --accept-source-agreements --accept-package-agreements
    $env:Path += ";C:\Program Files\OpenSSL-Win64\bin"
    $OpenSSL = Get-Command openssl -ErrorAction SilentlyContinue
    if (-not $OpenSSL) {
        Write-Host "❌ OpenSSL не установлен. Установите вручную: https://slproweb.com/products/Win32OpenSSL.html" -ForegroundColor Red
        exit 1
    }
}

# 2. Очистка
Write-Host "🧹 Очистка старых сертификатов..." -ForegroundColor Cyan
Remove-Item -Recurse -Force $CertDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $CaDir, $ServicesDir -Force | Out-Null

# 3. Root CA
Write-Host "`n🔐 [1/3] Создание Root CA..." -ForegroundColor Cyan
& openssl genrsa -out "$CaDir\ca.key" 4096 2>$null
& openssl req -new -x509 -days 3650 `
    -key "$CaDir\ca.key" `
    -out "$CaDir\ca.crt" `
    -subj "/C=RU/ST=Local/L=Dev/O=VideoPlatform/CN=VideoPlatform Root CA" 2>$null
Write-Host "   ✅ Root CA создан: $CaDir\ca.crt" -ForegroundColor Green

# 4. Сертификаты сервисов
Write-Host "`n📜 [2/3] Генерация сертификатов для сервисов..." -ForegroundColor Cyan
foreach ($svc in $Services) {
    Write-Host "   → $svc" -ForegroundColor White
    $SvcDir = Join-Path $ServicesDir $svc
    New-Item -ItemType Directory -Path $SvcDir -Force | Out-Null
    $ShortName = ($svc -split '\.')[0]

    $ConfPath = "$SvcDir\openssl.cnf"
    @"
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
CN = $svc

[v3_req]
basicConstraints = CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth, clientAuth
subjectAltName = @alt_names

[alt_names]
DNS.1 = $svc
DNS.2 = $ShortName
DNS.3 = localhost
IP.1 = 127.0.0.1
"@ | Set-Content -Path $ConfPath -Encoding UTF8

    & openssl genrsa -out "$SvcDir\$svc.key" 2048 2>$null
    & openssl req -new -key "$SvcDir\$svc.key" -out "$SvcDir\$svc.csr" -config $ConfPath 2>$null
    & openssl x509 -req -days 365 `
        -in "$SvcDir\$svc.csr" `
        -CA "$CaDir\ca.crt" `
        -CAkey "$CaDir\ca.key" `
        -CAcreateserial `
        -out "$SvcDir\$svc.crt" `
        -extensions v3_req `
        -extfile $ConfPath 2>$null
    & openssl pkcs12 -export `
        -in "$SvcDir\$svc.crt" `
        -inkey "$SvcDir\$svc.key" `
        -out "$SvcDir\$svc.pfx" `
        -passout "pass:$CertPassword" 2>$null

    Write-Host "     ✅ Готов" -ForegroundColor Green
}

# 5. Итог
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "🎉 Все сертификаты успешно сгенерированы!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "`n📂 Структура:" -ForegroundColor White
Write-Host "   $CertDir\" -ForegroundColor Gray
Write-Host "   ├── ca\" -ForegroundColor Gray
Write-Host "   │   ├── ca.crt        ← Установите в доверенные!" -ForegroundColor Yellow
Write-Host "   │   └── ca.key" -ForegroundColor Gray
Write-Host "   └── services\" -ForegroundColor Gray
foreach ($svc in $Services) {
    Write-Host "       └── $svc\" -ForegroundColor Gray
}
Write-Host "`n🔑 Пароль для PFX-файлов: $CertPassword" -ForegroundColor Yellow
Write-Host "`n📌 ВАЖНО: Установите корневой сертификат в доверенные!" -ForegroundColor Red
Write-Host "   Двойной клик по: $CaDir\ca.crt" -ForegroundColor White
Write-Host "   → Установить сертификат → Локальный компьютер → Далее" -ForegroundColor White
Write-Host "   → Поместить в доверенные корневые центры сертификации → OK → Готово" -ForegroundColor White
Write-Host ""