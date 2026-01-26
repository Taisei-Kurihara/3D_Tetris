using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// SE種類の列挙型.
public enum SEType
{
    None,
    LineClear,      // ライン消去時に再生.
    BlockLand,      // ブロック着地確定時に再生.
    BlockFall,      // ブロックが1マス落下時に再生.
    GameOver,       // ゲームオーバー時に再生.
    Rotate,         // ミノ回転成功時に再生.
    RotateFail,     // ミノ回転失敗時に再生.
    Move            // ミノ移動時に再生.
}

// BGM種類の列挙型.
public enum BGMType
{
    None,
    MainGame,
    Title,
    GameOver
}

public class AudioManager : Singleton_DestroyAvailableMonoSingleton<AudioManager>
{
    // 自身に付与するAudioSource(単発SE用).
    private AudioSource seAudioSource;

    // 自身に付与するAudioSource(BGM用).
    private AudioSource bgmAudioSource;

    // 位置音源用の辞書(親オブジェクト -> AudioSource).
    private Dictionary<GameObject, AudioSource> positionalAudioSources = new Dictionary<GameObject, AudioSource>();

    // SEクリップのキャッシュ.
    private Dictionary<SEType, AudioClip> seClips = new Dictionary<SEType, AudioClip>();

    // BGMクリップのキャッシュ.
    private Dictionary<BGMType, AudioClip> bgmClips = new Dictionary<BGMType, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        AudioManager.Instance();
    }

    private void Awake()
    {
        InitializeAudioSources();
    }

    // AudioSourceの初期化.
    private void InitializeAudioSources()
    {
        // SE用AudioSource.
        seAudioSource = gameObject.AddComponent<AudioSource>();
        seAudioSource.playOnAwake = false;

        // BGM用AudioSource.
        bgmAudioSource = gameObject.AddComponent<AudioSource>();
        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.loop = true;
    }

    // SE再生(単発、位置不要).
    public void PlaySE(SEType seType)
    {
        if (seType == SEType.None) return;

        AudioClip clip = GetSEClip(seType);
        if (clip != null && seAudioSource != null)
        {
            seAudioSource.PlayOneShot(clip);
        }
    }

    // SE再生(位置音源、親オブジェクト指定).
    public void PlaySE(SEType seType, GameObject parent, Vector3 localPosition = default)
    {
        if (seType == SEType.None || parent == null) return;

        AudioSource source = GetOrCreatePositionalAudioSource(parent, localPosition);
        AudioClip clip = GetSEClip(seType);

        if (clip != null && source != null)
        {
            source.PlayOneShot(clip);
        }
    }

    // 位置音源用AudioSourceを取得または作成.
    private AudioSource GetOrCreatePositionalAudioSource(GameObject parent, Vector3 localPosition)
    {
        // 辞書に登録済みならそれを返す.
        if (positionalAudioSources.TryGetValue(parent, out AudioSource existingSource))
        {
            if (existingSource != null)
            {
                return existingSource;
            }
            // nullになっていたら辞書から削除.
            positionalAudioSources.Remove(parent);
        }

        // 新規作成.
        GameObject audioObj = new GameObject($"PositionalAudio_{parent.name}");
        audioObj.transform.SetParent(parent.transform);
        audioObj.transform.localPosition = localPosition;

        AudioSource newSource = audioObj.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        newSource.spatialBlend = 1f;

        // 辞書に登録.
        positionalAudioSources[parent] = newSource;

        return newSource;
    }

    // BGM再生.
    public void PlayBGM(BGMType bgmType)
    {
        if (bgmType == BGMType.None)
        {
            StopBGM();
            return;
        }

        AudioClip clip = GetBGMClip(bgmType);
        if (clip != null && bgmAudioSource != null)
        {
            bgmAudioSource.clip = clip;
            bgmAudioSource.Play();
        }
    }

    // BGM停止.
    public void StopBGM()
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
        }
    }

    // SEクリップを取得(Addressablesからロード).
    private AudioClip GetSEClip(SEType seType)
    {
        // キャッシュにあればそれを返す.
        if (seClips.TryGetValue(seType, out AudioClip cachedClip))
        {
            return cachedClip;
        }

        // Addressablesから同期ロード(実際のプロジェクトでは非同期推奨).
        string key = $"SE_{seType}";
        var handle = Addressables.LoadAssetAsync<AudioClip>(key);
        handle.WaitForCompletion();

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            seClips[seType] = handle.Result;
            return handle.Result;
        }

        Debug.LogWarning($"SEクリップが見つかりません: {key}");
        return null;
    }

    // BGMクリップを取得(Addressablesからロード).
    private AudioClip GetBGMClip(BGMType bgmType)
    {
        // キャッシュにあればそれを返す.
        if (bgmClips.TryGetValue(bgmType, out AudioClip cachedClip))
        {
            return cachedClip;
        }

        // Addressablesから同期ロード(実際のプロジェクトでは非同期推奨).
        string key = $"BGM_{bgmType}";
        var handle = Addressables.LoadAssetAsync<AudioClip>(key);
        handle.WaitForCompletion();

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            bgmClips[bgmType] = handle.Result;
            return handle.Result;
        }

        Debug.LogWarning($"BGMクリップが見つかりません: {key}");
        return null;
    }

    // 不要になった位置音源を削除.
    public void RemovePositionalAudioSource(GameObject parent)
    {
        if (positionalAudioSources.TryGetValue(parent, out AudioSource source))
        {
            if (source != null)
            {
                Destroy(source.gameObject);
            }
            positionalAudioSources.Remove(parent);
        }
    }

    // 全ての位置音源をクリーンアップ.
    public void CleanupPositionalAudioSources()
    {
        List<GameObject> keysToRemove = new List<GameObject>();

        foreach (var kvp in positionalAudioSources)
        {
            if (kvp.Key == null || kvp.Value == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            positionalAudioSources.Remove(key);
        }
    }
}
