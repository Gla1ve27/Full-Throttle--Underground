using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FullThrottle.Audio
{
    public static class Mp3MetadataUtility
    {
        public struct MetadataResult
        {
            public string title;
            public string artist;
            public string album;
            public Sprite albumArt;
            public bool tagLibAvailable;
        }

        public static MetadataResult ReadMetadata(string filePath, string fallbackTitle)
        {
            MetadataResult result = new MetadataResult
            {
                title = fallbackTitle,
                artist = "Unknown Artist",
                album = string.Empty,
                albumArt = null,
                tagLibAvailable = false
            };

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return result;

            Assembly tagLibAssembly = FindTagLibAssembly();
            if (tagLibAssembly == null)
                return result;

            result.tagLibAvailable = true;

            object file = null;

            try
            {
                Type fileType = tagLibAssembly.GetType("TagLib.File");
                if (fileType == null)
                    return result;

                MethodInfo createMethod = fileType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (createMethod == null)
                    return result;

                file = createMethod.Invoke(null, new object[] { filePath });
                if (file == null)
                    return result;

                object tag = GetPropertyValue(file, "Tag");
                if (tag == null)
                    return result;

                result.title = FirstNonEmpty(
                    GetPropertyValue(tag, "Title") as string,
                    fallbackTitle
                );

                string[] performers = GetPropertyValue(tag, "Performers") as string[];
                result.artist = FirstNonEmpty(
                    GetPropertyValue(tag, "FirstPerformer") as string,
                    performers != null && performers.Length > 0 ? string.Join(", ", performers.Where(p => !string.IsNullOrWhiteSpace(p))) : null,
                    "Unknown Artist"
                );

                result.album = FirstNonEmpty(
                    GetPropertyValue(tag, "Album") as string,
                    string.Empty
                );

                byte[] artBytes = TryReadPictureBytes(tag);
                if (artBytes != null && artBytes.Length > 0)
                    result.albumArt = CreateSpriteFromBytes(artBytes, fallbackTitle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Mp3MetadataUtility: Failed to read metadata for '{filePath}'. Falling back to filename. {ex.Message}");
            }
            finally
            {
                if (file is IDisposable disposable)
                    disposable.Dispose();
            }

            return result;
        }

        private static Assembly FindTagLibAssembly()
        {
            Assembly loaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a =>
                {
                    string name = a.GetName().Name;
                    return string.Equals(name, "taglib-sharp", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "TagLibSharp", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "TagLib", StringComparison.OrdinalIgnoreCase);
                });

            if (loaded != null)
                return loaded;

            string[] candidateNames = { "taglib-sharp", "TagLibSharp", "TagLib" };

            foreach (string candidate in candidateNames)
            {
                try
                {
                    return Assembly.Load(candidate);
                }
                catch
                {
                    // Ignore and continue probing.
                }
            }

            return null;
        }

        private static object GetPropertyValue(object source, string propertyName)
        {
            if (source == null)
                return null;

            PropertyInfo property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                return null;

            return property.GetValue(source);
        }

        private static byte[] TryReadPictureBytes(object tag)
        {
            object picturesObject = GetPropertyValue(tag, "Pictures");
            IEnumerable pictures = picturesObject as IEnumerable;
            if (pictures == null)
                return null;

            foreach (object picture in pictures)
            {
                if (picture == null)
                    continue;

                object dataObject = GetPropertyValue(picture, "Data");
                if (dataObject == null)
                    continue;

                if (dataObject is byte[] directBytes && directBytes.Length > 0)
                    return directBytes;

                object nestedData = GetPropertyValue(dataObject, "Data");
                if (nestedData is byte[] nestedBytes && nestedBytes.Length > 0)
                    return nestedBytes;
            }

            return null;
        }

        private static Sprite CreateSpriteFromBytes(byte[] bytes, string fallbackName)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loaded = ImageConversion.LoadImage(texture, bytes, false);
            if (!loaded)
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.name = $"AlbumArt_{SanitizeName(fallbackName)}";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            return Sprite.Create(texture, rect, pivot, 100f);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private static string SanitizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Unknown";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                input = input.Replace(invalid, '_');

            return input;
        }
    }
}
