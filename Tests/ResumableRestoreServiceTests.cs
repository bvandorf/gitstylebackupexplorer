using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using gitstylebackupexplorer.Models;
using gitstylebackupexplorer.Services;
using Xunit;

namespace gitstylebackupexplorer.Tests
{
    /// <summary>
    /// Tests for ResumableRestoreService focusing on Phase 1 skip logic and resume functionality
    /// </summary>
    public class ResumableRestoreServiceTests : IDisposable
    {
        private readonly string _testBackupFolder;
        private readonly string _testFilesFolder;
        private readonly string _testVersionFolder;
        private readonly string _testDestinationFolder;
        private readonly string _testTempRestoreFolder;

        public ResumableRestoreServiceTests()
        {
            // Create temporary test directories
            _testBackupFolder = Path.Combine(Path.GetTempPath(), "BackupTest_" + Guid.NewGuid().ToString());
            _testFilesFolder = Path.Combine(_testBackupFolder, "Files");
            _testVersionFolder = Path.Combine(_testBackupFolder, "Version");
            _testDestinationFolder = Path.Combine(Path.GetTempPath(), "RestoreTest_" + Guid.NewGuid().ToString());
            _testTempRestoreFolder = Path.Combine(_testDestinationFolder, ".BackupRestore_" + Guid.NewGuid().ToString());

            Directory.CreateDirectory(_testFilesFolder);
            Directory.CreateDirectory(_testVersionFolder);
            Directory.CreateDirectory(_testDestinationFolder);
            Directory.CreateDirectory(_testTempRestoreFolder);
        }

        public void Dispose()
        {
            // Cleanup test directories
            try
            {
                if (Directory.Exists(_testBackupFolder))
                    Directory.Delete(_testBackupFolder, true);
                if (Directory.Exists(_testDestinationFolder))
                    Directory.Delete(_testDestinationFolder, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        [Fact]
        public void Phase1Skip_WhenPhase1Complete_ShouldSkipPhase1()
        {
            // Arrange
            var statusTracker = new RestoreStatusTracker(_testTempRestoreFolder);
            string nodeVersion = "test_version_1";
            string nodeDirPath = "C:\\TestBackup";
            
            // Mark Phase 1 as complete
            statusTracker.WriteRestoreStatus(nodeVersion, nodeDirPath, _testDestinationFolder, RestorePhase.Phase1Complete);

            // Act
            var status = statusTracker.GetCurrentStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Equal(RestorePhase.Phase1Complete, status.Status);
            Assert.True(statusTracker.CanResume());
        }

        [Fact]
        public void Phase1Skip_WhenPhase2Started_ShouldSkipPhase1()
        {
            // Arrange
            var statusTracker = new RestoreStatusTracker(_testTempRestoreFolder);
            string nodeVersion = "test_version_1";
            string nodeDirPath = "C:\\TestBackup";
            
            // Mark Phase 2 as started (implies Phase 1 complete)
            statusTracker.WriteRestoreStatus(nodeVersion, nodeDirPath, _testDestinationFolder, RestorePhase.Phase2Started);

            // Act
            var status = statusTracker.GetCurrentStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Equal(RestorePhase.Phase2Started, status.Status);
            Assert.True(statusTracker.CanResume());
        }

        [Fact]
        public void PauseResume_ShouldToggleCorrectly()
        {
            // Arrange
            var service = new ResumableRestoreService(_testFilesFolder, _testVersionFolder);

            // Act & Assert - Initial state
            Assert.False(service.IsPaused);

            // Pause
            service.Pause();
            Assert.True(service.IsPaused);

            // Resume
            service.Resume();
            Assert.False(service.IsPaused);
        }

        [Fact]
        public void StatusTracker_MarkFileComplete_Phase1()
        {
            // Arrange
            var statusTracker = new RestoreStatusTracker(_testTempRestoreFolder);
            string testHash = "abc123def456";

            // Act
            statusTracker.MarkFileComplete(testHash, RestorePhase.Phase1Complete);
            var completedFiles = statusTracker.GetCompletedPhase1Files();

            // Assert
            Assert.Contains(testHash, completedFiles);
        }

        [Fact]
        public void StatusTracker_MarkFileComplete_Phase2()
        {
            // Arrange
            var statusTracker = new RestoreStatusTracker(_testTempRestoreFolder);
            string testHash = "abc123def456";

            // Act
            statusTracker.MarkFileComplete(testHash, RestorePhase.Phase2Complete);
            var completedFiles = statusTracker.GetCompletedPhase2Files();

            // Assert
            Assert.Contains(testHash, completedFiles);
        }

        [Fact]
        public void StatusTracker_GetCurrentStatus_ReturnsCorrectInfo()
        {
            // Arrange
            var statusTracker = new RestoreStatusTracker(_testTempRestoreFolder);
            string nodeVersion = "v1.0";
            string nodeDirPath = "C:\\Source";
            string destinationPath = "C:\\Destination";

            // Act
            statusTracker.WriteRestoreStatus(nodeVersion, nodeDirPath, destinationPath, RestorePhase.Phase1Started);
            var status = statusTracker.GetCurrentStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Equal(nodeVersion, status.NodeVersion);
            Assert.Equal(nodeDirPath, status.NodeDirPath);
            Assert.Equal(destinationPath, status.DestinationPath);
            Assert.Equal(RestorePhase.Phase1Started, status.Status);
        }

        [Fact]
        public void StatusTracker_CanResume_WhenNoStatus_ReturnsFalse()
        {
            // Arrange
            var statusTracker = new RestoreStatusTracker(_testTempRestoreFolder);

            // Act
            bool canResume = statusTracker.CanResume();

            // Assert
            Assert.False(canResume);
        }

        [Fact]
        public void StatusTracker_CanResume_WhenStatusExists_ReturnsTrue()
        {
            // Arrange
            var statusTracker = new RestoreStatusTracker(_testTempRestoreFolder);
            statusTracker.WriteRestoreStatus("v1", "C:\\Source", "C:\\Dest", RestorePhase.Phase1Started);

            // Act
            bool canResume = statusTracker.CanResume();

            // Assert
            Assert.True(canResume);
        }
    }
}
