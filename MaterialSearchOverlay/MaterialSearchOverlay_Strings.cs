using PeterHan.PLib.Core;

namespace BobbyModding.MaterialSearchOverlay {
    public static class MaterialSearchOverlayStrings {
        public const string OVERLAY_ACTION = "action_overlay_material_search";
        public const string OVERLAY_ICON = "overlay_material_search";

        public static class INPUT_BINDINGS {
            public static class ROOT {
                public static LocString MATERIALSEARCH = "Open Material Search Overlay";
            }
        }

        public static class UI {
            public static class OVERLAYS {
                public static class MATERIALSEARCH {
                    public static LocString NAME = "MATERIAL SEARCH OVERLAY";

                    /// <summary>
                    /// Shown in the overlay legend, so it has to do two jobs: say what the
                    /// overlay is for, and show that a name is enough to search by. The
                    /// partial-name line matters more than it looks - matching is
                    /// <c>Contains</c> and case insensitive
                    /// (<see cref="MaterialSearchOverlay_SearchIndex.SearchEntry.Matches"/>),
                    /// so 'hatch' finding 'Hatches' is the one thing worth spending a line on.
                    /// <para>
                    /// Every example here is a real display name from the index, taken from
                    /// the game's own dropdown rows. They have to be exact: the kind words
                    /// match the <c>TYPES</c> badges, and a name that is off by a letter finds
                    /// nothing. "Atmo Suit" is the one to be careful with - the game calls it
                    /// that, not "Atmos Suit", and it is Equipment rather than an industrial
                    /// product. No {0} placeholders: the legend calls
                    /// string.Format with a null formatData, so anything past {0} would throw
                    /// and a {0} would just render blank.
                    /// </para>
                    /// </summary>
                    public static LocString DESCRIPTION =
                        "Search by name to find anything in your colony and highlight it on the map.\n" +
                        "Names can be partial and ignore case: 'hatch' finds Hatches, 'iron' finds Iron Ore.\n" +
                        "Searchable kinds:\n" +
                        "- Materials, e.g. Iron Ore, Bleach Stone\n" +
                        "- Plants and food, e.g. Mealwood, Mirth Leaf, Raw Egg\n" +
                        "- Equipment and boosters, e.g. Atmo Suit, Advanced Medical Booster\n" +
                        "- Industrial products, e.g. Microchip, Atomic Power Bank\n" +
                        "- Critters and duplicants, e.g. Hatches, Pufts, Duplicant\n" +
                        "- Artifacts, e.g. Nuclear Power Plant Model\n";

                    public static LocString BUTTON = "Material Search Overlay";
                    public static LocString TOOLTIP = "Search for materials, equipment, creatures and more by typing their name";
                    public static LocString SEARCH_PLACEHOLDER = "Type a name to search...";

                    public static LocString MASS_LABEL = "Natural Tile: {0} |  Debris: {1} |  Buildings: {2} |  Backwall: {3}";

                    public static LocString COUNT_LABEL = "Found: {0}";

                    public static LocString NO_MATCHES_LABEL = "No matches found";

                    /// <summary>
                    /// One label per searchable kind, shown as a badge on dropdown rows so a
                    /// name that matches several kinds ("Fish" as food and as creature) can be
                    /// told apart. {0} is the count for that kind.
                    /// </summary>
                    public static LocString COUNT_MATERIAL = "{0} materials";
                    public static LocString COUNT_EQUIPMENT = "{0} equipment";
                    public static LocString COUNT_DUPLICANT = "{0} duplicants";
                    public static LocString COUNT_BIONIC = "{0} bionic duplicants";
                    public static LocString COUNT_BOOSTER = "{0} boosters";
                    public static LocString COUNT_CRITTER = "{0} critters";
                    public static LocString COUNT_FOOD = "{0} food";
                    public static LocString COUNT_PLANT = "{0} plants";
                    public static LocString COUNT_INDUSTRIAL = "{0} industrial products";
                    public static LocString COUNT_ARTIFACT = "{0} artifacts";

                    /// <summary>Short kind names, shown on the dropdown row badge.</summary>
                    public static class TYPES {
                        public static LocString MATERIAL = "Material";
                        public static LocString EQUIPMENT = "Equipment";
                        public static LocString DUPLICANT = "Duplicant";
                        public static LocString BIONIC = "Bionic";
                        public static LocString BOOSTER = "Booster";
                        public static LocString CRITTER = "Critter";
                        public static LocString FOOD = "Food";
                        public static LocString PLANT = "Plant";
                        public static LocString INDUSTRIAL = "Industrial";
                        public static LocString ARTIFACT = "Artifact";
                    }

                    public static class TOOLTIPS {
                        public static LocString MATCH = "Material matches the search term";
                        public static LocString NO_MATCH = "Material does not match the search term";
                    }
                }
            }
        }
    }
}