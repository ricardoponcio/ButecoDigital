using System.Collections.Generic;
using UnityEngine;

namespace Buteco
{
    /// <summary>Guarda o volume "de fábrica" de uma AudioSource e multiplica pelo volume da categoria.</summary>
    [DisallowMultipleComponent]
    public class SoundVolume : MonoBehaviour
    {
        public SoundCategory category;
        public float baseVolume = 1f;
        AudioSource src;

        internal static readonly List<SoundVolume> All = new();

        public static SoundVolume Tag(AudioSource src, SoundCategory cat)
        {
            var v = src.GetComponent<SoundVolume>();
            if (v == null)
            {
                v = src.gameObject.AddComponent<SoundVolume>();
                v.baseVolume = src.volume;
            }
            v.src = src;
            v.category = cat;
            if (!All.Contains(v)) All.Add(v);
            v.Apply();
            return v;
        }

        public void Apply()
        {
            if (src != null) src.volume = baseVolume * GameSettings.VolumeFor(category);
        }

        void OnDestroy() => All.Remove(this);
    }
}
