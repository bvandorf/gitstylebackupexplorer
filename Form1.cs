using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using gitstylebackupexplorer.Models;
using gitstylebackupexplorer.Services;
using gitstylebackupexplorer.Utilities;

namespace gitstylebackupexplorer
{
    public partial class Form1 : Form
    {
        private string backupFolderPath = "";
        private string backupInUseFile = "";
        private string backupVersionFolderPath = "";
        private string backupFilesFolderPath = "";
        private TreeNode LastNodeClick;
        
        private ResumableRestoreService _restoreService;
        private BackupVersionReader _versionReader;
        private EncryptionConfig _encryptionConfig = new EncryptionConfig();
        
        // Version file caching for improved performance
        private Dictionary<string, List<string>> _versionFileCache = new Dictionary<string, List<string>>();
        private string _lastCachedVersion = "";
        private readonly object _cacheLock = new object();

        public Form1()
        {
            InitializeComponent();
            
            // Resume from Folder is now enabled by default since it works independently
            resumeFromFolderToolStripMenuItem.Enabled = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void openBackupDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //open backup db
            FolderBrowserDialog openFolder = new FolderBrowserDialog();
            if (openFolder.ShowDialog() == DialogResult.OK)
            {
                PopulateTree(openFolder.SelectedPath);
            }
        }

        private void resumeFromFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Allow user to pick a folder containing .BackupRestore_ folders
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.Description = "Select the folder containing incomplete restore operations (.BackupRestore_ folders)";
            
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Look for .BackupRestore_ folders
                    var restoreFolders = Directory.GetDirectories(folderDialog.SelectedPath, ".BackupRestore_*")
                        .Where(dir => 
                        {
                            var tracker = new RestoreStatusTracker(dir);
                            return tracker.CanResume();
                        })
                        .ToArray();
                        
                    if (restoreFolders.Length == 0)
                    {
                        MessageBox.Show("No resumable restore operations found in the selected folder.", 
                            "No Restore Operations", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    
                    string selectedRestoreFolder;
                    
                    if (restoreFolders.Length == 1)
                    {
                        selectedRestoreFolder = restoreFolders[0];
                    }
                    else
                    {
                        // Multiple restore folders found, let user choose with detailed information
                        var restoreInfoList = new List<string>();
                        var restoreStatusList = new List<RestoreStatus>();
                        
                        foreach (var folder in restoreFolders)
                        {
                            var tracker = new RestoreStatusTracker(folder);
                            var folderStatus = tracker.GetCurrentStatus();
                            
                            if (folderStatus != null)
                            {
                                var displayText = $"{Path.GetFileName(folder)} - {folderStatus.Status} - {folderStatus.Timestamp:MM/dd HH:mm} - {Path.GetFileName(folderStatus.DestinationPath)}";
                                restoreInfoList.Add(displayText);
                                restoreStatusList.Add(folderStatus);
                            }
                            else
                            {
                                var displayText = $"{Path.GetFileName(folder)} - Unknown Status";
                                restoreInfoList.Add(displayText);
                                restoreStatusList.Add(null);
                            }
                        }
                        
                        using (var selectionForm = new Form())
                        {
                            selectionForm.Text = "Select Restore Operation to Resume";
                            selectionForm.Size = new Size(600, 300);
                            selectionForm.StartPosition = FormStartPosition.CenterParent;
                            
                            var label = new Label();
                            label.Text = "Select a restore operation to resume:";
                            label.Dock = DockStyle.Top;
                            label.Height = 25;
                            label.Padding = new Padding(5);
                            
                            var listBox = new ListBox();
                            listBox.Items.AddRange(restoreInfoList.ToArray());
                            listBox.Dock = DockStyle.Fill;
                            listBox.SelectedIndex = 0;
                            
                            var buttonPanel = new Panel();
                            buttonPanel.Height = 40;
                            buttonPanel.Dock = DockStyle.Bottom;
                            
                            var okButton = new Button();
                            okButton.Text = "Resume Selected";
                            okButton.DialogResult = DialogResult.OK;
                            okButton.Size = new Size(120, 30);
                            okButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                            okButton.Location = new Point(buttonPanel.Width - 130, 5);
                            
                            var cancelButton = new Button();
                            cancelButton.Text = "Cancel";
                            cancelButton.DialogResult = DialogResult.Cancel;
                            cancelButton.Size = new Size(80, 30);
                            cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                            cancelButton.Location = new Point(buttonPanel.Width - 220, 5);
                            
                            buttonPanel.Controls.Add(okButton);
                            buttonPanel.Controls.Add(cancelButton);
                            
                            selectionForm.Controls.Add(listBox);
                            selectionForm.Controls.Add(buttonPanel);
                            selectionForm.Controls.Add(label);
                            
                            if (selectionForm.ShowDialog() == DialogResult.OK && listBox.SelectedIndex >= 0)
                            {
                                selectedRestoreFolder = restoreFolders[listBox.SelectedIndex];
                            }
                            else
                            {
                                return; // User cancelled
                            }
                        }
                    }
                    
                    // Get restore status information
                    var statusTracker = new RestoreStatusTracker(selectedRestoreFolder);
                    var status = statusTracker.GetCurrentStatus();
                    
                    if (status == null)
                    {
                        MessageBox.Show("Could not read restore status information.", 
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    
                    // Confirm with user
                    var result = MessageBox.Show(
                        $"Resume restore operation?\n\n" +
                        $"Source: {status.NodeDirPath}\n" +
                        $"Destination: {status.DestinationPath}\n" +
                        $"Status: {status.Status}\n" +
                        $"Started: {status.Timestamp:yyyy-MM-dd HH:mm:ss}",
                        "Resume Restore Operation", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Question);
                        
                    if (result == DialogResult.Yes)
                    {
                        // Auto-detect backup source from restore folder location
                        string detectedBackupFolder = null;
                        string detectedBackupFilesFolder = null;
                        string detectedBackupVersionFolder = null;
                        
                        // Try to find backup folder by looking for common backup structures
                        var restoreParentDir = Directory.GetParent(selectedRestoreFolder)?.FullName;
                        if (restoreParentDir != null)
                        {
                            // Look for backup structure in parent directories
                            var currentDir = new DirectoryInfo(restoreParentDir);
                            while (currentDir != null && detectedBackupFolder == null)
                            {
                                // Check if this directory contains backup structure (Files and Version folders)
                                var filesDir = Path.Combine(currentDir.FullName, "Files");
                                var versionDir = Path.Combine(currentDir.FullName, "Version");
                                
                                if (Directory.Exists(filesDir) && Directory.Exists(versionDir))
                                {
                                    detectedBackupFolder = currentDir.FullName;
                                    detectedBackupFilesFolder = filesDir;
                                    detectedBackupVersionFolder = versionDir;
                                    break;
                                }
                                
                                currentDir = currentDir.Parent;
                            }
                        }
                        
                        // If auto-detection failed, ask user to locate backup folder
                        if (detectedBackupFolder == null)
                        {
                            var result2 = MessageBox.Show(
                                "Could not auto-detect the backup source folder.\n\n" +
                                "Would you like to manually select the backup folder?",
                                "Backup Source Not Found",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);
                                
                            if (result2 == DialogResult.Yes)
                            {
                                FolderBrowserDialog backupDialog = new FolderBrowserDialog();
                                backupDialog.Description = "Select the backup folder containing 'Files' and 'Version' subdirectories";
                                
                                if (backupDialog.ShowDialog() == DialogResult.OK)
                                {
                                    var filesDir = Path.Combine(backupDialog.SelectedPath, "Files");
                                    var versionDir = Path.Combine(backupDialog.SelectedPath, "Version");
                                    
                                    if (Directory.Exists(filesDir) && Directory.Exists(versionDir))
                                    {
                                        detectedBackupFolder = backupDialog.SelectedPath;
                                        detectedBackupFilesFolder = filesDir;
                                        detectedBackupVersionFolder = versionDir;
                                    }
                                    else
                                    {
                                        MessageBox.Show("Selected folder does not contain valid backup structure (Files and Version folders).",
                                            "Invalid Backup Folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        return;
                                    }
                                }
                                else
                                {
                                    return; // User cancelled
                                }
                            }
                            else
                            {
                                return; // User chose not to manually select
                            }
                        }
                        
                        // Create a new independent restore service with detected backup paths
                        var independentRestoreService = new ResumableRestoreService(detectedBackupFilesFolder, detectedBackupVersionFolder, _encryptionConfig);
                        
                        // Launch the restore window in resume mode
                        var restoreWindow = new RestoreWindow(independentRestoreService, status.NodeVersion, 
                            status.NodeDirPath, null, status.DestinationPath, selectedRestoreFolder, true);
                        restoreWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error resuming restore operation: {ex.Message}", 
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Opens the encryption configuration dialog
        /// </summary>
        public void ConfigureEncryption()
        {
            using (var configForm = new EncryptionConfigForm(_encryptionConfig))
            {
                if (configForm.ShowDialog() == DialogResult.OK)
                {
                    _encryptionConfig = configForm.EncryptionConfig;
                    MessageBox.Show($"Encryption configuration updated: {_encryptionConfig.GetEncryptionDescription()}", 
                        "Encryption Configuration", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void DecompressToFile(string source, string dest)
        {
            DecompressToFileWithEncryption(source, dest, _encryptionConfig.GetEncryptionKey());
        }

        private void DecompressToFileWithEncryption(string source, string dest, byte[] encryptionKey)
        {
            try
            {
                // Read the source file
                byte[] sourceData = File.ReadAllBytes(source);
                
                // Check if encryption is configured and if the file appears to be encrypted
                bool shouldDecrypt = encryptionKey != null && EncryptionService.IsDataEncrypted(sourceData);
                
                byte[] dataToDecompress;
                
                if (shouldDecrypt)
                {
                    // Decrypt the data first
                    dataToDecompress = EncryptionService.DecryptData(sourceData, encryptionKey);
                }
                else
                {
                    // Use data as-is (unencrypted)
                    dataToDecompress = sourceData;
                }
                
                // Decompress the data
                using (var inputStream = new MemoryStream(dataToDecompress))
                using (var gzipStream = new System.IO.Compression.GZipStream(inputStream, System.IO.Compression.CompressionMode.Decompress))
                using (var outputStream = new FileStream(dest, FileMode.Create))
                {
                    gzipStream.CopyTo(outputStream);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to decompress file {source}: {ex.Message}", ex);
            }
        }

        static string SizeSuffix(long value)
        {
            return SizeFormatter.FormatSize(value);
        }

        private void infoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //info
            if (LastNodeClick != null)
            {
                TreeNode node = LastNodeClick;

                string nodeTag = node.Tag.ToString();
                string nodeVersion = nodeTag.Substring(0, nodeTag.IndexOf("~"));
                string nodeDirPath = nodeTag.Substring(nodeTag.IndexOf("~") + 1);
                string nodeFileName = "";
                if (nodeTag.Split('~').Length == 3)
                {
                    nodeFileName = nodeDirPath.Substring(nodeDirPath.IndexOf("~") + 1);
                    nodeDirPath = nodeDirPath.Substring(0, nodeDirPath.IndexOf("~"));
                }

                if (nodeFileName != "")
                {
                    //file
                    string sfile = "";
                    string shash = "";
                    DateTime moddate = new DateTime();
                    long lsize = 0;
                    string ssize = "";

                    System.IO.StreamReader verFile = new System.IO.StreamReader(backupVersionFolderPath + "\\" + nodeVersion);
                    bool bfound = false;
                    string line = "";
                    while ((line = verFile.ReadLine()) != null)
                    {
                        if (line.StartsWith("FILE:"))
                        {
                            if (bfound)
                                break;

                            sfile = line.Substring(("FILE:").Length);
                            if (sfile == nodeDirPath + "\\" + nodeFileName)
                                bfound = true;
                        }
                        else if (line.StartsWith("MODDATE:"))
                        {
                            moddate = DateTime.Parse(line.Substring(("MODDATE:").Length));
                        }
                        else if (line.StartsWith("SIZE:"))
                        {
                            ssize = line.Substring(("SIZE:").Length);
                            ssize = ssize.Replace(".", "");
                            lsize = long.Parse(ssize);
                        }
                        else if (line.StartsWith("HASH:"))
                        {
                            shash = line.Substring(("HASH:").Length);
                        }
                    }
                    verFile.Close();

                    if (bfound)
                    {
                        string sinfo = "";
                        sinfo += "Name: " + sfile + "\n";
                        sinfo += "Directory: " + nodeDirPath + "\n";
                        sinfo += "Version: " + nodeVersion + "\n";
                        sinfo += "Date Modified: " + moddate.ToShortDateString() + " " + moddate.ToLongTimeString() + "\n";
                        sinfo += "Size: " + SizeSuffix(lsize) + " \n";
                        sinfo += "Hash: " + shash + "\n";

                        MessageBox.Show(sinfo, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Could Not Find File", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    //dir
                    DateTime verDate = new DateTime();

                    System.IO.StreamReader verFile = new System.IO.StreamReader(backupVersionFolderPath + "\\" + nodeVersion);
                    string line = "";
                    while ((line = verFile.ReadLine()) != null)
                    {
                        if (line.StartsWith("DATE:"))
                        {
                            verDate = DateTime.Parse(line.Substring(("DATE:").Length));
                        }
                    }
                    verFile.Close();

                    string sinfo = "";
                    sinfo += "Directory: " + nodeDirPath + "\n";
                    sinfo += "Version: " + nodeVersion + "\n";
                    sinfo += "Date: " + verDate.ToShortDateString() + " " + verDate.ToLongTimeString() + "\n";

                    MessageBox.Show(sinfo, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void restoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastNodeClick != null)
            {
                TreeNode node = LastNodeClick;
                string nodeTag = node.Tag.ToString();
                string nodeVersion = nodeTag.Substring(0, nodeTag.IndexOf("~"));
                string nodeDirPath = nodeTag.Substring(nodeTag.IndexOf("~") + 1);
                string nodeFileName = "";
                if (nodeTag.Split('~').Length == 3)
                {
                    nodeFileName = nodeDirPath.Substring(nodeDirPath.IndexOf("~") + 1);
                    nodeDirPath = nodeDirPath.Substring(0, nodeDirPath.IndexOf("~"));
                }

                LaunchRestoreWindow(nodeVersion, nodeDirPath, nodeFileName, false);
            }
            else
            {
                MessageBox.Show("Please select a file or directory to restore.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void LaunchRestoreWindow(string nodeVersion, string nodeDirPath, string nodeFileName, bool isResume)
        {
            string destinationPath = "";
            
            // Get destination path from user
            if (!string.IsNullOrEmpty(nodeFileName))
            {
                // Single file restore
                SaveFileDialog saveFile = new SaveFileDialog();
                saveFile.FileName = nodeFileName;
                saveFile.OverwritePrompt = true;
                
                if (saveFile.ShowDialog() != DialogResult.OK)
                    return;
                    
                destinationPath = saveFile.FileName;
            }
            else
            {
                // Directory restore
                FolderBrowserDialog saveFolder = new FolderBrowserDialog();
                if (saveFolder.ShowDialog() != DialogResult.OK)
                    return;
                    
                destinationPath = saveFolder.SelectedPath;
            }
            
            // Create temp restore folder in the same location as destination for better performance and space management
            string destinationDirectory = string.IsNullOrEmpty(nodeFileName) ? destinationPath : Path.GetDirectoryName(destinationPath);
            string tempRestoreFolder = null;
            
            // For new restores, clean up old/stale restore folders (older than 24 hours)
            // This prevents interfering with active restore operations in other windows
            if (!isResume)
            {
                try
                {
                    var existingRestoreFolders = Directory.GetDirectories(destinationDirectory, ".BackupRestore_*");
                    var staleThreshold = DateTime.Now.AddHours(-24);
                    int cleanedCount = 0;
                    int failedCount = 0;
                    
                    foreach (var folder in existingRestoreFolders)
                    {
                        try
                        {
                            var folderInfo = new DirectoryInfo(folder);
                            
                            // Only delete folders older than 24 hours
                            if (folderInfo.LastWriteTime < staleThreshold)
                            {
                                // Additional check: verify the folder isn't actively being used
                                var statusTracker = new RestoreStatusTracker(folder);
                                var status = statusTracker.GetCurrentStatus();
                                
                                // Only delete if status is old or can't be read
                                if (status == null || status.Timestamp < staleThreshold)
                                {
                                    Directory.Delete(folder, true);
                                    cleanedCount++;
                                }
                            }
                        }
                        catch
                        {
                            // Count failures but don't block the restore operation
                            failedCount++;
                        }
                    }
                    
                    // Inform user if cleanup occurred
                    if (cleanedCount > 0)
                    {
                        string message = $"Cleaned up {cleanedCount} old restore folder(s).";
                        if (failedCount > 0)
                        {
                            message += $" ({failedCount} could not be removed - may be in use)";
                        }
                        // Log to console for debugging (won't block UI)
                        System.Diagnostics.Debug.WriteLine(message);
                    }
                }
                catch (Exception ex)
                {
                    // Don't let cleanup errors prevent the restore operation
                    System.Diagnostics.Debug.WriteLine($"Error during restore folder cleanup: {ex.Message}");
                }
            }
            
            // Create new temp folder if none exists or user chose not to resume
            if (tempRestoreFolder == null)
            {
                string restoreId = Guid.NewGuid().ToString();
                tempRestoreFolder = Path.Combine(destinationDirectory, ".BackupRestore_" + restoreId);
                
                if (!Directory.Exists(tempRestoreFolder))
                    Directory.CreateDirectory(tempRestoreFolder);
            }
            
            // Create a new independent restore service for this window
            var independentRestoreService = new ResumableRestoreService(backupFilesFolderPath, backupVersionFolderPath, _encryptionConfig);
            
            // Launch the restore window with its own service instance
            var restoreWindow = new RestoreWindow(independentRestoreService, nodeVersion, nodeDirPath, nodeFileName, 
                destinationPath, tempRestoreFolder, isResume);
            restoreWindow.Show();
        }

        private void RestoreSingleFile(string nodeVersion, string nodeDirPath, string nodeFileName)
        {
            SaveFileDialog saveFile = new SaveFileDialog();
            saveFile.FileName = nodeFileName;
            saveFile.OverwritePrompt = true;
            
            if (saveFile.ShowDialog() == DialogResult.OK)
            {
                var result = _restoreService.RestoreSingleFile(nodeVersion, nodeDirPath, nodeFileName, saveFile.FileName);
                
                MessageBoxIcon icon = result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error;
                MessageBox.Show(result.Message, "Restore", MessageBoxButtons.OK, icon);
            }
        }

        private void RestoreDirectory(string nodeVersion, string nodeDirPath)
        {
            FolderBrowserDialog saveFolder = new FolderBrowserDialog();
            if (saveFolder.ShowDialog() == DialogResult.OK)
            {
                string restoreId = Guid.NewGuid().ToString();
                string tempRestoreFolder = Path.Combine(Path.GetTempPath(), "BackupRestore_" + restoreId);
                
                if (!Directory.Exists(tempRestoreFolder))
                    Directory.CreateDirectory(tempRestoreFolder);

                // Check if this is a resume operation
                var statusTracker = new RestoreStatusTracker(tempRestoreFolder);
                bool isResume = statusTracker.CanResume();
                
                if (isResume)
                {
                    var result = MessageBox.Show("Found incomplete restore operation. Do you want to resume it?", 
                        "Resume Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                    if (result == DialogResult.No)
                    {
                        // Clean up and start fresh
                        if (Directory.Exists(tempRestoreFolder))
                            Directory.Delete(tempRestoreFolder, true);
                        Directory.CreateDirectory(tempRestoreFolder);
                        isResume = false;
                    }
                }

                // Subscribe to progress updates
                _restoreService.ProgressChanged += OnRestoreProgressChanged;
                
                try
                {
                    var restoreResult = _restoreService.StartResumableRestore(nodeVersion, nodeDirPath, saveFolder.SelectedPath, tempRestoreFolder, isResume);
                    
                    MessageBoxIcon icon = restoreResult.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error;
                    MessageBox.Show(restoreResult.Message, "Restore", MessageBoxButtons.OK, icon);
                }
                finally
                {
                    _restoreService.ProgressChanged -= OnRestoreProgressChanged;
                    this.Text = "Git Style Backup Explorer";
                }
            }
        }

        private void OnRestoreProgressChanged(object sender, RestoreProgressEventArgs e)
        {
            // Update the window title with progress information
            this.Text = e.Progress.StatusMessage;
            Application.DoEvents();
        }
        
        /// <summary>
        /// Gets cached version file data, reading from disk only if not already cached
        /// </summary>
        /// <param name="nodeVersion">The backup version to get data for</param>
        /// <returns>List of lines from the version file</returns>
        private List<string> GetCachedVersionFileData(string nodeVersion)
        {
            lock (_cacheLock)
            {
                // Check if we already have this version cached
                if (_versionFileCache.ContainsKey(nodeVersion))
                {
                    return _versionFileCache[nodeVersion];
                }
                
                // Not cached, need to read from disk
                try
                {
                    string versionFilePath = backupVersionFolderPath + "\\" + nodeVersion;
                    var lines = new List<string>();
                    
                    using (var verFile = new System.IO.StreamReader(versionFilePath))
                    {
                        string line;
                        while ((line = verFile.ReadLine()) != null)
                        {
                            lines.Add(line);
                        }
                    }
                    
                    // Cache the data for future use
                    _versionFileCache[nodeVersion] = lines;
                    _lastCachedVersion = nodeVersion;
                    
                    return lines;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading version file {nodeVersion}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return new List<string>();
                }
            }
        }


        private void PopulateTree(string backupFolder)
        {
            try
            {
                // Clear version file cache when opening new backup to prevent memory growth
                lock (_cacheLock)
                {
                    _versionFileCache.Clear();
                    _lastCachedVersion = "";
                }
                
                backupFolderPath = backupFolder;
                backupInUseFile = backupFolderPath + "\\InUse.txt";
                backupVersionFolderPath = backupFolderPath + "\\Version";
                backupFilesFolderPath = backupFolderPath + "\\Files";

                // Initialize services
                _versionReader = new BackupVersionReader(backupVersionFolderPath);
                _restoreService = new ResumableRestoreService(backupFilesFolderPath, backupVersionFolderPath, _encryptionConfig);

                // Enable Resume from Folder now that backup is loaded
                resumeFromFolderToolStripMenuItem.Enabled = true;

                LastNodeClick = null;

                treeView1.Nodes.Clear();
                //add tree item for each version
                List<string> files = System.IO.Directory.GetFiles(backupVersionFolderPath).ToList();
                files.Sort(new FileNameComparer());


                foreach (string file in files)
                {
                    if (file.EndsWith(".tmp") == false)
                    {
                        DateTime verDateTime = new DateTime();
                        string verNumber = "";

                        System.IO.StreamReader verFile = new System.IO.StreamReader(file);
                        string line = "";
                        while ((line = verFile.ReadLine()) != null)
                        {
                            if (line.StartsWith("VERSION:"))
                            {
                                verNumber = line.Substring("VERSION:".Length);
                            }
                            else if (line.StartsWith("DATE:"))
                            {
                                verDateTime = DateTime.Parse(line.Substring(("DATE:").Length));
								break;
                            }
                        }
                        verFile.Close();

                        TreeNode tn = new TreeNode(verNumber + " - " + verDateTime.ToShortDateString() + " " + verDateTime.ToLongTimeString());
                        tn.Tag = verNumber + "~";
                        tn.Nodes.Add("");
                        treeView1.Nodes.Add(tn);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error Opening Backup File: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;
            node.Nodes.Clear();

            string nodeTag = node.Tag.ToString();
            string nodeVersion = nodeTag.Substring(0, nodeTag.IndexOf("~"));
            string nodeDirPath = nodeTag.Substring(nodeTag.IndexOf("~") + 1);
            string nodeFileName = "";
            if (nodeTag.Split('~').Length == 3)
            {
                nodeFileName = nodeDirPath.Substring(nodeDirPath.IndexOf("~") + 1);
                nodeDirPath = nodeDirPath.Substring(0, nodeDirPath.IndexOf("~"));
            }

            string nodeKey = "";
            TreeNode tn = node;
            while(tn.Parent != null)
            {
                nodeKey = tn.Text + "\\" + nodeKey;
                tn = tn.Parent;
            }

            // Get cached version file data (only reads from disk if not already cached)
            var versionLines = GetCachedVersionFileData(nodeVersion);
            
            //fill only the next row of items
            string sfile = "";

            foreach (string line in versionLines)
            {
                if (line.StartsWith("FILE:"))
                {
                    sfile = line.Substring("FILE:".Length);
                }

                if (sfile != "")
                {
                    if (sfile.StartsWith(nodeKey))
                    {
                        string stmp = sfile;
                        if (nodeKey != "")
                            stmp = sfile.Replace(nodeKey, "");

                        string[] split = stmp.Split(new char[] { '\\' });
                        if (node.Nodes.ContainsKey(split[0]) == false)
                        {
                            if (split.Length == 1)
                            {
                                //file
                                node.Nodes.Add(new TreeNode() { Name = split[0], Text = split[0], Tag = nodeVersion + "~" + nodeKey.TrimEnd('\\') + "~" + split[0] });
                            }
                            else
                            {
                                //dir
                                int inode = node.Nodes.Add(new TreeNode() { Name = split[0], Text = split[0], Tag = nodeVersion + "~" + nodeKey + split[0] });
                                node.Nodes[inode].Nodes.Add("");
                            }
                        }
                    }
                    sfile = "";
                }
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            LastNodeClick = e.Node;
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (!e.Node.IsExpanded)
                e.Node.Expand();
        }
    }
}
