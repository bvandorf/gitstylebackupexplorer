using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gitstylebackupexplorer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
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

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //exit
            Application.Exit();
        }

        private void DecompressToFile(string source, string dest)
        {
            using (System.IO.Compression.GZipStream stream = new System.IO.Compression.GZipStream(new System.IO.FileStream(source, System.IO.FileMode.Open), System.IO.Compression.CompressionMode.Decompress))
            using (System.IO.FileStream outstream = new System.IO.FileStream(dest, System.IO.FileMode.Create))
            {
                stream.CopyTo(outstream);

                outstream.Flush();
                outstream.Close();
                stream.Close();
            }
        }

        static readonly string[] SizeSuffixes =
                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(long value)
        {
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return "0.0 bytes"; }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
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
            //restore
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

                    System.IO.StreamReader verFile = new System.IO.StreamReader(backupVersionFolderPath + "\\" + nodeVersion);
                    string line = "";
                    bool bfound = false;
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
                        else if (line.StartsWith("HASH:"))
                        {
                            shash = line.Substring(("HASH:").Length);
                        }
                    }
                    verFile.Close();

                    if (bfound)
                    {
                        SaveFileDialog saveFile = new SaveFileDialog();
                        saveFile.FileName = nodeFileName;
                        saveFile.OverwritePrompt = true;
                        if (saveFile.ShowDialog() == DialogResult.OK)
                        {
                            string StatusMessage = "Done";
                            bool ErrorStatus = false;
                            string sourceFile = backupFilesFolderPath + "\\" + shash.Substring(0, 2) + "\\" + shash;
                            string destFile = saveFile.FileName;

                            try
                            {
                                DecompressToFile(sourceFile, destFile);
                            }
                            catch (Exception ex)
                            {
                                StatusMessage = "File Restore Did Not Complete\n" + ex.ToString();
                                ErrorStatus = true;
                            }

                            if (ErrorStatus)
                                MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            else
                                MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Could Not Find File", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    //dir
                    FolderBrowserDialog saveFolder = new FolderBrowserDialog();
                    if (saveFolder.ShowDialog() == DialogResult.OK)
                    {
                        string StatusMessage = "Done";
                        bool ErrorStatus = false;

                        string sfile = "";
                        string shash = "";

                        System.IO.StreamReader verFile = new System.IO.StreamReader(backupVersionFolderPath + "\\" + nodeVersion);
                        string line = "";
                        while ((line = verFile.ReadLine()) != null)
                        {
                            if (line.StartsWith("FILE:"))
                            {
                                sfile = line.Substring(("FILE:").Length);
                            }
                            else if (line.StartsWith("HASH:"))
                            {
                                shash = line.Substring(("HASH:").Length);
                            }

                            if (sfile != "" && shash != "")
                            {
                                if (sfile.StartsWith(nodeDirPath))
                                {
                                    string sourceFile = backupFilesFolderPath + "\\" + shash.Substring(0, 2) + "\\" + shash;
                                    string destFile = sfile.Replace(nodeDirPath, saveFolder.SelectedPath);

                                    try
                                    {
                                        System.IO.FileInfo destFileInfo = new System.IO.FileInfo(destFile);
                                        if (!destFileInfo.Directory.Exists)
                                            System.IO.Directory.CreateDirectory(destFileInfo.Directory.FullName);

                                        DecompressToFile(sourceFile, destFile);
                                    }
                                    catch (Exception ex)
                                    {
                                        StatusMessage = "File Restore Did Not Complete\n" + ex.ToString();
                                        ErrorStatus = true;
                                    }
                                }

                                sfile = "";
                                shash = "";
                            }
                        }
                        verFile.Close();

                        if (ErrorStatus)
                            MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else
                            MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }


        
        string backupFolderPath = "";
        string backupInUseFile = "";
        string backupVersionFolderPath = "";
        string backupFilesFolderPath = "";
        private void PopulateTree(string backupFolder)
        {
            try
            {
                backupFolderPath = backupFolder;
                backupInUseFile = backupFolderPath + "\\InUse.txt";
                backupVersionFolderPath = backupFolderPath + "\\Version";
                backupFilesFolderPath = backupFolderPath + "\\Files";


                LastNodeClick = null;

                treeView1.Nodes.Clear();
                //add tree item for each version
                foreach(string file in System.IO.Directory.GetFiles(backupVersionFolderPath))
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
                MessageBox.Show("Error Opening Backup File", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        TreeNode LastNodeClick;
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
