#!/bin/bash
# Local PR Check Script
# This script simulates the GitHub Actions PR checks locally

set -e

echo "========================================="
echo "Running Local PR Status Checks"
echo "========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

cd "$(dirname "$0")"

echo "📦 Step 1: Restoring dependencies..."
if dotnet restore Product-Manager/Product-Manager.csproj; then
    echo -e "${GREEN}✅ Dependencies restored successfully${NC}"
else
    echo -e "${RED}❌ Failed to restore dependencies${NC}"
    exit 1
fi
echo ""

echo "🔨 Step 2: Building project..."
if dotnet build Product-Manager/Product-Manager.csproj --no-restore --configuration Release; then
    echo -e "${GREEN}✅ Build succeeded${NC}"
else
    echo -e "${RED}❌ Build failed${NC}"
    exit 1
fi
echo ""

echo "🔍 Step 3: Checking for vulnerabilities..."
if dotnet list Product-Manager/Product-Manager.csproj package --vulnerable --include-transitive 2>&1 | tee /tmp/vuln-check.log; then
    if grep -q "has the following vulnerable packages" /tmp/vuln-check.log; then
        echo -e "${YELLOW}⚠️  Vulnerabilities found - review above${NC}"
    else
        echo -e "${GREEN}✅ No vulnerabilities detected${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  Vulnerability check completed with warnings${NC}"
fi
echo ""

echo "✨ Step 4: Checking code formatting..."
if command -v dotnet-format &> /dev/null || dotnet tool list -g | grep -q dotnet-format; then
    if dotnet format Product-Manager/Product-Manager.csproj --verify-no-changes 2>&1; then
        echo -e "${GREEN}✅ Code formatting is correct${NC}"
    else
        echo -e "${YELLOW}⚠️  Code formatting issues detected. Run 'dotnet format' to fix${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  dotnet-format not installed. Run: dotnet tool install -g dotnet-format${NC}"
fi
echo ""

echo "========================================="
echo -e "${GREEN}✅ All checks completed!${NC}"
echo "========================================="
echo ""
echo "Your code is ready for PR submission."
echo "The GitHub Actions workflow will run these same checks automatically."
