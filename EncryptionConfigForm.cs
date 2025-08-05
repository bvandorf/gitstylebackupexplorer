using System;
using System.IO;
using System.Windows.Forms;
using gitstylebackupexplorer.Models;

namespace gitstylebackupexplorer
{
    /// <summary>
    /// Form for configuring encryption settings
    /// </summary>
    public partial class EncryptionConfigForm : Form
    {
        private EncryptionConfig _config;
        
        private RadioButton _noEncryptionRadio;
        private RadioButton _passwordRadio;
        private RadioButton _keyFileRadio;
        private TextBox _passwordTextBox;
        private TextBox _keyFileTextBox;
        private Button _browseKeyFileButton;
        private Button _okButton;
        private Button _cancelButton;
        private Label _statusLabel;

        public EncryptionConfig EncryptionConfig
        {
            get { return _config; }
            set 
            { 
                _config = value ?? new EncryptionConfig();
                UpdateUI();
            }
        }

        public EncryptionConfigForm()
        {
            InitializeComponent();
            _config = new EncryptionConfig();
        }

        public EncryptionConfigForm(EncryptionConfig config)
        {
            InitializeComponent();
            _config = config ?? new EncryptionConfig();
            UpdateUI();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "Encryption Configuration";
            this.Size = new System.Drawing.Size(450, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // No encryption radio button
            _noEncryptionRadio = new RadioButton();
            _noEncryptionRadio.Text = "No encryption (for unencrypted backups)";
            _noEncryptionRadio.Location = new System.Drawing.Point(20, 20);
            _noEncryptionRadio.Size = new System.Drawing.Size(300, 20);
            _noEncryptionRadio.Checked = true;
            _noEncryptionRadio.CheckedChanged += OnEncryptionTypeChanged;

            // Password radio button
            _passwordRadio = new RadioButton();
            _passwordRadio.Text = "Password encryption";
            _passwordRadio.Location = new System.Drawing.Point(20, 50);
            _passwordRadio.Size = new System.Drawing.Size(200, 20);
            _passwordRadio.CheckedChanged += OnEncryptionTypeChanged;

            // Password text box
            _passwordTextBox = new TextBox();
            _passwordTextBox.Location = new System.Drawing.Point(40, 75);
            _passwordTextBox.Size = new System.Drawing.Size(350, 20);
            _passwordTextBox.UseSystemPasswordChar = true;
            _passwordTextBox.Enabled = false;

            // Key file radio button
            _keyFileRadio = new RadioButton();
            _keyFileRadio.Text = "Key file encryption";
            _keyFileRadio.Location = new System.Drawing.Point(20, 110);
            _keyFileRadio.Size = new System.Drawing.Size(200, 20);
            _keyFileRadio.CheckedChanged += OnEncryptionTypeChanged;

            // Key file text box
            _keyFileTextBox = new TextBox();
            _keyFileTextBox.Location = new System.Drawing.Point(40, 135);
            _keyFileTextBox.Size = new System.Drawing.Size(270, 20);
            _keyFileTextBox.Enabled = false;

            // Browse key file button
            _browseKeyFileButton = new Button();
            _browseKeyFileButton.Text = "Browse...";
            _browseKeyFileButton.Location = new System.Drawing.Point(320, 133);
            _browseKeyFileButton.Size = new System.Drawing.Size(70, 25);
            _browseKeyFileButton.Enabled = false;
            _browseKeyFileButton.Click += OnBrowseKeyFile;

            // Status label
            _statusLabel = new Label();
            _statusLabel.Location = new System.Drawing.Point(20, 170);
            _statusLabel.Size = new System.Drawing.Size(400, 40);
            _statusLabel.Text = "No encryption configured";
            _statusLabel.ForeColor = System.Drawing.Color.Blue;

            // OK button
            _okButton = new Button();
            _okButton.Text = "OK";
            _okButton.Location = new System.Drawing.Point(230, 220);
            _okButton.Size = new System.Drawing.Size(75, 25);
            _okButton.DialogResult = DialogResult.OK;
            _okButton.Click += OnOK;

            // Cancel button
            _cancelButton = new Button();
            _cancelButton.Text = "Cancel";
            _cancelButton.Location = new System.Drawing.Point(315, 220);
            _cancelButton.Size = new System.Drawing.Size(75, 25);
            _cancelButton.DialogResult = DialogResult.Cancel;

            // Add controls to form
            this.Controls.Add(_noEncryptionRadio);
            this.Controls.Add(_passwordRadio);
            this.Controls.Add(_passwordTextBox);
            this.Controls.Add(_keyFileRadio);
            this.Controls.Add(_keyFileTextBox);
            this.Controls.Add(_browseKeyFileButton);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_okButton);
            this.Controls.Add(_cancelButton);

            this.ResumeLayout(false);
        }

        private void UpdateUI()
        {
            if (!_config.IsEncryptionEnabled)
            {
                _noEncryptionRadio.Checked = true;
            }
            else if (!string.IsNullOrEmpty(_config.Password))
            {
                _passwordRadio.Checked = true;
                _passwordTextBox.Text = _config.Password;
            }
            else if (!string.IsNullOrEmpty(_config.KeyFilePath))
            {
                _keyFileRadio.Checked = true;
                _keyFileTextBox.Text = _config.KeyFilePath;
            }

            OnEncryptionTypeChanged(null, null);
            UpdateStatus();
        }

        private void OnEncryptionTypeChanged(object sender, EventArgs e)
        {
            _passwordTextBox.Enabled = _passwordRadio.Checked;
            _keyFileTextBox.Enabled = _keyFileRadio.Checked;
            _browseKeyFileButton.Enabled = _keyFileRadio.Checked;

            UpdateStatus();
        }

        private void OnBrowseKeyFile(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select Encryption Key File";
                openFileDialog.Filter = "All Files (*.*)|*.*";
                
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    _keyFileTextBox.Text = openFileDialog.FileName;
                    UpdateStatus();
                }
            }
        }

        private void UpdateStatus()
        {
            var tempConfig = GetConfigFromUI();
            
            if (!tempConfig.IsEncryptionEnabled)
            {
                _statusLabel.Text = "No encryption - for unencrypted backups";
                _statusLabel.ForeColor = System.Drawing.Color.Blue;
            }
            else if (tempConfig.IsValid())
            {
                _statusLabel.Text = $"✓ {tempConfig.GetEncryptionDescription()}";
                _statusLabel.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                _statusLabel.Text = "⚠ Invalid encryption configuration";
                _statusLabel.ForeColor = System.Drawing.Color.Red;
            }
        }

        private EncryptionConfig GetConfigFromUI()
        {
            var config = new EncryptionConfig();

            if (_passwordRadio.Checked)
            {
                config.Password = _passwordTextBox.Text;
            }
            else if (_keyFileRadio.Checked)
            {
                config.KeyFilePath = _keyFileTextBox.Text;
            }

            return config;
        }

        private void OnOK(object sender, EventArgs e)
        {
            var tempConfig = GetConfigFromUI();
            
            if (!tempConfig.IsValid())
            {
                MessageBox.Show("Please fix the encryption configuration before continuing.", 
                    "Invalid Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config = tempConfig;
        }
    }
}
