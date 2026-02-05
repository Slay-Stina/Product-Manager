#!/bin/bash
# Database Query Pattern Analyzer for Product Manager
# Identifies potential performance issues in EF Core usage

set -e

REPO_ROOT="/home/runner/work/Product-Manager/Product-Manager"
cd "$REPO_ROOT"

echo "🔍 Analyzing Product Manager Database Query Patterns..."
echo "=================================================="

WARNINGS=0

# Pattern 1: Missing AsNoTracking on read operations
echo ""
echo "📋 Pattern 1: Read-only queries should use AsNoTracking()"

# Find FirstOrDefault, ToList, etc. without AsNoTracking
QUERIES_WITHOUT_TRACKING=$(grep -r "\.FirstOrDefault\|\.ToList\|\.SingleOrDefault" \
    Product-Manager/Services --include="*.cs" 2>/dev/null \
    | grep -v "AsNoTracking\|SaveChanges\|Add\|Update\|Remove" \
    | wc -l)

if [ "$QUERIES_WITHOUT_TRACKING" -gt 3 ]; then
    echo "   ⚠️  Found $QUERIES_WITHOUT_TRACKING queries potentially missing AsNoTracking()"
    echo "      Sample locations:"
    grep -rn "\.FirstOrDefault\|\.ToList\|\.SingleOrDefault" \
        Product-Manager/Services --include="*.cs" 2>/dev/null \
        | grep -v "AsNoTracking\|SaveChanges\|Add\|Update\|Remove" \
        | head -3 \
        | sed 's/^/      /'
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ Query tracking usage looks reasonable ($QUERIES_WITHOUT_TRACKING potential issues)"
fi

# Pattern 2: N+1 queries (database calls inside loops)
echo ""
echo "📋 Pattern 2: Potential N+1 query patterns"

N_PLUS_ONE_PATTERNS=$(grep -A 5 "foreach\|for (" Product-Manager/Services/*.cs 2>/dev/null \
    | grep "_context\.\|_dbContext\." \
    | wc -l)

if [ "$N_PLUS_ONE_PATTERNS" -gt 0 ]; then
    echo "   ⚠️  Found $N_PLUS_ONE_PATTERNS potential N+1 patterns"
    echo "      Database operations inside loops detected:"
    grep -B 2 -A 3 "foreach\|for (" Product-Manager/Services/*.cs 2>/dev/null \
        | grep -A 3 "_context\.\|_dbContext\." \
        | head -5 \
        | sed 's/^/      /'
    echo "      Consider using .Include() for eager loading"
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ No obvious N+1 patterns detected"
fi

# Pattern 3: Missing pagination on large result sets
echo ""
echo "📋 Pattern 3: Large result sets should be paginated"

UNPAGINATED_LISTS=$(grep -rn "\.ToList()" Product-Manager/Services --include="*.cs" 2>/dev/null \
    | grep -v "Take\|Skip\|AsNoTracking" \
    | wc -l)

if [ "$UNPAGINATED_LISTS" -gt 5 ]; then
    echo "   ⚠️  Found $UNPAGINATED_LISTS .ToList() calls without obvious pagination"
    echo "      Consider adding .Take() or pagination for large collections:"
    grep -rn "\.ToList()" Product-Manager/Services --include="*.cs" 2>/dev/null \
        | grep -v "Take\|Skip" \
        | head -3 \
        | sed 's/^/      /'
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ Pagination usage looks reasonable"
fi

# Pattern 4: Image data loading strategy
echo ""
echo "📋 Pattern 4: Binary data (images) loading patterns"

IMAGE_LOADING=$(grep -rn "ImageData" Product-Manager/Services --include="*.cs" 2>/dev/null \
    | grep "Select\|Include" \
    | wc -l)

if [ "$IMAGE_LOADING" -eq 0 ]; then
    echo "   ⚠️  WARNING: ImageData might be loaded eagerly without projection"
    echo "      Consider using .Select() to exclude ImageData in list queries"
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ Image data appears to use projection ($IMAGE_LOADING instances)"
fi

# Pattern 5: SaveChanges in loops
echo ""
echo "📋 Pattern 5: Batch operations check"

SAVE_IN_LOOPS=$(grep -A 10 "foreach\|for (" Product-Manager/Services/*.cs 2>/dev/null \
    | grep "SaveChanges" \
    | wc -l)

if [ "$SAVE_IN_LOOPS" -gt 0 ]; then
    echo "   ⚠️  Found SaveChanges() inside loops ($SAVE_IN_LOOPS instances)"
    echo "      Consider batching database writes:"
    grep -B 5 "SaveChanges" Product-Manager/Services/*.cs 2>/dev/null \
        | grep -B 5 "foreach\|for (" \
        | tail -8 \
        | sed 's/^/      /'
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ No SaveChanges in loops detected"
fi

# Pattern 6: Connection management
echo ""
echo "📋 Pattern 6: Database connection management"

MANUAL_CONNECTIONS=$(grep -r "new SqlConnection\|new SqlCommand" Product-Manager/ --include="*.cs" 2>/dev/null | wc -l)

if [ "$MANUAL_CONNECTIONS" -gt 0 ]; then
    echo "   ⚠️  WARNING: Manual SQL connections detected ($MANUAL_CONNECTIONS instances)"
    echo "      EF Core DbContext should handle connection management"
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ Using EF Core for connection management"
fi

# Summary with recommendations
echo ""
echo "=================================================="
if [ $WARNINGS -eq 0 ]; then
    echo "✅ Database query patterns look good!"
    echo ""
    echo "Keep maintaining these practices:"
    echo "  • Use AsNoTracking() for read-only queries"
    echo "  • Eager load with Include() to avoid N+1"
    echo "  • Paginate large result sets"
    echo "  • Use projections for binary data"
    exit 0
else
    echo "⚠️  Found $WARNINGS potential query optimization opportunities"
    echo ""
    echo "These are suggestions, not blockers. Consider:"
    echo "  • Add AsNoTracking() to read queries"
    echo "  • Use Include() for related data"
    echo "  • Implement pagination for lists"
    echo "  • Batch database operations"
    echo ""
    echo "See EF Core performance docs for guidance."
    exit 0  # Exit 0 because these are warnings, not errors
fi
