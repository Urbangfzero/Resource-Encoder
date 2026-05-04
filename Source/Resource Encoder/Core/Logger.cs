using System;

public static class Log
{
    public static void Info(string msg)
    {
        Write("[INFO] ", ConsoleColor.Cyan, msg);
    }

    public static void Warn(string msg)
    {
        Write("[WARN] ", ConsoleColor.Yellow, msg);
    }

    public static void Error(string msg)
    {
        Write("[ERROR]", ConsoleColor.Red, msg);
    }

    public static void Success(string msg)
    {
        Write("[ OK ] ", ConsoleColor.Green, msg);
    }

    public static void Step(string msg)
    {
        Write("[STEP] ", ConsoleColor.Magenta, msg);
    }

    public static void Exception(Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine("----- EXCEPTION -----");
        Console.ResetColor();

        Console.WriteLine(ex.Message);
        Console.WriteLine(ex.StackTrace);

        if (ex.InnerException != null)
        {
            Console.WriteLine("---- INNER ----");
            Console.WriteLine(ex.InnerException.Message);
            Console.WriteLine(ex.InnerException.StackTrace);
        }
    }

    private static void Write(string prefix, ConsoleColor color, string msg)
    {
        Console.ForegroundColor = color;
        Console.Write(prefix);
        Console.ResetColor();
        Console.WriteLine(" " + msg);
    }
}