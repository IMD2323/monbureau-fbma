using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace MonBureau.Infrastructure.Security
{
    /// <summary>
    /// Service for creating and restoring encrypted backups
    /// </summary>
    public class EncryptedBackupService
    {
        private const int SALT_SIZE = 32;
        private const int KEY_SIZE = 32;
        private const int ITERATIONS = 100000; // PBKDF2 iterations
        private const string BACKUP_VERSION = "2.0"; // Version with encryption

        public class BackupMetadata
        {
            public string Version { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool IsEncrypted { get; set; }
            public string ApplicationVersion { get; set; }
            public long OriginalSize { get; set; }
        }

        /// <summary>
        /// Creates an encrypted backup with password protection
        /// </summary>
        public static bool CreateBackup(string backupPath, string password, string[] filesToBackup)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    throw new ArgumentException("Password cannot be empty");
                }

                if (password.Length < 8)
                {
                    throw new ArgumentException("Password must be at least 8 characters");
                }

                // Create temporary unencrypted zip
                string tempZipPath = Path.GetTempFileName();

                try
                {
                    // Create metadata
                    var metadata = new BackupMetadata
                    {
                        Version = BACKUP_VERSION,
                        CreatedAt = DateTime.UtcNow,
                        IsEncrypted = true,
                        ApplicationVersion = GetApplicationVersion(),
                        OriginalSize = 0
                    };

                    // Create ZIP archive
                    using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
                    {
                        // Add metadata
                        var metadataEntry = archive.CreateEntry("backup_metadata.json");
                        using (var writer = new StreamWriter(metadataEntry.Open()))
                        {
                            writer.Write(JsonConvert.SerializeObject(metadata, Formatting.Indented));
                        }

                        // Add files
                        foreach (var filePath in filesToBackup)
                        {
                            if (File.Exists(filePath))
                            {
                                string fileName = Path.GetFileName(filePath);
                                archive.CreateEntryFromFile(filePath, fileName, CompressionLevel.Optimal);

                                metadata.OriginalSize += new FileInfo(filePath).Length;
                            }
                        }
                    }

                    // Encrypt the backup
                    EncryptFile(tempZipPath, backupPath, password);

                    Console.WriteLine($"Encrypted backup created successfully: {backupPath}");
                    Console.WriteLine($"Original size: {FormatBytes(metadata.OriginalSize)}");
                    Console.WriteLine($"Encrypted size: {FormatBytes(new FileInfo(backupPath).Length)}");

                    return true;
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Backup creation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores an encrypted backup
        /// </summary>
        public static bool RestoreBackup(string backupPath, string password, string extractPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    throw new FileNotFoundException("Backup file not found", backupPath);
                }

                // Check if backup is encrypted
                if (!IsEncryptedBackup(backupPath))
                {
                    // Legacy unencrypted backup
                    return RestoreLegacyBackup(backupPath, extractPath);
                }

                // Decrypt to temporary file
                string tempZipPath = Path.GetTempFileName();

                try
                {
                    DecryptFile(backupPath, tempZipPath, password);

                    // Extract files
                    using (var archive = ZipFile.OpenRead(tempZipPath))
                    {
                        // Read metadata
                        var metadataEntry = archive.GetEntry("backup_metadata.json");
                        BackupMetadata metadata = null;

                        if (metadataEntry != null)
                        {
                            using (var reader = new StreamReader(metadataEntry.Open()))
                            {
                                string json = reader.ReadToEnd();
                                metadata = JsonConvert.DeserializeObject<BackupMetadata>(json);
                            }

                            Console.WriteLine($"Restoring backup from {metadata.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                        }

                        // Extract all files
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Name == "backup_metadata.json") continue;

                            string destinationPath = Path.Combine(extractPath, entry.Name);

                            // Create directory if needed
                            string directory = Path.GetDirectoryName(destinationPath);
                            if (!Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            entry.ExtractToFile(destinationPath, true);
                        }
                    }

                    Console.WriteLine($"Backup restored successfully to: {extractPath}");
                    return true;
                }
                finally
                {
                    if (File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                    }
                }
            }
            catch (CryptographicException)
            {
                Console.Error.WriteLine("Invalid password or corrupted backup file");
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Backup restoration failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Encrypts a file using AES-256 with PBKDF2 key derivation
        /// </summary>
        private static void EncryptFile(string inputPath, string outputPath, string password)
        {
            // Generate random salt
            byte[] salt = new byte[SALT_SIZE];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            // Derive key from password
            byte[] key;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, ITERATIONS, HashAlgorithmName.SHA256))
            {
                key = pbkdf2.GetBytes(KEY_SIZE);
            }

            // Encrypt file
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = key;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    // Write header: "ENCRYPTED" marker (9 bytes)
                    outputStream.Write(Encoding.ASCII.GetBytes("ENCRYPTED"), 0, 9);

                    // Write version (1 byte)
                    outputStream.WriteByte(2);

                    // Write salt
                    outputStream.Write(salt, 0, salt.Length);

                    // Write IV
                    outputStream.Write(aes.IV, 0, aes.IV.Length);

                    // Encrypt and write data
                    using (var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    using (var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
                    {
                        inputStream.CopyTo(cryptoStream);
                    }
                }
            }

            // Clear sensitive data
            Array.Clear(key, 0, key.Length);
        }

        /// <summary>
        /// Decrypts a file using AES-256
        /// </summary>
        private static void DecryptFile(string inputPath, string outputPath, string password)
        {
            using (var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            {
                // Read and verify header
                byte[] header = new byte[9];
                inputStream.Read(header, 0, 9);
                string headerString = Encoding.ASCII.GetString(header);

                if (headerString != "ENCRYPTED")
                {
                    throw new InvalidDataException("Not an encrypted backup file");
                }

                // Read version
                int version = inputStream.ReadByte();
                if (version != 2)
                {
                    throw new InvalidDataException($"Unsupported backup version: {version}");
                }

                // Read salt
                byte[] salt = new byte[SALT_SIZE];
                inputStream.Read(salt, 0, SALT_SIZE);

                // Derive key
                byte[] key;
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, ITERATIONS, HashAlgorithmName.SHA256))
                {
                    key = pbkdf2.GetBytes(KEY_SIZE);
                }

                try
                {
                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = 256;
                        aes.Key = key;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        // Read IV
                        byte[] iv = new byte[aes.BlockSize / 8];
                        inputStream.Read(iv, 0, iv.Length);
                        aes.IV = iv;

                        // Decrypt data
                        using (var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                        using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                        {
                            cryptoStream.CopyTo(outputStream);
                        }
                    }
                }
                finally
                {
                    Array.Clear(key, 0, key.Length);
                }
            }
        }

        /// <summary>
        /// Checks if a backup file is encrypted
        /// </summary>
        public static bool IsEncryptedBackup(string backupPath)
        {
            try
            {
                using (var stream = new FileStream(backupPath, FileMode.Open, FileAccess.Read))
                {
                    if (stream.Length < 9) return false;

                    byte[] header = new byte[9];
                    stream.Read(header, 0, 9);
                    return Encoding.ASCII.GetString(header) == "ENCRYPTED";
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restores legacy unencrypted backup (backward compatibility)
        /// </summary>
        private static bool RestoreLegacyBackup(string backupPath, string extractPath)
        {
            try
            {
                Console.WriteLine("Restoring legacy unencrypted backup...");
                ZipFile.ExtractToDirectory(backupPath, extractPath, true);
                Console.WriteLine("Legacy backup restored successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to restore legacy backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates password strength
        /// </summary>
        public static (bool isValid, string message) ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return (false, "Password cannot be empty");

            if (password.Length < 8)
                return (false, "Password must be at least 8 characters");

            bool hasUpper = false, hasLower = false, hasDigit = false, hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                if (char.IsLower(c)) hasLower = true;
                if (char.IsDigit(c)) hasDigit = true;
                if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            int strength = 0;
            if (hasUpper) strength++;
            if (hasLower) strength++;
            if (hasDigit) strength++;
            if (hasSpecial) strength++;

            if (strength < 3)
                return (false, "Password should contain uppercase, lowercase, numbers, and special characters");

            return (true, "Password is strong");
        }

        private static string GetApplicationVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}