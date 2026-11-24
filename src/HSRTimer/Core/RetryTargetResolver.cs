using System;
using System.Globalization;
using HumanAPI;

namespace HSRTimer
{
    /// <summary>
    /// Resolves the user-specified one-key retry target (R6.5) to a level id +
    /// source pair suitable for <c>App.LaunchSinglePlayer</c>.
    ///
    /// Accepted inputs:
    /// - the game's English localized level name (case-insensitive) for a
    ///   BuiltIn or EditorPick level, e.g. "Intro", "Power Plant", "Aztec";
    /// - on Store, plcc absolute IDs for BuiltIn and EditorPick levels;
    /// - on Steam, a Workshop level numeric id (loaded in
    ///   <c>WorkshopRepository.levelRepo</c> as Subscription).
    ///
    /// The resolver intentionally keeps workshop targeting id-based: name
    /// collisions between built-in and workshop titles are avoided by only
    /// matching names against official BuiltIn/EditorPick levels.
    /// </summary>
    public static class RetryTargetResolver
    {
        public static bool TryResolve(string input, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            string trimmed = input.Trim();

#if STORE_IL2CPP
            const NumberStyles numericStyle = NumberStyles.None;
#else
            const NumberStyles numericStyle = NumberStyles.Integer;
#endif
            if (ulong.TryParse(trimmed, numericStyle, CultureInfo.InvariantCulture, out ulong numericId))
            {
#if STORE_IL2CPP
                return TryFindStoreLevel(numericId, out levelId, out levelType);
#else
                return TryFindWorkshop(numericId, out levelId, out levelType);
#endif
            }

#if STORE_IL2CPP
            if (MatchesRepriseAlias(trimmed)
                && TryFindStoreLevel(12, out levelId, out levelType))
                return true;
#endif
            return TryFindLevelByName(trimmed, out levelId, out levelType);
        }

#if STORE_IL2CPP
        private const ulong EditorPickOffset = 100;

        private static bool TryFindStoreLevel(ulong retryId, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0;
            levelType = WorkshopItemSource.NotSpecified;
            var game = Game.instance;
            if (game == null || retryId >= 2 * EditorPickOffset)
                return false;

            if (retryId >= EditorPickOffset)
            {
                var levels = game.editorPickLevels;
                int index = (int)(retryId - EditorPickOffset);
                if (levels == null || index >= levels.Length || string.IsNullOrWhiteSpace(levels[index]))
                    return false;

                levelId = (ulong)index;
                levelType = WorkshopItemSource.EditorPick;
                return true;
            }

            var builtInLevels = game.levels;
            int builtInIndex = (int)retryId;
            if (builtInLevels == null || builtInIndex >= builtInLevels.Length
                || string.IsNullOrWhiteSpace(builtInLevels[builtInIndex]))
                return false;

            // The final campaign scene can be omitted from the selectable
            // built-in count. Recognize only the exact known scene at ID 12.
            bool reprise = builtInIndex == 12
                && string.Equals(builtInLevels[builtInIndex], "Intro_Reprise", StringComparison.OrdinalIgnoreCase);
            if (!reprise && (game.levelCount <= 0 || builtInIndex >= game.levelCount))
                return false;

            levelId = retryId;
            levelType = WorkshopItemSource.BuiltIn;
            return true;
        }

        private static bool MatchesRepriseAlias(string input)
            => string.Equals(input, "Reprise", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "Intro_Reprise", StringComparison.OrdinalIgnoreCase);
#else
        private static bool TryFindWorkshop(ulong workshopId, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            var repo = WorkshopRepository.instance != null ? WorkshopRepository.instance.levelRepo : null;
            if (repo == null)
                return false;

            foreach (var item in repo)
            {
                if (item == null)
                    continue;

                // Steam Workshop subscriptions launch through
                // MonoApp.LaunchSinglePlayer(workshopId, Subscription). Local
                // Workshop (lvl:...) levels use a folder path instead of a
                // numeric id, so they are intentionally not accepted here.
                if (item.levelType != WorkshopItemSource.Subscription
                    || item.workshopId != workshopId)
                    continue;

                levelId = item.workshopId;
                levelType = item.levelType;
                return true;
            }

            return false;
        }

#endif

        private static bool TryFindLevelByName(string name, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            var repo = WorkshopRepository.instance != null ? WorkshopRepository.instance.levelRepo : null;
            if (repo != null)
            {
                foreach (var item in repo)
                {
                    if (item == null)
                        continue;

                    if (item.levelType != WorkshopItemSource.BuiltIn
                        && item.levelType != WorkshopItemSource.EditorPick)
                        continue;

                    if (!MatchesName(item, name))
                        continue;

                    levelId = item.workshopId;
                    levelType = item.levelType;
                    return true;
                }
            }

            // Fallback to the raw game arrays when the WorkshopRepository has
            // not loaded the built-in / editor-pick metadata yet.
            return TryFindByGameArrays(name, out levelId, out levelType);
        }

        private static bool TryFindByGameArrays(string name, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            var game = Game.instance;
            if (game == null)
                return false;

            if (game.levels != null)
            {
                int playable = game.levelCount > 0 ? game.levelCount : game.levels.Length;
                for (int i = 0; i < game.levels.Length && i < playable; i++)
                {
                    string internalName = game.levels[i];
                    if (MatchesInternalName(internalName, name))
                    {
                        levelId = (ulong)i;
                        levelType = WorkshopItemSource.BuiltIn;
                        return true;
                    }
                }
            }

            if (game.editorPickLevels != null)
            {
                for (int i = 0; i < game.editorPickLevels.Length; i++)
                {
                    string internalName = game.editorPickLevels[i];
                    if (MatchesInternalName(internalName, name))
                    {
                        levelId = (ulong)i;
                        levelType = WorkshopItemSource.EditorPick;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchesInternalName(string internalName, string input)
        {
            if (string.IsNullOrEmpty(internalName))
                return false;
            if (string.Equals(internalName, input, StringComparison.OrdinalIgnoreCase))
                return true;
            string english = GetEnglishLocalizedLevelName("LEVEL/" + internalName, internalName);
            return !string.IsNullOrEmpty(english)
                && string.Equals(english, input, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesName(WorkshopLevelMetadata metadata, string input)
        {
#if STORE_IL2CPP
            var builtin = metadata?.TryCast<BuiltinLevelMetadata>();
#else
            var builtin = metadata as BuiltinLevelMetadata;
#endif
            string internalName = builtin != null ? builtin.internalName : null;

            // The internal scene id is a useful fallback even when the HFF
            // localization asset is not ready yet.
            if (!string.IsNullOrEmpty(internalName)
                && string.Equals(internalName, input, StringComparison.OrdinalIgnoreCase))
                return true;

            string english = GetEnglishLocalizedLevelName("LEVEL/" + internalName, internalName);
            if (!string.IsNullOrEmpty(english)
                && string.Equals(english, input, StringComparison.OrdinalIgnoreCase))
                return true;

            // Fall back to the metadata title as loaded by the game. This lets
            // the input also match the currently displayed localized title, and
            // covers editor picks whose English term differs from the scene id.
            return !string.IsNullOrEmpty(metadata.title)
                && string.Equals(metadata.title, input, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Return the English (not current-language) translation for a game
        /// localization term such as <c>LEVEL/Aztec</c>. Delegates to the shared
        /// <see cref="LevelIdentity"/> helper.
        /// </summary>
        private static string GetEnglishLocalizedLevelName(string term, string fallback)
            => LevelIdentity.EnglishLevelName(term, fallback);
    }
}
