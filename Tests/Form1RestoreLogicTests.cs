using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace gitstylebackupexplorer.Tests
{
    /// <summary>
    /// Tests for Form1 restore folder cleanup logic
    /// </summary>
    public class Form1RestoreLogicTests : IDisposable
    {
        private readonly string _testDestinationFolder;

        public Form1RestoreLogicTests()
        {
            _testDestinationFolder = Path.Combine(Path.GetTempPath(), "RestoreLogicTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDestinationFolder);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDestinationFolder))
                    Directory.Delete(_testDestinationFolder, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void CleanupLogic_ShouldNotDeleteRecentFolders()
        {
            // Arrange - Create a recent restore folder (less than 24 hours old)
            string recentFolder = Path.Combine(_testDestinationFolder, ".BackupRestore_Recent");
            Directory.CreateDirectory(recentFolder);
            File.WriteAllText(Path.Combine(recentFolder, "test.txt"), "recent data");

            // Simulate the cleanup logic from Form1.cs
            var existingRestoreFolders = Directory.GetDirectories(_testDestinationFolder, ".BackupRestore_*");
            var staleThreshold = DateTime.Now.AddHours(-24);
            int cleanedCount = 0;

            foreach (var folder in existingRestoreFolders)
            {
                var folderInfo = new DirectoryInfo(folder);
                if (folderInfo.LastWriteTime < staleThreshold)
                {
                    Directory.Delete(folder, true);
                    cleanedCount++;
                }
            }

            // Assert - Recent folder should NOT be deleted
            Assert.Equal(0, cleanedCount);
            Assert.True(Directory.Exists(recentFolder));
        }

        [Fact]
        public void CleanupLogic_ShouldDeleteOldFolders()
        {
            // Arrange - Create an old restore folder
            string oldFolder = Path.Combine(_testDestinationFolder, ".BackupRestore_Old");
            Directory.CreateDirectory(oldFolder);
            File.WriteAllText(Path.Combine(oldFolder, "test.txt"), "old data");

            // Manually set the LastWriteTime to 25 hours ago
            var oldTime = DateTime.Now.AddHours(-25);
            Directory.SetLastWriteTime(oldFolder, oldTime);

            // Simulate the cleanup logic from Form1.cs
            var existingRestoreFolders = Directory.GetDirectories(_testDestinationFolder, ".BackupRestore_*");
            var staleThreshold = DateTime.Now.AddHours(-24);
            int cleanedCount = 0;

            foreach (var folder in existingRestoreFolders)
            {
                var folderInfo = new DirectoryInfo(folder);
                if (folderInfo.LastWriteTime < staleThreshold)
                {
                    Directory.Delete(folder, true);
                    cleanedCount++;
                }
            }

            // Assert - Old folder SHOULD be deleted
            Assert.Equal(1, cleanedCount);
            Assert.False(Directory.Exists(oldFolder));
        }

        [Fact]
        public void CleanupLogic_ShouldHandleMultipleFolders()
        {
            // Arrange - Create mix of old and recent folders
            string recentFolder1 = Path.Combine(_testDestinationFolder, ".BackupRestore_Recent1");
            string recentFolder2 = Path.Combine(_testDestinationFolder, ".BackupRestore_Recent2");
            string oldFolder1 = Path.Combine(_testDestinationFolder, ".BackupRestore_Old1");
            string oldFolder2 = Path.Combine(_testDestinationFolder, ".BackupRestore_Old2");

            Directory.CreateDirectory(recentFolder1);
            Directory.CreateDirectory(recentFolder2);
            Directory.CreateDirectory(oldFolder1);
            Directory.CreateDirectory(oldFolder2);

            // Set old folders to 25 hours ago
            var oldTime = DateTime.Now.AddHours(-25);
            Directory.SetLastWriteTime(oldFolder1, oldTime);
            Directory.SetLastWriteTime(oldFolder2, oldTime);

            // Simulate the cleanup logic
            var existingRestoreFolders = Directory.GetDirectories(_testDestinationFolder, ".BackupRestore_*");
            var staleThreshold = DateTime.Now.AddHours(-24);
            int cleanedCount = 0;

            foreach (var folder in existingRestoreFolders)
            {
                var folderInfo = new DirectoryInfo(folder);
                if (folderInfo.LastWriteTime < staleThreshold)
                {
                    Directory.Delete(folder, true);
                    cleanedCount++;
                }
            }

            // Assert
            Assert.Equal(2, cleanedCount); // Only 2 old folders deleted
            Assert.True(Directory.Exists(recentFolder1));
            Assert.True(Directory.Exists(recentFolder2));
            Assert.False(Directory.Exists(oldFolder1));
            Assert.False(Directory.Exists(oldFolder2));
        }

        [Fact]
        public void CleanupLogic_ShouldHandleEmptyDirectory()
        {
            // Arrange - No restore folders exist
            var existingRestoreFolders = Directory.GetDirectories(_testDestinationFolder, ".BackupRestore_*");

            // Assert
            Assert.Empty(existingRestoreFolders);
        }

        [Fact]
        public void CleanupLogic_ShouldIgnoreNonMatchingFolders()
        {
            // Arrange - Create folders that don't match the pattern
            string normalFolder = Path.Combine(_testDestinationFolder, "NormalFolder");
            string otherFolder = Path.Combine(_testDestinationFolder, "BackupRestore_NoPrefix");
            
            Directory.CreateDirectory(normalFolder);
            Directory.CreateDirectory(otherFolder);

            // Act
            var existingRestoreFolders = Directory.GetDirectories(_testDestinationFolder, ".BackupRestore_*");

            // Assert - Should not find non-matching folders
            Assert.Empty(existingRestoreFolders);
            Assert.True(Directory.Exists(normalFolder));
            Assert.True(Directory.Exists(otherFolder));
        }
    }
}
