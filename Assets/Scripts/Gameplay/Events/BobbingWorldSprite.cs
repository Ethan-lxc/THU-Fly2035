using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>本地 Y 轴上下浮动（取药点提示图等）。若挂在 <see cref="SickClassmateNpcController"/> / <see cref="DirectionGuideNpcController"/> 的 prompt 下， Awake 时会自动禁用本组件，改由 NPC 脚本的浮动参数驱动，避免重复浮动。</summary>
    public class BobbingWorldSprite : MonoBehaviour
    {
        [Min(0f)]
        public float amplitude = 0.12f;

        [Min(0.01f)]
        public float frequency = 2f;

        Vector3 _baseLocal;

        void Awake()
        {
            _baseLocal = transform.localPosition;
        }

        void Update()
        {
            var y = Mathf.Sin(Time.time * frequency) * amplitude;
            transform.localPosition = new Vector3(_baseLocal.x, _baseLocal.y + y, _baseLocal.z);
        }
    }
}
