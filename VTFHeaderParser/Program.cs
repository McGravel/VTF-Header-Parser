using System;

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
                var vtfFile = new VtfHeader(fileName);
            }
        }
    }
}
