// Copyright 2025, gunjambi.
// SPDX-License-Identifier: MIT

namespace Rotenbanner
{
    /// <summary>
    /// Mapping from an Android or iOS technical model name to a human-readable branding name.
    /// </summary>
    public static partial class DeviceNameLookup
    {
        /// <summary>
        /// Maps the technical model name to a human-readable branding name.
        /// If no mapping exists, returns <c>null</c>.
        /// </summary>
        public static string? TryGetDisplayName(string? modelName)
        {
            State? state = s_state.Value;
            if (state == null || modelName == null)
                return null;

            return InternalTryGetDisplayName(state.Database, modelName);
        }

        internal static string? InternalTryGetDisplayName(byte[] database, string modelName)
        {
            int offset = LookupInternal.TryLookupTrieIndex(LookupInternal.GetModelNameTrie(database), modelName);
            if (offset == -1)
                return null;

            return LookupInternal.GetKeyFromIndex(LookupInternal.GetDisplayNameTrie(database), offset);
        }
    }
}
