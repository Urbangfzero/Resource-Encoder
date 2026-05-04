using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace Runtime
{
    public static class ResRuntime
    {
        internal static void Initialize()
        {
            byte[] array = null;
            try
            {
                array = new byte[32];
                int[] array2 = new int[]
                {
                    MutationClass.Key<int>(20),
                    MutationClass.Key<int>(21),
                    MutationClass.Key<int>(22),
                    MutationClass.Key<int>(23),
                    MutationClass.Key<int>(24),
                    MutationClass.Key<int>(25),
                    MutationClass.Key<int>(26),
                    MutationClass.Key<int>(27)
                };
                int[] array3 = new int[]
                {
                    MutationClass.Key<int>(28),
                    MutationClass.Key<int>(29),
                    MutationClass.Key<int>(30),
                    MutationClass.Key<int>(31),
                    MutationClass.Key<int>(32),
                    MutationClass.Key<int>(33),
                    MutationClass.Key<int>(34),
                    MutationClass.Key<int>(35)
                };
                for (int i = 0; i < 8; i++)
                {
                    int num = array2[i] ^ array3[i];
                    Buffer.BlockCopy(BitConverter.GetBytes(num), 0, array, i * 4, 4);
                }
                ResRuntime._masterKey = ResRuntime.DeriveMasterKey(array);
                ResRuntime._protectedNames = ResRuntime.LoadProtectedNames();
                ResRuntime._protectedStoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool flag = ResRuntime._protectedNames != null;
                if (flag)
                {
                    foreach (string text in ResRuntime._protectedNames)
                    {
                        ResRuntime._protectedStoredNames.Add(ResRuntime.GetStoredResourceName(ResRuntime._masterKey, text));
                    }
                }
                ResRuntime._protectedStoredNames.Add(ResRuntime.GetStoredResourceName(ResRuntime._masterKey, "SKIDED"));
                ResRuntime._resCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                bool flag2 = ResRuntime._protectedNames != null;
                if (flag2)
                {
                    foreach (string text2 in ResRuntime._protectedNames)
                    {
                        bool flag3 = text2.EndsWith(".resources", StringComparison.OrdinalIgnoreCase);
                        if (flag3)
                        {
                            byte[] array4 = ResRuntime.DecryptResourceInternal(text2);
                            bool flag4 = array4 != null;
                            if (flag4)
                            {
                                ResRuntime._resCache[text2] = array4;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                bool flag5 = array != null;
                if (flag5)
                {
                    Array.Clear(array, 0, array.Length);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        internal static Stream GetResourceStream(string name)
        {
            byte[] decryptedResource = ResRuntime.GetDecryptedResource(name);
            bool flag = decryptedResource != null;
            Stream stream;
            if (flag)
            {
                stream = new MemoryStream(decryptedResource, false);
            }
            else
            {
                stream = typeof(ResRuntime).Assembly.GetManifestResourceStream(name);
            }
            return stream;
        }

        private static byte[] GetDecryptedResource(string name)
        {
            bool flag = ResRuntime._resCache != null;
            if (flag)
            {
                byte[] array;
                bool flag2 = ResRuntime._resCache.TryGetValue(name, out array);
                if (flag2)
                {
                    return array;
                }
            }
            return ResRuntime.DecryptResourceInternal(name);
        }

        internal static string[] GetResourceNames()
        {
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool flag = ResRuntime._protectedNames != null;
            if (flag)
            {
                foreach (string text in ResRuntime._protectedNames)
                {
                    hashSet.Add(text);
                }
            }
            foreach (string text2 in typeof(ResRuntime).Assembly.GetManifestResourceNames())
            {
                bool flag2 = ResRuntime._protectedStoredNames != null && ResRuntime._protectedStoredNames.Contains(text2);
                if (!flag2)
                {
                    hashSet.Add(text2);
                }
            }
            string[] array = new string[hashSet.Count];
            hashSet.CopyTo(array);
            return array;
        }

        internal static ResourceManager CreateResourceManager(string baseName, Assembly assembly)
        {
            return new ResManager(baseName, assembly);
        }

        internal static ComponentResourceManager CreateComponentResourceManager(Type componentType)
        {
            return new ResManager(componentType);
        }

        private static byte[] DecryptResourceInternal(string name)
        {
            byte[] array = null;
            Stream stream = null;
            byte[] array2;
            try
            {
                bool flag = ResRuntime._masterKey == null || string.IsNullOrEmpty(name) || !ResRuntime.IsProtectedResource(name);
                if (flag)
                {
                    array2 = null;
                }
                else
                {
                    string storedResourceName = ResRuntime.GetStoredResourceName(ResRuntime._masterKey, name);
                    stream = typeof(ResRuntime).Assembly.GetManifestResourceStream(storedResourceName);
                    bool flag2 = stream == null;
                    if (flag2)
                    {
                        array2 = null;
                    }
                    else
                    {
                        array = new byte[stream.Length];
                        stream.Read(array, 0, array.Length);
                        array2 = ResRuntime.DecryptBlob(array, ResRuntime._masterKey, name);
                    }
                }
            }
            catch
            {
                array2 = null;
            }
            finally
            {
                bool flag3 = stream != null;
                if (flag3)
                {
                    stream.Close();
                    stream.Dispose();
                }
                bool flag4 = array != null;
                if (flag4)
                {
                    Array.Clear(array, 0, array.Length);
                }
            }
            return array2;
        }

        private static byte[] DeriveMasterKey(byte[] masterSeed)
        {
            byte[] array;
            try
            {
                using (SHA256 sha = SHA256.Create())
                {
                    array = sha.ComputeHash(masterSeed);
                }
            }
            finally
            {
            }
            return array;
        }

        private static byte[] DeriveResourceKey(byte[] masterKey, byte[] salt, string resourceName, byte label)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(resourceName ?? string.Empty);
            byte[] array = new byte[salt.Length + bytes.Length + 1];
            Buffer.BlockCopy(salt, 0, array, 0, salt.Length);
            Buffer.BlockCopy(bytes, 0, array, salt.Length, bytes.Length);
            array[array.Length - 1] = label;
            byte[] array2;
            try
            {
                using (HMACSHA256 hmacsha = new HMACSHA256(masterKey))
                {
                    array2 = hmacsha.ComputeHash(array);
                }
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
                Array.Clear(array, 0, array.Length);
            }
            return array2;
        }

        private static string GetStoredResourceName(byte[] masterKey, string resourceName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(resourceName ?? string.Empty);
            string text;
            try
            {
                using (HMACSHA256 hmacsha = new HMACSHA256(masterKey))
                {
                    byte[] array = hmacsha.ComputeHash(bytes);
                    StringBuilder stringBuilder = new StringBuilder(64);
                    for (int i = 0; i < array.Length; i++)
                    {
                        stringBuilder.Append(array[i].ToString("x2"));
                    }
                    text = stringBuilder.ToString();
                }
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
            return text;
        }

        private static HashSet<string> LoadProtectedNames()
        {
            byte[] array = null;
            byte[] array2 = null;
            Stream stream = null;
            HashSet<string> hashSet;
            try
            {
                bool flag = ResRuntime._masterKey == null;
                if (flag)
                {
                    hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    string storedResourceName = ResRuntime.GetStoredResourceName(ResRuntime._masterKey, "SKIDED");
                    stream = typeof(ResRuntime).Assembly.GetManifestResourceStream(storedResourceName);
                    bool flag2 = stream == null;
                    if (flag2)
                    {
                        hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        array = new byte[stream.Length];
                        stream.Read(array, 0, array.Length);
                        array2 = ResRuntime.DecryptBlob(array, ResRuntime._masterKey, "SKIDED");
                        bool flag3 = array2 == null;
                        if (flag3)
                        {
                            hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                        else
                        {
                            hashSet = ResRuntime.DeserializeResourceNames(array2);
                        }
                    }
                }
            }
            catch
            {
                hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                bool flag4 = stream != null;
                if (flag4)
                {
                    stream.Close();
                    stream.Dispose();
                }
                bool flag5 = array != null;
                if (flag5)
                {
                    Array.Clear(array, 0, array.Length);
                }
                bool flag6 = array2 != null;
                if (flag6)
                {
                    Array.Clear(array2, 0, array2.Length);
                }
            }
            return hashSet;
        }

        private static bool IsProtectedResource(string name)
        {
            return ResRuntime._protectedNames != null && ResRuntime._protectedNames.Contains(name);
        }

        private static HashSet<string> DeserializeResourceNames(byte[] data)
        {
            HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int num = 0;
            int num2 = BitConverter.ToInt32(data, num);
            num += 4;
            for (int i = 0; i < num2; i++)
            {
                int num3 = BitConverter.ToInt32(data, num);
                num += 4;
                string @string = Encoding.UTF8.GetString(data, num, num3);
                num += num3;
                hashSet.Add(@string);
            }
            return hashSet;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            bool flag = left == null || right == null || left.Length != right.Length;
            bool flag2;
            if (flag)
            {
                flag2 = false;
            }
            else
            {
                int num = 0;
                for (int i = 0; i < left.Length; i++)
                {
                    num |= (int)(left[i] ^ right[i]);
                }
                flag2 = num == 0;
            }
            return flag2;
        }

        private static byte[] DecryptBlob(byte[] dat, byte[] masterKey, string resourceName)
        {
            byte[] array = null;
            byte[] array2 = null;
            byte[] array3 = null;
            byte[] array4;
            try
            {
                bool flag = dat == null || dat.Length < 64;
                if (flag)
                {
                    array4 = null;
                }
                else
                {
                    byte[] array5 = new byte[16];
                    Buffer.BlockCopy(dat, 0, array5, 0, 16);
                    byte[] array6 = new byte[16];
                    Buffer.BlockCopy(dat, 16, array6, 0, 16);
                    byte[] array7 = new byte[32];
                    Buffer.BlockCopy(dat, 32, array7, 0, 32);
                    byte[] array8 = new byte[dat.Length - 64];
                    Buffer.BlockCopy(dat, 64, array8, 0, array8.Length);
                    array2 = ResRuntime.DeriveResourceKey(masterKey, array5, resourceName, 1);
                    array3 = ResRuntime.DeriveResourceKey(masterKey, array5, resourceName, 2);
                    byte[] array9;
                    using (HMACSHA256 hmacsha = new HMACSHA256(array3))
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            memoryStream.Write(array5, 0, array5.Length);
                            memoryStream.Write(array6, 0, array6.Length);
                            memoryStream.Write(array8, 0, array8.Length);
                            array9 = hmacsha.ComputeHash(memoryStream.ToArray());
                        }
                    }
                    bool flag2 = !ResRuntime.FixedTimeEquals(array9, array7);
                    if (flag2)
                    {
                        array4 = null;
                    }
                    else
                    {
                        using (Aes aes = Aes.Create())
                        {
                            aes.Key = array2;
                            aes.IV = array6;
                            aes.Mode = CipherMode.CBC;
                            aes.Padding = PaddingMode.PKCS7;
                            MemoryStream memoryStream2 = new MemoryStream();
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream2, aes.CreateDecryptor(), CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(array8, 0, array8.Length);
                                cryptoStream.FlushFinalBlock();
                            }
                            array = memoryStream2.ToArray();
                        }
                        byte[] array10 = QuickLZDecompression.decompress(array);
                        Array.Clear(array, 0, array.Length);
                        Array.Clear(array9, 0, array9.Length);
                        array4 = array10;
                    }
                }
            }
            catch
            {
                bool flag3 = array != null;
                if (flag3)
                {
                    Array.Clear(array, 0, array.Length);
                }
                array4 = null;
            }
            finally
            {
                bool flag4 = array2 != null;
                if (flag4)
                {
                    Array.Clear(array2, 0, array2.Length);
                }
                bool flag5 = array3 != null;
                if (flag5)
                {
                    Array.Clear(array3, 0, array3.Length);
                }
            }
            return array4;
        }

        private const string ProtectedResourceListMarker = "SKIDED";

        internal static Dictionary<string, byte[]> _resCache;

        private static HashSet<string> _protectedNames;

        private static HashSet<string> _protectedStoredNames;

        private static byte[] _masterKey;
    }
}