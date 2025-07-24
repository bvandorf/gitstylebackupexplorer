namespace gitstylebackupexplorer.Models
{
    /// <summary>
    /// Represents the result of a restore operation
    /// </summary>
    public class RestoreResult
    {
        /// <summary>
        /// Indicates if the restore operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Optional message describing the result or any issues
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the operation can be resumed (for partial failures)
        /// </summary>
        public bool CanResume { get; set; }

        /// <summary>
        /// Path to the temporary folder where resume data is stored
        /// </summary>
        public string TempFolder { get; set; } = string.Empty;

        /// <summary>
        /// Number of files successfully processed
        /// </summary>
        public int ProcessedFiles { get; set; }

        /// <summary>
        /// Total number of files in the operation
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static RestoreResult CreateSuccess(string message = "Operation completed successfully")
        {
            return new RestoreResult
            {
                Success = true,
                Message = message
            };
        }

        /// <summary>
        /// Creates a failed result
        /// </summary>
        public static RestoreResult CreateFailure(string message, bool canResume = false, string tempFolder = "")
        {
            return new RestoreResult
            {
                Success = false,
                Message = message,
                CanResume = canResume,
                TempFolder = tempFolder
            };
        }
    }
}
