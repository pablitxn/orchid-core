#!/bin/bash

# Script to run all unit tests in the backend project

set -e

echo "==================================="
echo "Running Backend Unit Tests"
echo "==================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Start time
START_TIME=$(date +%s)

# Test results directory
RESULTS_DIR="test-results/unit"
mkdir -p $RESULTS_DIR

echo -e "${YELLOW}Starting unit tests...${NC}"
echo ""

# Get the directory where the script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Run unit tests with detailed output
dotnet test Backend.sln \
    --filter "FullyQualifiedName~.Tests&FullyQualifiedName!~IntegrationTests" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=$RESULTS_DIR/unit-test-results.trx" \
    --logger "html;LogFileName=$RESULTS_DIR/unit-test-results.html" \
    --collect:"XPlat Code Coverage" \
    --results-directory $RESULTS_DIR \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput=$RESULTS_DIR/coverage/

# Check test result
TEST_RESULT=$?

# End time
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo ""
echo "==================================="
if [ $TEST_RESULT -eq 0 ]; then
    echo -e "${GREEN}✓ Unit tests completed successfully!${NC}"
else
    echo -e "${RED}✗ Unit tests failed!${NC}"
fi
echo "==================================="
echo "Duration: ${DURATION} seconds"
echo "Results saved to: $RESULTS_DIR"
echo ""

# Generate coverage report if tests passed
if [ $TEST_RESULT -eq 0 ] && [ -f "$RESULTS_DIR/coverage/coverage.cobertura.xml" ]; then
    echo -e "${YELLOW}Generating coverage report...${NC}"
    
    # Install ReportGenerator if not present
    if ! dotnet tool list -g | grep -q "dotnet-reportgenerator-globaltool"; then
        echo "Installing ReportGenerator..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
    fi
    
    # Generate HTML coverage report
    reportgenerator \
        -reports:"$RESULTS_DIR/coverage/coverage.cobertura.xml" \
        -targetdir:"$RESULTS_DIR/coverage/report" \
        -reporttypes:Html \
        -verbosity:Error
    
    echo -e "${GREEN}Coverage report generated at: $RESULTS_DIR/coverage/report/index.html${NC}"
fi

exit $TEST_RESULT