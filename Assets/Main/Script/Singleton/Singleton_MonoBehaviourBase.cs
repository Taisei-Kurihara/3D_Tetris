using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;


namespace Common
{
    public class Singleton_MonoBehaviourBase<T> : MonoBehaviour where T : Singleton_MonoBehaviourBase<T>
    {
        protected static T instance;

        // 初期化完了フラグ.
        bool isInitialized = false;
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// �{�̂̎擾
        /// </summary>
        /// <returns></returns>
        public static T Instance()
        {
            if (instance == null)
            {
                var gameObject = new GameObject(typeof(T).Name);
                instance = gameObject.AddComponent<T>();

                // Awake ���Ă΂��O�ɋ���������
                instance.Init().Forget();

                DontDestroyOnLoad(gameObject);
            }
            return instance;
        }

        // SceneManagerSingleton ��
        private async UniTaskVoid Init()
        {
            Debug.Log("[SceneManager] Forced Init start");
            try
            {
                await Addressables.InitializeAsync();
                isInitialized = true;
                Debug.Log("[SceneManager] Forced Init complete");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SceneManager] Forced Init failed: {e}");
            }
        }
    }
}
