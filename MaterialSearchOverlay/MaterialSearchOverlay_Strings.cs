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
                        "Search for things in your colony (e.g., hatch, iron, meal lice, atmos suit, arbor tree, artifact, booster)" ;

                    public static LocString BUTTON = "Material Search Overlay";

                    /// <summary>
                    /// Hover text on the overlay menu button. {Hotkey} is filled in by the
                    /// game's own <c>OverlayToggleInfo</c> constructor, and again by
                    /// <c>OverlayMenu.RefreshButtons</c> whenever bindings or the controller
                    /// change, so the key shown always matches the controls menu.
                    /// </summary>
                    public static LocString TOOLTIP = "Search for materials, equipment, creatures and more by typing their name {Hotkey}";
                    public static LocString SEARCH_PLACEHOLDER = "Type a name to search...";

                    /// <summary>
                    /// Caption above the dropdown rows. The arrow keys are spelled out rather
                    /// than drawn as arrow glyphs, since the game font is not guaranteed to
                    /// carry them, and the line has to stay on one line at the legend's width.
                    /// No mouse mention either: the rows already read as clickable, and the
                    /// point of the line is the shortcut nobody can guess at.
                    /// </summary>
                    public static LocString SELECTION_HINT = "Up/Down arrows to choose, Enter to select";

                    public static LocString MASS_LABEL = "Natural Tile: {0} |  Debris: {1} |  Buildings: {2} |  Backwall: {3}";

                    public static LocString COUNT_LABEL = "Found: {0}";

                    public static LocString NO_MATCHES_LABEL = "No matches found";

                    /// <summary>
                    /// The position list shown below the counts once a dropdown row is pinned.
                    /// Every coordinate is world local: X and Y count cells from the bottom
                    /// left corner of the active asteroid, so a distant rocket interior still
                    /// reports small numbers. Areas are connected groups of matching cells;
                    /// clicking any row moves the camera there without changing the zoom.
                    /// <para>
                    /// The list is a snapshot taken while the game is paused and is never
                    /// refreshed, so the strings only have to describe what was found at that
                    /// moment. Section headers count their own rows: areas or items for the
                    /// flat sections, distinct display names for the grouped ones.
                    /// </para>
                    /// </summary>
                    public static LocString LOCATION_TILE_HEADER = "Tile areas ({0})";
                    public static LocString LOCATION_BACKWALL_HEADER = "Backwall areas ({0})";
                    public static LocString LOCATION_DEBRIS_HEADER = "Debris ({0})";
                    public static LocString LOCATION_BUILDING_HEADER = "Building ({0})";
                    public static LocString LOCATION_CONTENTS_HEADER = "Contents ({0})";
                    public static LocString LOCATION_ENTITIES_HEADER = "Entities ({0})";

                    /// <summary>An area row: its world-local bounding box.</summary>
                    public static LocString LOCATION_AREA_BOUNDS = "X {0}-{1}, Y {2}-{3}";

                    /// <summary>A group row, e.g. "Mesh Tile (12)".</summary>
                    public static LocString LOCATION_GROUP_ROW = "{0} ({1})";

                    /// <summary>An instance row: where the matched object stands.</summary>
                    public static LocString LOCATION_ITEM_ROW = "X {0} Y {1}";

                    /// <summary>
                    /// A selected / total pair in one of the two value columns, e.g.
                    /// "1,000 kg / 5,400 kg" for mass or "240 / 402" for tiles and items.
                    /// </summary>
                    public static LocString LOCATION_PAIR = "{0} / {1}";

                    /// <summary>The count column of a single matched object row.</summary>
                    public static LocString LOCATION_ITEM_ONE = "1 item";

                    /// <summary>The count column of a single tile inside an expanded area.</summary>
                    public static LocString LOCATION_TILE_SINGLE = "1 tile";

                    /// <summary>One tile of an expanded area: its X and Y.</summary>
                    public static LocString LOCATION_CELL_ROW = "X {0} Y {1}";

                    /// <summary>
                    /// The scroll position on a section header. 100% is the top and also what
                    /// a section that fits in its pane reports, since there is nothing to
                    /// scroll there.
                    /// </summary>
                    public static LocString LOCATION_SCROLL_PERCENT = "{0}%";

                    /// <summary>
                    /// Last row of a list that hit the build cap, so a huge vein never looks
                    /// complete when it is not. {0} is how many entries are not shown.
                    /// </summary>
                    public static LocString LOCATION_TRUNCATED = "… and {0} more";

                    public static LocString LOCATION_EMPTY = "Nothing found in this world";

                    /// <summary>
                    /// The two box tools above the position list. They act on the rows below
                    /// them: a drag over the map ticks, or unticks, every matching row whose
                    /// cells the box covers, which is why they only appear once a result is
                    /// pinned.
                    /// </summary>
                    public static LocString AREA_ADD = "Add area";

                    public static LocString AREA_REMOVE = "Remove area";

                    public static LocString AREA_ADD_TOOLTIP =
                        "Drag a box over the map to select the matching cells, buildings and items inside it";

                    public static LocString AREA_REMOVE_TOOLTIP =
                        "Drag a box over the map to deselect the matching cells, buildings and items inside it";

                    /// <summary>
                    /// One label per searchable kind, shown as a badge on dropdown rows so a
                    /// name that matches several kinds ("Fish" as food and as creature) can be
                    /// told apart. {0} is the count for that kind.
                    /// <para>
                    /// The count is an item count, not an object count: the game merges
                    /// identical stackable items, so 299 stored Arbor Acorns are reported as
                    /// 299 seeds rather than as the one or few pile objects they occupy.
                    /// </para>
                    /// <para>
                    /// Food is the exception: it reports the combined mass and calories of the
                    /// matches instead of a count, so {0} is the mass and {1} the calories.
                    /// Zero-calorie cooking ingredients have no calorie data, so the calories
                    /// part is omitted for them and only the mass is shown.
                    /// </para>
                    /// </summary>
                    public static LocString COUNT_MATERIAL = "{0} materials";
                    public static LocString COUNT_EQUIPMENT = "{0} equipment";
                    public static LocString COUNT_DUPLICANT = "{0} duplicants";
                    public static LocString COUNT_BIONIC = "{0} bionic duplicants";
                    public static LocString COUNT_BOOSTER = "{0} boosters";
                    public static LocString COUNT_CRITTER = "{0} critters";
                    public static LocString COUNT_EGG = "{0} eggs";
                    public static LocString COUNT_FOOD = "{0} ({1})";
                    public static LocString COUNT_PLANT = "{0} plants";
                    public static LocString COUNT_SEED = "{0} seeds";
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
                        public static LocString EGG = "Egg";
                        public static LocString FOOD = "Food";
                        public static LocString PLANT = "Plant";
                        public static LocString SEED = "Seed";
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