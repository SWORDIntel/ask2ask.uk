#!/bin/bash
# Generate CNSA 2.0 compliant API keys for ask2ask.com

echo "=== API Key Generator ==="
echo "Generating CNSA 2.0 compliant API keys (512-bit random keys)"
echo ""

echo "Read-only API Key:"
openssl rand -base64 64 | tr -d '\n'
echo ""
echo ""

echo "Export API Key:"
openssl rand -base64 64 | tr -d '\n'
echo ""
echo ""

echo "Admin API Key:"
openssl rand -base64 64 | tr -d '\n'
echo ""
echo ""

echo "=== Client Certificate Generation ==="
echo "To generate client certificates for mTLS:"
echo ""
echo "1. Generate CA (if not exists):"
echo "   openssl req -x509 -newkey rsa:4096 -sha384 -days 3650 -nodes \"
echo "     -keyout ca-key.pem -out ca.crt \"
echo "     -subj '/CN=Ask2Ask API CA/O=Ask2Ask/C=UK'"
echo ""
echo "2. Generate client certificate:"
echo "   openssl req -newkey rsa:4096 -sha384 -nodes \"
echo "     -keyout client-key.pem -out client-req.pem \"
echo "     -subj '/CN=API Client/O=Ask2Ask/C=UK'"
echo ""
echo "3. Sign client certificate:"
echo "   openssl x509 -req -in client-req.pem -days 365 -sha384 \"
echo "     -CA ca.crt -CAkey ca-key.pem -CAcreateserial \"
echo "     -out client-cert.pem"
echo ""
echo "4. Get SHA-384 thumbprint:"
echo "   openssl x509 -in client-cert.pem -outform DER | openssl dgst -sha384 -binary | base64"
echo ""
echo "5. Add thumbprint to appsettings.Api.json -> AllowedCertificateThumbprints"
echo ""