using System;

namespace gitstylebackupexplorer.Models
{
    /// <summary>
    /// Represents progress information for a restore operation
    /// </summary>
    public class RestoreProgress
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public RestorePhase CurrentPhase { get; set; }
        public string CurrentFileName { get; set; }
        public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
        public string StatusMessage { get; set; }
    }

    /// <summary>
    /// Event args for restore progress updates
    /// </summary>
    public class RestoreProgressEventArgs : EventArgs
    {
        public RestoreProgress Progress { get; set; }

        public RestoreProgressEventArgs(RestoreProgress progress)
        {
            Progress = progress;
        }
    }
}
