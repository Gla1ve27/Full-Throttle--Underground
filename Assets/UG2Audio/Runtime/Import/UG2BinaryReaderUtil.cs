using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UG2Audio.Import
{
    public static class UG2BinaryReaderUtil
    {
        private static readonly Regex PrintableTokenRegex = new Regex(@"[ -~]{4,}", RegexOptions.Compiled);

        public static byte[] ReadAllBytes(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path is required.", nameof(path));

            return File.ReadAllBytes(path);
        }

        public static string ReadAsciiPrefix(byte[] bytes, int count)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            int length = Math.Min(count, bytes.Length);
            return Encoding.ASCII.GetString(bytes, 0, length).TrimEnd('\0', ' ');
        }

        public static List<string> ExtractPrintableTokens(byte[] bytes)
        {
            var results = new List<string>();
            if (bytes == null || bytes.Length == 0)
                return results;

            string text = GetLatin1String(bytes);
            MatchCollection matches = PrintableTokenRegex.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                string token = matches[i].Value.Trim('\0', ' ', '\t', '\r', '\n');
                if (token.Length >= 4 && !results.Contains(token))
                    results.Add(token);
            }

            return results;
        }

        public static List<int> ReadInt32HeaderValues(byte[] bytes, int maxValues)
        {
            var values = new List<int>();
            if (bytes == null)
                return values;

            int count = Math.Min(bytes.Length / 4, maxValues);
            for (int i = 0; i < count; i++)
                values.Add(BitConverter.ToInt32(bytes, i * 4));

            return values;
        }

        public static UG2SourceAssetRecord CreateRecord(string sourceRoot, string path, UG2SourceAssetKind kind, byte[] bytes)
        {
            var info = new FileInfo(path);
            return new UG2SourceAssetRecord
            {
                kind = kind,
                sourcePath = NormalizePath(path),
                relativePath = GetRelativePath(sourceRoot, path),
                fileName = info.Name,
                extension = info.Extension.ToLowerInvariant(),
                byteLength = info.Length,
                signature = ReadAsciiPrefix(bytes, 8),
                identifiers = ExtractPrintableTokens(bytes)
            };
        }

        public static string GetRelativePath(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
                return NormalizePath(path);

            string normalizedRoot = NormalizePath(Path.GetFullPath(root)).TrimEnd('/') + "/";
            string normalizedPath = NormalizePath(Path.GetFullPath(path));
            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedRoot.Length);

            return normalizedPath;
        }

        public static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        public static string GetLatin1String(byte[] bytes)
        {
            try
            {
                return Encoding.GetEncoding(28591).GetString(bytes);
            }
            catch
            {
                return Encoding.ASCII.GetString(bytes);
            }
        }
    }
}
