// Copyright 2025, gunjambi.
// SPDX-License-Identifier: MIT

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Generator
{
    internal class Generator
    {
        public class GooglePlayDeviceInfo
        {
            [Name("Device")]
            public string? DeviceId { get; set; }

            [Name("Retail Branding")]
            public string? Brand { get; set; }

            [Name("Marketing Name")]
            public string? MarketingName { get; set; }

            [Name("Model")]
            public string? ModelName { get; set; }
        }

        static int Main(string[] args)
        {
            Dictionary<string, string> modelToDisplayName = new Dictionary<string, string>();

            for (int ndx = 0; ndx < args.Length; ++ndx)
            {
                if (args[ndx] == "-GooglePlayDevices" && ndx < args.Length - 1)
                {
                    ParseGooglePlaySupportedDevices(modelToDisplayName, args[ndx] + 1);
                    ndx++;
                }
                else if (args[ndx] == "-AppleDevices" && ndx < args.Length - 1)
                {
                    ParseAppleDeviceTraits(modelToDisplayName, args[ndx] + 1);
                    ndx++;
                }
                else
                {
                    PrintUsage();
                    return -1;
                }
            }

            if (modelToDisplayName.Count == 0)
            {
                Console.WriteLine("At least one data source must be set");
                PrintUsage();
                return -1;
            }

            (byte[] displayNames, byte[] modelNames) = BuildDatabase(modelToDisplayName);

            MemoryStream stream = new MemoryStream();
            WriteVarInt(stream, modelNames.Length);
            stream.Write(modelNames);
            stream.Write(displayNames);

            byte[] database = stream.ToArray();
            Validate(database, modelToDisplayName);

            database = Compress(database);

            File.WriteAllBytes("Database.dat", database);
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: Generator [-GooglePlayDevices supported_devices.csv] [-AppleDevices device_traits.db]");
            Console.WriteLine("Outputs into Database.dat");
        }

        static void Validate(byte[] database, Dictionary<string, string> modelToDisplayName)
        {
            foreach ((string model, string displayName) in modelToDisplayName)
            {
                string? displayNameFromDb = Rotenbanner.DeviceNameLookup.InternalTryGetDisplayName(database, model);
                if (displayNameFromDb == null)
                    throw new InvalidOperationException($"did not find {model}");
                if (displayName != displayNameFromDb)
                    throw new InvalidOperationException($"found wrong result {displayNameFromDb}, expected {displayName}");
            }

            HashSet<string> nxKeys = new HashSet<string>();
            foreach (string model in modelToDisplayName.Keys)
            {
                nxKeys.Add(model + "*");
                nxKeys.Add(model.Substring(0, model.Length - 1));
            }
            nxKeys.Add("");
            nxKeys.Add("*");
            for (int ndx = 0; ndx < 10; ++ndx)
                nxKeys.Add(Encoding.UTF8.GetString(RandomNumberGenerator.GetBytes(10)));
            nxKeys.RemoveWhere(k => modelToDisplayName.ContainsKey(k));

            foreach (string nxModel in nxKeys)
            {
                string? displayNameFromDb = Rotenbanner.DeviceNameLookup.InternalTryGetDisplayName(database, nxModel);
                if (displayNameFromDb != null)
                    throw new InvalidOperationException($"found unexpected model {nxModel}");
            }

            // poor-man's random testing
            /*
            byte[] random = new byte[1024];
            for (int ndx = 0; ndx < 1000000; ++ndx)
            {
                RandomNumberGenerator.Fill(random);
                _ = Rotenbanner.DeviceNameLookup.InternalTryGetDisplayName(random, "ab");
            }*/
        }

        static byte[] Compress(byte[] data)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var compress = new BrotliStream(outputStream, new BrotliCompressionOptions() { Quality = 5 }))
                {
                    compress.Write(data);
                }
                return outputStream.ToArray();
            }
        }

        /// <summary>
        /// Parses the Google play certified devices CSV.
        /// See https://storage.googleapis.com/play_public/supported_devices.html
        /// </summary>
        static void ParseGooglePlaySupportedDevices(Dictionary<string, string> modelToDisplayName, string path)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
            };
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, config))
            {
                foreach (var device in csv.GetRecords<GooglePlayDeviceInfo>())
                {
                    // No model name? Skip
                    if (string.IsNullOrEmpty(device.ModelName))
                        continue;

                    // No human name => skip. We could use the retail branding name
                    // but is it any useful? You'd need to search online anyway.
                    if (string.IsNullOrEmpty(device.MarketingName))
                        continue;

                    // Add branding unless already in the name in some form
                    string humanName;
                    if (string.IsNullOrEmpty(device.Brand) || device.MarketingName.StartsWith(device.Brand, StringComparison.InvariantCultureIgnoreCase))
                        humanName = device.MarketingName;
                    else
                        humanName = $"{device.Brand} {device.MarketingName}";

                    // Model name is the same as human name => skip (no need for lookup)
                    if (humanName == device.ModelName)
                        continue;

                    modelToDisplayName[device.ModelName] = humanName;
                }
            }
        }

        /// <summary>
        /// Parses XCode device_traits.db for apple devices.
        /// </summary>
        static void ParseAppleDeviceTraits(Dictionary<string, string> modelToDisplayName, string path)
        {
            SqliteConnection connection = new SqliteConnection($"Data Source={path}");
            connection.Open();

            using var command = new SqliteCommand("SELECT ProductType, ProductDescription FROM Devices;", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string productName = reader.GetString(0);
                string productDescription = reader.GetString(1);

                // Remove trailing -A -B from product name. Apparently devices themselves don't report the suffix.
                if (productName.EndsWith("-A") || productName.EndsWith("-B"))
                    productName = productName.Substring(0, productName.Length - 2);

                modelToDisplayName[productName] = productDescription;
            }
        }

        static (byte[] displayNames, byte[] modelNames) BuildDatabase(Dictionary<string, string> modelToDisplayName)
        {
            string[] displayName = modelToDisplayName.Values.Order().Distinct().ToArray();
            string[] modelNames = modelToDisplayName.Keys.Order().Distinct().ToArray();

            Trie displayNamesTrie = BuildTrie(Array.ConvertAll(displayName, name => (name, 0)));
            SerializedTrieKeyStorage displayNamesBlob = SerializeTrieKeys(displayNamesTrie);

            Trie modelNameTrie = BuildTrie(modelToDisplayName.Select(kv => (kv.Key, displayNamesBlob.Offsets[kv.Value])).ToArray());
            byte[] modelNameBlob = SerializeKeyToStringTrie(modelNameTrie);

            return (displayNamesBlob.Bytes, modelNameBlob);
        }

        class Trie
        {
            public OrderedDictionary<string, Trie> Children = new OrderedDictionary<string, Trie>();
            public int? Value;
        }

        static Trie BuildTrie((string, int)[] elements)
        {
            static void InsertTo(Trie target, string fullKey, string key, int value)
            {
                if (key == "")
                {
                    if (target.Value == null)
                        target.Value = value;
                    else
                        throw new InvalidOperationException($"duplicate key {fullKey}");
                    return;
                }

                string next = key.Substring(0, 1);
                string remainingKey = key.Substring(1);
                if (!target.Children.TryGetValue(next, out Trie? child))
                {
                    child = new Trie();
                    target.Children.Add(next, child);
                }
                InsertTo(child, fullKey, remainingKey, value);
            }

            // Create naive trie
            Trie root = new Trie();
            foreach ((string key, int value) in elements)
                InsertTo(root, key, key, value);

            // Collapse keys.
            static void CollapseKeys(Trie? parent, string key, Trie child, int nodeCost)
            {
                // Bottom up. This changes the keys
                foreach (var grandChild in child.Children.Keys.ToArray())
                {
                    CollapseKeys(child, grandChild, child.Children[grandChild], nodeCost);
                }

                // Collapse with the parent: We can remove this node and move the grandchildren to parent
                // if we don't have a value (and we have a parent)
                if (parent == null)
                    return;
                if (child.Value is not null)
                    return;

                // We want to merge the node with the parent when its beneficial. The more we merge,
                // the flatter the trie becomes (and eventually degenerates to a list). The less we merge,
                // the deeper trie we have and more nodes we have.
                //
                // When we collapse a node, we need to duplicate our Key to the childen. That uses space.
                // But we avoid the node size.
                int collapseCost = key.Length * child.Children.Count;
                int collapseWin = key.Length + 1 + nodeCost; // Key, zero-byte, and content
                if (collapseWin <= collapseCost)
                    return;

                parent.Children.Remove(key);
                foreach ((string grandChildKey, Trie grandChild) in child.Children)
                    parent.Children.Add(key + grandChildKey, grandChild);
            }

            // Collapse nodes to save space. Since collapsing order matters,
            // we first collapse the best, then second best, and so forth.
            //
            // We approximate a node to be approximately 2 bytes of content,
            // so collapse up to that.
            for (int ndx = 0; ndx < 3; ++ndx)
                CollapseKeys(null, "", root, nodeCost: ndx);

            return root;
        }

        /// <summary>
        /// Stores trie keys compactly in a byte array, reusing the prefixes.
        /// Each key is mapped to an offset, and the key can be retrieved from
        /// the serialized data with this prefix.
        /// </summary>
        class SerializedTrieKeyStorage
        {
            public required byte[] Bytes;

            /// <summary>
            /// Offset to the value node of each Trie element.
            /// </summary>
            public required Dictionary<string, int> Offsets;
        }

        static SerializedTrieKeyStorage SerializeTrieKeys(Trie trie)
        {
            // Layout: [child trie]xN [parent node]

            // Node structure:
            //  [ KEY ] [ PARENT OFFSET : int ]
            //  if KEY is EMPTY, the node is root

            static (MemoryStream, Dictionary<string, int>) SerializeTrieWithoutParentOffset(string wholeKey, string trieKey, Trie trie)
            {
                string[] children = trie.Children.Keys.ToArray();
                (MemoryStream blob, Dictionary<string, int> offsets)[] childBlobs = children.Select((string childKey) => SerializeTrieWithoutParentOffset(wholeKey + childKey, childKey, trie.Children[childKey])).ToArray();

                // Append offset to each childBlob
                // Note that the blobs are mutated and hence we need the reverse order to keep the offsets valid
                for (int ndx = childBlobs.Length - 1; ndx >= 0; --ndx)
                {
                    long bytesAfterThisBlob = childBlobs.Skip(ndx + 1).Sum(x => x.blob.Length);
                    WriteVarInt(childBlobs[ndx].blob, bytesAfterThisBlob);
                }

                Dictionary<string, int> offsets = new Dictionary<string, int>();
                MemoryStream blob = new MemoryStream();

                // Concat children and relocate offsets of the children
                foreach ((MemoryStream childBlob, Dictionary<string, int> childOffsets) in childBlobs)
                {
                    foreach (string key in childOffsets.Keys)
                        offsets[key] = checked((int)(childOffsets[key] + blob.Length));
                    CopyWholeStreamTo(blob, childBlob);
                    childBlob.Dispose();
                }

                // If this node has a key, add the offset
                if (trie.Value is not null)
                    offsets[wholeKey] = checked((int)blob.Length);

                // Add this node
                WriteString(blob, trieKey);

                return (blob, offsets);
            }

            (MemoryStream blob, Dictionary<string, int> offsets) = SerializeTrieWithoutParentOffset("", "", trie);
            return new SerializedTrieKeyStorage()
            {
                Bytes = blob.ToArray(),
                Offsets = offsets,
            };
        }

        static byte[] SerializeKeyToStringTrie(Trie trie)
        {
            // Layout: [node] [child trie] xN
            // [node]:
            //      [num headers count]: varInt
            //      [child header] *N
            //      [value header]
            //  [child header]:
            //      key: byte[]
            //      offset: varint > 0
            // [value]:
            //      byte[1] = 0
            //      varInt

            static MemoryStream SerializeNode(Trie trie)
            {
                string[] childKeys = trie.Children.Keys.ToArray();
                MemoryStream[] childBlobs = childKeys.Select(key => SerializeNode(trie.Children[key])).ToArray();
                MemoryStream[] headers = new MemoryStream[childKeys.Length + (trie.Value.HasValue ? 1 : 0)];

                if (trie.Value is int trieValue)
                {
                    headers[childKeys.Length] = new MemoryStream();
                    WriteVarInt(headers[childKeys.Length], 0);
                    WriteVarInt(headers[childKeys.Length], trieValue);
                }

                // Compute offset to children first. The offsets are relative so
                // we can compute them locally. Last node's offset will be the size
                // of all other child nodes and the value header. The second last will
                // contain all preceding recordss nodes, the value header, and the following
                // headers.
                for (int childNdx = childKeys.Length - 1; childNdx >= 0; childNdx--)
                {
                    string childKey = childKeys[childNdx];
                    long bytesOfOtherHeadersFollowing = headers.Skip(childNdx + 1).Sum(blob => blob.Length);
                    long bytesOfOtherTriesPreceeding = childBlobs.Take(childNdx).Sum(blob => blob.Length);

                    // After this header, there are the remaining headers and the preceeding blobs until our node blob.
                    headers[childNdx] = new MemoryStream();
                    WriteString(headers[childNdx], childKey);
                    WriteVarInt(headers[childNdx], bytesOfOtherHeadersFollowing + bytesOfOtherTriesPreceeding);
                }

                MemoryStream blob = new MemoryStream();
                WriteVarInt(blob, headers.Length);
                foreach (var header in headers)
                {
                    CopyWholeStreamTo(blob, header);
                    header.Dispose();
                }

                foreach (var child in childBlobs)
                {
                    CopyWholeStreamTo(blob, child);
                    child.Dispose();
                }

                return blob;
            }

            return SerializeNode(trie).ToArray();
        }

        static void CopyWholeStreamTo(MemoryStream target, MemoryStream src)
        {
            src.Position = 0;
            src.CopyTo(target);
        }

        static void WriteString(MemoryStream stream, string value)
        {
            stream.Write(Encoding.UTF8.GetBytes(value));
            stream.Write(new byte[1] { 0 });
        }

        static void WriteVarInt(MemoryStream stream, long value)
        {
            if (value < 0 || value > UInt32.MaxValue)
                throw new ArgumentOutOfRangeException();

            Span<byte> buf = stackalloc byte[10];
            int cursor = 0;
            UInt32 remaining = (UInt32)value;

            for (;;)
            {
                byte b = (byte)(remaining & 0x7F);
                remaining >>= 7;
                buf[cursor] = b;
                cursor++;
                if (remaining == 0)
                    break;
            }

            buf = buf.Slice(0, cursor);
            buf.Reverse();
            for (int ndx = 0; ndx < buf.Length - 1; ++ndx)
                buf[ndx] |= 0x80;
            stream.Write(buf);
        }
    }
}
