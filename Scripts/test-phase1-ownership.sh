#!/bin/bash
# Phase 1 - Ownership Testing (Bash Version)
# Requires: curl, jq
# Usage: ./test-phase1-ownership.sh

set -euo pipefail

API_URL="${API_URL:-https://localhost:5206}"
AUTH_URL="${AUTH_URL:-https://localhost:5001}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Counters
PASS=0
FAIL=0

echo ""
echo -e "${CYAN}========================================"
echo "PHASE 1 - Ownership Testing"
echo -e "========================================${NC}"
echo ""

# Ignore SSL cert validation (for localhost testing)
CURL_OPTS="-k -s"

#=====================================================================
# STEP 1: Authenticate as Alice (Admin)
#=====================================================================

echo -e "${YELLOW}Step 1: Authenticating as Alice (admin)...${NC}"
ALICE_RESPONSE=$(curl $CURL_OPTS -X POST "$AUTH_URL/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password"}')

if [ $? -ne 0 ]; then
  echo -e "${RED}? Authentication failed${NC}"
  exit 1
fi

ALICE_TOKEN=$(echo "$ALICE_RESPONSE" | jq -r '.accessToken')
ALICE_PAYLOAD=$(echo "$ALICE_TOKEN" | cut -d'.' -f2 | base64 -d 2>/dev/null | jq '.')
ALICE_USER_ID=$(echo "$ALICE_PAYLOAD" | jq -r '.userId')
ALICE_ROLE=$(echo "$ALICE_PAYLOAD" | jq -r '.role')

echo -e "${GREEN}? Alice authenticated!${NC}"
echo -e "   userId: ${GRAY}$ALICE_USER_ID${NC}"
echo -e "   role: ${GRAY}$ALICE_ROLE${NC}"
echo ""

#=====================================================================
# STEP 2: Seed Affiliates & Drivers (Alice - Admin)
#=====================================================================

echo -e "${YELLOW}Step 2: Seeding affiliates and drivers as Alice...${NC}"
SEED_AFFILIATES=$(curl $CURL_OPTS -X POST "$API_URL/dev/seed-affiliates" \
  -H "Authorization: Bearer $ALICE_TOKEN")

AFFILIATES_ADDED=$(echo "$SEED_AFFILIATES" | jq -r '.affiliatesAdded')
DRIVERS_ADDED=$(echo "$SEED_AFFILIATES" | jq -r '.driversAdded')

echo -e "${GREEN}? Affiliates & drivers created!${NC}"
echo -e "   Affiliates: ${GRAY}$AFFILIATES_ADDED${NC}"
echo -e "   Drivers: ${GRAY}$DRIVERS_ADDED${NC}"
echo ""

#=====================================================================
# STEP 3: Authenticate as Chris (Booker)
#=====================================================================

echo -e "${YELLOW}Step 3: Authenticating as Chris (booker)...${NC}"
CHRIS_RESPONSE=$(curl $CURL_OPTS -X POST "$AUTH_URL/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"chris","password":"password"}')

CHRIS_TOKEN=$(echo "$CHRIS_RESPONSE" | jq -r '.accessToken')
CHRIS_PAYLOAD=$(echo "$CHRIS_TOKEN" | cut -d'.' -f2 | base64 -d 2>/dev/null | jq '.')
CHRIS_USER_ID=$(echo "$CHRIS_PAYLOAD" | jq -r '.userId')
CHRIS_ROLE=$(echo "$CHRIS_PAYLOAD" | jq -r '.role')
CHRIS_EMAIL=$(echo "$CHRIS_PAYLOAD" | jq -r '.email')

echo -e "${GREEN}? Chris authenticated!${NC}"
echo -e "   userId: ${GRAY}$CHRIS_USER_ID${NC}"
echo -e "   role: ${GRAY}$CHRIS_ROLE${NC}"
echo -e "   email: ${GRAY}$CHRIS_EMAIL${NC}"
echo ""

#=====================================================================
# STEP 4: Seed Quotes as Alice (Admin)
#=====================================================================

echo -e "${YELLOW}Step 4: Seeding quotes as Alice (admin)...${NC}"
ALICE_QUOTES=$(curl $CURL_OPTS -X POST "$API_URL/quotes/seed" \
  -H "Authorization: Bearer $ALICE_TOKEN")

ALICE_QUOTES_COUNT=$(echo "$ALICE_QUOTES" | jq -r '.added')
ALICE_QUOTES_OWNER=$(echo "$ALICE_QUOTES" | jq -r '.createdByUserId')

echo -e "${GREEN}? Alice's quotes created!${NC}"
echo -e "   Count: ${GRAY}$ALICE_QUOTES_COUNT${NC}"
echo -e "   CreatedByUserId: ${GRAY}$ALICE_QUOTES_OWNER${NC}"
echo ""

#=====================================================================
# STEP 5: Seed Quotes as Chris (Booker)
#=====================================================================

echo -e "${YELLOW}Step 5: Seeding quotes as Chris (booker)...${NC}"
CHRIS_QUOTES=$(curl $CURL_OPTS -X POST "$API_URL/quotes/seed" \
  -H "Authorization: Bearer $CHRIS_TOKEN")

CHRIS_QUOTES_COUNT=$(echo "$CHRIS_QUOTES" | jq -r '.added')
CHRIS_QUOTES_OWNER=$(echo "$CHRIS_QUOTES" | jq -r '.createdByUserId')

echo -e "${GREEN}? Chris's quotes created!${NC}"
echo -e "   Count: ${GRAY}$CHRIS_QUOTES_COUNT${NC}"
echo -e "   CreatedByUserId: ${GRAY}$CHRIS_QUOTES_OWNER${NC}"
echo ""

#=====================================================================
# STEP 6: Seed Bookings as Alice (Admin)
#=====================================================================

echo -e "${YELLOW}Step 6: Seeding bookings as Alice (admin)...${NC}"
ALICE_BOOKINGS=$(curl $CURL_OPTS -X POST "$API_URL/bookings/seed" \
  -H "Authorization: Bearer $ALICE_TOKEN")

ALICE_BOOKINGS_COUNT=$(echo "$ALICE_BOOKINGS" | jq -r '.added')
ALICE_BOOKINGS_OWNER=$(echo "$ALICE_BOOKINGS" | jq -r '.createdByUserId')

echo -e "${GREEN}? Alice's bookings created!${NC}"
echo -e "   Count: ${GRAY}$ALICE_BOOKINGS_COUNT${NC}"
echo -e "   CreatedByUserId: ${GRAY}$ALICE_BOOKINGS_OWNER${NC}"

# FIX CHECK: Verify owner is populated
if [ "$ALICE_BOOKINGS_OWNER" == "null" ] || [ -z "$ALICE_BOOKINGS_OWNER" ]; then
  echo -e "${RED}   ? WARNING: CreatedByUserId is null/empty!${NC}"
  ((FAIL++))
else
  ((PASS++))
fi
echo ""

#=====================================================================
# STEP 7: Seed Bookings as Chris (Booker)
#=====================================================================

echo -e "${YELLOW}Step 7: Seeding bookings as Chris (booker)...${NC}"
CHRIS_BOOKINGS=$(curl $CURL_OPTS -X POST "$API_URL/bookings/seed" \
  -H "Authorization: Bearer $CHRIS_TOKEN")

CHRIS_BOOKINGS_COUNT=$(echo "$CHRIS_BOOKINGS" | jq -r '.added')
CHRIS_BOOKINGS_OWNER=$(echo "$CHRIS_BOOKINGS" | jq -r '.createdByUserId')

echo -e "${GREEN}? Chris's bookings created!${NC}"
echo -e "   Count: ${GRAY}$CHRIS_BOOKINGS_COUNT${NC}"
echo -e "   CreatedByUserId: ${GRAY}$CHRIS_BOOKINGS_OWNER${NC}"

# FIX CHECK: Verify owner is populated
if [ "$CHRIS_BOOKINGS_OWNER" == "null" ] || [ -z "$CHRIS_BOOKINGS_OWNER" ]; then
  echo -e "${RED}   ? WARNING: CreatedByUserId is null/empty!${NC}"
  ((FAIL++))
else
  ((PASS++))
fi
echo ""

#=====================================================================
# STEP 8: Test Quote Access - Alice (Admin)
#=====================================================================

echo -e "${YELLOW}Step 8: Testing Alice's quote access (should see ALL quotes)...${NC}"
ALICE_QUOTES_LIST=$(curl $CURL_OPTS -X GET "$API_URL/quotes/list?take=100" \
  -H "Authorization: Bearer $ALICE_TOKEN")

ALICE_QUOTES_VISIBLE=$(echo "$ALICE_QUOTES_LIST" | jq '. | length')

echo -e "${GREEN}? Alice sees $ALICE_QUOTES_VISIBLE quotes (expected: 10)${NC}"

if [ "$ALICE_QUOTES_VISIBLE" -eq 10 ]; then
  ((PASS++))
else
  echo -e "${RED}   ? Count mismatch!${NC}"
  ((FAIL++))
fi
echo ""

#=====================================================================
# STEP 9: Test Quote Access - Chris (Booker)
#=====================================================================

echo -e "${YELLOW}Step 9: Testing Chris's quote access (should see ONLY his quotes)...${NC}"
CHRIS_QUOTES_LIST=$(curl $CURL_OPTS -X GET "$API_URL/quotes/list?take=100" \
  -H "Authorization: Bearer $CHRIS_TOKEN")

CHRIS_QUOTES_VISIBLE=$(echo "$CHRIS_QUOTES_LIST" | jq '. | length')

echo -e "${GREEN}? Chris sees $CHRIS_QUOTES_VISIBLE quotes (expected: 5)${NC}"

if [ "$CHRIS_QUOTES_VISIBLE" -eq 5 ]; then
  ((PASS++))
else
  echo -e "${RED}   ? Count mismatch!${NC}"
  ((FAIL++))
fi
echo ""

#=====================================================================
# STEP 10: Test Booking Access - Alice (Admin)
#=====================================================================

echo -e "${YELLOW}Step 10: Testing Alice's booking access (should see ALL bookings)...${NC}"
ALICE_BOOKINGS_LIST=$(curl $CURL_OPTS -X GET "$API_URL/bookings/list?take=100" \
  -H "Authorization: Bearer $ALICE_TOKEN")

ALICE_BOOKINGS_VISIBLE=$(echo "$ALICE_BOOKINGS_LIST" | jq '. | length')

echo -e "${GREEN}? Alice sees $ALICE_BOOKINGS_VISIBLE bookings (expected: 16)${NC}"

if [ "$ALICE_BOOKINGS_VISIBLE" -eq 16 ]; then
  ((PASS++))
else
  echo -e "${RED}   ? Count mismatch!${NC}"
  ((FAIL++))
fi
echo ""

#=====================================================================
# STEP 11: Test Booking Access - Chris (Booker)
#=====================================================================

echo -e "${YELLOW}Step 11: Testing Chris's booking access (should see ONLY his bookings)...${NC}"
CHRIS_BOOKINGS_LIST=$(curl $CURL_OPTS -X GET "$API_URL/bookings/list?take=100" \
  -H "Authorization: Bearer $CHRIS_TOKEN")

CHRIS_BOOKINGS_VISIBLE=$(echo "$CHRIS_BOOKINGS_LIST" | jq '. | length')

echo -e "${GREEN}? Chris sees $CHRIS_BOOKINGS_VISIBLE bookings (expected: 8)${NC}"

if [ "$CHRIS_BOOKINGS_VISIBLE" -eq 8 ]; then
  ((PASS++))
else
  echo -e "${RED}   ? Count mismatch! Expected 8, got $CHRIS_BOOKINGS_VISIBLE${NC}"
  ((FAIL++))
fi
echo ""

#=====================================================================
# STEP 12: Test Forbidden Access - Chris tries Alice's quote
#=====================================================================

echo -e "${YELLOW}Step 12: Testing forbidden access (Chris tries to get Alice's quote)...${NC}"

# Get one of Alice's quote IDs
ALICE_QUOTE_ID=$(echo "$ALICE_QUOTES_LIST" | jq -r '.[0].id')

# Try to access with Chris's token
HTTP_CODE=$(curl $CURL_OPTS -w "%{http_code}" -o /dev/null -X GET "$API_URL/quotes/$ALICE_QUOTE_ID" \
  -H "Authorization: Bearer $CHRIS_TOKEN")

if [ "$HTTP_CODE" -eq 403 ]; then
  echo -e "${GREEN}? Access correctly denied (403 Forbidden)${NC}"
  ((PASS++))
else
  echo -e "${RED}? SECURITY ISSUE: Chris can access Alice's quote! (HTTP $HTTP_CODE)${NC}"
  ((FAIL++))
fi
echo ""

#=====================================================================
# RESULTS SUMMARY
#=====================================================================

echo -e "${CYAN}========================================"
echo "Phase 1 Testing Complete!"
echo -e "========================================${NC}"
echo ""

echo -e "${GRAY}Summary:${NC}"
echo "  • Alice (admin) created: 5 quotes, 8 bookings"
echo "  • Chris (booker) created: 5 quotes, 8 bookings"
echo ""
echo -e "${GRAY}Access Control Results:${NC}"
echo "  • Alice sees: ALL quotes ($ALICE_QUOTES_VISIBLE), ALL bookings ($ALICE_BOOKINGS_VISIBLE)"
echo "  • Chris sees: OWN quotes ($CHRIS_QUOTES_VISIBLE), OWN bookings ($CHRIS_BOOKINGS_VISIBLE)"
echo ""

if [ $FAIL -eq 0 ]; then
  echo -e "${GREEN}? ALL TESTS PASSED ($PASS/$PASS)${NC}"
  exit 0
else
  echo -e "${RED}? FAILURES: $FAIL test(s) failed${NC}"
  echo -e "${GREEN}? PASSED: $PASS test(s)${NC}"
  exit 1
fi
