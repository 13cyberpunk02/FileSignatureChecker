using System;
using System.IO;

namespace FileSignatureChecker.Services
{
    public class Crc32Service
    {
        private static readonly uint[] Crc32Table = new uint[256];

        static Crc32Service()
        {
            const uint polynomial = 0xEDB88320u;
            for (uint i = 0; i < 256; i++)
            {
                var crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
                }
                Crc32Table[i] = crc;
            }
        }

        public static string CalculateChecksum(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            try
            {
                using var stream = File.OpenRead(filePath);
                var crc = 0xFFFFFFFFu;
                const int bufferSize = 8192;
                var buffer = new byte[bufferSize];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                {
                    for (var i = 0; i < bytesRead; i++)
                    {
                        var index = (byte)((crc & 0xFF) ^ buffer[i]);
                        crc = (crc >> 8) ^ Crc32Table[index];
                    }
                }

                crc ^= 0xFFFFFFFFu;
                return crc.ToString("X8");
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool CompareChecksums(string checksum1, string checksum2)
           => string.Equals(checksum1, checksum2, StringComparison.OrdinalIgnoreCase);
    }
}
