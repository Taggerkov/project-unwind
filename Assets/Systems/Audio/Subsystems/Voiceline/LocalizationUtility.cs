namespace Systems.Audio.Voiceline
{
    /// <summary>
    /// Utility class for retrieving localized text.
    /// Current implementation is a mock that always returns English (the key itself).
    /// In production, this would query a localization database or asset.
    /// </summary>
    public static class LocalizationUtility
    {
        /// <summary>
        /// Retrieves localized text for the given key and language.
        /// Mock implementation: always returns the key as-is (English).
        /// </summary>
        /// <param name="key">The localization key (e.g., "character.greeting").</param>
        /// <param name="language">The target language (currently ignored in mock).</param>
        /// <returns>The localized text. In mock implementation, returns the key itself.</returns>
        public static string GetLocalizedText(string key, Language language)
        {
            // Mock implementation: return key as English text
            // In production: query localization table by key and language
            return key;
        }
    }
}
