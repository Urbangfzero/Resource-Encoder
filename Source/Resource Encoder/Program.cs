using Core;
using Protections;
using System;
using System.IO;

internal class Program
{
    private static void Main(string[] args)
    {
        Log.Info("Loading context...");

        try
        {
            if (args.Length == 0)
            {
                Log.Warn("Drag and drop a .exe/.dll onto this tool.");
                Wait();
                return;
            }

            string inputPath = args[0];

            if (!File.Exists(inputPath))
            {
                Log.Error("File not found: " + inputPath);
                Wait();
                return;
            }

            string dir = Path.GetDirectoryName(inputPath);
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);

            string outputPath = Path.Combine(dir, name + "-protected" + ext);

            Log.Info($"Input : {inputPath}");
            Log.Info($"Output: {outputPath}");

            Context ctx;
            try
            {
                ctx = new Context(inputPath);
                ctx.OutPutPath = outputPath;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize context.");
                Log.Exception(ex);
                Wait();
                return;
            }

            try
            {
                Log.Step("Running protection...");
                var excluded = ResourcesEncoder.Execute(ctx);
                Log.Success($"Protection completed. Excluded methods: {excluded?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Log.Error("Protection phase failed.");
                Log.Exception(ex);
                Wait();
                return;
            }

            try
            {
                Log.Step("Saving file...");
                ctx.SaveFile();
                Log.Success("File saved successfully.");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save output file.");
                Log.Exception(ex);
                Wait();
                return;
            }

            Log.Success("Done!");
            Log.Info("Saved: " + outputPath);
        }
        catch (Exception ex)
        {
            Log.Error("Fatal error occurred.");
            Log.Exception(ex);
        }

        Wait();
    }

    private static void Wait()
    {
        Console.WriteLine();
        Console.WriteLine("Press ENTER to exit...");
        Console.ReadLine();
    }
}