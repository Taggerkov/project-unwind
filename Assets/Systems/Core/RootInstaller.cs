using System;
using KinematicCharacterController;
using Reflex.Core;
using Reflex.Enums;
using Systems.Audio;
using Systems.Audio.Music;
using Systems.Audio.Voiceline;
using Systems.Combat;
using Systems.UI.CombatantSelect;
using UnityEngine;
using Resolution = Reflex.Enums.Resolution;

namespace Systems.Core
{
    public class RootInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private KCCSettings kccSettings;
        [SerializeField] private Audio.Shared.AudioSettings audioSettings;
        [SerializeField] private MusicSettings musicSettings;

        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterValue(kccSettings);
            containerBuilder.RegisterValue(audioSettings);
            containerBuilder.RegisterValue(musicSettings);

            containerBuilder.RegisterType(typeof(GameManager), new[] { typeof(GameManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(CombatManager),
                new[] { typeof(CombatManager), typeof(ITickable<TickManager>) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(PlayerRegistry), new[] { typeof(PlayerRegistry), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(CombatantSelectManager),
                new[] { typeof(CombatantSelectManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

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