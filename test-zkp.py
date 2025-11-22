#!/usr/bin/env python3
import hashlib
import base64
import time
import uuid
import sys
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.backends import default_backend

def compute_sha384_hash(data):
    return base64.b64encode(hashlib.sha384(data.encode('utf-8')).digest()).decode('utf-8')

def sign_request(private_key_der_base64, method, path, body, timestamp, nonce):
    # Decode private key
    private_key_bytes = base64.b64decode(private_key_der_base64)
    private_key = serialization.load_der_private_key(
        private_key_bytes,
        password=None,
        backend=default_backend()
    )
    
    # Compute body hash
    body_hash = compute_sha384_hash(body) if body else ""
    
    # Create message: method|path|bodyHash|timestamp|nonce
    message = f"{method}|{path}|{body_hash}|{timestamp}|{nonce}"
    
    # Sign with ECDSA P-384
    signature = private_key.sign(
        message.encode('utf-8'),
        ec.ECDSA(hashes.SHA384())
    )
    
    return base64.b64encode(signature).decode('utf-8')

if __name__ == "__main__":
    if len(sys.argv) < 6:
        print("Usage: test-zkp.py <private-key-der-base64> <method> <path> <body> <timestamp> <nonce>")
        sys.exit(1)
    
    private_key = sys.argv[1]
    method = sys.argv[2]
    path = sys.argv[3]
    body = sys.argv[4]
    timestamp = int(sys.argv[5])
    nonce = sys.argv[6]
    
    signature = sign_request(private_key, method, path, body, timestamp, nonce)
    print(signature)

