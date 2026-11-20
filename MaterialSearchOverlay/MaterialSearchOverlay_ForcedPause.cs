using PeterHan.PLib.Core;

namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>
    /// Holds the colony paused from the moment a dropdown result is pinned until the overlay
    /// is closed, and refuses every attempt to unpause in between.
    /// <para>
    /// A frozen simulation is what makes the position list trustworthy: a pinned critter or
    /// duplicant cannot walk away, and a matched item cannot be created or destroyed between
    /// the scan and the moment the player clicks its row. The pause deliberately outlives the
    /// overlay, so the player can look around after closing it and resume with the normal
    /// pause key when ready.
    /// </para>
    /// <para>
    /// The pause state is left exactly one step away from running, never a stack of pauses:
    /// <see cref="Begin"/> only calls <c>Pause</c> when the game is not already paused, and
    /// <see cref="End"/> never calls <c>Unpause</c>. <see cref="Guard"/> only re-pauses when
    /// something else has dropped the count back to zero, so the count cannot grow while the
    /// overlay is open and a single press of the pause key is enough afterwards.
    /// </para>
    /// </summary>
    internal static class ForcedPause {
        /// <summary>
        /// True once a result has been pinned in the current overlay session. Set by
        /// <see cref="Begin"/>, cleared when the overlay closes or the game tears the speed
        /// screen down, never by the player.
        /// </summary>
        internal static bool Active { get; private set; }

        /// <summary>
        /// True while this mod's overlay is the one on screen. The mode is compared rather
        /// than a flag of our own, so a scene change or a missed <c>Disable</c> can never leave
        /// a different session unable to unpause.
        /// </summary>
        internal static bool OverlayOpen {
            get {
                var screen = OverlayScreen.Instance;
                return screen != null && screen.GetMode() == MaterialSearchOverlay.ID;
            }
        }

        /// <summary>True when the pause key and buttons must be ignored right now.</summary>
        internal static bool BlocksUnpause => Active && OverlayOpen;

        /// <summary>
        /// Pins the pause for this overlay session. Safe to call on every selection: the
        /// first call pauses (if the game was running) and the rest do nothing.
        /// </summary>
        internal static void Begin() {
            if (Active)
                return;
            var speed = SpeedControlScreen.Instance;
            if (speed == null)
                return;
            Active = true;
            // Not "toggle": a game that the player had already paused keeps its own pause, and
            // this only adds the one this mod needs. Repeating is impossible because Active
            // short circuits before the call.
            if (!speed.IsPaused)
                speed.Pause(playSound: false);
            Log.Debug("ForcedPause: begin, IsPaused={0}".F(speed.IsPaused));
        }

        /// <summary>
        /// Ends the refusal to unpause when the overlay closes. The simulation is left paused
        /// on purpose, so the player can still examine the map and resumes with the normal
        /// pause key.
        /// </summary>
        internal static void End() {
            if (!Active)
                return;
            Active = false;
            var speed = SpeedControlScreen.Instance;
            Log.Debug("ForcedPause: end, overlay closed and left paused={0}".F(speed != null &&
                speed.IsPaused));
        }

        /// <summary>
        /// Called once per frame by the overlay. Anything that unpauses without going through
        /// <see cref="SpeedControlScreen.TogglePause"/> - the cinema camera, a mod, a screen
        /// closing - is undone here before the simulation can run for a meaningful length of
        /// time. Only pauses when the count has reached zero, so it never stacks.
        /// </summary>
        internal static void Guard() {
            if (!Active)
                return;
            if (!OverlayOpen) {
                // The overlay was torn down without Disable running; the pause it set is left
                // alone, exactly as a normal close would.
                Active = false;
                Log.Debug("ForcedPause: cleared stale state, overlay is no longer active");
                return;
            }
            var speed = SpeedControlScreen.Instance;
            if (speed == null) {
                Active = false;
                return;
            }
            if (!speed.IsPaused) {
                speed.Pause(playSound: false);
                Log.Debug("ForcedPause: re-paused after an external unpause");
            }
        }

        /// <summary>
        /// Drops the flag without touching the pause count. Used when the speed screen itself
        /// is being destroyed (quit to main menu, load), where the pause state belongs to a
        /// world that is going away and must not carry into the next one.
        /// </summary>
        internal static void Reset() {
            Active = false;
        }
    }
}
