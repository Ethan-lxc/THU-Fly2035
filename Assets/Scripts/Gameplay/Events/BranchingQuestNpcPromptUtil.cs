using System;
using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>头顶提示上下（或左右）摆动轴向（本地空间）。2D 侧视一般用 Y；俯视/特殊层级可改用 X 或 Z。</summary>
    public enum BranchingNpcPromptBobAxis
    {
        LocalX = 0,
        LocalY = 1,
        LocalZ = 2
    }

    /// <summary>分支对白 NPC 头顶提示：显隐规则 + 浮动；供同学/指路两个控制器共用。</summary>
    internal static class BranchingQuestNpcPromptUtil
    {
        internal static void DisableBobbingWorldSpritesUnder(GameObject promptRoot)
        {
            if (promptRoot == null)
                return;
            foreach (var b in promptRoot.GetComponentsInChildren<BobbingWorldSprite>(true))
                b.enabled = false;
        }

        internal static Transform ResolveBobbingTransform(GameObject promptRoot, Transform bobbingTarget)
        {
            if (bobbingTarget != null)
                return bobbingTarget;
            return promptRoot != null ? promptRoot.transform : null;
        }

        /// <summary>使用 <see cref="Time.unscaledTime"/>，对白/弹窗把 <c>timeScale=0</c> 时仍会摆动。</summary>
        internal static void TickBobbing(
            Transform t,
            ref Vector3 baseLocal,
            ref bool baseReady,
            float amplitude,
            float frequency,
            bool enabled,
            BranchingNpcPromptBobAxis axis)
        {
            if (!enabled || t == null || !t.gameObject.activeInHierarchy)
                return;
            if (!baseReady)
            {
                baseLocal = t.localPosition;
                baseReady = true;
            }

            var off = Mathf.Sin(Time.unscaledTime * frequency) * amplitude;
            switch (axis)
            {
                case BranchingNpcPromptBobAxis.LocalX:
                    t.localPosition = new Vector3(baseLocal.x + off, baseLocal.y, baseLocal.z);
                    break;
                case BranchingNpcPromptBobAxis.LocalY:
                    t.localPosition = new Vector3(baseLocal.x, baseLocal.y + off, baseLocal.z);
                    break;
                default:
                    t.localPosition = new Vector3(baseLocal.x, baseLocal.y, baseLocal.z + off);
                    break;
            }
        }

        internal static bool ShouldShowPromptRoot(
            bool questComplete,
            bool proximityOnlyWhenNear,
            bool proximityHighlight,
            Func<bool> canInteractForWorldPrompt)
        {
            if (questComplete)
                return false;
            if (proximityOnlyWhenNear)
                return proximityHighlight && canInteractForWorldPrompt != null && canInteractForWorldPrompt();
            return true;
        }
    }
}
