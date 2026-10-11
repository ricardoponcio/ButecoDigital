using UnityEngine;

namespace Buteco
{
    /// <summary>Pequeno pool de AudioSources para os sons da sinuca.</summary>
    public class AudioFx : MonoBehaviour
    {
        public AudioClip ballClick, cushion, cueHit, pocket, chalk;
        [Range(0, 1)] public float volume = 0.9f;
        public int voices = 10;

        AudioSource[] pool;
        int next;
        float lastClickTime;

        void Awake()
        {
            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject($"Voz {i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.55f;
                src.minDistance = 1.5f;
                src.maxDistance = 20f;
                src.dopplerLevel = 0f;
                pool[i] = src;
            }
        }

        public void Play(AudioClip clip, Vector3 at, float vol, float pitch = 1f)
        {
            if (clip == null || pool == null || vol <= 0.01f) return;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.transform.position = at;
            src.pitch = pitch * Random.Range(0.94f, 1.06f);
            src.PlayOneShot(clip, Mathf.Clamp01(vol) * volume);
        }

        public void BallHit(Vector3 at, float speed)
        {
            // evita metralhadora de cliques no mesmo quadro (ex.: a saída)
            if (Time.time - lastClickTime < 0.012f && speed < 1f) return;
            lastClickTime = Time.time;
            Play(ballClick, at, Mathf.InverseLerp(0.03f, 3f, speed), Mathf.Lerp(0.9f, 1.1f, Mathf.InverseLerp(0, 4, speed)));
        }

        public void Cushion(Vector3 at, float speed) => Play(cushion, at, Mathf.InverseLerp(0.05f, 3f, speed));
        public void Cue(Vector3 at, float power) => Play(cueHit, at, 0.35f + power * 0.65f);
        public void Pocket(Vector3 at) => Play(pocket, at, 0.9f);
        public void Chalk(Vector3 at) => Play(chalk, at, 0.6f);
    }
}
