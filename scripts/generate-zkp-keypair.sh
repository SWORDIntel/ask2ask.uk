#!/bin/bash
# Generate ECDSA P-384 key pairs for ZKP authentication
# CNSA 2.0 Compliant: ECDSA P-384 with SHA-384

echo "=== ZKP Key Pair Generator ==="
echo "Generating ECDSA P-384 key pairs (CNSA 2.0 compliant)"
echo ""

# Generate key pair using OpenSSL
echo "Generating ECDSA P-384 key pair..."
openssl ecparam -genkey -name secp384r1 -noout -out private-key.pem
openssl ec -in private-key.pem -pubout -out public-key.pem

# Convert to base64 format for API configuration
PRIVATE_KEY=$(openssl ec -in private-key.pem -outform DER | base64 -w 0)
PUBLIC_KEY=$(openssl ec -in public-key.pem -pubin -outform DER | base64 -w 0)

echo ""
echo "=== Generated Key Pair ==="
echo ""
echo "Private Key (Base64 - KEEP SECRET):"
echo "$PRIVATE_KEY"
echo ""
echo "Public Key (Base64 - Add to appsettings.Api.json):"
echo "$PUBLIC_KEY"
echo ""
echo "Public Key Hash (SHA-384):"
echo "$PUBLIC_KEY" | base64 -d | openssl dgst -sha384 -binary | base64 -w 0
echo ""
echo ""
echo "=== Configuration Example ==="
echo "Add to appsettings.Api.json:"
echo ""
echo "{"
echo "  \"Key\": \"your-api-key-secret\","
echo "  \"PublicKey\": \"$PUBLIC_KEY\","
echo "  \"Scopes\": [\"read\", \"export\"],"
echo "  \"Description\": \"API client with ZKP authentication\""
echo "}"
echo ""
echo ""
echo "=== Files Created ==="
echo "- private-key.pem (KEEP SECRET - for client use)"
echo "- public-key.pem (can be shared - for server config)"
echo ""
echo "⚠️  IMPORTANT: Keep private-key.pem secure and never commit to git!"

