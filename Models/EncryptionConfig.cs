using System;

namespace gitstylebackupexplorer.Models
{
    /// <summary>
    /// Configuration for encryption settings when working with encrypted backups
    /// </summary>
    public class EncryptionConfig
    {
        /// <summary>
        /// Password for encryption/decryption (optional)
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Path to encryption key file (optional)
        /// </summary>
        public string KeyFilePath { get; set; }

        /// <summary>
        /// Gets whether encryption is configured (either password or key file)
        /// </summary>
        public bool IsEncryptionEnabled => !string.IsNullOrEmpty(Password) || !string.IsNullOrEmpty(KeyFilePath);

        /// <summary>
        /// Gets the encryption key based on the configuration
        /// </summary>
        /// <returns>32-byte encryption key or null if no encryption configured</returns>
        public byte[] GetEncryptionKey()
        {
            if (!string.IsNullOrEmpty(Password))
            {
                return Services.EncryptionService.DeriveKeyFromPassword(Password);
            }
            else if (!string.IsNullOrEmpty(KeyFilePath))
            {
                return Services.EncryptionService.ReadKeyFromFile(KeyFilePath);
            }

            return null;
        }

        /// <summary>
        /// Validates the encryption configuration
        /// </summary>
        /// <returns>True if valid, false if invalid</returns>
        public bool IsValid()
        {
            if (!IsEncryptionEnabled)
                return true; // No encryption is valid

            if (!string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(KeyFilePath))
                return false; // Can't have both password and key file

            if (!string.IsNullOrEmpty(KeyFilePath))
            {
                return System.IO.File.Exists(KeyFilePath);
            }

            return !string.IsNullOrEmpty(Password);
        }

        /// <summary>
        /// Gets a description of the encryption method being used
        /// </summary>
        public string GetEncryptionDescription()
        {
            if (!IsEncryptionEnabled)
                return "No encryption";

            if (!string.IsNullOrEmpty(Password))
                return "Password-based encryption";

            if (!string.IsNullOrEmpty(KeyFilePath))
                return $"Key file encryption: {KeyFilePath}";

            return "Unknown encryption";
        }
    }
}
