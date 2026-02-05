# CI/CD Pipeline Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    PR Status Check Workflow                  │
│                                                              │
│  Triggers: Pull Request or Push to main/master             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
        ┌────────────────────────────────────┐
        │    Parallel Job Execution          │
        └────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│  Build & Test   │  │  Code Quality   │  │ Security Scan   │
│                 │  │                 │  │                 │
│ ✅ Required     │  │ ⚠️  Warning     │  │ ✅ Required     │
└─────────────────┘  └─────────────────┘  └─────────────────┘
         │                    │                    │
         │  ┌─────────────────┤                    │
         │  │                 │                    │
         │  │  ┌─ Checkout    │  ┌─ Checkout      │
         │  │  │               │  │                │
         │  │  ├─ Setup .NET   │  ├─ Setup .NET   │
         │  │  │               │  │                │
         │  │  ├─ Install      │  ├─ Init CodeQL  │
         │  │  │   dotnet-     │  │                │
         │  │  │   format      │  ├─ Build        │
         │  │  │               │  │                │
         │  │  └─ Check        │  └─ Analyze      │
         │  │      Formatting  │                   │
         │  │                  │                   │
         ├─ Checkout           │                   │
         │                     │                   │
         ├─ Setup .NET         │                   │
         │                     │                   │
         ├─ Restore            │                   │
         │   Dependencies      │                   │
         │                     │                   │
         ├─ Build Project      │                   │
         │   (Release)         │                   │
         │                     │                   │
         └─ Check              │                   │
             Vulnerabilities   │                   │
                              │                    │
         └────────────────────┴────────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │ Status Summary  │
                    │                 │
                    │ Aggregates all  │
                    │ job results     │
                    └─────────────────┘
                              │
                ┌─────────────┴─────────────┐
                │                           │
                ▼                           ▼
         ┌──────────┐              ┌──────────────┐
         │   PASS   │              │     FAIL     │
         │    ✅     │              │      ❌      │
         │          │              │              │
         │ PR can   │              │ PR blocked,  │
         │ be       │              │ fix issues   │
         │ merged   │              │ required     │
         └──────────┘              └──────────────┘
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

### 4. Status Summary
- **Purpose**: Aggregate results and provide final verdict
- **Duration**: ~5-10 seconds
- **Actions**:
  - Check all job statuses
  - Report final pass/fail
- **Failure Impact**: Overall PR status

## Local Development

Run checks before pushing:

```bash
./check-pr.sh
```

This runs the same validations locally, helping catch issues early.
