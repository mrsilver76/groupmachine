/*
 * GroupMachine - Groups photos and videos into albums (folders) based on time & location changes.
 * Copyright (c) 2025 Richard Lawrence
 * http://github.com/mrsilver76/groupmachine/
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *  
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this Options.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace GroupMachine
{
    /// <summary>
    /// Handles file hashing for duplicate detection.
    /// </summary>
    internal sealed class Hashing
    {
        /// <summary>Prebuilt CRC64 lookup table (ECMA polynomial).</summary>
        private static readonly ulong[] Crc64Table = BuildTable(0xC96C5795D7870F42UL);

        /// <summary>
        /// Given a string input, attempts to parse it into a corresponding Globals.HashMode enum value.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Globals.HashMode TryParseHashMode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                ConsoleOutput.ShowUsage("Invalid hashing algorithm: use crc, md5 or sha");
                return Globals.HashMode.CRC;  // Never reached, but required to satisfy the compiler
            }

            // normalize to lowercase and strip digits
            string normalized = new(
                [.. input
                    .ToLower(CultureInfo.CurrentCulture)
                    .Where(c => !char.IsDigit(c))]
            );

            switch (normalized)
            {
                case "crc": return Globals.HashMode.CRC;
                case "md": return Globals.HashMode.MD5;
                case "sha": return Globals.HashMode.SHA;
                default:
                    ConsoleOutput.ShowUsage("Invalid hashing algorithm: " + input);
                    return Globals.HashMode.CRC; // unreachable, but required
            }
        }


        /// <summary>
        /// Computes the hash of a file using the configured <see cref="Globals.HashMode"/>.
        /// </summary>
        /// <param name="path">Path to the file whose hash should be computed.</param>
        /// <returns>Byte array representing the computed hash.</returns>
        private static byte[] ComputeFileHash(string path)
        {
            return Globals.DuplicateCheckMode switch
            {
                Globals.HashMode.CRC => ComputeCRC64(path),
                Globals.HashMode.MD5 => ComputeMD5(path),
                Globals.HashMode.SHA => ComputeSHA(path),
                _ => ComputeCRC64(path) // Default to CRC64 if unknown mode
            };
        }

        /// <summary>
        /// Compares the contents of two files to determine if they are identical,
        /// using the selected hashing algorithm. First checks file sizes for a quick pre-check.
        /// </summary>
        /// <param name="path1">The file path of the first file to compare.</param>
        /// <param name="path2">The file path of the second file to compare.</param>
        /// <returns><see langword="true"/> if the contents of the two files are identical; otherwise, <see langword="false"/>.</returns>
        internal static bool FilesAreEqual(string path1, string path2)
        {
            // First, check file sizes for a quick pre-check
            var fi1 = new FileInfo(path1);
            var fi2 = new FileInfo(path2);

            if (fi1.Length != fi2.Length)
                return false;

            // File sizes are the same, compute and compare hashes
            var hash1 = ComputeFileHash(path1);
            var hash2 = ComputeFileHash(path2);
            return hash1.SequenceEqual(hash2);
        }

        /// <summary>
        /// Computes a CRC64 hash for duplicate detection. Looks at only
        /// the first 64 KiB in order to be extremely fast.
        /// </summary>
        private static byte[] ComputeCRC64(string path)
        {
            const int BufferSize = 8192;
            const int MaxBytes = 64 * 1024; // 64 KiB
            byte[] buffer = new byte[BufferSize];
            ulong crc = ulong.MaxValue;
            int bytesRead;
            int totalRead = 0;

            using (var stream = File.OpenRead(path))
            {
                while ((bytesRead = stream.Read(buffer, 0, Math.Min(BufferSize, MaxBytes - totalRead))) > 0)
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        byte index = (byte)(crc ^ buffer[i]);
                        crc = Crc64Table[index] ^ (crc >> 8);
                    }
                    totalRead += bytesRead;
                    if (totalRead >= MaxBytes)
                        break;
                }
            }

            // convert ulong CRC to byte[]
            byte[] result = BitConverter.GetBytes(~crc);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);

            return result;
        }

        /// <summary>
        /// Builds the CRC64 lookup table based on the specified polynomial.
        /// </summary>
        private static ulong[] BuildTable(ulong poly)
        {
            var table = new ulong[256];
            for (int i = 0; i < 256; i++)
            {
                ulong crc = (ulong)i;
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? poly ^ (crc >> 1) : crc >> 1;
                table[i] = crc;
            }
            return table;
        }

        /// <summary>
        /// Function to compute MD5 hash for deduplication purposes. 
        /// MD5 is not secure, but it is safe for non-cryptographic duplicate detection.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Security", "CA5351:Do Not Use Broken Cryptographic Algorithms",
            Justification = "MD5 is safe for non-cryptographic duplicate detection.")]
        private static byte[] ComputeMD5(string path)
        {
            using var stream = File.OpenRead(path);
            using var md5 = MD5.Create();
            return md5.ComputeHash(stream);
        }

        /// <summary>
        /// Computes a SHA hash. Uses SHA-512 on 64-bit platforms, SHA-256 otherwise.
        /// </summary>
        private static byte[] ComputeSHA(string path)
        {
            using var stream = File.OpenRead(path);
            using HashAlgorithm hashAlgorithm = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 or Architecture.Arm64 => SHA512.Create(),
                _ => SHA256.Create()
            };
            return hashAlgorithm.ComputeHash(stream);
        }
    }
}
