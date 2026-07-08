namespace Horcrux.Editor.NullRefScanner
{
    /// <summary>
    /// Static lookup table — thống nhất icon, tag, color flag, severity cho mỗi NullRefKind.
    /// Indexed by (int)NullRefKind → O(1) lookup, zero allocation.
    /// </summary>
    public static class NullRefKindDisplay
    {
        public readonly struct Info
        {
            public readonly string Icon;     // "⚠", "✖", "◇"
            public readonly string Tag;      // "[missing]", "[unassigned]"
            public readonly bool   IsDanger; // true → DangerColor (đỏ), false → WarningColor (amber)
            public readonly int    Severity; // sort priority: cao hơn = nghiêm trọng hơn

            public Info(string icon, string tag, bool isDanger, int severity)
            {
                Icon     = icon;
                Tag      = tag;
                IsDanger = isDanger;
                Severity = severity;
            }
        }

        // ──────────────── Lookup table ────────────────

        private static readonly Info[] Table =
        {
            // NullRefKind.Unassigned     (0)
            new("⚠", "[unassigned]",  false, 0),

            // NullRefKind.MissingRef     (1)
            new("✖", "[missing]",     true,  4),

            // NullRefKind.NullAssetRef   (2)
            new("⚠", "[asset ref]",   false, 1),

            // NullRefKind.NullManagedRef (3)
            new("◇", "[managed ref]", true,  3),

            // NullRefKind.NullExposedRef (4)
            new("⚠", "[exposed ref]", false, 2),
        };

        /// <summary>O(1) lookup — trả về display info cho NullRefKind.</summary>
        public static ref readonly Info Get(NullRefKind kind) => ref Table[(int)kind];
    }
}
