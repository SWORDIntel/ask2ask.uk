#!/bin/bash
# Test ZKP Authentication

API_KEY="14VQaFbNEMJa5Q47NKcw7MURfSww4Jg9XCkA5oDthtOvF+lRmvE/cJjDta2/9vQZgYwdSsuWPn4AZt+59k8EeA=="
PRIVATE_KEY_FILE="test-keys/private-key.pem"
BASE_URL="http://localhost:9080"

# Function to compute SHA-384 hash
compute_sha384() {
    echo -n "$1" | openssl dgst -sha384 -binary | base64 -w 0
}

# Function to sign request
sign_request() {
    local method=$1
    local path=$2
    local body=$3
    local timestamp=$4
    local nonce=$5
    
    # Compute body hash
    local body_hash=""
    if [ -n "$body" ]; then
        body_hash=$(compute_sha384 "$body")
    fi
    
    # Create message: method|path|bodyHash|timestamp|nonce
    local message="${method}|${path}|${body_hash}|${timestamp}|${nonce}"
    
    # Sign with ECDSA P-384
    echo -n "$message" | openssl dgst -sha384 -sign "$PRIVATE_KEY_FILE" | base64 -w 0
}

echo "======================================"
echo "ZKP Authentication Test"
echo "======================================"
echo ""

# Test 1: GET /api/stats (no ZKP required, just API key)
echo "Test 1: GET /api/stats (API key only)"
echo "-----------------------------------"
curl -s -X GET "${BASE_URL}/api/stats" \
  -H "X-API-Key: ${API_KEY}" | jq '.success, .data.overview.totalVisitors' 2>/dev/null || echo "Failed"
echo ""

# Test 2: GET /api/export without ZKP (should fail)
echo "Test 2: GET /api/export without ZKP signature (should fail)"
echo "-----------------------------------"
curl -s -X GET "${BASE_URL}/api/export?format=json&limit=1" \
  -H "X-API-Key: ${API_KEY}" | jq '.error' 2>/dev/null || echo "Failed"
echo ""

# Test 3: GET /api/export with ZKP signature (should succeed)
echo "Test 3: GET /api/export with ZKP signature"
echo "-----------------------------------"
METHOD="GET"
PATH="/api/export?format=json&limit=1"
TIMESTAMP=$(date +%s)
NONCE=$(uuidgen | tr -d '-')
SIGNATURE=$(sign_request "$METHOD" "$PATH" "" "$TIMESTAMP" "$NONCE")

echo "Timestamp: $TIMESTAMP"
echo "Nonce: $NONCE"
echo "Signature: ${SIGNATURE:0:50}..."
echo ""

RESPONSE=$(curl -s -X "$METHOD" "${BASE_URL}${PATH}" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Signature: $SIGNATURE" \
  -H "X-Timestamp: $TIMESTAMP" \
  -H "X-Nonce: $NONCE")

echo "$RESPONSE" | jq '.success, .count' 2>/dev/null || echo "$RESPONSE"
echo ""

# Test 4: Invalid signature (should fail)
echo "Test 4: GET /api/export with invalid signature (should fail)"
echo "-----------------------------------"
INVALID_SIGNATURE="invalid_signature_base64_encoded_string"
curl -s -X "$METHOD" "${BASE_URL}${PATH}" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Signature: $INVALID_SIGNATURE" \
  -H "X-Timestamp: $TIMESTAMP" \
  -H "X-Nonce: $(uuidgen | tr -d '-')" | jq '.error' 2>/dev/null || echo "Failed"
echo ""

# Test 5: Expired timestamp (should fail)
echo "Test 5: GET /api/export with expired timestamp (should fail)"
echo "-----------------------------------"
EXPIRED_TIMESTAMP=$((TIMESTAMP - 400))  # 400 seconds ago (outside 5 min window)
NONCE=$(uuidgen | tr -d '-')
SIGNATURE=$(sign_request "$METHOD" "$PATH" "" "$EXPIRED_TIMESTAMP" "$NONCE")

curl -s -X "$METHOD" "${BASE_URL}${PATH}" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Signature: $SIGNATURE" \
  -H "X-Timestamp: $EXPIRED_TIMESTAMP" \
  -H "X-Nonce: $NONCE" | jq '.error' 2>/dev/null || echo "Failed"
echo ""

echo "======================================"
echo "Tests completed!"
echo "======================================"

