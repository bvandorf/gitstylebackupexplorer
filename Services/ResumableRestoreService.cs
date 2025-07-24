using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using gitstylebackupexplorer.Models;

namespace gitstylebackupexplorer.Services
{
    /// <summary>
    /// Service for handling resumable restore operations
    /// </summary>
    public class ResumableRestoreService
    {
        private readonly string _backupFilesFolderPath;
        private readonly BackupVersionReader _versionReader;
        private volatile bool _isPaused = false;
        private readonly object _pauseLock = new object();

        public event EventHandler<RestoreProgressEventArgs> ProgressChanged;

        public ResumableRestoreService(string backupFilesFolderPath, string backupVersionFolderPath)
        {
            _backupFilesFolderPath = backupFilesFolderPath;
            _versionReader = new BackupVersionReader(backupVersionFolderPath);
        }

        /// <summary>
        /// Pauses the restore operation
        /// </summary>
        public void Pause()
        {
            lock (_pauseLock)
            {
                _isPaused = true;
            }
        }

        /// <summary>
        /// Resumes the restore operation
        /// </summary>
        public void Resume()
        {
            lock (_pauseLock)
            {
                _isPaused = false;
                Monitor.PulseAll(_pauseLock);
            }
        }

        /// <summary>
        /// Gets whether the restore operation is currently paused
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Waits while the operation is paused, checking for cancellation
        /// </summary>
        private void WaitWhilePaused(CancellationToken cancellationToken)
        {
            lock (_pauseLock)
            {
                while (_isPaused && !cancellationToken.IsCancellationRequested)
                {
                    Monitor.Wait(_pauseLock, 100); // Wait with timeout to check cancellation
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Starts or resumes a directory restore operation
        /// </summary>
        public RestoreResult StartResumableRestore(string nodeVersion, string nodeDirPath, string destinationPath, string tempRestoreFolder, bool isResume = false, CancellationToken cancellationToken = default)
        {
            var statusTracker = new RestoreStatusTracker(tempRestoreFolder);

            try
            {
                // Create or update status file
                statusTracker.WriteRestoreStatus(nodeVersion, nodeDirPath, destinationPath, RestorePhase.Phase1Started);

                // Phase 1: Extract files to temp folder with hash names
                var phase1Result = ExecutePhase1(nodeVersion, nodeDirPath, tempRestoreFolder, statusTracker, isResume, cancellationToken);
                
                if (phase1Result.Success)
                {
                    statusTracker.WriteRestoreStatus(nodeVersion, nodeDirPath, destinationPath, RestorePhase.Phase1Complete);
                    
                    // Phase 2: Move files to final destination
                    var phase2Result = ExecutePhase2(nodeVersion, nodeDirPath, destinationPath, tempRestoreFolder, statusTracker, isResume, cancellationToken);
                    
                    if (phase2Result.Success)
                    {
                        statusTracker.WriteRestoreStatus(nodeVersion, nodeDirPath, destinationPath, RestorePhase.Complete);
                        statusTracker.Cleanup();
                        
                        return new RestoreResult { Success = true, Message = "Restore completed successfully!" };
                    }
                    else
                    {
                        return new RestoreResult 
                        { 
                            Success = false, 
                            Message = $"Phase 2 (file placement) incomplete. You can resume later.\nTemp files are in: {tempRestoreFolder}",
                            CanResume = true,
                            TempFolder = tempRestoreFolder
                        };
                    }
                }
                else
                {
                    return new RestoreResult 
                    { 
                        Success = false, 
                        Message = $"Phase 1 (file extraction) incomplete. You can resume later.\nTemp files are in: {tempRestoreFolder}",
                        CanResume = true,
                        TempFolder = tempRestoreFolder
                    };
                }
            }
            catch (Exception ex)
            {
                return new RestoreResult { Success = false, Message = $"Restore process failed: {ex.Message}" };
            }
        }

        /// <summary>
        /// Restores a single file directly to the specified location
        /// </summary>
        public RestoreResult RestoreSingleFile(string nodeVersion, string nodeDirPath, string nodeFileName, string destinationPath)
        {
            try
            {
                var fileInfo = _versionReader.GetSingleFileInfo(nodeVersion, nodeDirPath, nodeFileName);
                
                if (fileInfo == null)
                {
                    return new RestoreResult { Success = false, Message = "Could not find file in backup" };
                }

                string sourceFile = Path.Combine(_backupFilesFolderPath, fileInfo.Hash.Substring(0, 2), fileInfo.Hash);
                DecompressToFile(sourceFile, destinationPath);
                
                return new RestoreResult { Success = true, Message = "File restored successfully" };
            }
            catch (Exception ex)
            {
                return new RestoreResult { Success = false, Message = $"File restore failed: {ex.Message}" };
            }
        }

        private RestoreResult ExecutePhase1(string nodeVersion, string nodeDirPath, string tempRestoreFolder, RestoreStatusTracker statusTracker, bool isResume, CancellationToken cancellationToken)
        {
            var filesToRestore = _versionReader.GetFilesToRestore(nodeVersion, nodeDirPath);
            var completedFiles = isResume ? statusTracker.GetCompletedPhase1Files() : new HashSet<string>();

            int totalFiles = filesToRestore.Count;
            int processedFiles = completedFiles.Count;

            foreach (var fileInfo in filesToRestore)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
                
                // Wait while paused
                WaitWhilePaused(cancellationToken);
                
                if (completedFiles.Contains(fileInfo.Hash))
                {
                    continue; // Skip already processed files
                }

                try
                {
                    string sourceFile = Path.Combine(_backupFilesFolderPath, fileInfo.Hash.Substring(0, 2), fileInfo.Hash);
                    string tempDestFile = Path.Combine(tempRestoreFolder, fileInfo.Hash);

                    // Copy compressed file to temp location
                    File.Copy(sourceFile, tempDestFile, true);

                    // Mark as completed
                    statusTracker.MarkFileComplete(fileInfo.Hash, RestorePhase.Phase1Complete);
                    processedFiles++;

                    // Report progress
                    OnProgressChanged(new RestoreProgress
                    {
                        TotalFiles = totalFiles,
                        ProcessedFiles = processedFiles,
                        CurrentPhase = RestorePhase.Phase1Started,
                        CurrentFileName = Path.GetFileName(fileInfo.FilePath),
                        StatusMessage = $"Phase 1: Copying files to temp location ({processedFiles}/{totalFiles})"
                    });
                }
                catch (Exception ex)
                {
                    return RestoreResult.CreateFailure($"Error copying file {fileInfo.Hash}: {ex.Message}");
                }
            }

            return RestoreResult.CreateSuccess();
        }

        private RestoreResult ExecutePhase2(string nodeVersion, string nodeDirPath, string destinationPath, string tempRestoreFolder, RestoreStatusTracker statusTracker, bool isResume, CancellationToken cancellationToken)
        {
            var filesToRestore = _versionReader.GetFilesToRestore(nodeVersion, nodeDirPath);
            var completedFiles = isResume ? statusTracker.GetCompletedPhase2Files() : new HashSet<string>();

            int totalFiles = filesToRestore.Count;
            int processedFiles = completedFiles.Count;

            foreach (var fileInfo in filesToRestore)
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
                
                // Wait while paused
                WaitWhilePaused(cancellationToken);
                
                if (completedFiles.Contains(fileInfo.Hash))
                {
                    continue; // Skip already processed files
                }

                try
                {
                    string tempSourceFile = Path.Combine(tempRestoreFolder, fileInfo.Hash);
                    string finalDestFile = fileInfo.FilePath.Replace(nodeDirPath, destinationPath);

                    // Ensure destination directory exists
                    var destFileInfo = new FileInfo(finalDestFile);
                    if (!destFileInfo.Directory.Exists)
                        Directory.CreateDirectory(destFileInfo.Directory.FullName);

                    // Decompress from temp to final location
                    DecompressToFile(tempSourceFile, finalDestFile);

                    // Mark as completed
                    statusTracker.MarkFileComplete(fileInfo.Hash, RestorePhase.Phase2Complete);
                    processedFiles++;

                    // Report progress
                    OnProgressChanged(new RestoreProgress
                    {
                        TotalFiles = totalFiles,
                        ProcessedFiles = processedFiles,
                        CurrentPhase = RestorePhase.Phase2Started,
                        CurrentFileName = Path.GetFileName(fileInfo.FilePath),
                        StatusMessage = $"Phase 2: Unzipping files to destination ({processedFiles}/{totalFiles})"
                    });
                }
                catch (Exception ex)
                {
                    return RestoreResult.CreateFailure($"Error decompressing file {fileInfo.FilePath}: {ex.Message}");
                }
            }

            return RestoreResult.CreateSuccess();
        }

        private void DecompressToFile(string source, string dest)
        {
            using (var stream = new GZipStream(new FileStream(source, FileMode.Open), CompressionMode.Decompress))
            using (var outstream = new FileStream(dest, FileMode.Create))
            {
                stream.CopyTo(outstream);
                outstream.Flush();
            }
        }

        private void OnProgressChanged(RestoreProgress progress)
        {
            ProgressChanged?.Invoke(this, new RestoreProgressEventArgs(progress));
        }
    }


}
