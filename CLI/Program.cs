// Copyright 2025, gunjambi.
// SPDX-License-Identifier: MIT

using Rotenbanner;

namespace CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            for (;;)
            {
                string? device = Console.In.ReadLine();
                if (device == null)
                    return;

                string? displayName = DeviceNameLookup.TryGetDisplayName(device);
                if (displayName != null)
                    Console.WriteLine(displayName);
                else
                    Console.WriteLine("*");
            }
        }
    }
}
