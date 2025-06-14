#!/bin/bash

# Script to run all tests (unit and integration) in the backend project

set -e

echo "==================================="
echo "Running All Backend Tests"
echo "==================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Start time
TOTAL_START_TIME=$(date +%s)

# Create main results directory
RESULTS_DIR="test-results"
mkdir -p $RESULTS_DIR

# Get the directory where the script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Track overall result
OVERALL_RESULT=0

# Run unit tests
echo -e "${BLUE}=== UNIT TESTS ===${NC}"
"$SCRIPT_DIR/run-unit-tests.sh" || {
    OVERALL_RESULT=1
    echo -e "${RED}Unit tests failed!${NC}"
}

echo ""
echo -e "${BLUE}=== INTEGRATION TESTS ===${NC}"
"$SCRIPT_DIR/run-integration-tests.sh" || {
    OVERALL_RESULT=1
    echo -e "${RED}Integration tests failed!${NC}"
}

# End time
TOTAL_END_TIME=$(date +%s)
TOTAL_DURATION=$((TOTAL_END_TIME - TOTAL_START_TIME))

# Generate combined coverage report
if [ -f "$RESULTS_DIR/unit/coverage/coverage.cobertura.xml" ] && [ -f "$RESULTS_DIR/integration/coverage/coverage.cobertura.xml" ]; then
    echo ""
    echo -e "${YELLOW}Generating combined coverage report...${NC}"
    
    mkdir -p $RESULTS_DIR/combined/coverage
    
    # Install ReportGenerator if not present
    if ! dotnet tool list -g | grep -q "dotnet-reportgenerator-globaltool"; then
        echo "Installing ReportGenerator..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
    fi
    
    # Merge and generate combined report
    reportgenerator \
        -reports:"$RESULTS_DIR/unit/coverage/coverage.cobertura.xml;$RESULTS_DIR/integration/coverage/coverage.cobertura.xml" \
        -targetdir:"$RESULTS_DIR/combined/coverage/report" \
        -reporttypes:"Html;Cobertura" \
        -verbosity:Error \
        -title:"Backend Tests Coverage Report"
    
    echo -e "${GREEN}Combined coverage report generated at: $RESULTS_DIR/combined/coverage/report/index.html${NC}"
fi

# Summary
echo ""
echo "==================================="
echo "TEST EXECUTION SUMMARY"
echo "==================================="
echo "Total Duration: ${TOTAL_DURATION} seconds"
echo ""

if [ $OVERALL_RESULT -eq 0 ]; then
    echo -e "${GREEN}✓ All tests passed successfully!${NC}"
else
    echo -e "${RED}✗ Some tests failed!${NC}"
fi

echo ""
echo "Test Results:"
echo "- Unit Tests: $RESULTS_DIR/unit/"
echo "- Integration Tests: $RESULTS_DIR/integration/"
echo "- Combined Coverage: $RESULTS_DIR/combined/coverage/report/"
echo "==================================="

exit $OVERALL_RESULT