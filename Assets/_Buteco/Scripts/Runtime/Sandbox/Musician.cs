using UnityEngine;

namespace Buteco.Sandbox
{
    public enum Instrument { Cavaquinho, Banjo, Pandeiro, Tanta, Repique }

    /// <summary>Músico da roda: balança no tempo da música e toca o instrumento (animação procedural).</summary>
    [RequireComponent(typeof(NpcRig))]
    public class Musician : MonoBehaviour
    {
        public RodaDePagode roda;
        public Instrument instrument;

        NpcRig rig;
        float on;          // 0..1, suaviza a entrada/saída da música
        float phase;

        void Awake()
        {
            rig = GetComponent<NpcRig>();
            phase = Random.value * 10f;
        }

        void LateUpdate()
        {
            bool playing = roda != null && roda.Playing;
            on = Mathf.MoveTowards(on, playing ? 1f : 0f, Time.deltaTime * 3f);
            float b = playing ? roda.Beat : Time.time * 0.8f + phase;
            float tau = Mathf.PI * 2f;

            // corpo: balanço no tempo, cabeça marcando
            float sway = Mathf.Sin(b * Mathf.PI) * 4f * on;
            float nod = Mathf.Abs(Mathf.Sin(b * Mathf.PI)) * 7f * on;
            float idleLook = Mathf.Sin(Time.time * 0.3f + phase) * 18f * (1f - on);
            rig.Body(0f, new Vector3(2f * on, 0, sway), new Vector3(nod - 3f * on, idleLook, -sway * 0.6f));

            switch (instrument)
            {
                case Instrument.Cavaquinho:
                case Instrument.Banjo:
                {
                    // mão direita: palhetada em semicolcheias; esquerda troca de acorde a cada 2 tempos
                    float strum = Mathf.Sin(b * tau * 2f) * 16f * on;
                    float chord = (Mathf.Floor(b / 2f) % 2f) * 5f * on;
                    rig.ArmR(Vector3.zero, new Vector3(strum, 0, 0));
                    rig.ArmL(new Vector3(chord, 0, 0), new Vector3(-chord * 0.6f, 0, 0));
                    break;
                }
                case Instrument.Pandeiro:
                {
                    float shake = Mathf.Sin(b * tau * 2f) * 7f * on;
                    float hit = Mathf.Abs(Mathf.Sin(b * Mathf.PI * 2f)) * 22f * on;
                    rig.ArmL(new Vector3(0, 0, shake), Vector3.zero);
                    rig.ArmR(Vector3.zero, new Vector3(-hit, 0, 0));
                    break;
                }
                case Instrument.Tanta:
                {
                    float l = Mathf.Max(0f, Mathf.Sin(b * tau)) * 24f * on;
                    float r = Mathf.Max(0f, Mathf.Sin(b * tau + Mathf.PI)) * 24f * on;
                    rig.ArmL(Vector3.zero, new Vector3(-l, 0, 0));
                    rig.ArmR(Vector3.zero, new Vector3(-r, 0, 0));
                    break;
                }
                case Instrument.Repique:
                {
                    float r = Mathf.Abs(Mathf.Sin(b * tau)) * 20f * on;
                    float l = Mathf.Max(0f, Mathf.Sin(b * Mathf.PI * 0.5f)) * 14f * on;
                    rig.ArmR(Vector3.zero, new Vector3(-r, 0, 0));
                    rig.ArmL(Vector3.zero, new Vector3(-l, 0, 0));
                    break;
                }
            }
        }
    }
}
