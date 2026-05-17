using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Systems.Core;
using UnityEngine;

namespace Systems
{
    public class Bootstrapper : MonoBehaviour
    {
        [Inject] private readonly GameManager _gameManager;

        private void Awake()
        {
            // GameObjectInjector.InjectObject(gameObject, Container.RootContainer);
            _gameManager.BeginCharacterSelect();
        }
    }
}