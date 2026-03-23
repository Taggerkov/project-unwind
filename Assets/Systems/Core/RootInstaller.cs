using System;
using FixedLogic;
using KinematicCharacterController;
using Reflex.Core;
using Reflex.Enums;
using Systems.AsyncLoading;
using Systems.Combat;
using UnityEngine;
using Resolution = Reflex.Enums.Resolution;

namespace Systems.Core
{
    public class RootInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private KCCSettings kccSettings;

        public void InstallBindings(ContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterValue(kccSettings);

            containerBuilder.RegisterType(typeof(GameManager), new[] { typeof(GameManager), typeof(IDisposable) },
                Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(AsyncLoader), Lifetime.Singleton, Resolution.Eager);

            containerBuilder.RegisterType(typeof(CombatManager), new[] { typeof(CombatManager), typeof(ITickable) },
                Lifetime.Singleton, Resolution.Eager);
        }
    }
}