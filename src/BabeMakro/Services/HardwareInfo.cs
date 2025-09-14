using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace BabeMakro.Services
{
    public static class HardwareInfo
    {
        private static string? _cachedHWID;

        public static string GetHWID()
        {
            if (!string.IsNullOrEmpty(_cachedHWID))
                return _cachedHWID;

            var cpuId = GetCPUId();
            var diskId = GetDiskId();
            var motherboardId = GetMotherboardId();

            var combinedId = $"{cpuId}-{diskId}-{motherboardId}";
            _cachedHWID = GenerateHash(combinedId);

            return _cachedHWID;
        }

        private static string GetCPUId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var processorId = obj["ProcessorId"]?.ToString();
                    if (!string.IsNullOrEmpty(processorId))
                        return processorId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting CPU ID: {ex.Message}");
            }

            return "CPU-DEFAULT";
        }

        private static string GetDiskId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var serialNumber = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serialNumber))
                        return serialNumber;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting Disk ID: {ex.Message}");
            }

            return "DISK-DEFAULT";
        }

        private static string GetMotherboardId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                using var collection = searcher.Get();

                foreach (ManagementObject obj in collection)
                {
                    var serialNumber = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serialNumber) && serialNumber != "To be filled by O.E.M.")
                        return serialNumber;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting Motherboard ID: {ex.Message}");
            }

            return "MB-DEFAULT";
        }

        private static string GenerateHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var builder = new StringBuilder();

            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString().ToUpper().Substring(0, 32);
        }
    }
}