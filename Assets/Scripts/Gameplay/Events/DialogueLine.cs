using System;
using UnityEngine;

namespace Gameplay.Events
{
    [Serializable]
    public struct DialogueLine
    {
        public DialogueSpeaker speaker;

        [TextArea(2, 6)]
        public string text;

        [Tooltip("为空则用 HospitalFetchMedicineEventConfig 中的默认 NPC / 玩家头像")]
        public Sprite overridePortrait;
    }
}
