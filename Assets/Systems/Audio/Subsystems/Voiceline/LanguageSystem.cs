namespace Systems.Audio.Voiceline
{
    /// <summary>
    /// Supported languages for localization.
    /// Extensible for future language additions.
    /// </summary>
    public enum Language
    {
        English,
        Spanish,
        French,
        German,
        Japanese
    }

    /// <summary>
    /// Tracks the current runtime language for localization.
    /// </summary>
    public sealed class LanguageSystem
    {
        /// <summary>The currently active runtime language. Defaults to <see cref="Language.English"/>.</summary>
        private Language _currentLanguage = Language.English;

        /// <summary>
        /// Gets the current runtime language.
        /// </summary>
        public Language CurrentLanguage => _currentLanguage;

        /// <summary>
        /// Sets the current runtime language.
        /// </summary>
        /// <param name="language">The language to set.</param>
        public void SetLanguage(Language language)
        {
            _currentLanguage = language;
        }
    }
}
