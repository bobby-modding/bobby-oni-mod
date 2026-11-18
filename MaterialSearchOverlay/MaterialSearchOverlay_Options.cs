using Newtonsoft.Json;
using PeterHan.PLib.Options;
using System.Collections.Generic;

namespace BobbyModding.MaterialSearchOverlay {
    [ModInfo("https://github.com/bobby-modding/bobby-oni-mod")]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class MaterialSearchOverlayOptions : IOptions {
        [Option("Enable Debug Logging", "Logs detailed debug information to Player.log")]
        [JsonProperty]
        public bool EnableDebugLogging { get; set; }

        [Option("Show Type Badges", "Shows what kind of thing each search result is, next to its name")]
        [JsonProperty]
        public bool ShowTypeBadges { get; set; }

        /// <summary>
        /// How many matches a search keeps. The dropdown lists this many rows and the map
        /// lights up the same set, so a lower value also keeps a one letter query cheap.
        /// </summary>
        [Option("Maximum Search Results", "How many results a search shows, in the dropdown and on the map. Lower it if a single letter returns more rows than you want to scroll past.")]
        [Limit(5, 100)]
        [JsonProperty]
        public int MaxSearchResults { get; set; }

        /// <summary>
        /// Whether the search box takes the keyboard as soon as the overlay opens, which
        /// is the same whether it was opened from the overlay menu or from the keybind.
        /// </summary>
        [Option("Focus Search Box On Open", "Put the cursor in the search box as soon as the overlay opens. While the box has the keyboard, camera keys type into it instead of moving the view.")]
        [JsonProperty]
        public bool AutoFocusSearch { get; set; }

        /// <summary>
        /// How many rows each section of the position list shows at once. Every section has
        /// its own scroll pane, so a big vein cannot grow the legend off the screen and the
        /// rest of the overlay stays visible around it.
        /// </summary>
        [Option("Maximum Location Rows", "How many rows each section of the position list shows at once. A longer section scrolls instead of making the legend taller. Range 2-12.")]
        [Limit(MIN_LOCATION_ROWS, MAX_LOCATION_ROWS)]
        [JsonProperty]
        public int MaxLocationRows { get; set; }

        /// <summary>Kept in step with the clamp <see cref="MatchLocationUI"/> applies.</summary>
        private const int MIN_LOCATION_ROWS = MatchLocationUI.MIN_VISIBLE_ROWS;

        private const int MAX_LOCATION_ROWS = MatchLocationUI.MAX_VISIBLE_ROWS;

        public MaterialSearchOverlayOptions() {
            EnableDebugLogging = false;
            ShowTypeBadges = true;
            MaxSearchResults = SearchIndex.DEFAULT_MAX_RESULTS;
            AutoFocusSearch = true;
            MaxLocationRows = 5;
        }

        /// <summary>
        /// PLib reads the settings file into a fresh instance each time the options dialog
        /// opens and calls this once the user applies it. Pushing the values into the live
        /// statics is what lets a change take effect without restarting the game.
        /// </summary>
        public void OnOptionsChanged() {
            Patches.ApplyOptions(this);
        }

        /// <summary>No dynamically built options; every option is a property above.</summary>
        public IEnumerable<IOptionsEntry> CreateOptions() {
            return new IOptionsEntry[0];
        }
    }
}
