using UnityEngine;

namespace Gameplay.Events
{
    /// <summary>本地 Y 轴上下浮动（取药点提示图等）。</summary>
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
