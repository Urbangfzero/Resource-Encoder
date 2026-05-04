using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;

namespace Core
{
    public static class Utils
    {
        public static Random rnd = new Random();

        public static byte[] RandomByteArr(int size)
        {
            var result = new byte[size];
            rnd.NextBytes(result);
            return result;
        }

        public static Code GetCode(bool supported = false)
        {
            var codes = new Code[] { Code.Add, Code.And, Code.Xor, Code.Sub, Code.Or };
            if (supported)
                codes = new Code[] { Code.Add, Code.Sub, Code.Xor };
            return codes[rnd.Next(0, codes.Length)];
        }

        public static void Rename(IDnlibDef def)
        {
            if (def == null) return;
            switch (def)
            {
                case MethodDef md:

                    if (!md.IsConstructor && !md.HasImplMap)
                        md.Name = Random();
                    break;

                case FieldDef fd:
                    fd.Name = Random();
                    break;

                case TypeDef td:

                    if (!td.IsGlobalModuleType)
                    {
                        td.Name = Random();
                        td.Namespace = string.Empty;
                    }
                    break;

                case PropertyDef pd:
                    pd.Name = Random();
                    break;

                case EventDef ed:
                    ed.Name = Random();
                    break;
            }
        }

        public static string Random()
        {
            return Safe.GenerateRandomString();
        }

        public static FieldDefUser CreateField(FieldSig sig)
        {
            return new FieldDefUser(GenerateString(), sig, FieldAttributes.Public | FieldAttributes.Static);
        }

        public static MethodDefUser CreateMethod(ModuleDef mod, int num, string mname, string content)
        {
            MethodDefUser mdefuser = null;
            for (int i = 0; i < num; i++)
            {
                mdefuser = new MethodDefUser(mname, MethodSig.CreateStatic(mod.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.Static);
                mdefuser.Body = new CilBody();
                mdefuser.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(content));
                mdefuser.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                mod.GlobalType.Methods.Add(mdefuser);
            }
            return mdefuser;
        }

        public static string GenerateString()
        {
            return Safe.GenerateRandomString();
        }

        public static void MethodsRenamig(IDnlibDef mem)
        {
            Safe.GenerateRandomString();
        }

        public static string MethodsRenamig()
        {
            return Safe.GenerateRandomString();
        }

        public static int RandomTinyInt32() => rnd.Next(2, 25);

        public static int RandomSmallInt32() => rnd.Next(15, 40);

        public static int RandomInt32() => rnd.Next(100, 300);

        public static int RandomBigInt32() => rnd.Next();

        public static bool RandomBoolean() => Convert.ToBoolean(rnd.Next(0, 2));
    }
}