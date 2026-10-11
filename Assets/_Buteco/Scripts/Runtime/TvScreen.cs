using UnityEngine;

namespace Buteco
{
    /// <summary>Tela de TV "passando o jogo": varia a luz emitida.</summary>
    public class TvScreen : MonoBehaviour
    {
        public Renderer screen;
        public Light glow;
        public Color a = new(0.2f, 0.8f, 0.3f), b = new(0.6f, 0.9f, 1f);
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock mpb;
        float t, next;
        Color cur, target;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            cur = target = a;
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t > next)
            {
                next = t + Random.Range(0.4f, 3f);
                target = Color.Lerp(a, b, Random.value) * Random.Range(0.8f, 1.4f);
            }
            cur = Color.Lerp(cur, target, Time.deltaTime * 6f);
            if (screen != null)
            {
                mpb ??= new MaterialPropertyBlock();
                mpb.SetColor(EmissionId, cur * 1.6f);
                screen.SetPropertyBlock(mpb);
            }
            if (glow != null) glow.color = cur;
        }
    }
}
