using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>
    /// 分支 NPC 事件：成就卡弹出时的胜利短乐、未获奖就关对话时的失败短乐（2D 一次性播放）。
    /// </summary>
    public static class NpcEventOutcomeAudio
    {
        public static void PlayClip2D(AudioClip clip, float volume)
        {
            if (clip == null || !Application.isPlaying)
                return;

            var v = Mathf.Clamp01(volume);
            if (v <= 0f)
                return;

            var go = new GameObject("NpcOutcomeOneShotAudio");
            Object.DontDestroyOnLoad(go);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = v;
            src.loop = false;
            src.clip = clip;
            src.Play();
            Object.Destroy(go, clip.length + 0.25f);
        }
    }
}
