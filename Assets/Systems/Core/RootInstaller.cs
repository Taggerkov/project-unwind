using System;
using KinematicCharacterController;
using Reflex.Core;
using Reflex.Enums;
using Systems.AsyncLoading;
using Systems.Audio;
using Systems.Combat;
using Systems.UI.CombatantSelect;
using UnityEngine;
using Resolution = Reflex.Enums.Resolution;

namespace Systems.Core
{
    public class RootInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private KCCSettings kccSettings;
        [SerializeField] private Audio.AudioSettings audioSettings;

        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterValue(kccSettings);
            containerBuilder.RegisterValue(audioSettings);

            containerBuilder.RegisterType(typeof(GameManager), new[] { typeof(GameManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(AsyncLoader), Lifetime.Singleton, Resolution.Eager);

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
        }
    }
}