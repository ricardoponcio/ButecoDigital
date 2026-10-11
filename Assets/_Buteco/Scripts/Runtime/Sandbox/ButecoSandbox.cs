using Buteco.Pool;
using Buteco.UI;
using UnityEngine;

namespace Buteco.Sandbox
{
    public enum SandboxState { Title, Explore, Pool }

    /// <summary>
    /// O buteco como sandbox: tela de título, passeio em primeira pessoa (balcão, roda de pagode, cadeiras)
    /// e a mesa de sinuca, que entra no fluxo de partida que já existia.
    /// </summary>
    public class ButecoSandbox : MonoBehaviour
    {
        public ButecoPlayer player;
        public PoolGame pool;
        public PoolCamera poolCam;
        public ButecoUI ui;
        public RodaDePagode roda;
        [Tooltip("Onde o freguês aparece ao entrar (calçada, olhando pro bar).")]
        public Transform spawn;

        [Header("Câmera do menu da sinuca")]
        public Vector3 poolMenuPosition = new(1.3f, 1.75f, -1.35f);
        public Vector3 poolMenuLookAt = new(-1.0f, 0.75f, 0.25f);

        public SandboxState State { get; private set; } = SandboxState.Title;
        public bool Paused { get; private set; }

        Vector3 titlePos, titleLook;
        float titleSway;
        int pausedFrame = -1;

        void Start()
        {
            titlePos = poolCam.menuPosition;
            titleLook = poolCam.menuLookAt;
            titleSway = poolCam.menuSway;
            ShowTitle();
        }

        // ------------------------------------------------------------------ estados

        public void ShowTitle()
        {
            SetPaused(false);
            if (player.CurrentSeat != null) player.Stand();
            player.Deactivate();
            State = SandboxState.Title;
            poolCam.menuPosition = titlePos;
            poolCam.menuLookAt = titleLook;
            poolCam.menuSway = titleSway;
            poolCam.enabled = true;
            poolCam.SetMode(CamMode.Menu);
            ui.ShowTitle();
        }

        public void EnterBar()
        {
            State = SandboxState.Explore;
            poolCam.enabled = false;
            player.Activate(spawn.position, spawn.eulerAngles.y, 1.8f);
            ui.ShowExplore();
            Toast("Chegou no buteco! Chega mais…", 3.5f);
        }

        public void EnterPool()
        {
            if (State != SandboxState.Explore) return;
            if (player.CurrentSeat != null) player.Stand();
            player.Deactivate();
            State = SandboxState.Pool;
            poolCam.menuPosition = poolMenuPosition;
            poolCam.menuLookAt = poolMenuLookAt;
            poolCam.menuSway = 0.08f;
            poolCam.enabled = true;
            poolCam.SetMode(CamMode.Menu);
            pool.ToMenu();
        }

        public void ExitPool()
        {
            if (State != SandboxState.Pool) return;
            pool.Close();
            poolCam.enabled = false;
            State = SandboxState.Explore;
            player.Activate(0.7f);
            ui.ShowExplore();
        }

        public void SetPaused(bool p)
        {
            if (State != SandboxState.Explore) p = false;
            Paused = p;
            if (p) pausedFrame = Time.frameCount;
            Time.timeScale = p ? 0f : 1f;
            ui.ShowSandboxPause(p);
            ButecoPlayer.LockCursor(!p && State == SandboxState.Explore);
        }

        public void OnSeatChanged(Seat seat)
        {
            if (seat != null && seat.atRoda)
                Toast(roda != null && roda.Playing ? $"Senta aí que a roda tá tocando \"{roda.Current.title}\"" : "Senta aí, já já começa a próxima!");
        }

        public void Toast(string msg, float seconds = 3f) => ui.Toast(msg, seconds);

        void Update()
        {
            // no pause do passeio, o Esc/Start volta pro jogo (o menu de pausa da sinuca é do PoolGame)
            if (Paused && State == SandboxState.Explore && Time.frameCount != pausedFrame && !ui.OptionsBlocking)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                bool resume = kb != null && kb.escapeKey.wasPressedThisFrame;
                foreach (var g in UnityEngine.InputSystem.Gamepad.all)
                    resume |= g.startButton.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame;
                if (resume) SetPaused(false);
            }
        }
    }
}
