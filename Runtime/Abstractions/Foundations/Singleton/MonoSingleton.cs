using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Singleton
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        public static T Instance => instance;

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
                Destroy(gameObject);
            else
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
        }
    }
}
