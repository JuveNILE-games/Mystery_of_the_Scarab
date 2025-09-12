using UnityEngine;

namespace Core.Utility
{
    public class PersistentSingleton<T> : MonoBehaviour where T : Component
    {
        [Header("Persistent Singleton")]
        [Tooltip("If true, this singleton will detach from any parent on Awake.")]
        public bool UnparentOnAwake = true;

        private static T instance;
        private static readonly object lockObj = new object();

        public static bool HasInstance => instance != null;
        public static T Current => Instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObj)
                    {
                        if (instance == null)
                        {
                            instance = FindFirstObjectByType<T>() ?? CreateNewInstance();
                        }
                    }
                }
                return instance;
            }
        }

        protected virtual void Awake()
        {
            InitializeSingleton();
        }

        private static T CreateNewInstance()
        {
            GameObject obj = new GameObject(typeof(T).Name + " (AutoCreated)");
            return obj.AddComponent<T>();
        }

        protected virtual void InitializeSingleton()
        {
            if (!Application.isPlaying) return;

            if (UnparentOnAwake)
            {
                transform.SetParent(null);
            }

            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (this != instance)
            {
                Destroy(gameObject);
            }
        }
    }
}