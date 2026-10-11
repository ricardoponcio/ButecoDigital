using UnityEngine;

namespace Buteco
{
    /// <summary>Lâmpada que pisca de vez em quando (poste velho, fluorescente cansada).</summary>
    public class LightFlicker : MonoBehaviour
    {
        public Light target;
        public Renderer emissiveRenderer;
        public float baseIntensity = 2f;
        public float flickerChance = 0.15f;
        Color baseEmission;
        float timer;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock mpb;

        void Start()
        {
            if (target == null) target = GetComponent<Light>();
            if (target != null) baseIntensity = target.intensity;
            if (emissiveRenderer != null)
            {
                baseEmission = emissiveRenderer.sharedMaterial.GetColor(EmissionId);
                mpb = new MaterialPropertyBlock();
            }
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            bool off = Random.value < flickerChance;
            float k = off ? Random.Range(0.05f, 0.35f) : Random.Range(0.92f, 1.05f);
            timer = off ? Random.Range(0.03f, 0.12f) : Random.Range(0.1f, 2.5f);
            if (target != null) target.intensity = baseIntensity * k;
            if (emissiveRenderer != null)
            {
                mpb.SetColor(EmissionId, baseEmission * k);
                emissiveRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
