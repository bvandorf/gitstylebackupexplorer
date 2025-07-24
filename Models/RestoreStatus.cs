using System;

namespace gitstylebackupexplorer.Models
{
    /// <summary>
    /// Represents the status of a restore operation
    /// </summary>
    public class RestoreStatus
    {
        public string RestoreId { get; set; }
        public string NodeVersion { get; set; }
        public string NodeDirPath { get; set; }
        public string DestinationPath { get; set; }
        public RestorePhase Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Enumeration of restore phases
    /// </summary>
    public enum RestorePhase
    {
        NotStarted,
        Phase1Started,
        Phase1Complete,
        Phase2Started,
        Phase2Complete,
        Complete,
        Failed
    }
}
