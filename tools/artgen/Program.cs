using System;
using Banda.ArtGen;

/// <summary>Консольний запуск генераторів: арт у Assets/Art/Heist, превʼю в Captures/previews.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : "../..";
        string version = args.Length > 1 ? args[1] : "v1";
        foreach (string path in ArtGenRunner.GenerateAll(root))
        {
            Console.WriteLine("арт: " + path);
        }

        foreach (string path in ArtGenRunner.Previews(root, version))
        {
            Console.WriteLine("превʼю: " + path);
        }

        return 0;
    }
}
