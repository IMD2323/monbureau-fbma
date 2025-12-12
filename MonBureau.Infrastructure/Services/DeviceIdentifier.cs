using System;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace MonBureau.Infrastructure.Services
{
    public class DeviceIdentifier
    {
        public string GenerateDeviceId()
        {
            try
            {
                var cpuId = GetProcessorId();
                var motherboardId = GetMotherboardSerial();
                var machineName = Environment.MachineName;

                var combined = $"{cpuId}-{motherboardId}-{machineName}";
                return ComputeSHA256Hash(combined);
            }
            catch
            {
                // Fallback to machine name only
                return ComputeSHA256Hash(Environment.MachineName);
            }
        }

        private string GetProcessorId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                var result = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                return result?["ProcessorId"]?.ToString() ?? "UNKNOWN";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        private string GetMotherboardSerial()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                var result = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                return result?["SerialNumber"]?.ToString() ?? "UNKNOWN";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        private string ComputeSHA256Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}