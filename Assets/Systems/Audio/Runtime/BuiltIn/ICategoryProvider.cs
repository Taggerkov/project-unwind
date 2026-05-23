using Systems.Audio.Contracts;

namespace Systems.Audio.Runtime.BuiltIn
{
    /// <summary>
    /// Provides read-only access to the current category volume and speed multipliers.
    /// </summary>
    internal interface ICategoryProvider
    {
        /// <summary>Returns the current volume multiplier for the given category.</summary>
        /// <param name="category">The category to query.</param>
        float GetCategoryVolume(AudioCategory category);

        /// <summary>Returns the current speed multiplier for the given category.</summary>
        /// <param name="category">The category to query.</param>
        float GetCategorySpeed(AudioCategory category);
    }
}