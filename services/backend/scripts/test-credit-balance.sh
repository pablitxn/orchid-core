#!/bin/bash

echo "Testing Credit Balance Endpoint"
echo "==============================="

# Test 1: Without authentication (should return 401)
echo -e "\n1. Testing without authentication:"
curl -X GET http://localhost:5210/api/credithistory/balance \
  -H "Accept: application/json" \
  -w "\nStatus: %{http_code}\n"

# Test 2: With invalid token (should return 401)
echo -e "\n\n2. Testing with invalid token:"
curl -X GET http://localhost:5210/api/credithistory/balance \
  -H "Accept: application/json" \
  -H "Authorization: Bearer invalid_token" \
  -w "\nStatus: %{http_code}\n"

echo -e "\n\nTo test with a valid token:"
echo "1. Login through the UI or use the /api/auth/login endpoint"
echo "2. Get the JWT token from the response or browser DevTools"
echo "3. Run: curl -X GET http://localhost:5210/api/credithistory/balance -H 'Authorization: Bearer YOUR_TOKEN'"