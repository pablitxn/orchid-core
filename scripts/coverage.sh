#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$SCRIPT_DIR/../services/backend"
RESULTS_DIR="$BACKEND_DIR/TestResults"

echo -e "${YELLOW}Running tests with coverage...${NC}"

# Clean previous results
rm -rf "$RESULTS_DIR"

# Run tests with coverage
cd "$BACKEND_DIR"
dotnet test \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --settings coverlet.runsettings \
    --results-directory ./TestResults \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

# Check if reportgenerator is installed
if ! command -v reportgenerator &> /dev/null; then
    echo -e "${YELLOW}Installing ReportGenerator...${NC}"
    dotnet tool install --global dotnet-reportgenerator-globaltool
fi

# Generate HTML report
echo -e "${YELLOW}Generating coverage report...${NC}"
reportgenerator \
    -reports:"./TestResults/**/coverage.cobertura.xml" \
    -targetdir:"./TestResults/CoverageReport" \
    -reporttypes:"Html;TextSummary"

# Display summary
echo ""
echo -e "${GREEN}=== Coverage Summary ===${NC}"
cat ./TestResults/CoverageReport/Summary.txt

# Extract and check threshold
COVERAGE=$(grep "Line coverage" ./TestResults/CoverageReport/Summary.txt | grep -oP '\d+\.?\d*(?=%)' | head -1)
THRESHOLD=40

echo ""
if (( $(echo "$COVERAGE < $THRESHOLD" | bc -l) )); then
    echo -e "${RED}FAIL: Coverage ${COVERAGE}% is below threshold ${THRESHOLD}%${NC}"
    exit 1
else
    echo -e "${GREEN}PASS: Coverage ${COVERAGE}% meets threshold ${THRESHOLD}%${NC}"
fi

echo ""
echo -e "${GREEN}HTML report available at:${NC}"
echo "file://$RESULTS_DIR/CoverageReport/index.html"
