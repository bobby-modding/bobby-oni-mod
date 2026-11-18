using System.Collections.Generic;
using UnityEngine;

namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>
    /// The map side of the position list check boxes. The list decides which cells and which
    /// highlighted objects are currently selected; this class publishes that as immutable
    /// snapshots the overlay reads while it paints.
    /// <para>
    /// A null mask means "no filter", not "nothing visible": with no row unchecked there is no
    /// reason to walk the grid, and the overlay keeps its normal highlight path. The masks are
    /// built on the main thread, filled completely and only then assigned, because
    /// <see cref="MaterialSearchOverlay.GetColor"/> reads them from the SimDebugView worker
    /// thread.
    /// </para>
    /// </summary>
    internal static class LocationSelection {
        private static bool[] tileMask;

        private static bool[] backwallMask;

        private static HashSet<GameObject> hiddenTargets;

        /// <summary>Whether the matching tile at <paramref name="cell"/> is still selected.</summary>
        internal static bool IsTileVisible(int cell) {
            var mask = tileMask;
            return mask == null || ((uint) cell < (uint) mask.Length && mask[cell]);
        }

        /// <summary>Whether the matching backwall at <paramref name="cell"/> is still selected.</summary>
        internal static bool IsBackwallVisible(int cell) {
            var mask = backwallMask;
            return mask == null || ((uint) cell < (uint) mask.Length && mask[cell]);
        }

        /// <summary>Whether a highlighted object was unchecked in the position list.</summary>
        internal static bool IsTargetHidden(GameObject go) {
            var hidden = hiddenTargets;
            return hidden != null && hidden.Count > 0 && go != null && hidden.Contains(go);
        }

        /// <summary>
        /// Publishes the current selection and repaints the overlay. Any null mask or a null
        /// <paramref name="hidden"/> set means that part is unfiltered.
        /// </summary>
        internal static void Apply(bool[] tiles, bool[] backwalls,
                HashSet<GameObject> hidden) {
            tileMask = tiles;
            backwallMask = backwalls;
            hiddenTargets = hidden;
            Repaint();
        }

        /// <summary>Removes every filter, so the overlay shows the full search again.</summary>
        internal static void Clear() {
            if (tileMask == null && backwallMask == null &&
                    (hiddenTargets == null || hiddenTargets.Count == 0))
                return;
            tileMask = null;
            backwallMask = null;
            hiddenTargets = null;
            Repaint();
        }

        private static void Repaint() {
            var game = Game.Instance;
            if (game != null)
                game.ForceOverlayUpdate();
        }
    }
}
