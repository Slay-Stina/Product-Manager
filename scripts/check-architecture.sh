#!/bin/bash
# Architecture Boundary Validator for Product Manager
# Ensures code respects architectural layer separation

set -e

REPO_ROOT="/home/runner/work/Product-Manager/Product-Manager"
cd "$REPO_ROOT"

echo "🏗️  Validating Product Manager Architecture Boundaries..."
echo "=================================================="

VIOLATIONS=0

# Rule 1: Services layer should not import UI Components
echo ""
echo "📋 Rule 1: Services must not reference UI Components"
if find Product-Manager/Services -name "*.cs" -exec grep -l "using Product_Manager.Components" {} \; 2>/dev/null | grep -q .; then
    echo "   ❌ VIOLATION DETECTED:"
    find Product-Manager/Services -name "*.cs" -exec grep -l "using Product_Manager.Components" {} \; | sed 's/^/      - /'
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ Services layer is clean"
fi

# Rule 2: Data layer should not reference Services
echo ""
echo "📋 Rule 2: Data layer must not reference Services"
if find Product-Manager/Data -name "*.cs" -exec grep -l "using Product_Manager.Services" {} \; 2>/dev/null | grep -q .; then
    echo "   ❌ VIOLATION DETECTED:"
    find Product-Manager/Data -name "*.cs" -exec grep -l "using Product_Manager.Services" {} \; | sed 's/^/      - /'
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ Data layer is independent"
fi

# Rule 3: Middleware should not directly instantiate Services
echo ""
echo "📋 Rule 3: Middleware must not directly couple to business Services"
if grep -r "new ProductCrawlerService\|ProductCrawlerService.*=" Product-Manager/Middleware/*.cs 2>/dev/null | grep -v "//" | grep -q .; then
    echo "   ❌ VIOLATION DETECTED:"
    grep -r "new ProductCrawlerService\|ProductCrawlerService.*=" Product-Manager/Middleware/*.cs | sed 's/^/      - /'
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ Middleware properly isolated"
fi

# Rule 4: Check that DbContext is only in Data layer (except registrations)
echo ""
echo "📋 Rule 4: DbContext usage should be centralized"
DBCONTEXT_VIOLATIONS=$(grep -r "ApplicationDbContext" Product-Manager/ \
    --include="*.cs" \
    --exclude-dir=Data \
    --exclude-dir=Migrations \
    | grep -v "AddDbContext\|Program.cs\|\.Services" \
    | wc -l)

if [ "$DBCONTEXT_VIOLATIONS" -gt 0 ]; then
    echo "   ⚠️  WARNING: DbContext used outside expected locations ($DBCONTEXT_VIOLATIONS instances)"
    echo "      Review if these are appropriate:"
    grep -r "ApplicationDbContext" Product-Manager/ \
        --include="*.cs" \
        --exclude-dir=Data \
        --exclude-dir=Migrations \
        | grep -v "AddDbContext\|Program.cs\|\.Services" \
        | head -3 \
        | sed 's/^/      - /'
else
    echo "   ✅ DbContext properly encapsulated"
fi

# Summary
echo ""
echo "=================================================="
if [ $VIOLATIONS -eq 0 ]; then
    echo "✅ All architecture boundaries validated successfully!"
    echo ""
    exit 0
else
    echo "❌ Found $VIOLATIONS architecture violation(s)"
    echo ""
    echo "Please fix violations before merging."
    echo "See docs/architecture/architecture-governance.md for details."
    exit 1
fi
