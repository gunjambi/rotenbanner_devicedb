// Copyright 2025, gunjambi.
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("Generator")]

namespace Rotenbanner
{
    internal static class LookupInternal
    {
        static int ReadVarInt(ReadOnlySpan<byte> data, out int numBytes)
        {
            int value = 0;
            int cursor = 0;
            for (;;)
            {
                if (cursor >= data.Length || cursor > 5)
                    break;

                byte b = data[cursor];
                cursor++;

                value = unchecked( (value << 7) | (b & 0x7f) );
                if (value < 0)
                    break;
                if ((b & 0x80) == 0)
                {
                    numBytes = cursor;
                    return value;
                }
            }

            numBytes = 0;
            return -1;
        }

        internal static ReadOnlySpan<byte> GetModelNameTrie(ReadOnlySpan<byte> database)
        {
            int breakOffset = ReadVarInt(database, out int numBytes);
            if (breakOffset == -1 || numBytes + breakOffset > database.Length)
                return ReadOnlySpan<byte>.Empty;
            return database.Slice(numBytes, breakOffset);
        }

        internal static ReadOnlySpan<byte> GetDisplayNameTrie(ReadOnlySpan<byte> database)
        {
            int breakOffset = ReadVarInt(database, out int numBytes);
            if (breakOffset == -1 || numBytes + breakOffset > database.Length)
                return ReadOnlySpan<byte>.Empty;
            return database.Slice(numBytes + breakOffset);
        }

        internal static int TryLookupTrieIndex(ReadOnlySpan<byte> trie, string deviceModel)
        {
            static int Lookup(ReadOnlySpan<byte> trie, ReadOnlySpan<byte> key)
            {
            process_node:
                int numHeaders = ReadVarInt(trie, out int numBytes);
                trie = trie.Slice(numBytes);

                for (int ndx = 0; ndx < numHeaders; ++ndx)
                {
                    int keyLen = trie.IndexOf((byte)0);
                    if (keyLen == -1)
                        break;
                    ReadOnlySpan<byte> childKey = trie.Slice(0, keyLen);
                    trie = trie.Slice(keyLen + 1);

                    int offset = ReadVarInt(trie, out numBytes);
                    trie = trie.Slice(numBytes);

                    if (offset == -1)
                        break;

                    if (keyLen > key.Length)
                        continue;
                    if (!childKey.SequenceEqual(key.Slice(0, keyLen)))
                        continue;

                    // Match found, this child matches the key

                    if (keyLen > 0)
                    {
                        if (offset >= trie.Length)
                            break;

                        // "Tailcall" by gotoing back up
                        trie = trie.Slice(offset);
                        key = key.Slice(keyLen);

                        // Since we move forward on key (and less trivially on the trie), this will eventually terminate.
                        goto process_node;
                    }
                    else
                    {
                        // Terminal. Matches only if key has been exhausted.
                        if (key.Length == 0)
                            return offset;
                        break;
                    }
                }

                return -1;
            }

            ReadOnlySpan<byte> cursor = trie;
            int keyByteCount = Encoding.UTF8.GetByteCount(deviceModel);
            Span<byte> key = (keyByteCount < 256) ? stackalloc byte[keyByteCount] : new byte[keyByteCount];
            Encoding.UTF8.GetBytes(deviceModel, key);
            return Lookup(cursor, key);
        }

        internal static string? GetKeyFromIndex(ReadOnlySpan<byte> trie, int offset)
        {
            ReadOnlySpan<byte> cursor = trie;

            if (offset < 0 || offset >= cursor.Length)
                return null;
            cursor = cursor.Slice(offset);

            Span<byte> outputBuffer = stackalloc byte[256];
            int outputBufferCursor = outputBuffer.Length;

            // This will terminate as cursor moves forward on every iteration
            for (;;)
            {
                int keyLen = cursor.IndexOf((byte)0);
                if (keyLen == -1)
                    return null;

                if (keyLen == 0)
                    break;

                ReadOnlySpan<byte> key = cursor.Slice(0, keyLen);
                cursor = cursor.Slice(keyLen + 1);

                // ensure there's enough space in the buffer, and copy the key
                // Note we are walking from the leaf to root, so we copy the text segments
                // backwards
                if (key.Length > outputBufferCursor)
                {
                    int contentLength = outputBuffer.Length - outputBufferCursor;
                    int newSize = Math.Max(contentLength + key.Length, outputBuffer.Length * 2);
                    byte[] newBuffer = new byte[newSize];
                    outputBuffer
                        .Slice(outputBufferCursor, contentLength)
                        .CopyTo(newBuffer.AsSpan(newBuffer.Length - contentLength, contentLength));

                    outputBufferCursor = (newBuffer.Length - contentLength);
                    outputBuffer = newBuffer;
                }
                key.CopyTo(outputBuffer.Slice(outputBufferCursor - key.Length, key.Length));
                outputBufferCursor -= key.Length;

                int offsetToParent = ReadVarInt(cursor, out int numBytes);
                offsetToParent += numBytes;
                if (offsetToParent < 0 || offsetToParent >= cursor.Length)
                    return null;
                cursor = cursor.Slice(offsetToParent);
            }

            ReadOnlySpan<byte> result = outputBuffer.Slice(outputBufferCursor);
            return Encoding.UTF8.GetString(result);
        }
    }
}
