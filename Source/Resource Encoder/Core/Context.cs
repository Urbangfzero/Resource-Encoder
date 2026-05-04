using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Core
{
    public class Context
    {
        public ModuleWriterOptions modOpts { get; set; }
        public string Path { get; set; }
        public string FileName { get; set; }
        public string OutPutPath { get; set; }
        public string DirPath { get; set; }
        public static bool samePath { get; set; }
        public ModuleDefMD Module { get; set; }
        public Context ctx;

        public Context(string path)
        {
            Path = path;
            Module = ModuleDefMD.Load(path);
            modOpts = new ModuleWriterOptions(Module);
        }

        private void WriterEvent(object sender, ModuleWriterEventArgs e)
        {
        }

        public void SaveFile()
        {
            modOpts.MetadataOptions.Flags =
                MetadataFlags.AlwaysCreateGuidHeap |
                MetadataFlags.AlwaysCreateStringsHeap |
                MetadataFlags.AlwaysCreateUSHeap |
                MetadataFlags.AlwaysCreateBlobHeap |
                MetadataFlags.PreserveAllMethodRids;

            modOpts.Cor20HeaderOptions.Flags = dnlib.DotNet.MD.ComImageFlags.ILOnly;
            modOpts.MetadataLogger = DummyLogger.NoThrowInstance;

            modOpts.WriterEvent += WriterEvent;

            Module.Write(OutPutPath, modOpts);
        }

        public void Clear()
        {
            Path = null;
            Module = null;
        }
    }
}