using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using gitstylebackupexplorer.Models;
using gitstylebackupexplorer.Utilities;

namespace gitstylebackupexplorer.Services
{
    /// <summary>
    /// Service for reading backup version files and extracting file information
    /// </summary>
    public class BackupVersionReader
    {
        private readonly string _backupVersionFolderPath;

        public BackupVersionReader(string backupVersionFolderPath)
        {
            _backupVersionFolderPath = backupVersionFolderPath;
        }

        /// <summary>
        /// Gets all files that need to be restored for a specific directory path.
        /// Builds a complete snapshot by reading all versions up to and including the target version,
        /// since backups are incremental (only changed files are stored in each version).
        /// </summary>
        public List<FileRestoreInfo> GetFilesToRestore(string nodeVersion, string nodeDirPath)
        {
            // Use a dictionary to track the latest hash for each file path
            // This allows later versions to override earlier versions
            var fileMap = new Dictionary<string, FileRestoreInfo>();
            
            // Get all version files and sort them
            var allVersionFiles = Directory.GetFiles(_backupVersionFolderPath)
                .Where(f => !f.EndsWith(".tmp"))
                .OrderBy(f => f, new FileNameComparer())
                .ToList();
            
            // Find the index of the target version
            int targetIndex = -1;
            for (int i = 0; i < allVersionFiles.Count; i++)
            {
                if (Path.GetFileName(allVersionFiles[i]) == nodeVersion)
                {
                    targetIndex = i;
                    break;
                }
            }
            
            if (targetIndex == -1)
            {
                // Target version not found, fall back to reading just that version
                return GetFilesFromSingleVersion(nodeVersion, nodeDirPath);
            }
            
            // Read all versions from the first up to and including the target version
            for (int i = 0; i <= targetIndex; i++)
            {
                string versionFilePath = allVersionFiles[i];
                string sfile = "";
                string shash = "";
                
                using (StreamReader verFile = new StreamReader(versionFilePath))
                {
                    string line;
                    while ((line = verFile.ReadLine()) != null)
                    {
                        if (line.StartsWith("FILE:"))
                        {
                            sfile = line.Substring("FILE:".Length);
                        }
                        else if (line.StartsWith("HASH:"))
                        {
                            shash = line.Substring("HASH:".Length);
                        }

                        if (!string.IsNullOrEmpty(sfile) && !string.IsNullOrEmpty(shash))
                        {
                            // Only include files in the requested directory
                            if (sfile.StartsWith(nodeDirPath))
                            {
                                // Update or add the file - later versions override earlier ones
                                fileMap[sfile] = new FileRestoreInfo { FilePath = sfile, Hash = shash };
                            }

                            sfile = "";
                            shash = "";
                        }
                    }
                }
            }

            return fileMap.Values.ToList();
        }
        
        /// <summary>
        /// Fallback method to get files from a single version (used if version hierarchy cannot be determined)
        /// </summary>
        private List<FileRestoreInfo> GetFilesFromSingleVersion(string nodeVersion, string nodeDirPath)
        {
            var files = new List<FileRestoreInfo>();
            string sfile = "";
            string shash = "";

            string versionFilePath = Path.Combine(_backupVersionFolderPath, nodeVersion);
            
            using (StreamReader verFile = new StreamReader(versionFilePath))
            {
                string line;
                while ((line = verFile.ReadLine()) != null)
                {
                    if (line.StartsWith("FILE:"))
                    {
                        sfile = line.Substring("FILE:".Length);
                    }
                    else if (line.StartsWith("HASH:"))
                    {
                        shash = line.Substring("HASH:".Length);
                    }

                    if (!string.IsNullOrEmpty(sfile) && !string.IsNullOrEmpty(shash))
                    {
                        if (sfile.StartsWith(nodeDirPath))
                        {
                            files.Add(new FileRestoreInfo { FilePath = sfile, Hash = shash });
                        }

                        sfile = "";
                        shash = "";
                    }
                }
            }

            return files;
        }

        /// <summary>
        /// Gets information for a single file restore.
        /// Searches through all versions up to and including the target version to build complete snapshot.
        /// </summary>
        public FileRestoreInfo GetSingleFileInfo(string nodeVersion, string nodeDirPath, string nodeFileName)
        {
            string targetFilePath = Path.Combine(nodeDirPath, nodeFileName);
            FileRestoreInfo foundFile = null;
            
            // Get all version files and sort them
            var allVersionFiles = Directory.GetFiles(_backupVersionFolderPath)
                .Where(f => !f.EndsWith(".tmp"))
                .OrderBy(f => f, new FileNameComparer())
                .ToList();
            
            // Find the index of the target version
            int targetIndex = -1;
            for (int i = 0; i < allVersionFiles.Count; i++)
            {
                if (Path.GetFileName(allVersionFiles[i]) == nodeVersion)
                {
                    targetIndex = i;
                    break;
                }
            }
            
            if (targetIndex == -1)
            {
                // Fall back to searching just the target version
                return GetSingleFileFromVersion(nodeVersion, targetFilePath);
            }
            
            // Search through all versions from first to target, keeping the latest match
            for (int i = 0; i <= targetIndex; i++)
            {
                string versionFilePath = allVersionFiles[i];
                string sfile = "";
                string shash = "";
                
                using (StreamReader verFile = new StreamReader(versionFilePath))
                {
                    string line;
                    while ((line = verFile.ReadLine()) != null)
                    {
                        if (line.StartsWith("FILE:"))
                        {
                            sfile = line.Substring("FILE:".Length);
                        }
                        else if (line.StartsWith("HASH:"))
                        {
                            shash = line.Substring("HASH:".Length);
                        }

                        if (!string.IsNullOrEmpty(sfile) && !string.IsNullOrEmpty(shash))
                        {
                            if (sfile == targetFilePath)
                            {
                                // Found the file in this version - keep it (may be overridden by later version)
                                foundFile = new FileRestoreInfo { FilePath = sfile, Hash = shash };
                            }

                            sfile = "";
                            shash = "";
                        }
                    }
                }
            }

            return foundFile; // Returns null if file not found in any version
        }
        
        /// <summary>
        /// Fallback method to get a single file from one version
        /// </summary>
        private FileRestoreInfo GetSingleFileFromVersion(string nodeVersion, string targetFilePath)
        {
            string versionFilePath = Path.Combine(_backupVersionFolderPath, nodeVersion);
            string sfile = "";
            string shash = "";

            using (StreamReader verFile = new StreamReader(versionFilePath))
            {
                string line;
                bool found = false;
                
                while ((line = verFile.ReadLine()) != null)
                {
                    if (line.StartsWith("FILE:"))
                    {
                        if (found)
                            break;

                        sfile = line.Substring("FILE:".Length);
                        if (sfile == targetFilePath)
                            found = true;
                    }
                    else if (line.StartsWith("HASH:") && found)
                    {
                        shash = line.Substring("HASH:".Length);
                        return new FileRestoreInfo { FilePath = sfile, Hash = shash };
                    }
                }
            }

            return null; // File not found
        }
    }
}
