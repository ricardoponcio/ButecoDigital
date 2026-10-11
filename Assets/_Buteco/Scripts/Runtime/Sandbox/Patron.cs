using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>Freguês sentado: curte a roda balançando a cabeça e de vez em quando toma um gole.</summary>
    [RequireComponent(typeof(NpcRig))]
    public class Patron : MonoBehaviour
    {
        public RodaDePagode roda;

        NpcRig rig;
        float nextSip, sipT = -1f;
        float phase;

        void Awake()
        {
            rig = GetComponent<NpcRig>();
            phase = Random.value * 10f;
            nextSip = Time.time + Random.Range(3f, 12f);
        }

        void LateUpdate()
        {
            bool playing = roda != null && roda.Playing;
            float b = playing ? roda.Beat : 0f;
            float nod = playing ? Mathf.Abs(Mathf.Sin(b * Mathf.PI)) * 5f : 0f;
            float look = Mathf.Sin(Time.time * 0.21f + phase) * 25f;

            float lift = 0f;
            if (sipT >= 0f)
            {
                sipT += Time.deltaTime;
                lift = Mathf.Sin(Mathf.Clamp01(sipT / 1.8f) * Mathf.PI);
                if (sipT > 1.8f) sipT = -1f;
            }
            else if (Time.time > nextSip)
            {
                sipT = 0f;
                nextSip = Time.time + Random.Range(8f, 18f);
            }

            rig.Body(0f, new Vector3(0, 0, Mathf.Sin(b * Mathf.PI) * (playing ? 2.5f : 0f)), new Vector3(nod - lift * 18f, look * (1f - lift), 0));
            rig.ArmR(new Vector3(-lift * 55f, 0, 0), new Vector3(-lift * 70f, 0, 0));
        }
    }
}
