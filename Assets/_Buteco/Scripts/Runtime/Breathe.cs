using UnityEngine;

namespace Buteco
{
    /// <summary>Vira-lata respirando (escala sobe e desce).</summary>
    public class Breathe : MonoBehaviour
    {
        public float amount = 0.04f;
        public float speed = 1.6f;
        Vector3 baseScale;

        void Start() => baseScale = transform.localScale;

        void Update()
        {
            float k = 1f + Mathf.Sin(Time.time * speed) * amount;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * k, baseScale.z * (1f + (k - 1f) * 0.5f));
        }
    }
}
