using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using gitstylebackupexplorer.Models;
using gitstylebackupexplorer.Services;

namespace gitstylebackupexplorer
{
    /// <summary>
    /// Dedicated window for restore operations with progress tracking and threading
    /// </summary>
    public partial class RestoreWindow : Form
    {
        private readonly ResumableRestoreService _restoreService;
        private readonly string _nodeVersion;
        private readonly string _nodeDirPath;
        private readonly string _nodeFileName;
        private readonly string _destinationPath;
        private readonly string _tempRestoreFolder;
        private bool _isResume;
        private readonly bool _isSingleFile;

        private CancellationTokenSource _cancellationTokenSource;
        private Task _restoreTask;

        // UI Controls
        private ProgressBar progressBarOverall;
        private ProgressBar progressBarCurrent;
        private Label labelStatus;
        private Label labelOverallProgress;
        private Label labelCurrentProgress;
        private Label labelCurrentFile;
        private Button buttonCancel;
        private Button buttonPause;
        private Button buttonClose;
        private Button buttonRestart;
        private Button buttonResume;
        private TextBox textBoxLog;

        public RestoreWindow(ResumableRestoreService restoreService, string nodeVersion, string nodeDirPath, 
            string nodeFileName, string destinationPath, string tempRestoreFolder, bool isResume)
        {
            _restoreService = restoreService;
            _nodeVersion = nodeVersion;
            _nodeDirPath = nodeDirPath;
            _nodeFileName = nodeFileName;
            _destinationPath = destinationPath;
            _tempRestoreFolder = tempRestoreFolder;
            _isResume = isResume;
            _isSingleFile = !string.IsNullOrEmpty(nodeFileName);

            InitializeComponent();
            SetupEventHandlers();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = _isSingleFile ? $"Restoring File: {_nodeFileName}" : $"Restoring Directory: {_nodeDirPath}";
            this.Size = new Size(600, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Status label
            labelStatus = new Label
            {
                Text = _isResume ? "Resuming restore operation..." : "Starting restore operation...",
                Location = new Point(12, 12),
                Size = new Size(560, 23),
                Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };

            // Overall progress label
            labelOverallProgress = new Label
            {
                Text = "Overall Progress: 0%",
                Location = new Point(12, 45),
                Size = new Size(560, 15)
            };

            // Overall progress bar
            progressBarOverall = new ProgressBar
            {
                Location = new Point(12, 65),
                Size = new Size(560, 23),
                Style = ProgressBarStyle.Continuous
            };

            // Current progress label
            labelCurrentProgress = new Label
            {
                Text = "Current Phase: Initializing...",
                Location = new Point(12, 98),
                Size = new Size(560, 15)
            };

            // Current progress bar
            progressBarCurrent = new ProgressBar
            {
                Location = new Point(12, 118),
                Size = new Size(560, 23),
                Style = ProgressBarStyle.Continuous
            };

            // Current file label
            labelCurrentFile = new Label
            {
                Text = "Current File: ",
                Location = new Point(12, 151),
                Size = new Size(560, 15),
                AutoEllipsis = true
            };

            // Log text box
            textBoxLog = new TextBox
            {
                Location = new Point(12, 176),
                Size = new Size(560, 180),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 8F)
            };

            // Buttons
            buttonPause = new Button
            {
                Text = "Pause",
                Location = new Point(181, 370),
                Size = new Size(75, 23),
                Enabled = false
            };

            buttonResume = new Button
            {
                Text = "Resume",
                Location = new Point(262, 370),
                Size = new Size(75, 23),
                Enabled = false
            };

            buttonRestart = new Button
            {
                Text = "Restart",
                Location = new Point(343, 370),
                Size = new Size(75, 23),
                Enabled = false
            };

            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(424, 370),
                Size = new Size(75, 23)
            };

            buttonClose = new Button
            {
                Text = "Close",
                Location = new Point(505, 370),
                Size = new Size(75, 23),
                Enabled = false
            };

            // Add controls to form
            this.Controls.AddRange(new Control[]
            {
                labelStatus, labelOverallProgress, progressBarOverall,
                labelCurrentProgress, progressBarCurrent, labelCurrentFile,
                textBoxLog, buttonPause, buttonResume, buttonRestart, buttonCancel, buttonClose
            });

            this.ResumeLayout(false);
        }

        private void SetupEventHandlers()
        {
            buttonCancel.Click += ButtonCancel_Click;
            buttonPause.Click += ButtonPause_Click;
            buttonResume.Click += ButtonResume_Click;
            buttonRestart.Click += ButtonRestart_Click;
            buttonClose.Click += ButtonClose_Click;
            this.FormClosing += RestoreWindow_FormClosing;
            this.Load += RestoreWindow_Load;

            // Subscribe to restore service progress events
            _restoreService.ProgressChanged += OnRestoreProgressChanged;
        }

        private void RestoreWindow_Load(object sender, EventArgs e)
        {
            StartRestoreOperation();
        }

        private void StartRestoreOperation()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                buttonPause.Enabled = true;
                buttonPause.Text = "Pause";
                
                LogMessage($"Starting restore operation at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogMessage($"Source: {_nodeDirPath}");
                LogMessage($"Destination: {_destinationPath}");
                LogMessage($"Temp Folder: {_tempRestoreFolder}");
                
                if (_isResume)
                {
                    LogMessage("Resuming incomplete restore operation...");
                }

                _restoreTask = Task.Run(() =>
                {
                    RestoreResult result;
                    
                    if (_isSingleFile)
                    {
                        result = _restoreService.RestoreSingleFile(_nodeVersion, _nodeDirPath, _nodeFileName, _destinationPath);
                    }
                    else
                    {
                        result = _restoreService.StartResumableRestore(_nodeVersion, _nodeDirPath, _destinationPath, _tempRestoreFolder, _isResume, _cancellationTokenSource.Token);
                    }

                    // Update UI on completion
                    this.Invoke(new Action(() => OnRestoreCompleted(result)));
                }, _cancellationTokenSource.Token);

                _restoreTask.ContinueWith(task =>
                {
                    if (task.IsCanceled)
                    {
                        this.Invoke(new Action(() =>
                        {
                            LogMessage("Restore operation was cancelled by user.");
                            OnRestoreCancelled();
                        }));
                    }
                    else if (task.IsFaulted)
                    {
                        this.Invoke(new Action(() =>
                        {
                            LogMessage($"Error during restore: {task.Exception?.GetBaseException()?.Message ?? "Unknown error"}");
                            OnRestoreError(task.Exception?.GetBaseException() ?? new Exception("Unknown error"));
                        }));
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                LogMessage($"Error during restore: {ex.Message}");
                OnRestoreError(ex);
            }
        }

        private void OnRestoreProgressChanged(object sender, RestoreProgressEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, RestoreProgressEventArgs>(OnRestoreProgressChanged), sender, e);
                return;
            }

            var progress = e.Progress;

            // Update overall progress
            progressBarOverall.Value = Math.Min(100, (int)progress.PercentComplete);
            labelOverallProgress.Text = $"Overall Progress: {progress.PercentComplete:F1}% ({progress.ProcessedFiles}/{progress.TotalFiles} files)";

            // Update current phase
            string phaseText;
            switch (progress.CurrentPhase)
            {
                case RestorePhase.Phase1Started:
                    phaseText = "Phase 1: Copying files to temporary location";
                    break;
                case RestorePhase.Phase2Started:
                    phaseText = "Phase 2: Decompressing files to destination";
                    break;
                default:
                    phaseText = progress.StatusMessage;
                    break;
            }
            
            // Add pause indicator if paused
            if (_restoreService.IsPaused)
            {
                phaseText += " (PAUSED)";
            }
            
            labelCurrentProgress.Text = phaseText;

            // Update current file
            if (!string.IsNullOrEmpty(progress.CurrentFileName))
            {
                labelCurrentFile.Text = $"Current File: {progress.CurrentFileName}";
            }

            // Update current phase progress bar
            if (progress.TotalFiles > 0)
            {
                int phaseProgress = (int)((double)progress.ProcessedFiles / progress.TotalFiles * 100);
                progressBarCurrent.Value = Math.Min(100, phaseProgress);
            }

            // Log progress periodically
            if (progress.ProcessedFiles % 10 == 0 || progress.ProcessedFiles == progress.TotalFiles)
            {
                LogMessage($"Progress: {progress.ProcessedFiles}/{progress.TotalFiles} files - {progress.CurrentFileName}");
            }
        }

        private void OnRestoreCompleted(RestoreResult result)
        {
            buttonPause.Enabled = false;
            buttonPause.Text = "Pause";
            buttonResume.Enabled = false;
            buttonRestart.Enabled = true;
            buttonCancel.Enabled = false;
            buttonClose.Enabled = true;

            if (result.Success)
            {
                labelStatus.Text = "Restore completed successfully!";
                labelStatus.ForeColor = Color.Green;
                progressBarOverall.Value = 100;
                progressBarCurrent.Value = 100;
                LogMessage("✓ Restore operation completed successfully!");
            }
            else
            {
                labelStatus.Text = "Restore completed with errors";
                labelStatus.ForeColor = Color.Orange;
                LogMessage($"⚠ Restore completed with issues: {result.Message}");
                
                if (result.CanResume)
                {
                    LogMessage($"Resume information saved in: {result.TempFolder}");
                }
            }
        }

        private void OnRestoreCancelled()
        {
            labelStatus.Text = "Restore operation cancelled";
            labelStatus.ForeColor = Color.Red;
            buttonPause.Enabled = false;
            buttonPause.Text = "Pause";
            
            // Check if we can resume after cancellation
            bool canResume = false;
            if (!_isSingleFile && Directory.Exists(_tempRestoreFolder))
            {
                var statusTracker = new RestoreStatusTracker(_tempRestoreFolder);
                canResume = statusTracker.CanResume();
            }
            
            buttonResume.Enabled = canResume;
            buttonRestart.Enabled = true;
            buttonCancel.Enabled = false;
            buttonClose.Enabled = true;
        }

        private void OnRestoreError(Exception ex)
        {
            labelStatus.Text = "Restore operation failed";
            labelStatus.ForeColor = Color.Red;
            LogMessage($"✗ Error: {ex.Message}");
            buttonPause.Enabled = false;
            buttonPause.Text = "Pause";
            
            // Check if we can resume after error
            bool canResume = false;
            if (!_isSingleFile && Directory.Exists(_tempRestoreFolder))
            {
                var statusTracker = new RestoreStatusTracker(_tempRestoreFolder);
                canResume = statusTracker.CanResume();
            }
            
            buttonResume.Enabled = canResume;
            buttonRestart.Enabled = true;
            buttonCancel.Enabled = false;
            buttonClose.Enabled = true;
        }

        private void LogMessage(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(LogMessage), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            textBoxLog.AppendText($"[{timestamp}] {message}\r\n");
            textBoxLog.ScrollToCaret();
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to cancel the restore operation?", 
                "Cancel Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                _cancellationTokenSource?.Cancel();
                LogMessage("Cancellation requested by user...");
            }
        }

        private void ButtonPause_Click(object sender, EventArgs e)
        {
            if (_restoreService.IsPaused)
            {
                // Resume the operation
                _restoreService.Resume();
                buttonPause.Text = "Pause";
                labelStatus.Text = "Restore operation resumed";
                labelStatus.ForeColor = Color.Black;
                LogMessage("Restore operation resumed by user");
                
                // Disable close button when resuming
                buttonClose.Enabled = false;
            }
            else
            {
                // Pause the operation
                _restoreService.Pause();
                buttonPause.Text = "Resume";
                labelStatus.Text = "Restore operation paused - You can safely close this window";
                labelStatus.ForeColor = Color.Orange;
                LogMessage("Restore operation paused by user");
                
                // Enable close button when paused - user can safely exit
                buttonClose.Enabled = true;
            }
        }

        private void ButtonResume_Click(object sender, EventArgs e)
        {
            // Check if there's an existing backup folder that could be resumed
            if (!_isSingleFile && Directory.Exists(_tempRestoreFolder))
            {
                var statusTracker = new RestoreStatusTracker(_tempRestoreFolder);
                if (statusTracker.CanResume())
                {
                    var result = MessageBox.Show(
                        "Resume the incomplete restore operation?", 
                        "Resume Restore", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Question);
                        
                    if (result == DialogResult.Yes)
                    {
                        // Cancel current operation
                        _cancellationTokenSource?.Cancel();
                        
                        // Reset UI state for resume
                        progressBarOverall.Value = 0;
                        progressBarCurrent.Value = 0;
                        labelStatus.Text = "Resuming restore operation...";
                        labelStatus.ForeColor = Color.Blue;
                        labelOverallProgress.Text = "Overall Progress: 0%";
                        labelCurrentProgress.Text = "Current Phase: Initializing...";
                        labelCurrentFile.Text = "Current File: ";
                        textBoxLog.Clear();
                        
                        // Reset button states
                        buttonPause.Enabled = true;
                        buttonPause.Text = "Pause";
                        buttonResume.Enabled = false;
                        buttonRestart.Enabled = false;
                        buttonCancel.Enabled = true;
                        buttonClose.Enabled = false;
                        
                        // Set resume mode
                        _isResume = true;
                        LogMessage("Resuming incomplete restore operation by user request");
                        
                        // Start the restore operation
                        StartRestoreOperation();
                    }
                }
                else
                {
                    MessageBox.Show("No resumable restore operation found.", "Resume", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("No resumable restore operation found.", "Resume", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ButtonRestart_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to restart the restore operation? This will cancel the current operation and start completely over.", 
                "Restart Restore", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Question);
                
            if (result == DialogResult.Yes)
            {
                // Cancel current operation
                _cancellationTokenSource?.Cancel();
                
                // Reset UI state
                progressBarOverall.Value = 0;
                progressBarCurrent.Value = 0;
                labelStatus.Text = "Restarting restore operation...";
                labelStatus.ForeColor = Color.Blue;
                labelOverallProgress.Text = "Overall Progress: 0%";
                labelCurrentProgress.Text = "Current Phase: Initializing...";
                labelCurrentFile.Text = "Current File: ";
                textBoxLog.Clear();
                
                // Reset button states
                buttonPause.Enabled = true;
                buttonPause.Text = "Pause";
                buttonResume.Enabled = false;
                buttonRestart.Enabled = false;
                buttonCancel.Enabled = true;
                buttonClose.Enabled = false;
                
                // Set fresh start mode
                _isResume = false;
                
                // Clean up any existing temp folder
                if (Directory.Exists(_tempRestoreFolder))
                {
                    try
                    {
                        Directory.Delete(_tempRestoreFolder, true);
                        LogMessage("Cleaned up previous restore attempt");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Warning: Could not clean up temp folder: {ex.Message}");
                    }
                }
                
                // Recreate the temp folder for fresh start
                try
                {
                    if (!Directory.Exists(_tempRestoreFolder))
                    {
                        Directory.CreateDirectory(_tempRestoreFolder);
                        LogMessage("Created fresh temp folder for restart");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error creating temp folder: {ex.Message}");
                    MessageBox.Show($"Could not create temp folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                LogMessage("Starting fresh restore operation by user request");
                
                // Start the restore operation
                StartRestoreOperation();
            }
        }

        private void ButtonClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void RestoreWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_restoreTask != null && !_restoreTask.IsCompleted)
            {
                // If the operation is paused, allow safe exit without canceling
                if (_restoreService.IsPaused)
                {
                    var result = MessageBox.Show(
                        "The restore operation is paused and can be resumed later using 'Resume from Folder' in the File menu.\n\n" +
                        "Do you want to close this window? The operation will remain paused and can be resumed.", 
                        "Paused Restore Operation", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Question);
                    
                    if (result == DialogResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                    
                    // Don't cancel the operation - just close the window
                    LogMessage("Window closed while operation is paused - operation can be resumed later");
                }
                else
                {
                    // Operation is running, ask if they want to cancel it
                    var result = MessageBox.Show(
                        "A restore operation is still running. Do you want to cancel it and close the window?", 
                        "Restore In Progress", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Warning);
                    
                    if (result == DialogResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                    
                    _cancellationTokenSource?.Cancel();
                    LogMessage("Restore operation cancelled due to window closing");
                }
            }

            // Unsubscribe from events
            _restoreService.ProgressChanged -= OnRestoreProgressChanged;
            
            // Clean up resources
            _cancellationTokenSource?.Dispose();
        }
    }
}
