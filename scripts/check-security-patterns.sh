#!/bin/bash
# Security Pattern Validator for Product Manager
# Ensures critical security patterns are maintained

set -e

REPO_ROOT="/home/runner/work/Product-Manager/Product-Manager"
cd "$REPO_ROOT"

echo "🔒 Validating Product Manager Security Patterns..."
echo "=================================================="

VIOLATIONS=0
WARNINGS=0

# Rule 1: No hardcoded crawler credentials
echo ""
echo "📋 Rule 1: Crawler credentials must not be hardcoded"
HARDCODED=$(grep -E '"(Username|Password)":\s*"[^"]{3,}"' Product-Manager/appsettings.json | grep -v '""' | grep -v "your-username\|your-password" || true)
if [ ! -z "$HARDCODED" ]; then
    echo "   ❌ VIOLATION: Credentials appear to be hardcoded:"
    echo "$HARDCODED" | sed 's/^/      /'
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ No hardcoded credentials found"
fi

# Rule 2: Security middleware order in Program.cs
echo ""
echo "📋 Rule 2: Security middleware must be properly ordered"

# Extract middleware registrations
MIDDLEWARE_SECTION=$(sed -n '/var app = builder.Build/,/app.Run/p' Product-Manager/Program.cs)

# Check HTTPS redirection exists
if ! echo "$MIDDLEWARE_SECTION" | grep -q "UseHttpsRedirection"; then
    echo "   ❌ VIOLATION: HTTPS redirection not configured"
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ HTTPS redirection enabled"
fi

# Check security headers exist
if ! echo "$MIDDLEWARE_SECTION" | grep -q "UseSecurityHeaders"; then
    echo "   ❌ VIOLATION: Security headers middleware missing"
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ Security headers configured"
fi

# Check rate limiting exists
if ! echo "$MIDDLEWARE_SECTION" | grep -q "UseRateLimiting"; then
    echo "   ❌ VIOLATION: Rate limiting not configured"
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ Rate limiting active"
fi

# Check antiforgery protection
if ! echo "$MIDDLEWARE_SECTION" | grep -q "UseAntiforgery"; then
    echo "   ⚠️  WARNING: Antiforgery protection not found"
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ Antiforgery protection enabled"
fi

# Rule 3: Password policy enforcement
echo ""
echo "📋 Rule 3: Strong password policy must be configured"
PASSWORD_CONFIG=$(grep -A 10 "options.Password" Product-Manager/Program.cs || echo "")
if echo "$PASSWORD_CONFIG" | grep -q "RequiredLength.*8"; then
    echo "   ✅ Minimum 8 character password required"
else
    echo "   ⚠️  WARNING: Password length requirement not set to 8+"
    WARNINGS=$((WARNINGS + 1))
fi

if echo "$PASSWORD_CONFIG" | grep -q "RequireNonAlphanumeric.*true"; then
    echo "   ✅ Special characters required"
else
    echo "   ⚠️  WARNING: Special character requirement not enforced"
    WARNINGS=$((WARNINGS + 1))
fi

# Rule 4: No SQL injection vulnerabilities (check for string concatenation with SQL)
echo ""
echo "📋 Rule 4: SQL injection prevention"
SQL_CONCAT=$(grep -r "\"SELECT.*\+\|\"INSERT.*\+\|\"UPDATE.*\+\|\"DELETE.*\+" Product-Manager/Services Product-Manager/Data --include="*.cs" 2>/dev/null || true)
if [ ! -z "$SQL_CONCAT" ]; then
    echo "   ❌ VIOLATION: Potential SQL concatenation detected:"
    echo "$SQL_CONCAT" | head -3 | sed 's/^/      /'
    VIOLATIONS=$((VIOLATIONS + 1))
else
    echo "   ✅ No obvious SQL concatenation found"
fi

# Rule 5: Sensitive data not logged
echo ""
echo "📋 Rule 5: Sensitive data logging check"
SENSITIVE_LOGGING=$(grep -r "_logger.*Password\|_logger.*Token\|_logger.*Secret" Product-Manager/ --include="*.cs" 2>/dev/null || true)
if [ ! -z "$SENSITIVE_LOGGING" ]; then
    echo "   ⚠️  WARNING: Potentially logging sensitive data:"
    echo "$SENSITIVE_LOGGING" | head -2 | sed 's/^/      /'
    WARNINGS=$((WARNINGS + 1))
else
    echo "   ✅ No obvious sensitive data logging"
fi

# Summary
echo ""
echo "=================================================="
if [ $VIOLATIONS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
    echo "✅ All security patterns validated successfully!"
    echo ""
    exit 0
elif [ $VIOLATIONS -eq 0 ]; then
    echo "✅ No critical violations found"
    echo "⚠️  $WARNINGS warning(s) detected - please review"
    echo ""
    exit 0
else
    echo "❌ Found $VIOLATIONS security violation(s)"
    if [ $WARNINGS -gt 0 ]; then
        echo "⚠️  Also found $WARNINGS warning(s)"
    fi
    echo ""
    echo "Security violations must be fixed before merging!"
    echo "See SECURITY.md and docs/architecture/architecture-governance.md"
    exit 1
fi
