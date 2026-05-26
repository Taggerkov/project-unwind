using System;
using KinematicCharacterController;
using Reflex.Core;
using Reflex.Enums;
using Systems.Audio;
using Systems.Audio.Music;
using Systems.Audio.Voiceline;
using Systems.Combat;
using Systems.UI;
using Systems.UI.Menu.CombatantSelect;
using Systems.UI.Core;
using Systems.UI.Menu.MainMenu;
using UnityEngine;
using Resolution = Reflex.Enums.Resolution;

namespace Systems.Core
{
    /// <summary>
    /// Reflex composition root. Registers all major game singletons with the DI container so
    /// they are resolved eagerly at scene load in dependency order. Drag asset references into
    /// the Inspector fields; missing optional assets log a warning and fall back to defaults.
    /// </summary>
    public class RootInstaller : MonoBehaviour, IInstaller
    {
        /// <summary>KCC physics settings applied globally by <see cref="GameManager"/>.</summary>
        [SerializeField] private KCCSettings kccSettings;

        /// <summary>Audio system configuration (backend selection, pool size).</summary>
        [SerializeField] private Audio.Shared.AudioSettings audioSettings;

        /// <summary>Music playlist configuration (menu and combat playlists).</summary>
        [SerializeField] private MusicSettings musicSettings;

        /// <summary>UI configuration (navigate/confirm sounds, cursor colours). Optional; falls back to defaults when unset.</summary>
        [SerializeField] private UISettings uiSettings;

        /// <summary>
        /// Registers all singletons in dependency order. Settings assets are bound as values;
        /// service types are bound as eager singletons resolved when the container is built.
        /// </summary>
        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterValue(kccSettings);
            containerBuilder.RegisterValue(audioSettings);
            containerBuilder.RegisterValue(musicSettings);

            if (!uiSettings)
                Debug.LogWarning("RootInstaller: UISettings is unassigned; UI navigate/confirm sounds are disabled.");
            containerBuilder.RegisterValue(uiSettings ? uiSettings : ScriptableObject.CreateInstance<UISettings>());

            containerBuilder.RegisterType(typeof(GameManager), new[] { typeof(GameManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(CombatManager),
                new[] { typeof(CombatManager), typeof(ITickable<TickManager>) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(PlayerRegistry), new[] { typeof(PlayerRegistry), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(UIManager), new[] { typeof(UIManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(MainMenuScreen),
                new[] { typeof(MainMenuScreen), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(CombatantSelectScreen), Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(AudioManager), new[] { typeof(AudioManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(MusicManager), new[] { typeof(MusicManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(LanguageSystem), Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(VoicelineManager),
                new[] { typeof(VoicelineManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);
        }
    }
}