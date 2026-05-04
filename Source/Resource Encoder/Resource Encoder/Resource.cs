using Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Protections
{
    internal static class NameGen
    {
        public static void RenameAll(TypeDef type)
        {
            if (type == null) return;
            foreach (MethodDef md in type.Methods) Utils.Rename(md);
            foreach (FieldDef fd in type.Fields) Utils.Rename(fd);
            foreach (PropertyDef pd in type.Properties) Utils.Rename(pd);
            foreach (EventDef ed in type.Events) Utils.Rename(ed);
            Utils.Rename(type);
        }
    }

    public static class ResourcesEncoder
    {
        public static IList<MethodDef> Execute(Context context)
        {
            byte[] masterSeed = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(masterSeed);

            int[] keyPart1 = new int[8];
            int[] keyPart2 = new int[8];
            for (int i = 0; i < 8; i++)
            {
                int chunk = BitConverter.ToInt32(masterSeed, i * 4);
                keyPart1[i] = Utils.RandomTinyInt32();
                keyPart2[i] = chunk ^ keyPart1[i];
            }

            byte[] masterKey = DeriveMasterKey(masterSeed);

            ModuleDefMD runtimeModule = null;
            var excludeMethods = new HashSet<MethodDef>();

            MethodDef initMethod = null;
            MethodDef getStreamMD = null;
            MethodDef getNamesMD = null;
            MethodDef createRMMD = null;
            MethodDef createCRMMD = null;
            FieldDef resCacheField = null;

            try
            {
                var resources = context.Module.Resources
                    .OfType<EmbeddedResource>()
                    .ToList();

                if (resources.Count == 0)
                    return Array.Empty<MethodDef>();

                var protectedNames = new List<string>(resources.Count);

                foreach (EmbeddedResource res in resources)
                {
                    string originalName = res.Name;
                    string storedName = GetStoredResourceName(masterKey, originalName);
                    protectedNames.Add(originalName);

                    byte[] rawData;
                    using (Stream s = res.CreateReader().AsStream())
                    {
                        rawData = new byte[s.Length];
                        s.Read(rawData, 0, rawData.Length);
                    }

                    byte[] encrypted = EncryptBlob(rawData, masterKey, originalName);
                    Array.Clear(rawData, 0, rawData.Length);

                    context.Module.Resources.Add(
                        new EmbeddedResource(storedName, encrypted, res.Attributes));
                    context.Module.Resources.Remove(res);
                }

                byte[] namesRaw = SerializeResourceNames(protectedNames);
                byte[] namesEncrypted = EncryptBlob(namesRaw, masterKey, "SKIDED");
                Array.Clear(namesRaw, 0, namesRaw.Length);

                context.Module.Resources.Add(new EmbeddedResource(GetStoredResourceName(masterKey, "SKIDED"), namesEncrypted, ManifestResourceAttributes.Private));

                runtimeModule = ModuleDefMD.Load(typeof(ResRuntime).Module);
                TypeDef resRuntimeTypeDef = runtimeModule.ResolveTypeDef(MDToken.ToRID(typeof(ResRuntime).MetadataToken));
                IEnumerable<IDnlibDef> injectedDefs = InjectHelper.Inject(resRuntimeTypeDef, context.Module.GlobalType, context.Module);

                TypeDef qlzTypeDef = runtimeModule.ResolveTypeDef(MDToken.ToRID(typeof(QuickLZDecompression).MetadataToken));
                TypeDef injectedQlz = InjectHelper.Inject(qlzTypeDef, context.Module);
                injectedQlz.Namespace = string.Empty;
                context.Module.Types.Add(injectedQlz);

                MethodDef qlzDecompressMethod = injectedQlz.FindMethod("decompress")
                    ?? throw new InvalidOperationException(
                           "Could not find 'decompress' in injected QuickLZDecompression.");

                TypeDef xResManagerTypeDef = runtimeModule.ResolveTypeDef(
                    MDToken.ToRID(typeof(ResManager).MetadataToken));

                TypeDef injectedResManager = InjectHelper.Inject(xResManagerTypeDef, context.Module);
                injectedResManager.Namespace = string.Empty;
                context.Module.Types.Add(injectedResManager);

                initMethod = FindMethod(injectedDefs, "Initialize");
                getStreamMD = FindMethod(injectedDefs, "GetResourceStream");
                getNamesMD = FindMethod(injectedDefs, "GetResourceNames");
                createRMMD = FindMethod(injectedDefs, "CreateResourceManager");
                createCRMMD = FindMethod(injectedDefs, "CreateComponentResourceManager");
                resCacheField = FindField(injectedDefs, "_resCache");

                foreach (IDnlibDef d in injectedDefs)
                    if (d is MethodDef md) excludeMethods.Add(md);
                foreach (MethodDef md in injectedResManager.Methods) excludeMethods.Add(md);
                foreach (MethodDef md in injectedQlz.Methods) excludeMethods.Add(md);

                var mutHelper = new MutationHelper("MutationClass");
                for (int j = 0; j < 8; j++)
                {
                    mutHelper.InjectKey<int>(initMethod, 20 + j, keyPart1[j]);
                    mutHelper.InjectKey<int>(initMethod, 28 + j, keyPart2[j]);
                }

                foreach (MethodDef md in excludeMethods)
                {
                    if (!md.HasBody) continue;
                    foreach (Instruction instr in md.Body.Instructions)
                        if (instr.OpCode == OpCodes.Call
                            && instr.Operand is IMethod cm && cm.Name == "decompress")
                            instr.Operand = qlzDecompressMethod;
                }

                if (resCacheField != null)
                {
                    foreach (MethodDef md in injectedResManager.Methods)
                    {
                        if (!md.HasBody) continue;
                        foreach (Instruction instr in md.Body.Instructions)
                            if ((instr.OpCode == OpCodes.Ldsfld || instr.OpCode == OpCodes.Stsfld)
                                && instr.Operand is IField f && f.Name == "_resCache")
                                instr.Operand = resCacheField;
                    }
                }

                foreach (MethodDef factoryMD in new[] { createRMMD, createCRMMD })
                {
                    if (!factoryMD.HasBody) continue;
                    foreach (Instruction instr in factoryMD.Body.Instructions)
                    {
                        if (instr.OpCode != OpCodes.Newobj) continue;
                        if (!(instr.Operand is IMethod ctorRef)) continue;
                        if (ctorRef.DeclaringType.Name != xResManagerTypeDef.Name) continue;

                        int paramCount = ctorRef.MethodSig.Params.Count;
                        foreach (MethodDef ctor in injectedResManager.Methods)
                        {
                            if (ctor.IsConstructor && ctor.MethodSig.Params.Count == paramCount)
                            {
                                instr.Operand = ctor;
                                break;
                            }
                        }
                    }
                }

                PatchResourceCallSites(context.Module, getStreamMD, getNamesMD, createRMMD, createCRMMD, excludeMethods);

                MethodDef cctor = context.Module.GlobalType.FindOrCreateStaticConstructor();
                cctor.Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(initMethod));

                foreach (IDnlibDef def in injectedDefs)
                    Utils.Rename(def);

                foreach (MethodDef md in injectedResManager.Methods)
                {
                    if (md.IsConstructor) continue;
                    if (md.HasImplMap) continue;
                    if (md.IsVirtual && md.IsReuseSlot) continue;

                    md.Name = Safe.GenerateRandomString();
                }
                foreach (FieldDef fd in injectedResManager.Fields)
                    fd.Name = Safe.GenerateRandomString();
                injectedResManager.Name = Safe.GenerateRandomString();
                injectedResManager.Namespace = string.Empty;
                NameGen.RenameAll(injectedQlz);

                return excludeMethods.ToList();
            }
            finally
            {
                Array.Clear(masterSeed, 0, masterSeed.Length);
                Array.Clear(masterKey, 0, masterKey.Length);
                Array.Clear(keyPart1, 0, keyPart1.Length);
                Array.Clear(keyPart2, 0, keyPart2.Length);
                runtimeModule?.Dispose();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private static void PatchResourceCallSites(ModuleDef module, MethodDef getResourceStream, MethodDef getResourceNames, MethodDef createRM, MethodDef createCRM, HashSet<MethodDef> excludeMethods)
        {
            foreach (TypeDef type in module.GetTypes())
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody || excludeMethods.Contains(method))
                        continue;

                    IList<Instruction> il = method.Body.Instructions;
                    for (int i = 0; i < il.Count; i++)
                    {
                        OpCode op = il[i].OpCode;
                        IMethod callee = il[i].Operand as IMethod;
                        if (callee == null) continue;

                        if ((op == OpCodes.Call || op == OpCodes.Callvirt)
                            && callee.Name == "GetManifestResourceStream"
                            && callee.MethodSig?.Params.Count == 1
                            && callee.MethodSig.Params[0].GetElementType() == ElementType.String
                            && IsAssemblyType(callee.DeclaringType))
                        {
                            Local loc = new Local(module.CorLibTypes.String);
                            method.Body.Variables.Add(loc);
                            il.Insert(i, OpCodes.Stloc.ToInstruction(loc)); i++;
                            il.Insert(i, OpCodes.Pop.ToInstruction()); i++;
                            il.Insert(i, OpCodes.Ldloc.ToInstruction(loc)); i++;
                            il[i].OpCode = OpCodes.Call;
                            il[i].Operand = getResourceStream;
                            continue;
                        }

                        if ((op == OpCodes.Call || op == OpCodes.Callvirt)
                            && callee.Name == "GetManifestResourceNames"
                            && callee.MethodSig?.Params.Count == 0
                            && IsAssemblyType(callee.DeclaringType))
                        {
                            il.Insert(i, OpCodes.Pop.ToInstruction()); i++;
                            il[i].OpCode = OpCodes.Call;
                            il[i].Operand = getResourceNames;
                            continue;
                        }

                        if (op == OpCodes.Newobj
                            && callee.Name == ".ctor"
                            && IsResourceManagerType(callee.DeclaringType)
                            && callee.MethodSig?.Params.Count == 2
                            && callee.MethodSig.Params[0].GetElementType() == ElementType.String)
                        {
                            il[i].OpCode = OpCodes.Call;
                            il[i].Operand = createRM;
                            continue;
                        }

                        if (op == OpCodes.Newobj
                            && callee.Name == ".ctor"
                            && IsComponentResourceManagerType(callee.DeclaringType)
                            && callee.MethodSig?.Params.Count == 1)
                        {
                            il[i].OpCode = OpCodes.Call;
                            il[i].Operand = createCRM;
                        }
                    }
                }
            }
        }

        private static byte[] DeriveMasterKey(byte[] seed)
        {
            using (SHA256 sha = SHA256.Create())
                return sha.ComputeHash(seed);
        }

        private static byte[] DeriveResourceKey(byte[] masterKey, byte[] salt, string name, byte label)
        {
            byte[] nb = Encoding.UTF8.GetBytes(name ?? string.Empty);
            byte[] buf = new byte[salt.Length + nb.Length + 1];
            Buffer.BlockCopy(salt, 0, buf, 0, salt.Length);
            Buffer.BlockCopy(nb, 0, buf, salt.Length, nb.Length);
            buf[buf.Length - 1] = label;
            try
            {
                using (HMACSHA256 hmac = new HMACSHA256(masterKey))
                    return hmac.ComputeHash(buf);
            }
            finally
            {
                Array.Clear(nb, 0, nb.Length);
                Array.Clear(buf, 0, buf.Length);
            }
        }

        private static byte[] EncryptBlob(byte[] rawData, byte[] masterKey, string resourceName)
        {
            byte[] compressed = DeflateCompressor.CompressBytes(rawData, CompressionLevel.Optimal);

            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] encKey = DeriveResourceKey(masterKey, salt, resourceName, 1);
            byte[] macKey = DeriveResourceKey(masterKey, salt, resourceName, 2);

            byte[] ciphertext;
            using (Aes aes = Aes.Create())
            {
                aes.Key = encKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(compressed, 0, compressed.Length);
                    cs.FlushFinalBlock();
                    ciphertext = ms.ToArray();
                }
            }
            Array.Clear(compressed, 0, compressed.Length);
            Array.Clear(encKey, 0, encKey.Length);

            byte[] mac;
            using (HMACSHA256 hmac = new HMACSHA256(macKey))
            {
                byte[] aad = new byte[salt.Length + iv.Length + ciphertext.Length];
                Buffer.BlockCopy(salt, 0, aad, 0, salt.Length);
                Buffer.BlockCopy(iv, 0, aad, salt.Length, iv.Length);
                Buffer.BlockCopy(ciphertext, 0, aad, salt.Length + iv.Length, ciphertext.Length);
                mac = hmac.ComputeHash(aad);
            }
            Array.Clear(macKey, 0, macKey.Length);

            using (MemoryStream out_ = new MemoryStream(salt.Length + iv.Length + mac.Length + ciphertext.Length))
            {
                out_.Write(salt, 0, salt.Length);
                out_.Write(iv, 0, iv.Length);
                out_.Write(mac, 0, mac.Length);
                out_.Write(ciphertext, 0, ciphertext.Length);
                return out_.ToArray();
            }
        }

        private static string GetStoredResourceName(byte[] masterKey, string resourceName)
        {
            byte[] nb = Encoding.UTF8.GetBytes(resourceName ?? string.Empty);
            try
            {
                using (HMACSHA256 hmac = new HMACSHA256(masterKey))
                {
                    byte[] hash = hmac.ComputeHash(nb);
                    StringBuilder sb = new StringBuilder(64);
                    foreach (byte b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            finally { Array.Clear(nb, 0, nb.Length); }
        }

        private static byte[] SerializeResourceNames(List<string> names)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(BitConverter.GetBytes(names.Count), 0, 4);
                foreach (string name in names)
                {
                    byte[] nb = Encoding.UTF8.GetBytes(name ?? string.Empty);
                    ms.Write(BitConverter.GetBytes(nb.Length), 0, 4);
                    ms.Write(nb, 0, nb.Length);
                }
                return ms.ToArray();
            }
        }

        private static bool IsAssemblyType(ITypeDefOrRef t) => t != null && (t.FullName == "System.Reflection.Assembly" || t.FullName == "System.Reflection.RuntimeAssembly");

        private static bool IsResourceManagerType(ITypeDefOrRef t) => t != null && t.FullName == "System.Resources.ResourceManager";

        private static bool IsComponentResourceManagerType(ITypeDefOrRef t) => t != null && t.FullName == "System.ComponentModel.ComponentResourceManager";

        private static MethodDef FindMethod(IEnumerable<IDnlibDef> defs, string name) => (MethodDef)defs.Single(d => d is MethodDef md && md.Name == name);

        private static FieldDef FindField(IEnumerable<IDnlibDef> defs, string name)
        {
            foreach (IDnlibDef d in defs)
                if (d is FieldDef fd && fd.Name == name) return fd;
            return null;
        }
    }
}