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

        public Form1()
        {
            InitializeComponent();
            
            // Initially disable Resume from Folder until backup is loaded
            resumeFromFolderToolStripMenuItem.Enabled = false;
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
                        // Multiple restore folders found, let user choose
                        var folderNames = restoreFolders.Select(Path.GetFileName).ToArray();
                        
                        using (var selectionForm = new Form())
                        {
                            selectionForm.Text = "Select Restore Operation";
                            selectionForm.Size = new Size(400, 200);
                            selectionForm.StartPosition = FormStartPosition.CenterParent;
                            
                            var listBox = new ListBox();
                            listBox.Items.AddRange(folderNames);
                            listBox.Dock = DockStyle.Fill;
                            listBox.SelectedIndex = 0;
                            
                            var okButton = new Button();
                            okButton.Text = "OK";
                            okButton.DialogResult = DialogResult.OK;
                            okButton.Dock = DockStyle.Bottom;
                            
                            selectionForm.Controls.Add(listBox);
                            selectionForm.Controls.Add(okButton);
                            
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
                        // Create a new independent restore service for this window
                        var independentRestoreService = new ResumableRestoreService(backupFilesFolderPath, backupVersionFolderPath, _encryptionConfig);
                        
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
            
            // Check for existing incomplete restore for directory operations first
            if (string.IsNullOrEmpty(nodeFileName) && !isResume)
            {
                // Look for existing .BackupRestore_ folders in the destination directory
                var existingRestoreFolders = Directory.GetDirectories(destinationDirectory, ".BackupRestore_*")
                    .Where(dir => 
                    {
                        var statusTracker = new RestoreStatusTracker(dir);
                        return statusTracker.CanResume();
                    })
                    .ToArray();
                    
                if (existingRestoreFolders.Length > 0)
                {
                    // Use the most recent existing restore folder
                    tempRestoreFolder = existingRestoreFolders
                        .OrderByDescending(dir => Directory.GetCreationTime(dir))
                        .First();
                        
                    var result = MessageBox.Show(
                        $"Found incomplete restore operation in:\n{Path.GetFileName(tempRestoreFolder)}\n\nDo you want to resume it?", 
                        "Resume Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        isResume = true;
                    }
                    else
                    {
                        // Clean up existing and create fresh
                        foreach (var folder in existingRestoreFolders)
                        {
                            try
                            {
                                Directory.Delete(folder, true);
                            }
                            catch
                            {
                                // Ignore cleanup errors
                            }
                        }
                        tempRestoreFolder = null; // Will create new one below
                    }
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


        private void PopulateTree(string backupFolder)
        {
            try
            {
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

            //fill only the next row of items
            string sfile = "";


            System.IO.StreamReader verFile = new System.IO.StreamReader(backupVersionFolderPath + "\\" + nodeVersion);
            string line = "";
            while ((line = verFile.ReadLine()) != null)
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
            verFile.Close();
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
