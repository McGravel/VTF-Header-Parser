using System;
using System.IO;

namespace VtfHeaderParser
{
    class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                // TODO: Supply Usage message?
                Console.WriteLine("No arguments were given.");
            }
            // TODO: Should each file be stored in its own unique variable? Perhaps a list.
            foreach (var fileName in args)
            {
                try
                {
                    var vtfFile = new VtfHeader(fileName);
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
}
