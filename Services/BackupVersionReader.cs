using System;
using System.Collections.Generic;
using System.IO;
using gitstylebackupexplorer.Models;

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
        /// Gets all files that need to be restored for a specific directory path
        /// </summary>
        public List<FileRestoreInfo> GetFilesToRestore(string nodeVersion, string nodeDirPath)
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
        /// Gets information for a single file restore
        /// </summary>
        public FileRestoreInfo GetSingleFileInfo(string nodeVersion, string nodeDirPath, string nodeFileName)
        {
            string targetFilePath = Path.Combine(nodeDirPath, nodeFileName);
            string sfile = "";
            string shash = "";

            string versionFilePath = Path.Combine(_backupVersionFolderPath, nodeVersion);

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
