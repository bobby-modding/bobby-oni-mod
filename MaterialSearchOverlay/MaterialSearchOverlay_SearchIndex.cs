using PeterHan.PLib.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BobbyModding.MaterialSearchOverlay {
    /// <summary>
    /// The kinds of thing the overlay can find. One entry is created per (kind, name) pair, so
    /// the dropdown can disambiguate "Fish" as both a food and a creature with a badge.
    /// </summary>
    internal enum TargetKind {
        Material,
        Equipment,
        Duplicant,
        Bionic,
        Booster,
        Critter,
        Food,
        Plant,
        Industrial,
        Artifact
    }

    /// <summary>
    /// A single searchable thing. Element-backed entries resolve against the cell grid, the
    /// rest resolve against live entities by prefab tag, species tag, or duplicant name.
    /// </summary>
    internal sealed class SearchEntry {
        internal TargetKind Kind;

        /// <summary>Upper cased display name, the primary thing the search matches against.</summary>
        internal string UpperName;

        /// <summary>
        /// Optional second name to match against, checked only after
        /// <see cref="UpperName"/>. Lets a row show a short badge word while still being
        /// findable by the game's own longer term, e.g. "Duplicant" / "Standard Duplicant".
        /// </summary>
        internal string AltUpperName;

        internal string DisplayName;

        internal Color Swatch;

        /// <summary>Only set when <see cref="Kind"/> is <see cref="TargetKind.Material"/>.</summary>
        internal SimHashes Element;

        /// <summary>
        /// Prefab tag for prefab-backed kinds, species tag for <see cref="TargetKind.Critter"/>,
        /// booster prefab tag for <see cref="TargetKind.Booster"/>. Invalid for
        /// <see cref="TargetKind.Duplicant"/> and <see cref="TargetKind.Bionic"/>, which match
        /// a model rather than a prefab.
        /// </summary>
        internal Tag Tag;

        internal SearchEntry(TargetKind kind, string displayName, Color swatch) {
            Kind = kind;
            DisplayName = displayName;
            UpperName = Clean(displayName).ToUpperInvariant();
            Swatch = swatch;
        }

        /// <summary>True when the query matches either of this entry's names.</summary>
        internal bool Matches(string upperQuery) {
            if (UpperName.Contains(upperQuery))
                return true;
            return AltUpperName != null && AltUpperName.Contains(upperQuery);
        }

        /// <summary>
        /// Strips the game's link markup and collapses whitespace so that matching does not
        /// fail on formatting characters baked into prefab names.
        /// </summary>
        internal static string Clean(string name) {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            return STRINGS.UI.StripLinkFormatting(name).Trim();
        }
    }

    /// <summary>
    /// Flat list of everything searchable, built once after the database finishes loading.
    /// A plain list rather than a trie: the total is a few thousand entries and a linear
    /// scan is comfortably faster than maintaining an index that has to stay in sync with
    /// modded prefabs.
    /// </summary>
    internal static class SearchIndex {
        /// <summary>Upper bound on dropdown rows, so a one letter query stays responsive.</summary>
        internal const int MAX_RESULTS = 50;

        internal static List<SearchEntry> Entries { get; private set; } = new List<SearchEntry>();

        private static readonly Color MATERIAL_SWATCH = new Color(0.72f, 0.72f, 0.76f);

        private static readonly Dictionary<TargetKind, Color> KIND_SWATCHES =
            new Dictionary<TargetKind, Color>() {
                { TargetKind.Material, MATERIAL_SWATCH },
                { TargetKind.Equipment, new Color(0.49f, 0.78f, 0.93f) },
                { TargetKind.Duplicant, new Color(0.93f, 0.80f, 0.49f) },
                { TargetKind.Bionic, new Color(0.76f, 0.56f, 0.93f) },
                { TargetKind.Booster, new Color(0.95f, 0.60f, 0.32f) },
                { TargetKind.Critter, new Color(0.56f, 0.87f, 0.60f) },
                { TargetKind.Food, new Color(0.96f, 0.84f, 0.44f) },
                { TargetKind.Plant, new Color(0.44f, 0.76f, 0.47f) },
                { TargetKind.Industrial, new Color(0.64f, 0.68f, 0.74f) },
                { TargetKind.Artifact, new Color(0.90f, 0.55f, 0.85f) }
            };

        /// <summary>
        /// Tags that mark a prefab as an industrial product. Mirrors the sandbox "Industrial
        /// Products" filter so the overlay covers exactly what in game tooling covers.
        /// </summary>
        private static readonly Tag[] INDUSTRIAL_TAGS = {
            GameTags.IndustrialProduct,
            GameTags.IndustrialIngredient,
            GameTags.TechComponents,
            GameTags.Medicine,
            GameTags.MedicalSupplies,
            GameTags.ChargedPortableBattery,
            GameTags.MoltShell,
            TableSaltConfig.TAG
        };

        private static readonly Dictionary<TargetKind, HashSet<string>> seen =
            new Dictionary<TargetKind, HashSet<string>>();

        private static bool worldBuilt;

        /// <summary>
        /// The kinds that are filtered through the active save's DLC set, and so can differ
        /// between two saves played in the same session. Everything else is global to the
        /// process and stays put across save changes. Kept in step with which builder adds
        /// which kind: <see cref="BuildPrefabs"/> and <see cref="BuildBoosters"/> and
        /// <see cref="BuildFoods"/> only ever add these, and <see cref="Build"/> never does.
        /// </summary>
        private static readonly TargetKind[] WORLD_KINDS = {
            TargetKind.Equipment,
            TargetKind.Artifact,
            TargetKind.Industrial,
            TargetKind.Plant,
            TargetKind.Booster,
            TargetKind.Food
        };

        internal static Color SwatchFor(TargetKind kind) {
            return KIND_SWATCHES.TryGetValue(kind, out var color) ? color : Color.gray;
        }

        /// <summary>
        /// Indexes everything that is readable at db init: elements, creature species and the
        /// duplicant models. None of these need a loaded world, so they cost nothing here.
        /// <para>
        /// Everything that needs a world is deliberately left out and deferred to
        /// <see cref="EnsureWorldBuilt"/>. See that method for why db init is too early.
        /// </para>
        /// </summary>
        internal static void Build() {
            Entries = new List<SearchEntry>();
            foreach (var set in seen.Values)
                set.Clear();
            BuildElements();
            BuildCritters();
            BuildDuplicantModels();
            BuildGeneralArtifact();
            Log.Debug("SearchIndex: built {0} entries: {1}".F(Entries.Count, Breakdown()));
        }

        /// <summary>
        /// Per kind entry tally. Cheap, and the only practical way to tell "this kind is
        /// genuinely empty" apart from "this kind silently failed to index", which is
        /// otherwise invisible until someone searches for a missing name in game.
        /// </summary>
        private static string Breakdown() {
            var counts = new Dictionary<TargetKind, int>();
            for (int i = 0; i < Entries.Count; i++) {
                var kind = Entries[i].Kind;
                counts.TryGetValue(kind, out int n);
                counts[kind] = n + 1;
            }
            var parts = new List<string>(counts.Count);
            foreach (var kind in System.Enum.GetValues(typeof(TargetKind))) {
                var k = (TargetKind)kind;
                counts.TryGetValue(k, out int n);
                parts.Add("{0}={1}".F(k, n));
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Marks the save-dependent half of the index stale, so the next overlay open rebuilds
        /// it. Driven by <c>RunAt.OnStartGame</c>, which fires on every world load, so
        /// returning to the main menu and loading a different save cannot leave the previous
        /// save's DLC-filtered equipment, boosters and foods in the dropdown.
        /// </summary>
        internal static void InvalidateWorld() {
            worldBuilt = false;
        }

        /// <summary>
        /// Indexes the parts that need a live world, run on the first overlay open after each
        /// world load.
        /// <para>
        /// This cannot happen at db init. PLib's AfterDbInit is a postfix on
        /// <c>Db.PostProcess</c>, whose only call site is inside <c>Assets.CreatePrefabs</c>,
        /// which runs before any world is loaded. At that moment
        /// <c>Game.Instance</c> is still null, so
        /// <see cref="Game.IsCorrectDlcActiveForCurrentSave"/> takes its "game is not running"
        /// branch and rejects every prefab outright, which silently empties equipment, plants,
        /// industrial products and artifacts. Booster prefabs are missing for the related
        /// reason that the per-save DLC set is not resolved until the world loads, so the
        /// game never creates them. <see cref="EdiblesManager.GetAllFoodTypes"/> additionally
        /// asserts it is only called once a save is in play.
        /// </para>
        /// <para>
        /// The overlay cannot be opened without a save, so first open is the earliest point
        /// where all of this is both available and safe to read. <see cref="InvalidateWorld"/>
        /// re-arms this on every world load.
        /// </para>
        /// </summary>
        internal static void EnsureWorldBuilt() {
            if (worldBuilt)
                return;
            worldBuilt = true;
            if (Game.Instance == null)
                // Would mean the overlay opened without a world. The DLC filter below
                // rejects everything in that state, so fail loudly rather than indexing
                // a silently empty set of prefab kinds.
                Log.Debug("SearchIndex: WARNING Game.Instance is null, prefab indexing " +
                    "would come back empty");
            // Purged here rather than in InvalidateWorld so the purge and the rebuild stay
            // adjacent and cannot drift apart. Add() dedupes through `seen`, so rebuilding on
            // top of a previous save's entries would silently no-op and leave that save's
            // data showing instead of this one's.
            //
            // Keyed on the tag as well as the kind: the general category rows carry no prefab
            // tag and are not save dependent, so purging on kind alone deleted the general
            // artifact row the moment the overlay was first opened.
            Entries.RemoveAll(e => e.Tag != null &&
                Array.IndexOf(WORLD_KINDS, e.Kind) >= 0);
            foreach (var kind in WORLD_KINDS)
                if (seen.TryGetValue(kind, out var set))
                    set.Clear();
            // Those category rows survived the purge, but the clear above forgot their dedup
            // keys, so re-seed them. Without this a second world build would add a duplicate
            // copy of each one.
            for (int i = 0; i < Entries.Count; i++) {
                var e = Entries[i];
                if (e.Tag != null || Array.IndexOf(WORLD_KINDS, e.Kind) < 0)
                    continue;
                if (!seen.TryGetValue(e.Kind, out var keys)) {
                    keys = new HashSet<string>(StringComparer.Ordinal);
                    seen[e.Kind] = keys;
                }
                keys.Add(e.UpperName);
            }
            var start = Time.realtimeSinceStartup;
            BuildPrefabs();
            BuildBoosters();
            BuildFoods();
            // Parenthesised for the same reason as the BuildPrefabs log below.
            Log.Debug(("SearchIndex: world entries added in {0:F0}ms, {1} entries total: " +
                "{2}").F((Time.realtimeSinceStartup - start) * 1000f, Entries.Count,
                    Breakdown()));
        }

        /// <summary>
        /// Adds an entry unless an identical (kind, name) pair is already indexed. Names are
        /// deduplicated per kind because the same display name routinely maps to several
        /// prefabs (every basic fabric variant, every plant stage of a species).
        /// </summary>
        private static SearchEntry Add(TargetKind kind, string displayName, string key) {
            string name = SearchEntry.Clean(displayName);
            if (string.IsNullOrEmpty(name))
                return null;
            if (!seen.TryGetValue(kind, out var keys)) {
                keys = new HashSet<string>(StringComparer.Ordinal);
                seen[kind] = keys;
            }
            if (key == null)
                key = name.ToUpperInvariant();
            if (!keys.Add(key))
                return null;
            var entry = new SearchEntry(kind, name, SwatchFor(kind));
            Entries.Add(entry);
            return entry;
        }

        private static void BuildElements() {
            var elements = ElementLoader.elements;
            foreach (var element in elements) {
                if (element?.tag == null || !element.tag.IsValid)
                    continue;
                if (string.IsNullOrEmpty(element.name))
                    continue;
                var entry = Add(TargetKind.Material, element.name, element.id.ToString());
                if (entry == null)
                    continue;
                entry.Element = element.id;
                entry.Swatch = element.substance?.uiColour ?? MATERIAL_SWATCH;
            }
        }

        /// <summary>
        /// One pass over the prefab registry covers equipment, plants, industrial products
        /// and artifacts. Each of those needs its own registry scan otherwise, and the
        /// registry holds several thousand entries.
        /// </summary>
        private static void BuildPrefabs() {
            var prefabs = Assets.Prefabs;
            int scanned = 0, deprecated = 0, dlcBlocked = 0, badTag = 0, noName = 0;
            int equippable = 0;
            for (int i = 0; i < prefabs.Count; i++) {
                var kPrefabID = prefabs[i];
                var go = kPrefabID?.gameObject;
                if (go == null)
                    continue;
                scanned++;
                if (kPrefabID.HasTag(GameTags.DeprecatedContent)) {
                    deprecated++;
                    continue;
                }
                if (!Game.IsCorrectDlcActiveForCurrentSave(kPrefabID)) {
                    dlcBlocked++;
                    continue;
                }
                var prefabTag = kPrefabID.PrefabTag;
                if (prefabTag == null || !prefabTag.IsValid) {
                    badTag++;
                    continue;
                }
                var name = go.GetProperName();
                if (string.IsNullOrEmpty(name)) {
                    noName++;
                    continue;
                }
                // Every kind resolved from a prefab needs its prefab tag stamped onto the
                // entry, since that tag is what the live entity is looked up by later.
                if (go.GetComponent<Equippable>() != null) {
                    equippable++;
                    AddPrefab(TargetKind.Equipment, name, prefabTag);
                }
                if ((go.GetComponent<Harvestable>() != null ||
                        go.GetComponent<WiltCondition>() != null) &&
                        !kPrefabID.HasTag(GameTags.HideFromCodex))
                    AddPrefab(TargetKind.Plant, name, prefabTag);
                if (kPrefabID.HasTag(GameTags.Artifact) || kPrefabID.HasTag(GameTags.Keepsake))
                    AddPrefab(TargetKind.Artifact, name, prefabTag);
                for (int t = 0; t < INDUSTRIAL_TAGS.Length; t++) {
                    if (!kPrefabID.HasTag(INDUSTRIAL_TAGS[t]))
                        continue;
                    AddPrefab(TargetKind.Industrial, name, prefabTag);
                    break;
                }
            }
            // A silent skip here is the difference between "this kind is genuinely absent"
            // and "this kind failed to index", so the filter tallies are worth logging.
            // Parenthesised deliberately: .F() binds tighter than +, so without the parens it
            // would only format the second literal and silently drop the first placeholders.
            Log.Debug(("BuildPrefabs: scanned={0}, deprecated={1}, dlcBlocked={2}, " +
                "badTag={3}, noName={4}, equippable={5}").F(scanned, deprecated, dlcBlocked,
                    badTag, noName, equippable));
        }

        private static void AddPrefab(TargetKind kind, string displayName, Tag prefabTag) {
            var entry = Add(kind, displayName, prefabTag.ToString());
            if (entry != null)
                entry.Tag = prefabTag;
        }

        private static void BuildFoods() {
            var foods = EdiblesManager.GetAllFoodTypes();
            foreach (var food in foods) {
                if (food == null || string.IsNullOrEmpty(food.Name))
                    continue;
                var entry = Add(TargetKind.Food, food.Name, food.Id);
                if (entry == null)
                    continue;
                entry.Tag = TagManager.Create(food.Id);
            }
        }

        private static void BuildCritters() {
            var species = GameTags.Creatures.Species.AllSpecies_REFLECTION();
            foreach (var tag in species) {
                if (tag == null || !tag.IsValid)
                    continue;
                var name = SearchEntry.Clean(tag.ProperName());
                if (string.IsNullOrEmpty(name))
                    continue;
                var entry = Add(TargetKind.Critter, name, tag.ToString());
                if (entry == null)
                    continue;
                entry.Tag = tag;
            }
        }

        private static void BuildBoosters() {
            var data = BionicUpgradeComponentConfig.UpgradesData;
            int invalidTag = 0, noPrefab = 0, noName = 0, added = 0;
            foreach (var kvp in data) {
                var tag = kvp.Key;
                if (tag == null || !tag.IsValid) {
                    invalidTag++;
                    continue;
                }
                var prefab = Assets.TryGetPrefab(tag);
                if (prefab == null) {
                    noPrefab++;
                    continue;
                }
                var name = prefab.GetProperName();
                if (string.IsNullOrEmpty(name)) {
                    noName++;
                    continue;
                }
                var entry = Add(TargetKind.Booster, name, tag.ToString());
                if (entry == null)
                    continue;
                entry.Tag = tag;
                added++;
            }
            // Every booster is gated behind the Bionic Booster Pack DLC, so an empty
            // UpgradesData is a legitimate state rather than a bug worth hiding.
            Log.Debug(("BuildBoosters: UpgradesData={0}, added={1}, invalidTag={2}, " +
                "noPrefab={3}, noName={4}, DLC3 subscribed={5}").F(
                    data.Count, added, invalidTag, noPrefab, noName,
                    DlcManager.IsContentSubscribed(DlcManager.DLC3_ID)));
        }

        /// <summary>
        /// One entry per duplicant model rather than one per duplicant, so a single search
        /// covers the whole model. Both are indexed under the game's own model name, which is
        /// localized, but the plain duplicant row is labelled with the short badge word
        /// instead so the dropdown does not read "Duplicant | Duplicant".
        /// </summary>
        private static void BuildDuplicantModels() {
            AddDuplicantModel(TargetKind.Duplicant, GameTags.Minions.Models.Standard,
                MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH.TYPES.DUPLICANT);
            AddDuplicantModel(TargetKind.Bionic, GameTags.Minions.Models.Bionic,
                null);
        }

        private static void AddDuplicantModel(TargetKind kind, Tag model, string displayName) {
            if (model == null || !model.IsValid)
                return;
            var modelName = SearchEntry.Clean(model.ProperName());
            if (string.IsNullOrEmpty(modelName))
                return;
            var entry = Add(kind, displayName ?? modelName, model.ToString());
            if (entry == null)
                return;
            // The model tag stays searchable even when the row is labelled more briefly.
            entry.AltUpperName = modelName.ToUpperInvariant();
        }

        /// <summary>
        /// Adds the general artifact row, so one search can cover every artifact in the colony
        /// rather than one named piece. The individually indexed artifacts are added by
        /// <see cref="BuildPrefabs"/> and each carries a prefab tag; this one carries none,
        /// which is how the match set tells the two apart.
        /// <para>
        /// Not tied to the save. Whether any artifacts exist is decided when the colony is
        /// scanned, and the DLC filtering of the individual rows happens there too.
        /// </para>
        /// </summary>
        private static void BuildGeneralArtifact() {
            var entry = Add(TargetKind.Artifact,
                MaterialSearchOverlayStrings.UI.OVERLAYS.MATERIALSEARCH.TYPES.ARTIFACT, null);
            if (entry == null)
                return;
            // "Artifacts" names the same category, so both words should find the row.
            entry.AltUpperName = (entry.DisplayName + "S").ToUpperInvariant();
        }

        private static string FindSample(TargetKind kind) {
            foreach (var entry in Entries) {
                if (entry.Kind == kind)
                    return entry.DisplayName;
            }
            return "MISSING";
        }

        /// <summary>
        /// Free text search across every indexed kind, ranked exact, then prefix, then
        /// contains, alphabetically within each rank.
        /// </summary>
        internal static List<SearchEntry> Match(string upperQuery) {
            var results = new List<SearchEntry>();
            if (string.IsNullOrEmpty(upperQuery))
                return results;
            for (int i = 0; i < Entries.Count; i++) {
                var entry = Entries[i];
                if (entry.Matches(upperQuery))
                    results.Add(entry);
            }
            results.Sort((a, b) => {
                int byRank = Rank(a, upperQuery).CompareTo(Rank(b, upperQuery));
                if (byRank != 0)
                    return byRank;
                int byName = string.CompareOrdinal(a.DisplayName, b.DisplayName);
                return byName != 0 ? byName : a.Kind.CompareTo(b.Kind);
            });
            if (results.Count > MAX_RESULTS)
                results = results.GetRange(0, MAX_RESULTS);
            return results;
        }

        private static int Rank(SearchEntry entry, string upperQuery) {
            return RankOf(entry.UpperName, upperQuery) ?? RankOf(entry.AltUpperName, upperQuery) ??
                2;
        }

        private static int? RankOf(string upperName, string upperQuery) {
            if (upperName == null)
                return null;
            if (upperName.Equals(upperQuery, StringComparison.Ordinal))
                return 0;
            return upperName.StartsWith(upperQuery, StringComparison.Ordinal) ? 1 : 2;
        }
    }
}
