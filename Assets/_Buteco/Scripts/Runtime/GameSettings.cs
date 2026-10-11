using System;
using System.Collections.Generic;
using System.Linq;
using Buteco.Sandbox;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Buteco
{
    public enum SoundCategory { Music, Sfx, Ambient }

    /// <summary>
    /// Opções do jogador (geral, vídeo, música), salvas em PlayerPrefs e aplicadas na hora.
    /// Sem AudioMixer: cada AudioSource ganha um <see cref="SoundVolume"/> com a categoria dela.
    /// </summary>
    public static class GameSettings
    {
        // ---------------- geral
        public static float MouseSens = 1f;     // multiplicador
        public static float StickSens = 1f;
        public static bool InvertY;
        public static float Fov = 68f;
        public static bool HeadBob = true;
        public static bool ShowHints = true;

        // ---------------- vídeo
        public static int ResolutionIndex = -1;  // em Resolutions; -1 = a do monitor
        public static FullScreenMode ScreenMode = FullScreenMode.FullScreenWindow;
        public static bool VSync = true;
        public static int FpsCapIndex = 1;       // em FpsCaps
        public static int RenderScaleIndex = 4;  // em RenderScales
        public static int MsaaIndex = 2;         // em MsaaLevels
        public static int ShadowsIndex = 2;      // 0 = desligadas
        public static bool PostFx = true;

        // ---------------- som
        public static float MasterVolume = 1f;
        public static float MusicVolume = 0.8f;
        public static float SfxVolume = 1f;
        public static float AmbientVolume = 0.8f;

        public static readonly int[] FpsCaps = { 30, 60, 120, 144, 0 };
        public static readonly float[] RenderScales = { 0.5f, 0.67f, 0.75f, 0.85f, 1f, 1.25f, 1.5f };
        public static readonly int[] MsaaLevels = { 1, 2, 4, 8 };
        public static readonly string[] ShadowNames = { "Desligadas", "Baixas", "Médias", "Altas" };
        static readonly float[] ShadowDistance = { 0f, 15f, 30f, 50f };
        static readonly int[] ShadowRes = { 512, 1024, 2048, 4096 };
        public static readonly FullScreenMode[] ScreenModes =
            { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
        public static readonly string[] ScreenModeNames = { "Tela cheia", "Tela cheia exclusiva", "Janela" };

        /// <summary>Resoluções distintas do monitor (sem repetir por taxa de atualização), da menor para a maior.</summary>
        public static Resolution[] Resolutions { get; private set; } = Array.Empty<Resolution>();

        public static event Action Changed;

        static bool loaded;
        static UniversalRenderPipelineAsset urpCopy;
#if UNITY_EDITOR
        static RenderPipelineAsset originalPipeline;
        static int originalVSync;
#endif

        // ------------------------------------------------------------------ ciclo

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Load();
            TagSceneAudio();
            ApplyAll();
        }

        public static void Load()
        {
            loaded = true;
            Resolutions = Screen.resolutions
                .GroupBy(r => (r.width, r.height)).Select(g => g.Last())
                .OrderBy(r => r.width).ThenBy(r => r.height).ToArray();

            MouseSens = PlayerPrefs.GetFloat("g.mouse", 1f);
            StickSens = PlayerPrefs.GetFloat("g.stick", 1f);
            InvertY = PlayerPrefs.GetInt("g.invertY", 0) == 1;
            Fov = PlayerPrefs.GetFloat("g.fov", 68f);
            HeadBob = PlayerPrefs.GetInt("g.bob", 1) == 1;
            ShowHints = PlayerPrefs.GetInt("g.hints", 1) == 1;

            ResolutionIndex = PlayerPrefs.GetInt("v.res", -1);
            if (ResolutionIndex >= Resolutions.Length) ResolutionIndex = -1;
            ScreenMode = (FullScreenMode)PlayerPrefs.GetInt("v.mode", (int)FullScreenMode.FullScreenWindow);
            VSync = PlayerPrefs.GetInt("v.vsync", 1) == 1;
            FpsCapIndex = Mathf.Clamp(PlayerPrefs.GetInt("v.fps", 1), 0, FpsCaps.Length - 1);
            RenderScaleIndex = Mathf.Clamp(PlayerPrefs.GetInt("v.scale", 4), 0, RenderScales.Length - 1);
            MsaaIndex = Mathf.Clamp(PlayerPrefs.GetInt("v.msaa", 2), 0, MsaaLevels.Length - 1);
            ShadowsIndex = Mathf.Clamp(PlayerPrefs.GetInt("v.shadows", 2), 0, ShadowNames.Length - 1);
            PostFx = PlayerPrefs.GetInt("v.post", 1) == 1;

            MasterVolume = PlayerPrefs.GetFloat("a.master", 1f);
            MusicVolume = PlayerPrefs.GetFloat("a.music", 0.8f);
            SfxVolume = PlayerPrefs.GetFloat("a.sfx", 1f);
            AmbientVolume = PlayerPrefs.GetFloat("a.amb", 0.8f);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("g.mouse", MouseSens);
            PlayerPrefs.SetFloat("g.stick", StickSens);
            PlayerPrefs.SetInt("g.invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("g.fov", Fov);
            PlayerPrefs.SetInt("g.bob", HeadBob ? 1 : 0);
            PlayerPrefs.SetInt("g.hints", ShowHints ? 1 : 0);

            PlayerPrefs.SetInt("v.res", ResolutionIndex);
            PlayerPrefs.SetInt("v.mode", (int)ScreenMode);
            PlayerPrefs.SetInt("v.vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("v.fps", FpsCapIndex);
            PlayerPrefs.SetInt("v.scale", RenderScaleIndex);
            PlayerPrefs.SetInt("v.msaa", MsaaIndex);
            PlayerPrefs.SetInt("v.shadows", ShadowsIndex);
            PlayerPrefs.SetInt("v.post", PostFx ? 1 : 0);

            PlayerPrefs.SetFloat("a.master", MasterVolume);
            PlayerPrefs.SetFloat("a.music", MusicVolume);
            PlayerPrefs.SetFloat("a.sfx", SfxVolume);
            PlayerPrefs.SetFloat("a.amb", AmbientVolume);
            PlayerPrefs.Save();
        }

        public static void ResetGeneral()
        {
            MouseSens = StickSens = 1f;
            InvertY = false;
            Fov = 68f;
            HeadBob = ShowHints = true;
        }

        public static void ResetVideo()
        {
            ResolutionIndex = -1;
            ScreenMode = FullScreenMode.FullScreenWindow;
            VSync = true;
            FpsCapIndex = 1;
            RenderScaleIndex = 4;
            MsaaIndex = 2;
            ShadowsIndex = 2;
            PostFx = true;
        }

        public static void ResetAudio()
        {
            MasterVolume = SfxVolume = 1f;
            MusicVolume = AmbientVolume = 0.8f;
        }

        // ------------------------------------------------------------------ aplicar

        public static void ApplyAll()
        {
            if (!loaded) Load();
            ApplyScreen();
            ApplyGraphics();
            ApplyAudio();
        }

        /// <summary>Resolução e modo de tela (no editor não faz nada — a janela do Game manda).</summary>
        public static void ApplyScreen()
        {
            if (Application.isEditor) return;
            int w, h;
            if (ResolutionIndex >= 0 && ResolutionIndex < Resolutions.Length)
            {
                w = Resolutions[ResolutionIndex].width;
                h = Resolutions[ResolutionIndex].height;
            }
            else
            {
                w = Screen.currentResolution.width;
                h = Screen.currentResolution.height;
            }
            if (ScreenMode == FullScreenMode.Windowed && ResolutionIndex < 0)
            {
                // janela "do monitor" não cabe com a barra de título: usa 80%
                w = Mathf.RoundToInt(w * 0.8f);
                h = Mathf.RoundToInt(h * 0.8f);
            }
            Screen.SetResolution(w, h, ScreenMode);
        }

        public static void ApplyGraphics()
        {
#if UNITY_EDITOR
            // no editor, mexer no QualitySettings em Play fica gravado no projeto: guarda e devolve ao sair
            if (originalPipeline == null)
            {
                originalPipeline = QualitySettings.renderPipeline;
                originalVSync = QualitySettings.vSyncCount;
                Application.quitting += RestoreEditor;
            }
#endif
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : (FpsCaps[FpsCapIndex] > 0 ? FpsCaps[FpsCapIndex] : -1);

            // trabalha numa cópia do asset do URP para não sujar o arquivo do projeto
            if (urpCopy == null && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset src)
            {
                urpCopy = UnityEngine.Object.Instantiate(src);
                urpCopy.name = src.name + " (opções)";
                QualitySettings.renderPipeline = urpCopy;
            }
            if (urpCopy != null)
            {
                urpCopy.renderScale = RenderScales[RenderScaleIndex];
                urpCopy.msaaSampleCount = MsaaLevels[MsaaIndex];
                if (ShadowsIndex > 0)
                {
                    urpCopy.shadowDistance = ShadowDistance[ShadowsIndex];
                    urpCopy.mainLightShadowmapResolution = ShadowRes[ShadowsIndex];
                    urpCopy.additionalLightsShadowmapResolution = ShadowRes[ShadowsIndex];
                }
            }

            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderShadows = ShadowsIndex > 0;
                data.renderPostProcessing = PostFx;
            }
            Changed?.Invoke();
        }

        public static float VolumeFor(SoundCategory c) => c switch
        {
            SoundCategory.Music => MusicVolume,
            SoundCategory.Ambient => AmbientVolume,
            _ => SfxVolume,
        };

        public static void ApplyAudio()
        {
            AudioListener.volume = MasterVolume;
            foreach (var v in SoundVolume.All) v.Apply();
            Changed?.Invoke();
        }

        /// <summary>Classifica as fontes de som da cena: roda = música, som da viela = ambiente, o resto = efeitos.</summary>
        static void TagSceneAudio()
        {
            foreach (var src in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                var cat = SoundCategory.Sfx;
                if (src.GetComponent<RodaDePagode>() != null) cat = SoundCategory.Music;
                else if (src.loop && src.playOnAwake) cat = SoundCategory.Ambient;
                SoundVolume.Tag(src, cat);
            }
        }

#if UNITY_EDITOR
        static void RestoreEditor()
        {
            Application.quitting -= RestoreEditor;
            QualitySettings.renderPipeline = originalPipeline;
            QualitySettings.vSyncCount = originalVSync;
            originalPipeline = null;
            if (urpCopy != null) UnityEngine.Object.Destroy(urpCopy);
            urpCopy = null;
        }
#endif
    }
}
