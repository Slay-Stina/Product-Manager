# CI/CD Pipeline Overview

```mermaid
flowchart TD
    Start([PR Status Check Workflow<br/>Triggers: PR or Push to main/master])
    Start --> Parallel[Parallel Job Execution]
    
    Parallel --> Build[Build & Test<br/>✅ Required]
    Parallel --> Quality[Code Quality<br/>⚠️ Warning]
    Parallel --> Security[Security Scan<br/>✅ Required]
    Parallel --> Arch[Architecture<br/>✅ Required]
    
    Build --> B1[1. Checkout Code]
    B1 --> B2[2. Setup .NET 9.0]
    B2 --> B3[3. Restore Dependencies]
    B3 --> B4[4. Build Release]
    B4 --> B5[5. Check Vulnerabilities]
    
    Quality --> Q1[1. Checkout Code]
    Q1 --> Q2[2. Setup .NET 9.0]
    Q2 --> Q3[3. Install dotnet-format]
    Q3 --> Q4[4. Check Formatting]
    
    Security --> S1[1. Checkout Code]
    S1 --> S2[2. Init CodeQL]
    S2 --> S3[3. Setup .NET 9.0]
    S3 --> S4[4. Build for Analysis]
    S4 --> S5[5. CodeQL Analyze]
    
    Arch --> A1[1. Checkout Code]
    A1 --> A2[2. Check Boundaries]
    A2 --> A3[3. Check Security]
    A3 --> A4[4. Check DB Queries]
    
    B5 --> Summary
    Q4 --> Summary
    S5 --> Summary
    A4 --> Summary
    
    Summary[Status Summary<br/>Aggregate Results]
    
    Summary --> Check{All Required<br/>Checks Pass?}
    Check -->|Yes| Pass[✅ PASS<br/>PR can be merged]
    Check -->|No| Fail[❌ FAIL<br/>PR blocked<br/>Fix issues required]
    
    style Build fill:#90EE90
    style Security fill:#90EE90
    style Arch fill:#90EE90
    style Quality fill:#FFE4B5
    style Pass fill:#90EE90
    style Fail fill:#FFB6C1
    style Summary fill:#87CEEB
```

## Job Details

### 1. Build and Test ✅ (Required)
- **Purpose**: Verify code compiles and is buildable
- **Duration**: ~30-60 seconds
- **Actions**:
  - Restore NuGet packages
  - Build in Release configuration
  - Check for vulnerable dependencies
- **Failure Impact**: Blocks PR merge

### 2. Code Quality ⚠️ (Warning)
- **Purpose**: Ensure code follows formatting standards
- **Duration**: ~20-40 seconds  
- **Actions**:
  - Install dotnet-format tool
  - Verify code formatting
- **Failure Impact**: Warning only, does not block merge

### 3. Security Scan ✅ (Required)
- **Purpose**: Detect security vulnerabilities
- **Duration**: ~2-5 minutes
- **Actions**:
  - Initialize CodeQL
  - Build with instrumentation
  - Analyze for vulnerabilities
- **Failure Impact**: Blocks PR merge

### 4. Architecture Validation ✅ (Required)
- **Purpose**: Enforce architectural constraints and patterns
- **Duration**: ~10-20 seconds
- **Actions**:
  - Check architecture boundaries (layer dependencies)
  - Validate security patterns (credentials, middleware order)
  - Analyze database query patterns (performance)
- **Failure Impact**: Blocks PR merge on critical violations

### 5. Status Summary
- **Purpose**: Aggregate results and provide final verdict
- **Duration**: ~5-10 seconds
- **Actions**:
  - Check all job statuses
  - Report final pass/fail
- **Failure Impact**: Overall PR status

## Local Development

Run checks before pushing:

```bash
# Run all pre-commit checks
./check-pr.sh

# Run architecture validation only
bash scripts/check-architecture.sh
bash scripts/check-security-patterns.sh
bash scripts/check-database-queries.sh
```

This runs the same validations locally, helping catch issues early.
