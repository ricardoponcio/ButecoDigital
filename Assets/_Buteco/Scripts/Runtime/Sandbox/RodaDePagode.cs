using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>A roda de pagode: toca o repertório em sequência, com aplauso e conversa entre as músicas.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class RodaDePagode : MonoBehaviour
    {
        [System.Serializable]
        public class Song
        {
            public string title;
            public AudioClip clip;
            public float bpm = 100f;
        }

        public Song[] songs;
        public AudioClip applause;
        public float pauseSeconds = 6f;

        public bool Playing { get; private set; }
        public Song Current => songs != null && songs.Length > 0 ? songs[index] : null;
        /// <summary>Tempos (batidas) desde o início da música atual.</summary>
        public float Beat { get; private set; }
        /// <summary>Segundos até a próxima música (durante a pausa).</summary>
        public float PauseLeft => Playing ? 0f : Mathf.Max(0f, nextAt - Time.time);

        AudioSource src;
        int index;
        float nextAt;

        void Awake()
        {
            src = GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
        }

        void Start()
        {
            index = 0;
            StartSong();
        }

        void StartSong()
        {
            var s = Current;
            if (s == null || s.clip == null) return;
            src.clip = s.clip;
            src.Play();
            Playing = true;
        }

        /// <summary>Pula pra próxima do repertório (menu de música).</summary>
        public void Skip()
        {
            if (songs == null || songs.Length == 0) return;
            src.Stop();
            index = (index + 1) % songs.Length;
            StartSong();
        }

        void Update()
        {
            if (Playing)
            {
                var s = Current;
                if (src.isPlaying)
                {
                    Beat = (float)src.timeSamples / s.clip.frequency * s.bpm / 60f;
                }
                else if (Time.timeScale > 0f && AudioListener.pause == false)
                {
                    // terminou: aplauso e respiro antes da próxima
                    Playing = false;
                    if (applause != null) src.PlayOneShot(applause, 0.9f);
                    nextAt = Time.time + pauseSeconds;
                    index = (index + 1) % songs.Length;
                }
            }
            else if (Time.time >= nextAt)
            {
                StartSong();
            }
        }
    }
}
