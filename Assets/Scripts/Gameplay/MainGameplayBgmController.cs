using UnityEngine;

/// <summary>
/// 正式游戏 Main BGM：仅在从主菜单点击「开始」后播放（与 IntroManager 阶段 3 菜单 BGM 分离）。
/// 挂在游戏场景中带 AudioSource 的物体上（可与 PauseMenuController 的 Bgm Audio Source 为同一 AudioSource）。
/// </summary>
public class MainGameplayBgmController : MonoBehaviour
{
    [Header("Main BGM")]
    [Tooltip("正式游戏背景音乐片段")]
    public AudioClip mainBgmClip;

    [Tooltip("留空则在本物体上 GetComponent/AddComponent AudioSource")]
    public AudioSource targetAudioSource;

    [Range(0f, 1f)] public float volume = 1f;

    public bool loop = true;

    [Tooltip("勾选时：仅当玩家点击过「开始」后才播放（推荐）")]
    public bool playOnlyAfterStartButton = true;

#if UNITY_EDITOR
    [Tooltip("在编辑器中直接打开游戏场景、未走开场时是否仍播放（方便测试；开场场景勿勾选）")]
    public bool playInEditorWithoutStart = false;
#endif

    void Awake()
    {
#if UNITY_EDITOR
        if (playInEditorWithoutStart && !GameplayBgmGate.EnteredFromStartButton)
        {
            PlayMainBgmInternal();
            return;
        }
#endif
        if (!playOnlyAfterStartButton)
        {
            PlayMainBgmInternal();
            return;
        }

        if (GameplayBgmGate.EnteredFromStartButton)
        {
            GameplayBgmGate.EnteredFromStartButton = false;
            PlayMainBgmInternal();
        }
    }

    /// <summary>同一场景内点击「开始」不重新加载场景时，由 IntroManager 调用。</summary>
    public static void PlayAllPending()
    {
        if (!GameplayBgmGate.EnteredFromStartButton) return;
        var all = FindObjectsOfType<MainGameplayBgmController>(true);
        GameplayBgmGate.EnteredFromStartButton = false;
        foreach (var m in all)
            m.PlayMainBgmInternal();
    }

    void PlayMainBgmInternal()
    {
        if (mainBgmClip == null) return;
        var src = targetAudioSource;
        if (src == null)
            src = GetComponent<AudioSource>();
        if (src == null)
            src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.volume = volume;
        src.clip = mainBgmClip;
        src.spatialBlend = 0f;
        if (!src.isPlaying)
            src.Play();
    }
}
