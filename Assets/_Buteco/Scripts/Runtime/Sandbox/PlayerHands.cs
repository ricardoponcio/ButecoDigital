using Buteco.Players;
using UnityEngine;

namespace Buteco.Sandbox
{
    public enum HeldItem { None, Gelada, Coxinha }

    /// <summary>O que o freguês tem na mão (vista em primeira pessoa): gelada com copo americano, coxinha ou palmas.</summary>
    public class PlayerHands : MonoBehaviour
    {
        [Header("Visual (filhos da câmera)")]
        public Transform glass;
        public Transform liquid;
        public Transform foam;
        public float liquidHeight = 0.075f;
        public Transform bottle;
        public Transform coxinha;
        public Transform handL, handR;

        [Header("Sons")]
        public AudioSource source;
        public AudioClip sipClip, pourClip, openClip, biteClip, clapClip;

        [Header("Gelada")]
        public int sipsPerGlass = 4;
        public int glassesPerBottle = 3;

        public HeldItem Item { get; private set; }
        public bool Busy => anim != Anim.None;
        public int GlassesLeftInBottle { get; private set; }
        public float GlassLevel { get; private set; }
        public int BitesLeft { get; private set; }
        public bool BottleEmpty => Item == HeldItem.Gelada && GlassLevel <= 0.001f && GlassesLeftInBottle == 0;

        enum Anim { None, Sip, Pour, Bite, Clap, Receive }
        Anim anim;
        float animT, animDur;
        float emptyTimer;

        // poses de descanso (local à câmera)
        static readonly Vector3 GlassRest = new(0.2f, -0.23f, 0.42f);
        static readonly Vector3 BottleRest = new(-0.24f, -0.31f, 0.5f);
        static readonly Vector3 CoxinhaRest = new(0.19f, -0.2f, 0.38f);

        void Awake()
        {
            SetItem(HeldItem.None);
        }

        // ------------------------------------------------------------------ itens

        public void GiveGelada()
        {
            SetItem(HeldItem.Gelada);
            GlassesLeftInBottle = glassesPerBottle - 1;
            GlassLevel = 1f;
            Play(openClip, 0.8f);
            StartAnim(Anim.Receive, 0.5f);
        }

        public void GiveCoxinha()
        {
            SetItem(HeldItem.Coxinha);
            BitesLeft = 3;
            StartAnim(Anim.Receive, 0.4f);
        }

        void SetItem(HeldItem item)
        {
            Item = item;
            emptyTimer = 0f;
            if (glass) glass.gameObject.SetActive(item == HeldItem.Gelada);
            if (bottle) bottle.gameObject.SetActive(item == HeldItem.Gelada);
            if (coxinha) coxinha.gameObject.SetActive(item == HeldItem.Coxinha);
            if (handL) handL.gameObject.SetActive(false);
            if (handR) handR.gameObject.SetActive(false);
            if (coxinha) coxinha.localScale = Vector3.one;
        }

        public void Clear() => SetItem(HeldItem.None);

        void StartAnim(Anim a, float dur)
        {
            anim = a;
            animT = 0f;
            animDur = dur;
            if (a == Anim.Clap)
            {
                handL.gameObject.SetActive(true);
                handR.gameObject.SetActive(true);
            }
        }

        void Play(AudioClip clip, float vol)
        {
            if (source != null && clip != null)
            {
                source.pitch = Random.Range(0.94f, 1.06f);
                source.PlayOneShot(clip, vol);
            }
        }

        // ------------------------------------------------------------------ quadro a quadro

        public void Tick(ButecoPlayer player, ExploreInput inp, float dt)
        {
            if (anim != Anim.None)
            {
                animT += dt;
                if (animT >= animDur) FinishAnim(player);
            }
            else if (inp.usePressed)
            {
                switch (Item)
                {
                    case HeldItem.Gelada:
                        if (GlassLevel > 0.001f)
                        {
                            StartAnim(Anim.Sip, 0.95f);
                            Play(sipClip, 0.7f);
                        }
                        else if (GlassesLeftInBottle > 0)
                        {
                            StartAnim(Anim.Pour, 1.4f);
                            Play(pourClip, 0.8f);
                        }
                        break;
                    case HeldItem.Coxinha:
                        StartAnim(Anim.Bite, 0.6f);
                        Play(biteClip, 0.8f);
                        break;
                    default:
                        StartAnim(Anim.Clap, 0.22f);
                        Play(clapClip, 0.9f);
                        break;
                }
            }

            // casco vazio volta pro balcão sozinho depois de um tempinho
            if (BottleEmpty && anim == Anim.None)
            {
                emptyTimer += dt;
                if (emptyTimer > 2.5f)
                {
                    Clear();
                    player.sandbox?.Toast("Casco vazio devolvido. Bora pedir outra?");
                }
            }
        }

        void FinishAnim(ButecoPlayer player)
        {
            switch (anim)
            {
                case Anim.Sip:
                    GlassLevel = Mathf.Max(0f, GlassLevel - 1f / sipsPerGlass);
                    player.Sip();
                    break;
                case Anim.Pour:
                    GlassesLeftInBottle--;
                    GlassLevel = 1f;
                    break;
                case Anim.Bite:
                    BitesLeft--;
                    if (BitesLeft <= 0) Clear();
                    break;
                case Anim.Clap:
                    handL.gameObject.SetActive(false);
                    handR.gameObject.SetActive(false);
                    break;
            }
            anim = Anim.None;
        }

        void LateUpdate()
        {
            float k = animDur > 0f ? Mathf.Clamp01(animT / animDur) : 1f;
            float arc = Mathf.Sin(k * Mathf.PI);          // 0 → 1 → 0
            float bob = Mathf.Sin(Time.time * 1.7f) * 0.004f;

            if (Item == HeldItem.Gelada)
            {
                var gp = GlassRest + Vector3.up * bob;
                var gr = Quaternion.identity;
                var bp = BottleRest + Vector3.up * bob * 0.7f;
                var br = Quaternion.Euler(0, 0, 8f);
                float level = GlassLevel;
                if (anim == Anim.Receive) gp += Vector3.down * (1f - k) * 0.2f;
                if (anim == Anim.Sip)
                {
                    float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arc * 1.6f));
                    gp = Vector3.Lerp(gp, new Vector3(0.03f, -0.1f, 0.21f), lift);
                    gr = Quaternion.Euler(-58f * lift, 0, 0);
                    // o nível desce durante o gole
                    level = GlassLevel - Mathf.Clamp01((k - 0.35f) / 0.4f) / sipsPerGlass;
                }
                else if (anim == Anim.Pour)
                {
                    float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arc * 1.8f));
                    bp = Vector3.Lerp(bp, GlassRest + new Vector3(-0.1f, 0.17f, 0.02f), lift);
                    br = Quaternion.Slerp(br, Quaternion.Euler(0, 0, -115f), lift);
                    gp = Vector3.Lerp(gp, GlassRest + new Vector3(-0.04f, 0.02f, 0f), lift);
                    level = Mathf.Clamp01((k - 0.25f) / 0.6f);
                }
                glass.localPosition = gp;
                glass.localRotation = gr;
                bottle.localPosition = bp;
                bottle.localRotation = br;
                level = Mathf.Clamp01(level);
                liquid.gameObject.SetActive(level > 0.01f);
                liquid.localScale = new Vector3(1f, Mathf.Max(0.01f, level), 1f);
                foam.gameObject.SetActive(level > 0.01f);
                foam.localPosition = new Vector3(0, liquidHeight * level + 0.004f, 0);
            }
            else if (Item == HeldItem.Coxinha)
            {
                var cp = CoxinhaRest + Vector3.up * bob;
                var cr = Quaternion.Euler(10f, -20f, 0);
                if (anim == Anim.Receive) cp += Vector3.down * (1f - k) * 0.2f;
                if (anim == Anim.Bite)
                {
                    float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arc * 1.6f));
                    cp = Vector3.Lerp(cp, new Vector3(0.0f, -0.06f, 0.14f), lift);
                    cr = Quaternion.Slerp(cr, Quaternion.Euler(-70f, 0, 0), lift);
                    float eaten = 3 - BitesLeft + (k > 0.5f ? 1 : 0);
                    coxinha.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, eaten / 3f);
                }
                coxinha.localPosition = cp;
                coxinha.localRotation = cr;
            }

            if (anim == Anim.Clap && handL != null)
            {
                float gap = Mathf.Lerp(0.16f, 0.012f, arc);
                handL.localPosition = new Vector3(-gap * 0.5f - 0.03f, -0.2f, 0.38f);
                handR.localPosition = new Vector3(gap * 0.5f - 0.03f, -0.2f, 0.38f);
            }
        }

        public string Hint(ButecoPlayer player)
        {
            bool pad = player.LastUsedGamepad;
            string use = pad ? "RT" : "Clique";
            switch (Item)
            {
                case HeldItem.Gelada:
                    if (BottleEmpty) return "Acabou a gelada!";
                    if (GlassLevel <= 0.001f) return $"{use}: encher o copo  •  garrafa: {GlassesLeftInBottle} copo(s)";
                    return $"{use}: tomar um gole  •  garrafa: {GlassesLeftInBottle} copo(s)";
                case HeldItem.Coxinha:
                    return $"{use}: dar uma mordida";
                default:
                    return player.CurrentSeat != null && player.CurrentSeat.atRoda ? $"{use}: bater palma com a roda" : null;
            }
        }
    }
}
