namespace BetterMouse.Core;

/// <summary>All persisted settings, including the authored border profiles and the fallback toggles.</summary>
public sealed class MouseMappingConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>MouseUnSnag-style: when the cursor lands over another display, snap the cursor there.</summary>
    public bool Unstick { get; set; } = true;

    /// <summary>MouseUnSnag-style: jump to the nearest display in the direction of travel.</summary>
    public bool Jump { get; set; } = true;

    /// <summary>MouseUnSnag-style: wrap around the far edge of the desktop.</summary>
    public bool Wrap { get; set; }

    /// <summary>Show the click-through overlay on the physical displays at startup.</summary>
    public bool ShowOverlay { get; set; }

    public List<BorderProfile> Borders { get; set; } = new();

    /// <summary>The first profile describing a crossing out of <paramref name="from"/> on <paramref name="side"/>.</summary>
    public BorderProfile? Find(DisplayInfo from, Side side) => FindAll(from, side).FirstOrDefault();

    /// <summary>
    /// Every profile describing a crossing out of <paramref name="from"/> on
    /// <paramref name="side"/>. A single display edge can face more than one neighbour (e.g.
    /// a tall monitor beside two stacked ones), so callers must consider all candidates and
    /// let the one whose authored range covers the crossing coordinate win.
    /// </summary>
    public IEnumerable<BorderProfile> FindAll(DisplayInfo from, Side side)
    {
        foreach (var p in Borders)
        {
            if (p.AId == from.Id && p.ASide == side) yield return p;
            else if (p.BId == from.Id && p.BSide == side) yield return p;
        }
    }

    /// <summary>
    /// Add a default profile for every adjacent pair that does not already have a
    /// correctly-oriented one, and re-orient a known pair whose displays were rearranged.
    /// Reuses existing profiles so user-authored handles survive restarts.
    ///
    /// Profiles for displays that are not currently attached are deliberately retained:
    /// a monitor can be temporarily absent (a driver-reset topology collapse, or an
    /// occasionally-connected TV or drawing tablet) and its authored handles must survive
    /// the round trip. Such profiles are inert while absent because matching only ever runs
    /// against the present layout. Use <see cref="ResetToDefaults"/> to discard explicitly.
    /// </summary>
    public void EnsureDefaults(DisplayLayout layout)
    {
        foreach (var border in layout.Borders)
        {
            var matches = Borders.Where(p => p.MatchesPair(border.A.Id, border.B.Id)).ToList();
            if (matches.Count > 0)
            {
                var correct = matches.FirstOrDefault(
                    p => p.AId == border.A.Id && p.ASide == border.ASide);
                if (correct is not null)
                {
                    foreach (var stale in matches.Where(m => !ReferenceEquals(m, correct)))
                        Borders.Remove(stale);
                    continue;
                }

                // The pair is known but is now oriented the other way (displays were
                // rearranged). The authored handles no longer apply; fall back to defaults.
                foreach (var stale in matches)
                    Borders.Remove(stale);
            }

            var profile = DefaultMapping.Create(border.A, border.ASide, border.B);
            if (profile is not null) Borders.Add(profile);
        }
    }

    /// <summary>Discard authored profiles and regenerate the DPI-derived defaults.</summary>
    public void ResetToDefaults(DisplayLayout layout)
    {
        Borders.Clear();
        EnsureDefaults(layout);
    }
}
