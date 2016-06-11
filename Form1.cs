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
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Backup DB File (*.db) | *.db;";
            openFile.Multiselect = false;
            if (openFile.ShowDialog() == DialogResult.OK)
            {
                PopulateTree(openFile.FileName);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //exit
            Application.Exit();
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
                    string sinfo = "";
                    sinfo += "Name: " + db.Version[nodeVersion].File[nodeDirPath + "/" + nodeFileName].Name + "\n";
                    sinfo += "Directory: " + nodeDirPath + "\n";
                    sinfo += "Version: " + db.Version[nodeVersion].Number.ToString() + "\n";
                    sinfo += "Date: " + db.Version[nodeVersion].File[nodeDirPath + "/" + nodeFileName].Date.ToShortDateString() + " " + db.Version[nodeVersion].File[nodeDirPath + "/" + nodeFileName].Date.ToLongTimeString() + "\n";
                    sinfo += "Date Modified: " + db.Version[nodeVersion].File[nodeDirPath + "/" + nodeFileName].DateModified.ToShortDateString() + " " + db.Version[nodeVersion].File[nodeDirPath + "/" + nodeFileName].DateModified.ToLongTimeString() + "\n";
                    sinfo += "Size: " + db.Version[nodeVersion].File[nodeDirPath + "/" + nodeFileName].Size + " MB\n";
                    sinfo += "Hash: " + HashToString(db.Version[nodeVersion].File[nodeDirPath + "/" + nodeFileName].Hash) + "\n";

                    MessageBox.Show(sinfo, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    //dir
                    string sinfo = "";
                    sinfo += "Directory: " + nodeDirPath + "\n";
                    sinfo += "Version: " + db.Version[nodeVersion].Number.ToString() + "\n";
                    sinfo += "Date: " + db.Version[nodeVersion].Date.ToShortDateString() + " " + db.Version[nodeVersion].Date.ToLongTimeString() + "\n";
                    sinfo += "Hash: " + HashToString(db.Version[nodeVersion].Hash) + "\n";

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

                System.IO.FileInfo fiConfigFileInfo = new System.IO.FileInfo(configFilePath);

                if (nodeFileName != "")
                {
                    //file
                    string HashFileName = "";
                    SaveFileDialog saveFile = new SaveFileDialog();
                    saveFile.FileName = nodeFileName;
                    saveFile.OverwritePrompt = true;
                    if (saveFile.ShowDialog() == DialogResult.OK)
                    {
                        string StatusMessage = "Done";
                        bool ErrorStatus = false;
                        foreach (string key in db.Version[nodeVersion].File.Keys)
                        {
                            if (key == nodeDirPath + "/" + nodeFileName)
                            {
                                HashFileName = HashToString(db.Version[nodeVersion].File[key].Hash);
                                string sourceFile = fiConfigFileInfo.Directory.FullName + "\\Files\\" + HashFileName.Substring(0, 2) + "\\" + HashFileName;
                                string destFile = saveFile.FileName;

                                try
                                {
                                    System.IO.File.Copy(sourceFile, destFile, true);
                                }
                                catch (Exception ex)
                                {
                                    StatusMessage = "File Restore Did Not Complete\n" + ex.ToString();
                                    ErrorStatus = true;
                                }
                            }
                        }
                        if (ErrorStatus)
                            MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else
                            MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    //dir
                    string HashFileName = "";
                    FolderBrowserDialog saveFolder = new FolderBrowserDialog();
                    if (saveFolder.ShowDialog() == DialogResult.OK)
                    {
                        string StatusMessage = "Done";
                        bool ErrorStatus = false;
                        foreach (string key in db.Version[nodeVersion].File.Keys)
                        {
                            if (key.StartsWith(nodeDirPath))
                            {
                                HashFileName = HashToString(db.Version[nodeVersion].File[key].Hash);
                                string sourceFile = fiConfigFileInfo.Directory.FullName + "\\Files\\" + HashFileName.Substring(0,2) + "\\" + HashFileName;
                                string destFile = key.Replace("/", "\\").Replace(nodeDirPath, saveFolder.SelectedPath);

                                try
                                {
                                    System.IO.FileInfo destFileInfo = new System.IO.FileInfo(destFile);
                                    if (!destFileInfo.Directory.Exists)
                                        System.IO.Directory.CreateDirectory(destFileInfo.Directory.FullName);

                                    System.IO.File.Copy(sourceFile, destFile);
                                }
                                catch (Exception ex)
                                {
                                    StatusMessage = "File Restore Did Not Complete\n" + ex.ToString();
                                    ErrorStatus = true;
                                }
                            }
                        }
                        if (ErrorStatus)
                            MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else
                            MessageBox.Show(StatusMessage, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private string HashToString(byte[] hash)
        {
            string shash = "";
            foreach(byte b in hash)
            {
                shash += b.ToString("000");
            }
            return shash;
        }

        class bdb
        {
            public bool Inuse;
            public Dictionary<string, bdb_version> Version = new Dictionary<string, bdb_version>();
        }

        class bdb_version
        {
            public int Number;
            public Dictionary<string, bdb_version_file> File = new Dictionary<string, bdb_version_file>();
            public byte[] Hash;
            public DateTime Date = new DateTime();
        }

        class bdb_version_file
        {
            public string Name;
            public byte[] Hash;
            public DateTime Date;
            public DateTime DateModified;
            public string Size;
        }

        bdb db = new bdb();
        string configFilePath = "";
        private void PopulateTree(string cfgFile)
        {
            try
            {
                configFilePath = cfgFile;
                LastNodeClick = null;

                db = Newtonsoft.Json.JsonConvert.DeserializeObject<bdb>(System.IO.File.ReadAllText(cfgFile));

                treeView1.Nodes.Clear();
                //add tree item for each version
                foreach(string key in db.Version.Keys)
                {
                    bdb_version ver = db.Version[key];

                    TreeNode tn = new TreeNode(ver.Number.ToString() + " : " + ver.Date.ToShortDateString() + " " + ver.Date.ToLongTimeString());
                    tn.Tag = ver.Number.ToString() + "~";
                    tn.Nodes.Add("");
                    treeView1.Nodes.Add(tn);
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
            bdb_version ver = db.Version[nodeVersion];

            List<string> sortedKeys = new List<string>();
            foreach(string key in ver.File.Keys)
            {
                sortedKeys.Add(key);
            }
            sortedKeys.Sort();

            foreach(string sfile in sortedKeys)
            {
                if (sfile.StartsWith(nodeKey) || sfile.StartsWith(nodeKey.TrimEnd('\\')+"/"))
                {
                    string stmp = sfile;
                    if (nodeKey != "")
                    {
                        stmp = sfile.Replace(nodeKey, "");
                        stmp = stmp.Replace(nodeKey.TrimEnd('\\') + "/", "");
                    }

                    string[] split = stmp.Split(new char[] { '\\', '/' });
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
            }
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
