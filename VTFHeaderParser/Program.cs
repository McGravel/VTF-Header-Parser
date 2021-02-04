using System;
using System.IO;
using VtfHeaderParserLib;

/// <summary>
/// Example program that parses VTFs from the command line.
/// </summary>
class Program
{
    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("No arguments were given.");
        }

        foreach (var fileName in args)
        {
            try
            {
                var vtfFile = new VtfFile(fileName);
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"The file was not found:\n{e.FileName}\n");
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("The file is not a valid VTF file.\n");
            }
        }
    }
}

