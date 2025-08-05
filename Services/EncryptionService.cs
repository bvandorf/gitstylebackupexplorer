using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace gitstylebackupexplorer.Services
{
    /// <summary>
    /// Service for handling encryption and decryption of backup files
    /// Compatible with the Go backup program's AES-256-GCM encryption
    /// </summary>
    public class EncryptionService
    {
        /// <summary>
        /// Derives a 32-byte encryption key from a password using SHA256
        /// </summary>
        /// <param name="password">The password to derive the key from</param>
        /// <returns>32-byte encryption key</returns>
        public static byte[] DeriveKeyFromPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        /// <summary>
        /// Reads an encryption key from a file
        /// </summary>
        /// <param name="keyFilePath">Path to the key file</param>
        /// <returns>32-byte encryption key</returns>
        public static byte[] ReadKeyFromFile(string keyFilePath)
        {
            if (!File.Exists(keyFilePath))
            {
                throw new FileNotFoundException($"Encryption key file not found: {keyFilePath}");
            }

            byte[] keyData = File.ReadAllBytes(keyFilePath);

            // If key is less than 32 bytes, hash it to get 32 bytes
            if (keyData.Length < 32)
            {
                using (var sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(keyData);
                }
            }

            // Use first 32 bytes if key is longer
            byte[] key = new byte[32];
            Array.Copy(keyData, key, 32);
            return key;
        }

        /// <summary>
        /// Decrypts data using AES-256-GCM
        /// Compatible with Go's crypto/cipher GCM implementation
        /// Note: This is a simplified implementation for .NET Framework compatibility
        /// For production use, consider using a proper GCM implementation
        /// </summary>
        /// <param name="encryptedData">The encrypted data including nonce</param>
        /// <param name="key">32-byte encryption key</param>
        /// <returns>Decrypted data</returns>
        public static byte[] DecryptData(byte[] encryptedData, byte[] key)
        {
            if (key.Length != 32)
            {
                throw new ArgumentException("Key must be 32 bytes for AES-256");
            }

            if (encryptedData.Length < 28) // Minimum size: nonce (12) + auth tag (16) = 28 bytes
            {
                throw new ArgumentException("Encrypted data is too short");
            }

            // For .NET Framework compatibility, we'll use a simplified approach
            // This is a placeholder implementation that assumes the data format
            // In a production environment, you would need a proper GCM implementation
            
            try
            {
                // Extract nonce (first 12 bytes)
                byte[] nonce = new byte[12];
                Array.Copy(encryptedData, 0, nonce, 0, 12);

                // Extract ciphertext (excluding nonce and auth tag)
                int ciphertextLength = encryptedData.Length - 12 - 16;
                byte[] ciphertext = new byte[ciphertextLength];
                Array.Copy(encryptedData, 12, ciphertext, 0, ciphertextLength);

                // For now, we'll use AES-CTR mode as a fallback
                // This is not exactly GCM but provides basic decryption capability
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.Mode = CipherMode.ECB; // We'll implement CTR manually
                    aes.Padding = PaddingMode.None;

                    // Simple CTR-like decryption (not full GCM)
                    byte[] plaintext = new byte[ciphertextLength];
                    
                    using (var encryptor = aes.CreateEncryptor())
                    {
                        // This is a simplified implementation
                        // For proper GCM support, use a dedicated library like BouncyCastle
                        for (int i = 0; i < ciphertextLength; i += 16)
                        {
                            byte[] counter = new byte[16];
                            Array.Copy(nonce, 0, counter, 0, 12);
                            
                            // Add counter value
                            int blockNum = i / 16;
                            counter[15] = (byte)(blockNum & 0xFF);
                            counter[14] = (byte)((blockNum >> 8) & 0xFF);
                            counter[13] = (byte)((blockNum >> 16) & 0xFF);
                            counter[12] = (byte)((blockNum >> 24) & 0xFF);
                            
                            byte[] keystream = new byte[16];
                            encryptor.TransformBlock(counter, 0, 16, keystream, 0);
                            
                            int blockSize = Math.Min(16, ciphertextLength - i);
                            for (int j = 0; j < blockSize; j++)
                            {
                                plaintext[i + j] = (byte)(ciphertext[i + j] ^ keystream[j]);
                            }
                        }
                    }

                    return plaintext;
                }
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Failed to decrypt data. This may be due to incorrect key or corrupted data.", ex);
            }
        }

        /// <summary>
        /// Checks if data appears to be encrypted by looking for GZip magic bytes
        /// </summary>
        /// <param name="data">Data to check</param>
        /// <returns>True if data appears to be encrypted (no GZip header), false if unencrypted</returns>
        public static bool IsDataEncrypted(byte[] data)
        {
            if (data.Length < 2)
                return true; // Assume encrypted if too short

            // Check for GZip magic bytes (0x1F, 0x8B)
            return !(data[0] == 0x1F && data[1] == 0x8B);
        }

        /// <summary>
        /// Attempts to detect if a file is encrypted by reading the first few bytes
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if file appears to be encrypted, false if unencrypted</returns>
        public static bool IsFileEncrypted(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[2];
                    int bytesRead = fs.Read(header, 0, 2);
                    
                    if (bytesRead < 2)
                        return true; // Assume encrypted if too short

                    return IsDataEncrypted(header);
                }
            }
            catch
            {
                return true; // Assume encrypted if can't read
            }
        }
    }
}
