#!/bin/bash
# API Testing Script

API_BASE="http://localhost:9080"
READ_KEY="eNBlQ6gl7bf/Z7oc++GRJ1aBcNWkndkO+XvMdpkV62V2rkFu7Nx2s6I8HfyVhUTWWkuNFaMvZQk+MzUxJZzJfg=="
EXPORT_KEY="14VQaFbNEMJa5Q47NKcw7MURfSww4Jg9XCkA5oDthtOvF+lRmvE/cJjDta2/9vQZgYwdSsuWPn4AZt+59k8EeA=="

echo "======================================"
echo "Ask2Ask API Test Suite"
echo "======================================"
echo ""

echo "Test 1: No API Key (should fail)"
echo "-----------------------------------"
curl -s "$API_BASE/api/stats" | jq '.'
echo ""

echo "Test 2: Valid Read Key on /api/stats"
echo "-----------------------------------"
curl -s "$API_BASE/api/stats" -H "X-API-Key: $READ_KEY" | jq '.success, .data.overview'
echo ""

echo "Test 3: Valid Read Key on /api/visits"
echo "-----------------------------------"
curl -s "$API_BASE/api/visits?page=1&pageSize=5" -H "X-API-Key: $READ_KEY" | jq '.success, .data.totalVisits'
echo ""

echo "Test 4: Read Key on Export (should fail)"
echo "-----------------------------------"
curl -s "$API_BASE/api/export?format=json&limit=1" -H "X-API-Key: $READ_KEY" | jq '.error'
echo ""

echo "Test 5: Export Key on Export (should succeed)"
echo "-----------------------------------"
curl -s "$API_BASE/api/export?format=json&limit=1" -H "X-API-Key: $EXPORT_KEY" | jq '.success, .count'
echo ""

echo "Test 6: Network Information"
echo "-----------------------------------"
docker network inspect ask2askuk_telemetry-network | jq '.[0].IPAM.Config, .[0].Containers | keys'
echo ""

echo "======================================"
echo "All tests completed!"
echo "======================================"
