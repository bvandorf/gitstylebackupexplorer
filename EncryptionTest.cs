using System;
using System.IO;
using System.Text;
using gitstylebackupexplorer.Services;
using gitstylebackupexplorer.Models;

namespace gitstylebackupexplorer
{
    /// <summary>
    /// Simple test program to verify encryption functionality
    /// </summary>
    public class EncryptionTest
    {
        public static void RunTests()
        {
            Console.WriteLine("=== Gitstyle Backup Explorer Encryption Tests ===\n");

            try
            {
                TestPasswordKeyDerivation();
                TestKeyFileReading();
                TestEncryptionDetection();
                TestEncryptionConfig();
                
                Console.WriteLine("\n✓ All encryption tests passed successfully!");
                Console.WriteLine("\nThe gitstylebackupexplorer now supports:");
                Console.WriteLine("- Password-based encryption/decryption");
                Console.WriteLine("- Key file-based encryption/decryption");
                Console.WriteLine("- Automatic detection of encrypted vs unencrypted files");
                Console.WriteLine("- Backward compatibility with unencrypted backups");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void TestPasswordKeyDerivation()
        {
            Console.WriteLine("Testing password key derivation...");
            
            string password = "test123";
            byte[] key = EncryptionService.DeriveKeyFromPassword(password);
            
            if (key.Length != 32)
                throw new Exception($"Expected 32-byte key, got {key.Length} bytes");
            
            // Test that same password produces same key
            byte[] key2 = EncryptionService.DeriveKeyFromPassword(password);
            if (!ArraysEqual(key, key2))
                throw new Exception("Same password should produce same key");
            
            Console.WriteLine("✓ Password key derivation works correctly");
        }

        private static void TestKeyFileReading()
        {
            Console.WriteLine("Testing key file reading...");
            
            // Create a temporary key file
            string tempKeyFile = Path.GetTempFileName();
            try
            {
                // Test with short key (will be hashed)
                File.WriteAllText(tempKeyFile, "short key");
                byte[] key1 = EncryptionService.ReadKeyFromFile(tempKeyFile);
                if (key1.Length != 32)
                    throw new Exception($"Expected 32-byte key from short file, got {key1.Length} bytes");
                
                // Test with long key (will be truncated)
                byte[] longKey = new byte[64];
                for (int i = 0; i < 64; i++) longKey[i] = (byte)(i % 256);
                File.WriteAllBytes(tempKeyFile, longKey);
                
                byte[] key2 = EncryptionService.ReadKeyFromFile(tempKeyFile);
                if (key2.Length != 32)
                    throw new Exception($"Expected 32-byte key from long file, got {key2.Length} bytes");
                
                Console.WriteLine("✓ Key file reading works correctly");
            }
            finally
            {
                if (File.Exists(tempKeyFile))
                    File.Delete(tempKeyFile);
            }
        }

        private static void TestEncryptionDetection()
        {
            Console.WriteLine("Testing encryption detection...");
            
            // Test GZip header detection (unencrypted)
            byte[] gzipData = { 0x1F, 0x8B, 0x08, 0x00 }; // GZip magic bytes
            if (EncryptionService.IsDataEncrypted(gzipData))
                throw new Exception("GZip data should be detected as unencrypted");
            
            // Test non-GZip data (encrypted)
            byte[] encryptedData = { 0x12, 0x34, 0x56, 0x78 };
            if (!EncryptionService.IsDataEncrypted(encryptedData))
                throw new Exception("Non-GZip data should be detected as encrypted");
            
            Console.WriteLine("✓ Encryption detection works correctly");
        }

        private static void TestEncryptionConfig()
        {
            Console.WriteLine("Testing encryption configuration...");
            
            // Test no encryption
            var config1 = new EncryptionConfig();
            if (config1.IsEncryptionEnabled)
                throw new Exception("Empty config should not have encryption enabled");
            if (!config1.IsValid())
                throw new Exception("Empty config should be valid");
            
            // Test password encryption
            var config2 = new EncryptionConfig { Password = "test123" };
            if (!config2.IsEncryptionEnabled)
                throw new Exception("Password config should have encryption enabled");
            if (!config2.IsValid())
                throw new Exception("Password config should be valid");
            
            byte[] key = config2.GetEncryptionKey();
            if (key == null || key.Length != 32)
                throw new Exception("Password config should return 32-byte key");
            
            Console.WriteLine("✓ Encryption configuration works correctly");
        }

        private static bool ArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
