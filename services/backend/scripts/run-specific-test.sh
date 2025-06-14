#!/bin/bash

# Script to run specific tests by pattern

if [ $# -eq 0 ]; then
    echo "Usage: $0 <test-pattern>"
    echo ""
    echo "Examples:"
    echo "  $0 ConsumeCreditsHandler  # Run all tests containing 'ConsumeCreditsHandler'"
    echo "  $0 CreditSystem           # Run all tests in CreditSystem namespace"
    echo "  $0 Application.Tests      # Run all tests in Application.Tests project"
    echo ""
    exit 1
fi

TEST_PATTERN=$1

echo "==================================="
echo "Running tests matching: $TEST_PATTERN"
echo "==================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Get the directory where the script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Test results directory
RESULTS_DIR="test-results/specific"
mkdir -p $RESULTS_DIR

# Run tests
dotnet test Backend.sln \
    --filter "FullyQualifiedName~$TEST_PATTERN" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=$RESULTS_DIR/$TEST_PATTERN-results.trx" \
    --collect:"XPlat Code Coverage" \
    --results-directory $RESULTS_DIR

TEST_RESULT=$?

echo ""
if [ $TEST_RESULT -eq 0 ]; then
    echo -e "${GREEN}✓ Tests completed successfully!${NC}"
else
    echo -e "${RED}✗ Tests failed!${NC}"
fi

exit $TEST_RESULT