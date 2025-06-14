#\!/bin/bash

# Test Team Creation API

echo "Testing Team Creation API..."

# Create a team without agents first
curl -X POST http://localhost:5210/api/teams \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjU4MmUxYTRlLWQ1MzctNGNmMy1iNTZlLTI0YjQ1OGZhMGNhMSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2VtYWlsYWRkcmVzcyI6ImFkbWluQGV4YW1wbGUuY29tIiwiZXhwIjoxNzQ4NDkwNDk3LCJpc3MiOiJwbGF5Z3JvdW5kIiwiYXVkIjoicGxheWdyb3VuZCJ9.ZWeZZ_G7FZuUgJMKqvARqCN4oGvI4jSEnbh7C_KdpuI" \
  -d '{
    "name": "Test Team",
    "description": "A test team for debugging",
    "policy": "Open",
    "agents": []
  }' \
  -v

echo -e "\n\nNow testing GET teams..."

# Get all teams
curl -X GET http://localhost:5210/api/teams \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjU4MmUxYTRlLWQ1MzctNGNmMy1iNTZlLTI0YjQ1OGZhMGNhMSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL2VtYWlsYWRkcmVzcyI6ImFkbWluQGV4YW1wbGUuY29tIiwiZXhwIjoxNzQ4NDkwNDk3LCJpc3MiOiJwbGF5Z3JvdW5kIiwiYXVkIjoicGxheWdyb3VuZCJ9.ZWeZZ_G7FZuUgJMKqvARqCN4oGvI4jSEnbh7C_KdpuI" \
  | jq .
