# GitHub Actions Workflows

This directory contains GitHub Actions workflows for continuous integration and code quality checks.

## Workflows

### PR Status Check (`pr-check.yml`)

This workflow runs automatically on every pull request and push to main/master branches.

#### Jobs

1. **Build and Test**
   - Checks out the code
   - Sets up .NET 9.0
   - Restores NuGet packages
   - Builds the project in Release configuration
   - Checks for vulnerable dependencies

2. **Code Quality Checks**
   - Verifies code formatting using `dotnet format`
   - Ensures consistent code style across the project

3. **Security Scan**
   - Runs CodeQL analysis to detect security vulnerabilities
   - Scans for common security issues in C# code
   - Reports findings as GitHub Security alerts

4. **Status Summary**
   - Aggregates results from all jobs
   - Provides a single status check for the PR
   - Fails if any critical checks fail

## Status Checks

All pull requests must pass the following checks before merging:

- ✅ **Build succeeds** - Code compiles without errors
- ✅ **No critical vulnerabilities** - Dependency scan passes
- ✅ **CodeQL security scan passes** - No security issues detected
- ⚠️ **Code formatting** - Warning only, but should follow .NET conventions

## Running Checks Locally

Before pushing code, you can run these checks locally:

```bash
# Restore and build
cd Product-Manager
dotnet restore
dotnet build --configuration Release

# Check for vulnerabilities
dotnet list package --vulnerable --include-transitive

# Format code
dotnet format
```

## Workflow Configuration

The workflow is triggered on:
- Pull requests targeting `main` or `master` branches
- Direct pushes to `main` or `master` branches

### Required Permissions

The workflow requires these GitHub permissions:
- `contents: read` - To check out the repository
- `security-events: write` - To upload CodeQL results
- `pull-requests: read` - To comment on PRs (future enhancement)

## Troubleshooting

### Build Failures
- Ensure all dependencies are properly referenced in the `.csproj` file
- Check that the .NET version matches the project's target framework
- Review build logs for specific compilation errors

### Security Scan Issues
- CodeQL may take several minutes to complete
- Review security alerts in the GitHub Security tab
- False positives can be dismissed with justification

### Code Formatting
- Run `dotnet format` to auto-fix most formatting issues
- Configure your IDE to follow .editorconfig settings (if present)

## Future Enhancements

Potential improvements to the CI pipeline:
- Add unit test execution when tests are added to the project
- Implement test coverage reporting
- Add integration tests
- Deploy preview environments for PRs
- Automated dependency updates
- Performance testing
