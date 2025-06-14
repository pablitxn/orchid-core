#!/bin/bash

# Test registration endpoint
echo "Testing registration with hardcoded email..."

# Generate unique email
UNIQUE_ID=$(date +%s)
EMAIL="test${UNIQUE_ID}@example.com"
PASSWORD="testpass123"

echo "Registering with email: $EMAIL"

# Register a new user
REGISTER_RESPONSE=$(curl -s -X POST http://localhost:5210/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Test User\",
    \"email\": \"$EMAIL\",
    \"password\": \"$PASSWORD\",
    \"roles\": [\"User\"]
  }")

echo "Registration response:"
echo $REGISTER_RESPONSE | jq .

# Extract user ID and email from response
USER_ID=$(echo $REGISTER_RESPONSE | jq -r '.id')
USER_EMAIL=$(echo $REGISTER_RESPONSE | jq -r '.email')

if [ "$USER_ID" != "null" ] && [ ! -z "$USER_ID" ]; then
  echo -e "\n✅ Registration successful!"
  echo "User ID: $USER_ID"
  echo "User Email: $USER_EMAIL"
  
  # Check subscription/credits
  echo -e "\nChecking user's subscription..."
  
  # First, let's login to get a token
  LOGIN_RESPONSE=$(curl -s -c cookies.txt -X POST http://localhost:5210/api/auth/login \
    -H "Content-Type: application/json" \
    -d "{
      \"email\": \"$USER_EMAIL\",
      \"password\": \"$PASSWORD\"
    }")
  
  TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.token')
  
  if [ "$TOKEN" != "null" ] && [ ! -z "$TOKEN" ]; then
    echo "Login successful, checking subscription..."
    
    # Get subscription details (using cookies)
    SUBSCRIPTION_RESPONSE=$(curl -s -b cookies.txt -X GET "http://localhost:5210/api/subscription/$USER_ID")
    
    echo -e "\nSubscription details:"
    echo "Raw response: $SUBSCRIPTION_RESPONSE"
    echo $SUBSCRIPTION_RESPONSE | jq . 2>/dev/null || echo "Failed to parse JSON"
    
    CREDITS=$(echo $SUBSCRIPTION_RESPONSE | jq -r '.credits')
    if [ "$CREDITS" = "1000" ]; then
      echo -e "\n✅ Success! User has been granted 1000 welcome credits!"
    else
      echo -e "\n❌ Error: Expected 1000 credits but got: $CREDITS"
    fi
  else
    echo "❌ Login failed"
  fi
else
  echo "❌ Registration failed"
fi