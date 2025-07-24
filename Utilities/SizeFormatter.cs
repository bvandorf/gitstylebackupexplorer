using System;

namespace gitstylebackupexplorer.Utilities
{
    /// <summary>
    /// Utility class for formatting file sizes in human-readable format
    /// </summary>
    public static class SizeFormatter
    {
        private static readonly string[] SizeSuffixes = 
            { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        /// <summary>
        /// Formats a byte count as a human-readable size string
        /// </summary>
        /// <param name="value">Size in bytes</param>
        /// <returns>Formatted size string (e.g., "1.5 MB")</returns>
        public static string FormatSize(long value)
        {
            if (value < 0) 
            { 
                return "-" + FormatSize(-value); 
            }
            
            if (value == 0) 
            { 
                return "0.0 bytes"; 
            }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }
    }
}
