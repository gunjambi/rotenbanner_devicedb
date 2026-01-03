// Copyright 2025, gunjambi.
// SPDX-License-Identifier: MIT

using System.IO.Compression;
using System.Reflection;

namespace Rotenbanner
{
    public static partial class DeviceNameLookup
    {
        record class State(byte[] Database);
        static Lazy<State?> s_state = new Lazy<State?>(InitState, LazyThreadSafetyMode.PublicationOnly);

        static State? InitState()
        {
            try
            {
                byte[] database = ReadResource("Lookup.Resources.Database.dat");
                return new State(database);
            }
            catch
            {
                return null;
            }
        }

        static byte[] ReadResource(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException("Resource not built");
            try
            {
                using var decompressStream = new BrotliStream(stream, CompressionMode.Decompress);
                using var decompressBuffer = new MemoryStream();
                decompressStream.CopyTo(decompressBuffer);
                return decompressBuffer.ToArray();
            }
            finally
            {
                stream.Dispose();
            }
        }
    }
}
