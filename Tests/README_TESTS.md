# Test Suite for Git-Style Backup Explorer

## Overview
This test suite validates the recent changes to the restore functionality, including:
- Phase 1 skip logic when resuming
- Stale restore folder cleanup (24-hour threshold)
- Version file caching performance

## Test Files

### 1. ResumableRestoreServiceTests.cs
Tests for the core restore service functionality:
- ✅ Phase 1 skip when already complete
- ✅ Phase 2 detection and skip logic
- ✅ Pause/Resume functionality
- ✅ Status tracking for Phase 1 and Phase 2
- ✅ File completion marking

### 2. Form1RestoreLogicTests.cs
Tests for the cleanup logic in Form1:
- ✅ Recent folders (< 24 hours) are NOT deleted
- ✅ Old folders (> 24 hours) ARE deleted
- ✅ Multiple folders handled correctly
- ✅ Non-matching folders ignored
- ✅ Empty directory handling

### 3. VersionFileCacheTests.cs
Tests for version file caching:
- ✅ First access reads from disk
- ✅ Second access uses cache
- ✅ Multiple versions cached separately
- ✅ Cache clearing works
- ✅ Thread-safe concurrent access
- ✅ Performance improvement verification

## Setup Instructions

### Option 1: Visual Studio Test Explorer
1. Open the solution in Visual Studio
2. Right-click on the solution → Add → Existing Project
3. Select `Tests\gitstylebackupexplorer.Tests.csproj`
4. Build the solution
5. Open Test Explorer (Test → Test Explorer)
6. Click "Run All Tests"

### Option 2: NuGet Package Manager
1. Right-click on the Tests project → Manage NuGet Packages
2. Restore packages (xUnit should auto-restore)
3. Build the test project
4. Run tests from Test Explorer

### Option 3: Command Line (if dotnet CLI supports .NET Framework)
```powershell
cd Tests
dotnet restore
dotnet test
```

## Expected Results
All tests should pass (green checkmarks):
- ResumableRestoreServiceTests: 8 tests
- Form1RestoreLogicTests: 6 tests
- VersionFileCacheTests: 7 tests

**Total: 21 tests**

## Test Coverage

### Critical Scenarios Covered
1. **Concurrent Restore Safety**: Ensures new restores don't delete active restore folders
2. **Resume Functionality**: Verifies Phase 1 skip works when resuming
3. **Cache Performance**: Confirms caching improves performance
4. **Thread Safety**: Validates concurrent access to cache is safe

### Manual Testing Still Needed
- Full end-to-end restore with real backup data
- Multiple restore windows running simultaneously
- Resume after application crash
- UI interactions (buttons, progress bars)

## Troubleshooting

### Tests Won't Run
- Ensure xUnit NuGet packages are restored
- Check that the main project builds successfully
- Verify .NET Framework 4.5.2 is installed

### Tests Fail
- Check temp folder permissions
- Ensure no antivirus blocking file operations
- Review test output for specific error messages

## Adding New Tests
To add new tests:
1. Create a new class in the Tests folder
2. Inherit from `IDisposable` for cleanup
3. Use `[Fact]` attribute for test methods
4. Follow AAA pattern: Arrange, Act, Assert
