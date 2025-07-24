using System;

namespace gitstylebackupexplorer.Models
{
    /// <summary>
    /// Represents information about a file that needs to be restored
    /// </summary>
    public class FileRestoreInfo
    {
        /// <summary>
        /// The original file path in the backup
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The hash identifier used to locate the file in backup storage
        /// </summary>
        public string Hash { get; set; }
    }
}
