using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using gitstylebackupexplorer.Models;

namespace gitstylebackupexplorer.Services
{
    /// <summary>
    /// Service for tracking and managing restore operation status
    /// </summary>
    public class RestoreStatusTracker
    {
        private readonly string _statusFilePath;

        public RestoreStatusTracker(string tempRestoreFolder)
        {
            _statusFilePath = Path.Combine(tempRestoreFolder, "restore_status.txt");
        }

        /// <summary>
        /// Writes the main restore status information
        /// </summary>
        public void WriteRestoreStatus(string nodeVersion, string nodeDirPath, string destinationPath, RestorePhase status)
        {
            var restoreId = Path.GetFileName(Path.GetDirectoryName(_statusFilePath))?.Replace("BackupRestore_", "") ?? Guid.NewGuid().ToString();
            
            var lines = new List<string>
            {
                "RESTORE_ID:" + restoreId,
                "NODE_VERSION:" + nodeVersion,
                "NODE_DIR_PATH:" + nodeDirPath,
                "DESTINATION_PATH:" + destinationPath,
                "STATUS:" + status.ToString().ToUpper(),
                "TIMESTAMP:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            File.WriteAllLines(_statusFilePath, lines);
        }

        /// <summary>
        /// Appends a completion marker for a file in a specific phase
        /// </summary>
        public void MarkFileComplete(string fileHash, RestorePhase phase)
        {
            string marker = phase == RestorePhase.Phase1Complete ? "PHASE1_FILE_COMPLETE:" : "PHASE2_FILE_COMPLETE:";
            AppendToStatus(marker + fileHash);
        }

        /// <summary>
        /// Gets all files that have completed Phase 1 (copying to temp)
        /// </summary>
        public HashSet<string> GetCompletedPhase1Files()
        {
            return GetCompletedFiles("PHASE1_FILE_COMPLETE:");
        }

        /// <summary>
        /// Gets all files that have completed Phase 2 (unzipping to final destination)
        /// </summary>
        public HashSet<string> GetCompletedPhase2Files()
        {
            return GetCompletedFiles("PHASE2_FILE_COMPLETE:");
        }

        /// <summary>
        /// Checks if a restore operation exists and can be resumed
        /// </summary>
        public bool CanResume()
        {
            return File.Exists(_statusFilePath);
        }

        /// <summary>
        /// Gets the current restore status from the status file
        /// </summary>
        public RestoreStatus GetCurrentStatus()
        {
            if (!File.Exists(_statusFilePath))
                return null;

            var lines = File.ReadAllLines(_statusFilePath);
            var status = new RestoreStatus();

            foreach (var line in lines.Take(6)) // Only read the header lines
            {
                if (line.StartsWith("RESTORE_ID:"))
                    status.RestoreId = line.Substring("RESTORE_ID:".Length);
                else if (line.StartsWith("NODE_VERSION:"))
                    status.NodeVersion = line.Substring("NODE_VERSION:".Length);
                else if (line.StartsWith("NODE_DIR_PATH:"))
                    status.NodeDirPath = line.Substring("NODE_DIR_PATH:".Length);
                else if (line.StartsWith("DESTINATION_PATH:"))
                    status.DestinationPath = line.Substring("DESTINATION_PATH:".Length);
                else if (line.StartsWith("STATUS:"))
                {
                    if (Enum.TryParse<RestorePhase>(line.Substring("STATUS:".Length), true, out var phase))
                        status.Status = phase;
                }
                else if (line.StartsWith("TIMESTAMP:"))
                {
                    if (DateTime.TryParse(line.Substring("TIMESTAMP:".Length), out var timestamp))
                        status.Timestamp = timestamp;
                }
            }

            return status;
        }

        /// <summary>
        /// Cleans up the status file and temp directory
        /// </summary>
        public void Cleanup()
        {
            try
            {
                var tempDir = Path.GetDirectoryName(_statusFilePath);
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private void AppendToStatus(string line)
        {
            File.AppendAllText(_statusFilePath, line + Environment.NewLine);
        }

        private HashSet<string> GetCompletedFiles(string prefix)
        {
            var completed = new HashSet<string>();

            if (File.Exists(_statusFilePath))
            {
                var lines = File.ReadAllLines(_statusFilePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith(prefix))
                    {
                        completed.Add(line.Substring(prefix.Length));
                    }
                }
            }

            return completed;
        }
    }
}
