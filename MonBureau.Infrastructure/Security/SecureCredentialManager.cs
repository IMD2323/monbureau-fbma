using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Security;

namespace MonBureau.Infrastructure.Security
{
    /// <summary>
    /// Manages secure credential storage using Windows Credential Manager
    /// </summary>
    public class SecureCredentialManager
    {
        private const string TARGET_PREFIX = "YourApp_";

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
        /// Stores a credential securely in Windows Credential Manager
        /// </summary>
        public static bool StoreCredential(string key, string username, string password)
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
                    Comment = "Managed by YourApp Security Module"
                };

                bool result = CredWrite(ref credential, 0);
                Marshal.FreeHGlobal(passwordPtr);
                Array.Clear(passwordBytes, 0, passwordBytes.Length);

                return result;
            }
            catch (Exception ex)
            {
                LogError($"Failed to store credential: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves a credential from Windows Credential Manager
        /// </summary>
        public static (string username, string password) RetrieveCredential(string key)
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
                LogError($"Failed to retrieve credential: {ex.Message}");
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

        /// <summary>
        /// Deletes a credential from Windows Credential Manager
        /// </summary>
        public static bool DeleteCredential(string key)
        {
            try
            {
                return CredDelete(TARGET_PREFIX + key, 1, 0);
            }
            catch (Exception ex)
            {
                LogError($"Failed to delete credential: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a credential exists
        /// </summary>
        public static bool CredentialExists(string key)
        {
            nint credPtr = nint.Zero;
            try
            {
                bool exists = CredRead(TARGET_PREFIX + key, 1, 0, out credPtr);
                return exists;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (credPtr != nint.Zero)
                {
                    CredFree(credPtr);
                }
            }
        }

        /// <summary>
        /// Retrieves credential from Credential Manager with environment variable fallback
        /// </summary>
        public static string GetSecureValue(string key, string envVarName = null)
        {
            // Try Credential Manager first
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

        private static void LogError(string message)
        {
            // Replace with your logging framework
            Console.Error.WriteLine($"[ERROR] {message}");
        }

        private static void LogWarning(string message)
        {
            // Replace with your logging framework
            Console.WriteLine($"[WARNING] {message}");
        }
    }
}