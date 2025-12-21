using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MonBureau.Infrastructure.Security
{
    /// <summary>
    /// FIXED: Uses DPAPI for large credentials (Firebase private key)
    /// Falls back to Credential Manager for small values
    /// </summary>
    public class SecureCredentialManager
    {
        private const string TARGET_PREFIX = "MonBureau_";
        private const int MAX_CMDKEY_SIZE = 2000; // Safe limit for cmdkey

        // DPAPI storage path
        private static readonly string DpapiStoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MonBureau",
            "Credentials"
        );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public nint CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public nint Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, uint flags, out nint credential);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(nint cred);

        /// <summary>
        /// FIXED: Automatically chooses storage method based on size
        /// </summary>
        public static bool StoreCredential(string key, string username, string password)
        {
            try
            {
                // Large credentials (e.g., Firebase private key) use DPAPI file storage
                if (password.Length > MAX_CMDKEY_SIZE)
                {
                    System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] Using DPAPI for {key} (size: {password.Length})");
                    return StoreDpapiCredential(key, password);
                }

                // Small credentials use Windows Credential Manager
                System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] Using Credential Manager for {key}");
                return StoreCmdKeyCredential(key, username, password);
            }
            catch (Exception ex)
            {
                LogError($"Failed to store credential: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// FIXED: Retrieves from both storage methods
        /// </summary>
        public static (string username, string password) RetrieveCredential(string key)
        {
            try
            {
                // Try DPAPI file first
                var dpapiValue = LoadDpapiCredential(key);
                if (!string.IsNullOrEmpty(dpapiValue))
                {
                    System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] Retrieved {key} from DPAPI");
                    return ("MonBureau", dpapiValue);
                }

                // Fall back to Credential Manager
                return RetrieveCmdKeyCredential(key);
            }
            catch (Exception ex)
            {
                LogError($"Failed to retrieve credential: {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// FIXED: Checks both storage locations
        /// </summary>
        public static bool CredentialExists(string key)
        {
            try
            {
                // Check DPAPI file
                if (DpapiCredentialExists(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] {key} exists in DPAPI");
                    return true;
                }

                // Check Credential Manager
                nint credPtr = nint.Zero;
                try
                {
                    bool exists = CredRead(TARGET_PREFIX + key, 1, 0, out credPtr);
                    if (exists)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] {key} exists in Credential Manager");
                    }
                    return exists;
                }
                finally
                {
                    if (credPtr != nint.Zero)
                    {
                        CredFree(credPtr);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes credential from both locations
        /// </summary>
        public static bool DeleteCredential(string key)
        {
            bool deleted = false;

            try
            {
                // Delete from DPAPI
                deleted |= DeleteDpapiCredential(key);

                // Delete from Credential Manager
                deleted |= CredDelete(TARGET_PREFIX + key, 1, 0);
            }
            catch (Exception ex)
            {
                LogError($"Failed to delete credential: {ex.Message}");
            }

            return deleted;
        }

        /// <summary>
        /// Gets credential with fallback to environment variable
        /// </summary>
        public static string GetSecureValue(string key, string envVarName = null)
        {
            // Try credential storage first
            var (_, password) = RetrieveCredential(key);
            if (!string.IsNullOrEmpty(password))
            {
                return password;
            }

            // Fallback to environment variable
            if (!string.IsNullOrEmpty(envVarName))
            {
                string envValue = Environment.GetEnvironmentVariable(envVarName, EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrEmpty(envValue))
                {
                    LogWarning($"Using environment variable fallback for {key}");
                    return envValue;
                }

                envValue = Environment.GetEnvironmentVariable(envVarName, EnvironmentVariableTarget.User);
                if (!string.IsNullOrEmpty(envValue))
                {
                    LogWarning($"Using user environment variable fallback for {key}");
                    return envValue;
                }
            }

            LogError($"No credential found for {key}");
            return null;
        }

        #region DPAPI File Storage (for large credentials)

        private static bool StoreDpapiCredential(string key, string value)
        {
            try
            {
                Directory.CreateDirectory(DpapiStoragePath);

                var filePath = GetDpapiFilePath(key);
                var plainBytes = Encoding.UTF8.GetBytes(value);
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    GetEntropy(key),
                    DataProtectionScope.CurrentUser
                );

                File.WriteAllBytes(filePath, encryptedBytes);
                System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] ✓ Stored {key} in DPAPI file");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"DPAPI storage failed: {ex.Message}");
                return false;
            }
        }

        private static string LoadDpapiCredential(string key)
        {
            try
            {
                var filePath = GetDpapiFilePath(key);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var encryptedBytes = File.ReadAllBytes(filePath);
                var decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    GetEntropy(key),
                    DataProtectionScope.CurrentUser
                );

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                LogError($"DPAPI load failed: {ex.Message}");
                return null;
            }
        }

        private static bool DpapiCredentialExists(string key)
        {
            var filePath = GetDpapiFilePath(key);
            return File.Exists(filePath);
        }

        private static bool DeleteDpapiCredential(string key)
        {
            try
            {
                var filePath = GetDpapiFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string GetDpapiFilePath(string key)
        {
            var safeKey = key.Replace("_", "-");
            return Path.Combine(DpapiStoragePath, $"{safeKey}.dat");
        }

        private static byte[] GetEntropy(string key)
        {
            // Generate deterministic entropy from key + machine
            var combined = $"{key}_{Environment.MachineName}_{Environment.UserName}";
            return SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        }

        #endregion

        #region Credential Manager Storage (for small credentials)

        private static bool StoreCmdKeyCredential(string key, string username, string password)
        {
            try
            {
                byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
                nint passwordPtr = Marshal.AllocHGlobal(passwordBytes.Length);
                Marshal.Copy(passwordBytes, 0, passwordPtr, passwordBytes.Length);

                CREDENTIAL credential = new CREDENTIAL
                {
                    Type = 1, // CRED_TYPE_GENERIC
                    TargetName = TARGET_PREFIX + key,
                    UserName = username,
                    CredentialBlob = passwordPtr,
                    CredentialBlobSize = (uint)passwordBytes.Length,
                    Persist = 2, // CRED_PERSIST_LOCAL_MACHINE
                    Comment = "Managed by MonBureau"
                };

                bool result = CredWrite(ref credential, 0);
                Marshal.FreeHGlobal(passwordPtr);
                Array.Clear(passwordBytes, 0, passwordBytes.Length);

                return result;
            }
            catch (Exception ex)
            {
                LogError($"CmdKey storage failed: {ex.Message}");
                return false;
            }
        }

        private static (string username, string password) RetrieveCmdKeyCredential(string key)
        {
            nint credPtr = nint.Zero;
            try
            {
                if (CredRead(TARGET_PREFIX + key, 1, 0, out credPtr))
                {
                    CREDENTIAL cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);

                    string username = cred.UserName ?? string.Empty;

                    if (cred.CredentialBlob != nint.Zero && cred.CredentialBlobSize > 0)
                    {
                        byte[] passwordBytes = new byte[cred.CredentialBlobSize];
                        Marshal.Copy(cred.CredentialBlob, passwordBytes, 0, (int)cred.CredentialBlobSize);
                        string password = Encoding.Unicode.GetString(passwordBytes);
                        Array.Clear(passwordBytes, 0, passwordBytes.Length);

                        return (username, password);
                    }
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                LogError($"CmdKey retrieve failed: {ex.Message}");
                return (null, null);
            }
            finally
            {
                if (credPtr != nint.Zero)
                {
                    CredFree(credPtr);
                }
            }
        }

        #endregion

        private static void LogError(string message)
        {
            Console.Error.WriteLine($"[ERROR] {message}");
            System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] ERROR: {message}");
        }

        private static void LogWarning(string message)
        {
            Console.WriteLine($"[WARNING] {message}");
            System.Diagnostics.Debug.WriteLine($"[SecureCredentialManager] WARNING: {message}");
        }
    }
}