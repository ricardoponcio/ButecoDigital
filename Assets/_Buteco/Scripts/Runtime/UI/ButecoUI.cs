using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buteco.Players;
using Buteco.Pool;
using Buteco.Sandbox;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Buteco.UI
{
    /// <summary>Toda a interface é montada por código (menu, entrada de jogadores, HUD, pausa, fim).</summary>
    public class ButecoUI : MonoBehaviour
    {
        public PoolGame game;
        [Tooltip("Modo sandbox (passeio pelo buteco). Null = só a sinuca.")]
        public ButecoSandbox sandbox;

        static readonly Color Yellow = new(1f, 0.80f, 0.15f);
        static readonly Color Cream = new(1f, 0.96f, 0.86f);
        static readonly Color Panel = new(0.10f, 0.06f, 0.04f, 0.80f);
        static readonly Color ButtonBg = new(0.75f, 0.13f, 0.12f, 0.95f);
        static readonly Color ButtonHi = new(0.95f, 0.70f, 0.10f, 1f);

        Font font;
        GameObject mainPanel, modePanel, joinPanel, hudPanel, pausePanel, overPanel;
        GameObject titlePanel, explorePanel, sbPausePanel;
        Button mainFirst, modeFirst, pauseFirst, overFirst, joinStart, titleFirst, sbPauseFirst;
        Text promptText, tabText, exploreHint, nowPlaying;
        Image crosshair;
        Text joinSlots, overTitle, overSub, toast, turnText, hintText;
        readonly Text[] teamText = new Text[2];
        readonly Image[] teamBg = new Image[2];
        RectTransform powerFill;
        GameObject powerRoot;
        float toastT;
        int pendingTeamSize = 1;

        // opções
        GameObject optionsPanel, optionsFrom;
        Button optionsFromFirst;
        readonly GameObject[] optPages = new GameObject[3];
        readonly Button[] optTabs = new Button[3];
        readonly List<Action> optRefresh = new();
        Text optNowPlaying;
        int optTab, optionsClosedFrame = -1;
        static readonly string[] TabNames = { "Geral", "Vídeo", "Música" };

        /// <summary>Menu de opções aberto (ou fechado neste quadro) — quem lê Esc/Start deve esperar.</summary>
        public bool OptionsBlocking => (optionsPanel != null && optionsPanel.activeSelf) || optionsClosedFrame == Time.frameCount;
        MatchConfig pending;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildEventSystem();
            BuildCanvas();
        }

        // ------------------------------------------------------------------ construção

        void BuildEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void BuildCanvas()
        {
            var cgo = new GameObject("Canvas UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = cgo.transform;

            BuildMain(root);
            BuildMode(root);
            BuildJoin(root);
            BuildHud(root);
            BuildPause(root);
            BuildOver(root);
            if (sandbox != null)
            {
                BuildTitle(root);
                BuildExplore(root);
                BuildSandboxPause(root);
            }
            BuildOptions(root);

            toast = Label(root, "", 40, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
            Place(toast.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(1300, 160));
            toast.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        }

        void BuildMain(Transform root)
        {
            mainPanel = Group(root, "Menu");
            var col = Column(mainPanel.transform, new Vector2(0.24f, 0.5f), new Vector2(720, 900), 22);
            var title = Label(col, sandbox != null ? "MESA DE\nSINUCA" : "BUTECO\nDIGITAL", 120, TextAnchor.MiddleLeft, Yellow, FontStyle.Bold);
            Layout(title.gameObject, 300);
            title.lineSpacing = 0.85f;
            Outline(title, 4);
            var sub = Label(col, "sinuca de esquina • bola 8 • lisas x listradas", 30, TextAnchor.MiddleLeft, Cream, FontStyle.Italic);
            Layout(sub.gameObject, 50);
            Outline(sub, 2);
            mainFirst = MakeButton(col, "1 contra 1", () => ShowMode(1));
            MakeButton(col, "2 contra 2 (duplas)", () => ShowMode(2));
            MakeButton(col, "Opções", () => ShowOptions(mainPanel, mainFirst));
            if (sandbox != null) MakeButton(col, "Voltar pro bar", () => sandbox.ExitPool());
            else MakeButton(col, "Sair", Quit);
            var foot = Label(col, "Mouse/teclado ou controle — vários controles funcionam ao mesmo tempo", 22, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.7f), FontStyle.Normal);
            Layout(foot.gameObject, 40);
            Outline(foot, 1);
        }

        void BuildMode(Transform root)
        {
            modePanel = Group(root, "Modo de controle");
            var col = Column(modePanel.transform, new Vector2(0.24f, 0.5f), new Vector2(760, 560), 22);
            var t = Label(col, "Como vocês vão jogar?", 54, TextAnchor.MiddleLeft, Yellow, FontStyle.Bold);
            Layout(t.gameObject, 90);
            Outline(t, 3);
            modeFirst = MakeButton(col, "Passando o controle (compartilhado)", () => StartShared());
            MakeButton(col, "Cada um com seu controle", () => ShowJoin());
            MakeButton(col, "Voltar", ShowMain);
            var hint = Label(col, "Compartilhado: qualquer teclado ou controle joga na vez de quem estiver na mesa.", 24, TextAnchor.MiddleLeft, Cream, FontStyle.Normal);
            Layout(hint.gameObject, 70);
            Outline(hint, 1);
        }

        void BuildJoin(Transform root)
        {
            joinPanel = Group(root, "Entrar na partida");
            var col = Column(joinPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(900, 720), 20, Panel);
            var t = Label(col, "Quem vai jogar?", 56, TextAnchor.MiddleCenter, Yellow, FontStyle.Bold);
            Layout(t.gameObject, 90);
            joinSlots = Label(col, "", 34, TextAnchor.MiddleLeft, Cream, FontStyle.Normal);
            joinSlots.supportRichText = true;
            Layout(joinSlots.gameObject, 280);
            var hint = Label(col, "Aperte <b>ESPAÇO</b> no teclado ou <b>A</b> no controle para entrar (de novo = mais um jogador no mesmo controle).\n<b>Backspace</b> / <b>B</b> libera a vaga.  <b>Enter</b> / <b>Start</b> começa.",
                24, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f), FontStyle.Normal);
            hint.supportRichText = true;
            Layout(hint.gameObject, 80);
            joinStart = MakeButton(col, "Começar", TryStartJoined);
            MakeButton(col, "Voltar", () => ShowMode(pendingTeamSize));
        }

        void BuildHud(Transform root)
        {
            hudPanel = Group(root, "HUD");
            for (int t = 0; t < 2; t++)
            {
                var bgGo = new GameObject($"Time {t}", typeof(RectTransform), typeof(Image));
                bgGo.transform.SetParent(hudPanel.transform, false);
                var rt = (RectTransform)bgGo.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(t == 0 ? 0f : 1f, 1f);
                rt.pivot = new Vector2(t == 0 ? 0f : 1f, 1f);
                rt.sizeDelta = new Vector2(470, 210);
                rt.anchoredPosition = new Vector2(t == 0 ? 24 : -24, -24);
                teamBg[t] = bgGo.GetComponent<Image>();
                teamBg[t].color = Panel;
                var txt = Label(bgGo.transform, "", 26, t == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight, Cream, FontStyle.Normal);
                txt.supportRichText = true;
                var trt = txt.rectTransform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(20, 14); trt.offsetMax = new Vector2(-20, -14);
                teamText[t] = txt;
            }

            turnText = Label(hudPanel.transform, "", 40, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
            turnText.supportRichText = true;
            Place(turnText.rectTransform, new Vector2(0.5f, 0.94f), new Vector2(900, 70));
            Outline(turnText, 3);

            powerRoot = new GameObject("Força", typeof(RectTransform), typeof(Image));
            powerRoot.transform.SetParent(hudPanel.transform, false);
            var prt = (RectTransform)powerRoot.transform;
            Place(prt, new Vector2(0.5f, 0.11f), new Vector2(640, 30));
            powerRoot.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
            var fill = new GameObject("Preenchimento", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(powerRoot.transform, false);
            powerFill = (RectTransform)fill.transform;
            powerFill.anchorMin = Vector2.zero; powerFill.anchorMax = new Vector2(0, 1);
            powerFill.offsetMin = new Vector2(3, 3); powerFill.offsetMax = new Vector2(-3, -3);
            fill.GetComponent<Image>().color = Yellow;
            var pl = Label(powerRoot.transform, "FORÇA", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            pl.rectTransform.anchorMin = Vector2.zero; pl.rectTransform.anchorMax = Vector2.one;
            pl.rectTransform.offsetMin = pl.rectTransform.offsetMax = Vector2.zero;
            Outline(pl, 1);

            hintText = Label(hudPanel.transform, "", 24, TextAnchor.MiddleCenter, Cream, FontStyle.Normal);
            Place(hintText.rectTransform, new Vector2(0.5f, 0.04f), new Vector2(1800, 60));
            Outline(hintText, 2);
        }

        void BuildPause(Transform root)
        {
            pausePanel = Group(root, "Pausa", new Color(0, 0, 0, 0.55f));
            var col = Column(pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(620, 580), 22, Panel);
            var t = Label(col, "Pausa pro cafezinho", 50, TextAnchor.MiddleCenter, Yellow, FontStyle.Bold);
            Layout(t.gameObject, 90);
            pauseFirst = MakeButton(col, "Continuar", () => game.SetPaused(false));
            MakeButton(col, "Reiniciar partida", () => { game.SetPaused(false); game.RestartMatch(); });
            MakeButton(col, "Opções", () => ShowOptions(pausePanel, pauseFirst));
            MakeButton(col, sandbox != null ? "Menu da sinuca" : "Menu principal", () => { game.SetPaused(false); game.ToMenu(); });
        }

        void BuildOver(Transform root)
        {
            overPanel = Group(root, "Fim de jogo", new Color(0, 0, 0, 0.35f));
            var col = Column(overPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(900, 500), 22, Panel);
            overTitle = Label(col, "", 64, TextAnchor.MiddleCenter, Yellow, FontStyle.Bold);
            Layout(overTitle.gameObject, 110);
            overSub = Label(col, "", 32, TextAnchor.MiddleCenter, Cream, FontStyle.Normal);
            Layout(overSub.gameObject, 70);
            overFirst = MakeButton(col, "Revanche!", () => { HideAll(); game.Rematch(); });
            MakeButton(col, sandbox != null ? "Menu da sinuca" : "Menu principal", () => game.ToMenu());
        }

        void BuildTitle(Transform root)
        {
            titlePanel = Group(root, "Título");
            var col = Column(titlePanel.transform, new Vector2(0.24f, 0.5f), new Vector2(760, 820), 22);
            var title = Label(col, "BUTECO\nDIGITAL", 120, TextAnchor.MiddleLeft, Yellow, FontStyle.Bold);
            Layout(title.gameObject, 300);
            title.lineSpacing = 0.85f;
            Outline(title, 4);
            var sub = Label(col, "sinuca, gelada trincando e roda de pagode na esquina", 30, TextAnchor.MiddleLeft, Cream, FontStyle.Italic);
            Layout(sub.gameObject, 50);
            Outline(sub, 2);
            titleFirst = MakeButton(col, "Entrar no buteco", () => sandbox.EnterBar());
            MakeButton(col, "Opções", () => ShowOptions(titlePanel, titleFirst));
            MakeButton(col, "Sair", Quit);
            var foot = Label(col, "WASD + mouse ou controle • E / A para interagir", 22, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.7f), FontStyle.Normal);
            Layout(foot.gameObject, 40);
            Outline(foot, 1);
        }

        void BuildExplore(Transform root)
        {
            explorePanel = Group(root, "Passeio");

            var ch = new GameObject("Mira", typeof(RectTransform), typeof(Image));
            ch.transform.SetParent(explorePanel.transform, false);
            Place((RectTransform)ch.transform, new Vector2(0.5f, 0.5f), new Vector2(8, 8));
            crosshair = ch.GetComponent<Image>();
            crosshair.color = new Color(1, 1, 1, 0.75f);
            crosshair.raycastTarget = false;

            promptText = Label(explorePanel.transform, "", 34, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
            Place(promptText.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(1200, 60));
            Outline(promptText, 2);

            tabText = Label(explorePanel.transform, "", 26, TextAnchor.UpperLeft, Cream, FontStyle.Normal);
            var trt = tabText.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.sizeDelta = new Vector2(700, 200);
            trt.anchoredPosition = new Vector2(32, -28);
            Outline(tabText, 2);

            nowPlaying = Label(explorePanel.transform, "", 24, TextAnchor.UpperRight, new Color(1f, 0.9f, 0.6f), FontStyle.Italic);
            var nrt = nowPlaying.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(1f, 1f);
            nrt.sizeDelta = new Vector2(800, 80);
            nrt.anchoredPosition = new Vector2(-32, -28);
            Outline(nowPlaying, 2);

            exploreHint = Label(explorePanel.transform, "", 24, TextAnchor.MiddleCenter, Cream, FontStyle.Normal);
            Place(exploreHint.rectTransform, new Vector2(0.5f, 0.05f), new Vector2(1800, 80));
            Outline(exploreHint, 2);
        }

        void BuildSandboxPause(Transform root)
        {
            sbPausePanel = Group(root, "Pausa do passeio", new Color(0, 0, 0, 0.55f));
            var col = Column(sbPausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(640, 580), 22, Panel);
            var t = Label(col, "Pausa pro cafezinho", 50, TextAnchor.MiddleCenter, Yellow, FontStyle.Bold);
            Layout(t.gameObject, 90);
            sbPauseFirst = MakeButton(col, "Continuar", () => sandbox.SetPaused(false));
            MakeButton(col, "Opções", () => ShowOptions(sbPausePanel, sbPauseFirst));
            MakeButton(col, "Voltar pra rua (título)", () => sandbox.ShowTitle());
            MakeButton(col, "Sair do jogo", Quit);
        }

        // ------------------------------------------------------------------ telas

        void HideAll()
        {
            mainPanel.SetActive(false);
            modePanel.SetActive(false);
            joinPanel.SetActive(false);
            hudPanel.SetActive(false);
            pausePanel.SetActive(false);
            overPanel.SetActive(false);
            if (titlePanel) titlePanel.SetActive(false);
            if (explorePanel) explorePanel.SetActive(false);
            if (sbPausePanel) sbPausePanel.SetActive(false);
            if (optionsPanel) optionsPanel.SetActive(false);
        }

        public void ShowTitle()
        {
            HideAll();
            titlePanel.SetActive(true);
            Select(titleFirst);
        }

        public void ShowExplore()
        {
            HideAll();
            explorePanel.SetActive(true);
            Select(null);
        }

        public void ShowSandboxPause(bool on)
        {
            if (sbPausePanel == null) return;
            if (!on && optionsFrom == sbPausePanel) CloseOptions(false);
            sbPausePanel.SetActive(on);
            Select(on ? sbPauseFirst : null);
        }

        void Select(Button b)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(b != null ? b.gameObject : null);
        }

        public void ShowMain()
        {
            HideAll();
            mainPanel.SetActive(true);
            Select(mainFirst);
        }

        void ShowMode(int teamSize)
        {
            pendingTeamSize = teamSize;
            HideAll();
            modePanel.SetActive(true);
            Select(modeFirst);
        }

        void StartShared()
        {
            HideAll();
            game.StartMatch(MatchConfig.Create(pendingTeamSize, ControlMode.Shared));
        }

        void ShowJoin()
        {
            pending = MatchConfig.Create(pendingTeamSize, ControlMode.PerDevice);
            HideAll();
            joinPanel.SetActive(true);
            Select(null);
            RefreshJoin();
        }

        public void ShowHud()
        {
            HideAll();
            hudPanel.SetActive(true);
            Select(null);
        }

        public void ShowPause(bool on)
        {
            if (!on && optionsFrom == pausePanel) CloseOptions(false);
            pausePanel.SetActive(on);
            if (on) Select(pauseFirst);
            else Select(null);
        }

        public void ShowGameOver(int winnerTeam, string message)
        {
            overPanel.SetActive(true);
            var cfg = game.Match;
            string name = cfg.TeamName(winnerTeam);
            overTitle.text = $"{name} venceu!";
            overTitle.color = MatchConfig.TeamColors[winnerTeam];
            overSub.text = message + "\nQuem perdeu paga a próxima!";
            Select(overFirst);
        }

        public void Toast(string msg, float seconds = 3f)
        {
            if (string.IsNullOrEmpty(msg)) return;
            toast.text = msg;
            toastT = seconds;
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ entrar com dispositivo

        void TryStartJoined()
        {
            if (pending == null || !pending.AllJoined()) return;
            HideAll();
            game.StartMatch(pending);
        }

        void UpdateJoin()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                ShowMode(pendingTeamSize);
                return;
            }

            // cada aperto ocupa a próxima vaga livre — o mesmo controle pode cuidar de mais de um jogador
            var dev = GameInput.JoinPressed();
            if (dev != null)
            {
                var free = pending.players.FirstOrDefault(p => p.devices.Count == 0);
                free?.devices.Add(dev);
            }

            var leave = GameInput.LeavePressed();
            if (leave != null)
            {
                var owner = pending.players.LastOrDefault(p => p.devices.Contains(leave));
                if (owner != null) owner.devices.Clear();
                else if (pending.players.All(p => p.devices.Count == 0))
                {
                    ShowMode(pendingTeamSize);
                    return;
                }
            }

            var start = GameInput.StartPressed();
            if (start != null && pending.AllJoined() && pending.players.Any(p => p.devices.Contains(start)))
            {
                TryStartJoined();
                return;
            }

            // controle desconectado
            foreach (var p in pending.players)
                p.devices.RemoveAll(d => d == null || !d.added);

            RefreshJoin();
        }

        void RefreshJoin()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < pending.players.Count; i++)
            {
                var p = pending.players[i];
                string teamName = pending.teamSize == 1 ? MatchConfig.TeamNames1v1[p.team] : MatchConfig.TeamNames2v2[p.team];
                string hex = ColorUtility.ToHtmlStringRGB(MatchConfig.TeamColors[p.team]);
                string dev = p.devices.Count > 0 ? $"<b>{GameInput.DeviceLabel(p.devices[0])}</b>" : "<i>aguardando…</i>";
                sb.AppendLine($"<color=#{hex}>■</color>  {p.name}  <color=#{hex}>({teamName})</color>  —  {dev}");
            }
            joinSlots.text = sb.ToString();
            joinStart.interactable = pending.AllJoined();
        }

        // ------------------------------------------------------------------ opções

        void BuildOptions(Transform root)
        {
            optionsPanel = Group(root, "Opções", new Color(0, 0, 0, 0.6f));
            var col = Column(optionsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(1240, 940), 16, Panel);
            var t = Label(col, "Opções", 56, TextAnchor.MiddleCenter, Yellow, FontStyle.Bold);
            Layout(t.gameObject, 76);
            Outline(t, 3);

            var tabs = HRow(col, 70, 16);
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                optTabs[i] = MakeButton(tabs, TabNames[i], () => SetOptionsTab(k));
                Layout(optTabs[i].gameObject, 66);
            }

            var pagesGo = new GameObject("Páginas", typeof(RectTransform));
            pagesGo.transform.SetParent(col, false);
            Layout(pagesGo, 590);
            for (int i = 0; i < 3; i++)
            {
                var pg = new GameObject(TabNames[i], typeof(RectTransform), typeof(VerticalLayoutGroup));
                pg.transform.SetParent(pagesGo.transform, false);
                var rt = (RectTransform)pg.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var lg = pg.GetComponent<VerticalLayoutGroup>();
                lg.spacing = 10;
                lg.childAlignment = TextAnchor.UpperCenter;
                lg.childControlHeight = lg.childControlWidth = true;
                lg.childForceExpandHeight = false;
                lg.childForceExpandWidth = true;
                lg.padding = new RectOffset(10, 10, 14, 0);
                optPages[i] = pg;
            }

            // ---- geral
            var g = optPages[0].transform;
            SliderRow(g, "Sensibilidade do mouse", 0.2f, 3f, () => GameSettings.MouseSens, v => GameSettings.MouseSens = v, v => $"{v:0.0}x");
            SliderRow(g, "Sensibilidade do analógico", 0.3f, 2.5f, () => GameSettings.StickSens, v => GameSettings.StickSens = v, v => $"{v:0.0}x");
            ToggleRow(g, "Inverter olhar (eixo Y)", () => GameSettings.InvertY, v => GameSettings.InvertY = v);
            SliderRow(g, "Campo de visão (passeio)", 55f, 100f, () => GameSettings.Fov, v => GameSettings.Fov = Mathf.Round(v), v => $"{v:0}°");
            ToggleRow(g, "Balanço da cabeça", () => GameSettings.HeadBob, v => GameSettings.HeadBob = v);
            ToggleRow(g, "Dicas de controle na tela", () => GameSettings.ShowHints, v => GameSettings.ShowHints = v);

            // ---- vídeo
            var v2 = optPages[1].transform;
            ChoiceRow(v2, "Resolução", ResolutionLabel, d =>
            {
                int n = GameSettings.Resolutions.Length;
                GameSettings.ResolutionIndex = Wrap(GameSettings.ResolutionIndex + 1 + d, n + 1) - 1;
                GameSettings.ApplyScreen();
            });
            ChoiceRow(v2, "Modo de tela", () => GameSettings.ScreenModeNames[Math.Max(0, Array.IndexOf(GameSettings.ScreenModes, GameSettings.ScreenMode))], d =>
            {
                int i = Math.Max(0, Array.IndexOf(GameSettings.ScreenModes, GameSettings.ScreenMode));
                GameSettings.ScreenMode = GameSettings.ScreenModes[Wrap(i + d, GameSettings.ScreenModes.Length)];
                GameSettings.ApplyScreen();
            });
            ToggleRow(v2, "VSync", () => GameSettings.VSync, v => GameSettings.VSync = v, GameSettings.ApplyGraphics);
            ChoiceRow(v2, "Limite de FPS", () =>
            {
                if (GameSettings.VSync) return "VSync manda";
                int f = GameSettings.FpsCaps[GameSettings.FpsCapIndex];
                return f > 0 ? f.ToString() : "Sem limite";
            }, d =>
            {
                GameSettings.FpsCapIndex = Wrap(GameSettings.FpsCapIndex + d, GameSettings.FpsCaps.Length);
                GameSettings.ApplyGraphics();
            });
            ChoiceRow(v2, "Escala de renderização", () => $"{GameSettings.RenderScales[GameSettings.RenderScaleIndex] * 100f:0}%", d =>
            {
                GameSettings.RenderScaleIndex = Mathf.Clamp(GameSettings.RenderScaleIndex + d, 0, GameSettings.RenderScales.Length - 1);
                GameSettings.ApplyGraphics();
            });
            ChoiceRow(v2, "Antisserrilhado (MSAA)", () =>
            {
                int m = GameSettings.MsaaLevels[GameSettings.MsaaIndex];
                return m > 1 ? $"{m}x" : "Desligado";
            }, d =>
            {
                GameSettings.MsaaIndex = Mathf.Clamp(GameSettings.MsaaIndex + d, 0, GameSettings.MsaaLevels.Length - 1);
                GameSettings.ApplyGraphics();
            });
            ChoiceRow(v2, "Sombras", () => GameSettings.ShadowNames[GameSettings.ShadowsIndex], d =>
            {
                GameSettings.ShadowsIndex = Mathf.Clamp(GameSettings.ShadowsIndex + d, 0, GameSettings.ShadowNames.Length - 1);
                GameSettings.ApplyGraphics();
            });
            ToggleRow(v2, "Pós-processamento (cor, brilho)", () => GameSettings.PostFx, v => GameSettings.PostFx = v, GameSettings.ApplyGraphics);

            // ---- música
            var m2 = optPages[2].transform;
            SliderRow(m2, "Volume geral", 0f, 1f, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent, GameSettings.ApplyAudio);
            SliderRow(m2, "Música (roda de pagode)", 0f, 1f, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent, GameSettings.ApplyAudio);
            SliderRow(m2, "Efeitos (sinuca, copo, passos)", 0f, 1f, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Percent, GameSettings.ApplyAudio);
            SliderRow(m2, "Ambiente (movimento da viela)", 0f, 1f, () => GameSettings.AmbientVolume, v => GameSettings.AmbientVolume = v, Percent, GameSettings.ApplyAudio);
            if (sandbox != null && sandbox.roda != null)
            {
                var sp = new GameObject("Respiro", typeof(RectTransform));
                sp.transform.SetParent(m2, false);
                Layout(sp, 20);
                optNowPlaying = Label(m2, "", 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f), FontStyle.Italic);
                Layout(optNowPlaying.gameObject, 50);
                var skipRow = HRow(m2, 70, 0);
                skipRow.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(300, 300, 0, 0);
                var skip = MakeButton(skipRow, "Pedir a próxima música", () => sandbox.roda.Skip());
                Vertical(skip);
            }

            // ---- rodapé
            var foot = HRow(col, 78, 24);
            MakeButton(foot, "Restaurar padrão", ResetOptionsTab);
            MakeButton(foot, "Voltar", () => CloseOptions(true));
            var hint = Label(col, "Setas / analógico: escolher e ajustar • LB/RB ou Q/E: trocar aba • Esc / B: voltar", 22, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f), FontStyle.Normal);
            Layout(hint.gameObject, 32);
        }

        static string Percent(float v) => $"{v * 100f:0}%";
        static int Wrap(int i, int n) => n <= 0 ? 0 : ((i % n) + n) % n;

        static string ResolutionLabel()
        {
            int i = GameSettings.ResolutionIndex;
            if (i < 0 || i >= GameSettings.Resolutions.Length)
                return $"Do monitor ({Screen.currentResolution.width}x{Screen.currentResolution.height})";
            var r = GameSettings.Resolutions[i];
            return $"{r.width}x{r.height}";
        }

        public void ShowOptions(GameObject from, Button fromFirst)
        {
            optionsFrom = from;
            optionsFromFirst = fromFirst;
            if (from != null) from.SetActive(false);
            optionsPanel.SetActive(true);
            RefreshOptions();
            SetOptionsTab(optTab);
        }

        /// <summary>Fecha as opções; com <paramref name="reopen"/>, volta pra tela de onde veio.</summary>
        void CloseOptions(bool reopen)
        {
            if (!optionsPanel.activeSelf) return;
            optionsPanel.SetActive(false);
            optionsClosedFrame = Time.frameCount;
            GameSettings.Save();
            if (reopen && optionsFrom != null)
            {
                optionsFrom.SetActive(true);
                Select(optionsFromFirst);
            }
            optionsFrom = null;
        }

        void SetOptionsTab(int i)
        {
            optTab = i;
            for (int k = 0; k < 3; k++)
            {
                optPages[k].SetActive(k == i);
                optTabs[k].GetComponent<Image>().color = k == i ? Color.white : new Color(0.55f, 0.5f, 0.5f, 1f);
            }
            Select(optTabs[i]);
        }

        void ResetOptionsTab()
        {
            if (optTab == 0) GameSettings.ResetGeneral();
            else if (optTab == 1) GameSettings.ResetVideo();
            else GameSettings.ResetAudio();
            GameSettings.Save();
            GameSettings.ApplyAll();
            RefreshOptions();
        }

        void RefreshOptions()
        {
            foreach (var r in optRefresh) r();
        }

        void UpdateOptions()
        {
            var kb = Keyboard.current;
            bool back = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame);
            int tab = 0;
            if (kb != null && kb.qKey.wasPressedThisFrame) tab--;
            if (kb != null && kb.eKey.wasPressedThisFrame) tab++;
            foreach (var g in Gamepad.all)
            {
                back |= g.buttonEast.wasPressedThisFrame || g.startButton.wasPressedThisFrame;
                if (g.leftShoulder.wasPressedThisFrame) tab--;
                if (g.rightShoulder.wasPressedThisFrame) tab++;
            }
            if (back)
            {
                CloseOptions(true);
                return;
            }
            if (tab != 0) SetOptionsTab(Wrap(optTab + tab, 3));
            if (optNowPlaying != null)
            {
                var roda = sandbox.roda;
                optNowPlaying.text = roda.Playing ? $"Tocando agora: \"{roda.Current.title}\"" : "A roda tá dando um respiro…";
            }
        }

        Transform HRow(Transform parent, float height, float spacing)
        {
            var go = new GameObject("Linha", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var lg = go.GetComponent<HorizontalLayoutGroup>();
            lg.spacing = spacing;
            lg.childAlignment = TextAnchor.MiddleCenter;
            lg.childControlHeight = lg.childControlWidth = true;
            lg.childForceExpandHeight = true;
            lg.childForceExpandWidth = true;
            Layout(go, height);
            go.GetComponent<LayoutElement>().flexibleHeight = 0f;
            return go.transform;
        }

        /// <summary>Linha de opção: rótulo à esquerda, controle à direita.</summary>
        Transform OptionRow(Transform page, string label)
        {
            var row = HRow(page, 60, 20);
            var lg = row.GetComponent<HorizontalLayoutGroup>();
            lg.childForceExpandWidth = false;
            var l = Label(row, label, 30, TextAnchor.MiddleLeft, Cream, FontStyle.Normal);
            var le = l.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 500;
            le.minWidth = 500;
            Outline(l, 1);
            return row;
        }

        static void Vertical(Selectable s)
        {
            var nav = s.navigation;
            nav.mode = Navigation.Mode.Vertical;
            s.navigation = nav;
        }

        void ChoiceRow(Transform page, string label, Func<string> value, Action<int> step)
        {
            var row = OptionRow(page, label);
            Button b;
            void Step(int d)
            {
                step(d);
                GameSettings.Save();
                RefreshOptions();
            }
            b = MakeButton(row, "", () => Step(1));
            Layout(b.gameObject, 52);
            b.GetComponent<LayoutElement>().flexibleWidth = 1;
            Vertical(b);
            b.gameObject.AddComponent<OptionStepper>().step = Step;
            var txt = b.GetComponentInChildren<Text>();
            txt.fontSize = 30;
            optRefresh.Add(() => txt.text = $"‹   {value()}   ›");
        }

        void ToggleRow(Transform page, string label, Func<bool> get, Action<bool> set, Action apply = null)
        {
            ChoiceRow(page, label, () => get() ? "Sim" : "Não", _ =>
            {
                set(!get());
                apply?.Invoke();
            });
        }

        void SliderRow(Transform page, string label, float min, float max, Func<float> get, Action<float> set,
            Func<float, string> format, Action apply = null)
        {
            var row = OptionRow(page, label);
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(row, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1;

            var bg = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = new Vector2(0, 0.3f); brt.anchorMax = new Vector2(1, 0.7f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.55f);

            var area = new GameObject("Área", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = new Vector2(0, 0.3f); art.anchorMax = new Vector2(1, 0.7f);
            art.offsetMin = art.offsetMax = Vector2.zero;
            var fill = new GameObject("Preenchimento", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(area.transform, false);
            ((RectTransform)fill.transform).sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.75f, 0.13f, 0.12f, 1f);

            var hArea = new GameObject("Área do pino", typeof(RectTransform));
            hArea.transform.SetParent(go.transform, false);
            var hart = (RectTransform)hArea.transform;
            hart.anchorMin = Vector2.zero; hart.anchorMax = Vector2.one;
            hart.offsetMin = new Vector2(14, 6); hart.offsetMax = new Vector2(-14, -6);
            var handle = new GameObject("Pino", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(hArea.transform, false);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(28, 0);
            var himg = handle.GetComponent<Image>();
            himg.color = Color.white;

            var s = go.GetComponent<Slider>();
            s.fillRect = (RectTransform)fill.transform;
            s.handleRect = (RectTransform)handle.transform;
            s.targetGraphic = himg;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = min;
            s.maxValue = max;
            var cb = s.colors;
            cb.normalColor = Cream;
            cb.highlightedColor = ButtonHi;
            cb.selectedColor = ButtonHi;
            cb.pressedColor = new Color(1f, 0.9f, 0.5f);
            cb.fadeDuration = 0.08f;
            s.colors = cb;
            Vertical(s);

            var val = Label(row, "", 30, TextAnchor.MiddleRight, Yellow, FontStyle.Bold);
            var vle = val.gameObject.AddComponent<LayoutElement>();
            vle.preferredWidth = vle.minWidth = 120;
            Outline(val, 1);

            bool syncing = false;
            s.onValueChanged.AddListener(v =>
            {
                if (syncing) return;
                set(v);
                apply?.Invoke();
                val.text = format(get());
            });
            optRefresh.Add(() =>
            {
                syncing = true;
                s.SetValueWithoutNotify(get());
                syncing = false;
                val.text = format(get());
            });
        }

        // ------------------------------------------------------------------ HUD

        void Update()
        {
            if (toastT > 0f)
            {
                toastT -= Time.unscaledDeltaTime;
                var c = toast.color;
                c.a = Mathf.Clamp01(toastT / 0.4f);
                toast.color = c;
            }
            else if (toast.text.Length > 0) toast.text = "";

            if (optionsPanel.activeSelf) UpdateOptions();
            if (joinPanel.activeSelf) UpdateJoin();
            if (hudPanel.activeSelf && game.Match != null) RefreshHud();
            if (explorePanel != null && explorePanel.activeSelf) RefreshExplore();
        }

        void RefreshHud()
        {
            var cfg = game.Match;
            var rules = game.Rules;
            for (int t = 0; t < 2; t++)
            {
                bool active = game.CurrentTeam == t;
                var sb = new StringBuilder();
                string hex = ColorUtility.ToHtmlStringRGB(MatchConfig.TeamColors[t]);
                sb.Append($"<size=34><b><color=#{hex}>{cfg.TeamName(t)}</color></b></size>\n");
                var g = rules.teamGroup[t];
                if (g == BallGroup.None) sb.Append("Mesa aberta\n");
                else if (rules.OnEight(t)) sb.Append($"{PoolRules.GroupName(g)} • <b>agora é a 8!</b>\n");
                else sb.Append($"{PoolRules.GroupName(g)} • faltam {rules.Remaining(g)}\n");
                if (g != BallGroup.None)
                    sb.Append("<size=22>" + string.Join("  ", rules.RemainingBalls(g)) + "</size>\n");
                if (cfg.teamSize > 1 || cfg.mode == ControlMode.PerDevice)
                {
                    for (int i = 0; i < cfg.players.Count; i++)
                    {
                        var p = cfg.players[i];
                        if (p.team != t) continue;
                        bool cur = i == game.CurrentPlayer;
                        string dev = cfg.mode == ControlMode.PerDevice && p.devices.Count > 0 ? $" <size=20>({GameInput.DeviceLabel(p.devices[0])})</size>" : "";
                        sb.Append(cur ? $"<b>» {p.name}</b>{dev}\n" : $"   {p.name}{dev}\n");
                    }
                }
                teamText[t].text = sb.ToString();
                teamBg[t].color = active ? new Color(0.25f, 0.14f, 0.05f, 0.92f) : Panel;
                teamBg[t].transform.localScale = Vector3.one * (active ? 1.03f : 1f);
            }

            var slot = game.CurrentSlot;
            string thex = ColorUtility.ToHtmlStringRGB(MatchConfig.TeamColors[game.CurrentTeam]);
            turnText.text = game.Phase == Phase.Simulating || game.Phase == Phase.GameOver ? "" : $"Vez de <color=#{thex}>{slot.name}</color>";

            bool aiming = game.Phase == Phase.Aiming;
            powerRoot.SetActive(aiming);
            if (aiming) powerFill.anchorMax = new Vector2(Mathf.Max(0.001f, game.Power), 1f);

            hintText.text = GameSettings.ShowHints ? Hint() : "";
        }

        void RefreshExplore()
        {
            var p = sandbox.player;
            bool pad = p.LastUsedGamepad;
            string key = pad ? "A" : "E";
            promptText.text = p.FocusPrompt != null ? $"[{key}]  {p.FocusPrompt}" : "";
            crosshair.color = p.FocusPrompt != null ? Yellow : new Color(1, 1, 1, 0.6f);
            crosshair.rectTransform.sizeDelta = Vector2.one * (p.FocusPrompt != null ? 12f : 7f);

            if (p.Geladas + p.Coxinhas > 0)
            {
                var sb = new StringBuilder("<b>Conta</b>\n");
                if (p.Geladas > 0) sb.Append($"{p.Geladas} gelada{(p.Geladas > 1 ? "s" : "")}\n");
                if (p.Coxinhas > 0) sb.Append($"{p.Coxinhas} coxinha{(p.Coxinhas > 1 ? "s" : "")}\n");
                sb.Append($"<color=#FFCC26>R$ {p.Conta:0},00</color>");
                tabText.text = sb.ToString();
            }
            else tabText.text = "";

            var roda = sandbox.roda;
            if (roda != null)
                nowPlaying.text = roda.Playing ? $"Na roda: \"{roda.Current.title}\"" : "A roda tá dando um respiro…";

            var lines = new StringBuilder();
            string held = p.hands != null ? p.hands.Hint(p) : null;
            if (held != null) lines.Append(held);
            if (p.CurrentSeat != null)
            {
                if (lines.Length > 0) lines.Append("   •   ");
                lines.Append(pad ? "B: levantar" : "Q / botão dir.: levantar");
            }
            else if (held == null)
            {
                lines.Append(pad
                    ? "Analógico esq.: andar (L3 corre) • dir.: olhar • A: interagir • Start: pausa"
                    : "WASD: andar (Shift corre) • Mouse: olhar • E: interagir • Esc: pausa");
            }
            exploreHint.text = GameSettings.ShowHints || p.CurrentSeat != null ? lines.ToString() : "";
        }

        string Hint()
        {
            bool pad = game.LastUsedGamepad;
            switch (game.Phase)
            {
                case Phase.Placing:
                    string where = game.BallInHandKitchen ? "atrás da linha de saída" : "em qualquer lugar";
                    return pad
                        ? $"Bola na mão ({where}) — Analógico: mover • A: confirmar"
                        : $"Bola na mão ({where}) — Mouse ou WASD: mover • Clique/Espaço: confirmar";
                case Phase.Aiming:
                    return pad
                        ? "Analógico esq.: mirar • dir.: ajuste fino • Segure A (ou aperte RT) e solte: tacada • B: cancela • Y: vista • LB/RB: zoom"
                        : "Mouse ou A/D: mirar (Shift = fino) • Segure clique/Espaço e solte: tacada • Botão dir.: cancela • Tab: vista • Roda/Q/E: zoom";
                default:
                    return "";
            }
        }

        // ------------------------------------------------------------------ helpers de UI

        GameObject Group(Transform parent, string name, Color? dim = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (dim.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = dim.Value;
            }
            go.SetActive(false);
            return go;
        }

        Transform Column(Transform parent, Vector2 anchor, Vector2 size, float spacing, Color? bg = null)
        {
            var go = new GameObject("Coluna", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, size);
            var lg = go.GetComponent<VerticalLayoutGroup>();
            lg.spacing = spacing;
            lg.childAlignment = TextAnchor.MiddleCenter;
            lg.childControlHeight = true;
            lg.childControlWidth = true;
            lg.childForceExpandHeight = false;
            lg.childForceExpandWidth = true;
            lg.padding = new RectOffset(36, 36, 30, 30);
            if (bg.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = bg.Value;
            }
            return go.transform;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        static void Layout(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        static void Outline(Text t, float size)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.1f, 0.03f, 0.02f, 0.9f);
            o.effectDistance = new Vector2(size, -size);
        }

        Text Label(Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Button MakeButton(Transform parent, string text, UnityAction onClick)
        {
            var go = new GameObject($"Botão {text}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            var b = go.GetComponent<Button>();
            var cb = b.colors;
            cb.normalColor = ButtonBg;
            cb.highlightedColor = ButtonHi;
            cb.selectedColor = ButtonHi;
            cb.pressedColor = new Color(1f, 0.9f, 0.5f);
            cb.disabledColor = new Color(0.3f, 0.25f, 0.22f, 0.8f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.onClick.AddListener(onClick);
            Layout(go, 78);
            var t = Label(go.transform, text, 36, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Outline(t, 2);
            return b;
        }
    }
}
