using System.Collections.Generic;
using System.Linq;

namespace BetterMouse.Core;

/// <summary>
/// Shared handle hit-testing for the physical overlay and the layout editor. When handles
/// coincide — e.g. the joint where a tall monitor meets two stacked neighbours, which the
/// default mapping places at exactly the same edge coordinate — the nearest candidate wins,
/// and repeated clicks at the same spot advance to the next coincident handle so every one
/// is reachable.
/// </summary>
public static class HandleHitTester
{
    public static T? Pick<T>(
        IReadOnlyList<(T Hit, double Distance)> candidates,
        double maxDistance,
        ref T? last)
        where T : class
    {
        var within = candidates
            .Where(c => c.Distance <= maxDistance)
            .OrderBy(c => c.Distance)
            .ToList();

        if (within.Count == 0)
        {
            last = null;
            return null;
        }

        // Only cycle through handles that effectively sit on top of each other; a handle
        // merely within the hit radius must not steal the click from the nearest one.
        var cluster = within
            .Where(c => c.Distance <= within[0].Distance + 3)
            .Select(c => c.Hit)
            .ToList();

        var pick = cluster[0];
        var previous = last;
        if (previous is not null)
        {
            var index = cluster.FindIndex(h => EqualityComparer<T>.Default.Equals(h, previous));
            if (index >= 0)
                pick = cluster[(index + 1) % cluster.Count];
        }

        last = pick;
        return pick;
    }
}
