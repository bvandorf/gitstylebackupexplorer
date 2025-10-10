using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace gitstylebackupexplorer.Tests
{
    /// <summary>
    /// Tests for version file caching functionality
    /// </summary>
    public class VersionFileCacheTests : IDisposable
    {
        private readonly string _testVersionFolder;
        private readonly Dictionary<string, List<string>> _versionFileCache;
        private readonly object _cacheLock;

        public VersionFileCacheTests()
        {
            _testVersionFolder = Path.Combine(Path.GetTempPath(), "VersionCacheTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testVersionFolder);
            
            _versionFileCache = new Dictionary<string, List<string>>();
            _cacheLock = new object();

            // Create test version files
            CreateTestVersionFile("version1.txt", new[] { "FILE:C:\\test1.txt", "HASH:abc123", "SIZE:1024" });
            CreateTestVersionFile("version2.txt", new[] { "FILE:C:\\test2.txt", "HASH:def456", "SIZE:2048" });
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testVersionFolder))
                    Directory.Delete(_testVersionFolder, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void CreateTestVersionFile(string fileName, string[] lines)
        {
            string filePath = Path.Combine(_testVersionFolder, fileName);
            File.WriteAllLines(filePath, lines);
        }

        private List<string> GetCachedVersionFileData(string nodeVersion)
        {
            lock (_cacheLock)
            {
                if (_versionFileCache.ContainsKey(nodeVersion))
                {
                    return _versionFileCache[nodeVersion];
                }

                string versionFilePath = Path.Combine(_testVersionFolder, nodeVersion);
                var lines = new List<string>();

                using (var verFile = new StreamReader(versionFilePath))
                {
                    string line;
                    while ((line = verFile.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }

                _versionFileCache[nodeVersion] = lines;
                return lines;
            }
        }

        [Fact]
        public void Cache_FirstAccess_ShouldReadFromDisk()
        {
            // Act
            var lines = GetCachedVersionFileData("version1.txt");

            // Assert
            Assert.NotNull(lines);
            Assert.Equal(3, lines.Count);
            Assert.Contains("FILE:C:\\test1.txt", lines);
            Assert.Single(_versionFileCache); // Cache should have 1 entry
        }

        [Fact]
        public void Cache_SecondAccess_ShouldUseCache()
        {
            // Arrange - First access to populate cache
            var firstAccess = GetCachedVersionFileData("version1.txt");

            // Delete the file to prove second access uses cache
            File.Delete(Path.Combine(_testVersionFolder, "version1.txt"));

            // Act - Second access should use cache
            var secondAccess = GetCachedVersionFileData("version1.txt");

            // Assert
            Assert.NotNull(secondAccess);
            Assert.Equal(firstAccess.Count, secondAccess.Count);
            Assert.Same(firstAccess, secondAccess); // Should be exact same object from cache
        }

        [Fact]
        public void Cache_MultipleVersions_ShouldCacheSeparately()
        {
            // Act
            var version1 = GetCachedVersionFileData("version1.txt");
            var version2 = GetCachedVersionFileData("version2.txt");

            // Assert
            Assert.Equal(2, _versionFileCache.Count);
            Assert.NotSame(version1, version2);
            Assert.Contains("test1.txt", version1[0]);
            Assert.Contains("test2.txt", version2[0]);
        }

        [Fact]
        public void Cache_Clear_ShouldEmptyCache()
        {
            // Arrange
            GetCachedVersionFileData("version1.txt");
            GetCachedVersionFileData("version2.txt");
            Assert.Equal(2, _versionFileCache.Count);

            // Act
            lock (_cacheLock)
            {
                _versionFileCache.Clear();
            }

            // Assert
            Assert.Empty(_versionFileCache);
        }

        [Fact]
        public void Cache_ThreadSafety_MultipleConcurrentAccesses()
        {
            // Arrange
            int threadCount = 10;
            var tasks = new Task[threadCount];
            var results = new List<string>[threadCount];

            // Act - Multiple threads accessing same version simultaneously
            for (int i = 0; i < threadCount; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    results[index] = GetCachedVersionFileData("version1.txt");
                });
            }

            Task.WaitAll(tasks);

            // Assert - All threads should get valid data
            foreach (var result in results)
            {
                Assert.NotNull(result);
                Assert.Equal(3, result.Count);
            }

            // Cache should only have 1 entry (not duplicated by threads)
            Assert.Single(_versionFileCache);
        }

        [Fact]
        public void Cache_Performance_CacheIsFasterThanDisk()
        {
            // Arrange - Create a larger test file
            var largeContent = new List<string>();
            for (int i = 0; i < 1000; i++)
            {
                largeContent.Add($"FILE:C:\\test{i}.txt");
                largeContent.Add($"HASH:hash{i}");
                largeContent.Add($"SIZE:{i * 1024}");
            }
            CreateTestVersionFile("large_version.txt", largeContent.ToArray());

            // Act - First access (disk read)
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var firstRead = GetCachedVersionFileData("large_version.txt");
            sw1.Stop();

            // Second access (cache read)
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var secondRead = GetCachedVersionFileData("large_version.txt");
            sw2.Stop();

            // Assert - Cache should be faster (or at least not slower)
            Assert.True(sw2.ElapsedMilliseconds <= sw1.ElapsedMilliseconds,
                $"Cache read ({sw2.ElapsedMilliseconds}ms) should be faster than disk read ({sw1.ElapsedMilliseconds}ms)");
        }
    }
}
