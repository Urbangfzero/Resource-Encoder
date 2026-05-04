using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;

namespace Runtime
{
    public class ResManager : ComponentResourceManager
    {
        public ResManager(string baseName, Assembly assembly)
        {
            this._baseName = baseName;
            FieldInfo field = typeof(ResourceManager).GetField("BaseNameField", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(this, baseName);
            }
            FieldInfo fieldInfo = typeof(ResourceManager).GetField("MainAssembly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(ResourceManager).GetField("_mainAssembly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(this, assembly);
            }
            MethodInfo method = typeof(ResourceManager).GetMethod("CommonAssemblyInit", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(this, null);
            }
        }

        public ResManager(Type componentType) : base(componentType)
        {
            this._baseName = componentType.FullName;
        }

        public override ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
        {
            ResourceSet resourceSet = this.TryGetCachedResourceSet(culture, tryParents);
            bool flag = resourceSet != null;
            ResourceSet resourceSet2;
            if (flag)
            {
                resourceSet2 = resourceSet;
            }
            else
            {
                resourceSet2 = this.TryBaseGetResourceSet(culture, createIfNotExists, tryParents);
            }
            return resourceSet2;
        }

        protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
        {
            ResourceSet resourceSet = this.TryGetCachedResourceSet(culture, tryParents);
            bool flag = resourceSet != null;
            ResourceSet resourceSet2;
            if (flag)
            {
                resourceSet2 = resourceSet;
            }
            else
            {
                resourceSet2 = this.TryBase(culture, createIfNotExists, tryParents);
            }
            return resourceSet2;
        }

        private ResourceSet TryGetCachedResourceSet(CultureInfo culture, bool tryParents)
        {
            bool flag = ResRuntime._resCache == null;
            ResourceSet resourceSet;
            if (flag)
            {
                resourceSet = null;
            }
            else
            {
                CultureInfo cultureInfo = culture ?? CultureInfo.InvariantCulture;
                string text;
                ResourceSet resourceSet2;
                byte[] array;
                for (; ; )
                {
                    text = this.BuildResourceFileName(cultureInfo);
                    bool flag2 = this._rsCache.TryGetValue(text, out resourceSet2);
                    if (flag2)
                    {
                        break;
                    }
                    bool flag3 = ResRuntime._resCache.TryGetValue(text, out array);
                    if (flag3)
                    {
                        goto Block_4;
                    }
                    bool flag4 = !tryParents || cultureInfo.Equals(CultureInfo.InvariantCulture);
                    if (flag4)
                    {
                        goto Block_6;
                    }
                    cultureInfo = cultureInfo.Parent;
                }
                return resourceSet2;
            Block_4:
                ResourceSet resourceSet3 = ResManager.CreateResourceSet(array);
                this._rsCache[text] = resourceSet3;
                return resourceSet3;
            Block_6:
                resourceSet = null;
            }
            return resourceSet;
        }

        private ResourceSet TryBase(CultureInfo culture, bool create, bool tryParents)
        {
            ResourceSet resourceSet;
            try
            {
                resourceSet = base.InternalGetResourceSet(culture, create, tryParents);
            }
            catch
            {
                resourceSet = null;
            }
            return resourceSet;
        }

        private ResourceSet TryBaseGetResourceSet(CultureInfo culture, bool create, bool tryParents)
        {
            ResourceSet resourceSet;
            try
            {
                resourceSet = base.GetResourceSet(culture, create, tryParents);
            }
            catch
            {
                resourceSet = null;
            }
            return resourceSet;
        }

        private static ResourceSet CreateResourceSet(byte[] data)
        {
            MemoryStream memoryStream = new MemoryStream(data, false);
            try
            {
                Type type = typeof(ResourceManager).Assembly.GetType("System.Resources.RuntimeResourceSet");
                bool flag = type != null;
                if (flag)
                {
                    return (ResourceSet)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { memoryStream }, null);
                }
            }
            catch
            {
            }
            return new ResourceSet(memoryStream);
        }

        private string BuildResourceFileName(CultureInfo culture)
        {
            bool flag = culture == null || culture.Equals(CultureInfo.InvariantCulture);
            string text;
            if (flag)
            {
                text = this._baseName + ".resources";
            }
            else
            {
                text = this._baseName + "." + culture.Name + ".resources";
            }
            return text;
        }

        private readonly string _baseName;

        private readonly Dictionary<string, ResourceSet> _rsCache = new Dictionary<string, ResourceSet>(StringComparer.OrdinalIgnoreCase);
    }
}