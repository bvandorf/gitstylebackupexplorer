# Manual Test Script for Recent Changes

## Test 1: Stale Folder Cleanup (24-Hour Logic)

### Setup
1. Create a test destination folder: `C:\TestRestore`
2. Manually create old restore folders:
   ```powershell
   mkdir "C:\TestRestore\.BackupRestore_Old1"
   mkdir "C:\TestRestore\.BackupRestore_Old2"
   ```
3. Set their timestamps to 25 hours ago:
   ```powershell
   $oldTime = (Get-Date).AddHours(-25)
   (Get-Item "C:\TestRestore\.BackupRestore_Old1").LastWriteTime = $oldTime
   (Get-Item "C:\TestRestore\.BackupRestore_Old2").LastWriteTime = $oldTime
   ```
4. Create a recent folder:
   ```powershell
   mkdir "C:\TestRestore\.BackupRestore_Recent"
   ```

### Test Steps
1. Open Git-Style Backup Explorer
2. Open a backup database
3. Select a directory to restore
4. Choose `C:\TestRestore` as destination
5. Start the restore

### Expected Results
- ✅ Old folders (Old1, Old2) should be deleted
- ✅ Recent folder should remain
- ✅ Debug output should show: "Cleaned up 2 old restore folder(s)."
- ✅ Restore should proceed normally

---

## Test 2: Multiple Concurrent Restores

### Test Steps
1. Open Git-Style Backup Explorer
2. Open a backup database
3. Start restore #1 to `C:\TestRestore1`
4. While restore #1 is running, start restore #2 to `C:\TestRestore1` (same destination)

### Expected Results
- ✅ Both restores should run independently
- ✅ Restore #2 should NOT delete restore #1's temp folder
- ✅ Each should have its own `.BackupRestore_GUID` folder
- ✅ Both should complete successfully

---

## Test 3: Resume After Phase 1 Complete

### Test Steps
1. Start a large directory restore
2. Wait for Phase 1 to complete (all files copied to temp)
3. Click "Cancel" or close the window
4. Use "Resume from Folder" menu option
5. Select the destination folder containing the `.BackupRestore_` folder
6. Confirm resume

### Expected Results
- ✅ Phase 1 should be skipped (not re-copying files)
- ✅ Progress should start directly at Phase 2
- ✅ Status should show "Phase 2: Unzipping files..."
- ✅ Restore should complete successfully

---

## Test 4: Version File Cache Performance

### Test Steps
1. Open a backup database
2. In the tree view, click on a version node to expand it
3. Note the time it takes to load
4. Collapse the node
5. Re-expand the same node

### Expected Results
- ✅ First expansion: Normal loading time
- ✅ Second expansion: Noticeably faster (cached)
- ✅ No errors or crashes

### Verification
Check memory usage doesn't grow excessively:
1. Open Task Manager
2. Find `gitstylebackupexplorer.exe`
3. Expand/collapse many different versions
4. Memory should stabilize (cache is cleared when opening new backup)

---

## Test 5: Cache Clearing on New Backup

### Test Steps
1. Open backup database #1
2. Expand several version nodes (populates cache)
3. Open backup database #2 (different backup)
4. Check memory usage

### Expected Results
- ✅ Cache should be cleared when opening new backup
- ✅ Memory should drop back to baseline
- ✅ No memory leak over multiple backup opens

---

## Test 6: Restart Button (Existing Feature)

### Test Steps
1. Start a restore
2. Let it run for a few seconds
3. Click "Restart" button

### Expected Results
- ✅ Confirmation dialog appears
- ✅ Current operation cancels
- ✅ New temp folder created
- ✅ Restore starts fresh from beginning
- ✅ Old temp folder remains (not deleted if < 24 hours)

---

## Test 7: Error Handling - Locked Folder

### Setup
1. Create a restore folder: `C:\TestRestore\.BackupRestore_Test`
2. Open a file in that folder with Notepad (to lock it)
3. Set folder timestamp to 25 hours ago

### Test Steps
1. Start a new restore to `C:\TestRestore`

### Expected Results
- ✅ Cleanup should fail silently for locked folder
- ✅ Debug output: "1 could not be removed - may be in use"
- ✅ Restore should still proceed
- ✅ No crash or error dialog

---

## Performance Benchmarks

### Cache Performance Test
**Objective**: Verify cache provides 50%+ speed improvement

1. Open a backup with large version files (1000+ files per version)
2. Time first expansion: `_______ ms`
3. Time second expansion: `_______ ms`
4. Improvement: `_______ %`

**Expected**: Second expansion should be at least 50% faster

---

## Regression Tests

Verify existing functionality still works:

- ✅ Single file restore
- ✅ Directory restore
- ✅ Pause/Resume during restore
- ✅ Cancel restore
- ✅ Resume from folder (crash recovery)
- ✅ Info dialog shows correct file details
- ✅ Tree view navigation
- ✅ Multiple restore windows

---

## Sign-Off

**Tester**: _________________  
**Date**: _________________  
**All Tests Passed**: ☐ Yes ☐ No  
**Issues Found**: _________________
