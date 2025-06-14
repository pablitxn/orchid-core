#!/bin/bash

# Script to run all integration tests in the backend project

set -e

echo "==================================="
echo "Running Backend Integration Tests"
echo "==================================="

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Error: Docker is not running!${NC}"
    echo "Please start Docker before running integration tests."
    exit 1
fi

# Start time
START_TIME=$(date +%s)

# Test results directory
RESULTS_DIR="test-results/integration"
mkdir -p $RESULTS_DIR

echo -e "${YELLOW}Starting integration tests...${NC}"
echo ""

# Get the directory where the script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Start test dependencies
echo -e "${BLUE}Starting test containers...${NC}"
# Check if docker-compose.test.yml exists in current dir or parent
if [ -f "docker-compose.test.yml" ]; then
    docker-compose -f docker-compose.test.yml up -d
elif [ -f "../../docker-compose.test.yml" ]; then
    docker-compose -f ../../docker-compose.test.yml up -d
else
    echo -e "${YELLOW}Warning: docker-compose.test.yml not found${NC}"
fi

# Wait for services to be ready
echo -e "${BLUE}Waiting for services to be ready...${NC}"
if [ -f "./scripts/wait-for-services.sh" ]; then
    ./scripts/wait-for-services.sh
elif [ -f "../../scripts/wait-for-services.sh" ]; then
    ../../scripts/wait-for-services.sh
else
    echo -e "${YELLOW}Note: wait-for-services.sh not found. Waiting 30 seconds...${NC}"
    sleep 30
fi

# Run integration tests with detailed output
echo -e "${YELLOW}Running tests...${NC}"
dotnet test Backend.sln \
    --filter "FullyQualifiedName~IntegrationTests" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=$RESULTS_DIR/integration-test-results.trx" \
    --logger "html;LogFileName=$RESULTS_DIR/integration-test-results.html" \
    --collect:"XPlat Code Coverage" \
    --results-directory $RESULTS_DIR \
    --blame \
    --diag $RESULTS_DIR/diagnostics.log \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput=$RESULTS_DIR/coverage/

# Check test result
TEST_RESULT=$?

# Stop test containers
echo ""
echo -e "${BLUE}Stopping test containers...${NC}"
if [ -f "docker-compose.test.yml" ]; then
    docker-compose -f docker-compose.test.yml down
elif [ -f "../../docker-compose.test.yml" ]; then
    docker-compose -f ../../docker-compose.test.yml down
fi

# End time
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo ""
echo "==================================="
if [ $TEST_RESULT -eq 0 ]; then
    echo -e "${GREEN}✓ Integration tests completed successfully!${NC}"
else
    echo -e "${RED}✗ Integration tests failed!${NC}"
    echo -e "${YELLOW}Check diagnostics log at: $RESULTS_DIR/diagnostics.log${NC}"
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