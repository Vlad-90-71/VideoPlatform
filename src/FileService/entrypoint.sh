#!/bin/bash
set -e

if [ -f /app/certs/ca.crt ]; then
    echo "Installing CA certificate..."
    cp /app/certs/ca.crt /usr/local/share/ca-certificates/vp-ca.crt
    update-ca-certificates
fi

exec gosu appuser dotnet FileService.dll
