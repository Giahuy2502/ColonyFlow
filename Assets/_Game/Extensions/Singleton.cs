using UnityEngine;

namespace ColonyFlow
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            T current = this as T;
            if (Instance != null && Instance != current)
            {
                Debug.LogError($"Only one {typeof(T).Name} may exist in a scene.", this);
                enabled = false;
                return;
            }
            Instance = current;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
