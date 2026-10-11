using System.Collections.Generic;
using Buteco.Players;
using Buteco.UI;
using UnityEngine;

namespace Buteco.Pool
{
    public enum Phase { Menu, Placing, Aiming, Simulating, GameOver }

    /// <summary>Orquestra a partida: turnos, mira, tacada, simulação e regras.</summary>
    public class PoolGame : MonoBehaviour
    {
        [Header("Referências")]
        public PoolTable table;
        [Tooltip("Índice = número da bola (0 = branca).")]
        public PoolBall[] balls = new PoolBall[16];
        public CueStick cue;
        public AimGuide guide;
        public PoolCamera cam;
        public AudioFx sfx;
        public ButecoUI ui;
        [Tooltip("Modo sandbox: a mesa é uma das atrações do buteco (null = jogo só de sinuca).")]
        public Buteco.Sandbox.ButecoSandbox sandbox;

        [Header("Tacada")]
        public float minShotSpeed = 0.25f;
        public float maxShotSpeed = 6.5f;
        public float chargeTime = 1.1f;
        public float aimSpeedDeg = 70f;
        public float fineAimSpeedDeg = 7f;
        public float mouseAimDegPerPixel = 0.08f;
        public float placeSpeed = 0.7f;
        public float maxShotSeconds = 25f;

        public PoolPhysics Phys { get; private set; }
        public PoolRules Rules { get; } = new();
        public MatchConfig Match { get; private set; }
        public Phase Phase { get; private set; } = Phase.Menu;
        public int CurrentPlayer { get; private set; }
        public int CurrentTeam => Match != null ? Match.players[CurrentPlayer].team : 0;
        public PlayerSlot CurrentSlot => Match?.players[CurrentPlayer];
        public float Power { get; private set; }
        public bool Paused { get; private set; }
        public bool PreferTopView { get; private set; }
        public bool LastUsedGamepad { get; private set; }
        public bool BallInHandKitchen => kitchenOnly;

        float aimAngle;
        bool waitRelease, charging;
        float chargeT, triggerPeak;
        bool isBreak, kitchenOnly;
        float simTime;
        int firstHit;
        int trayCount;
        int startPlayer;
        readonly List<int> pocketedNow = new();

        float R => Phys.R;

        void Awake()
        {
            Phys = table.CreatePhysics(16);
            Rack();
        }

        void Start()
        {
            cue.Visible = false;
            guide.Hide();
            if (sandbox != null) return; // o sandbox decide quando a mesa abre
            cam.SetMode(CamMode.Menu, true);
            if (ui != null) ui.ShowMain();
        }

        /// <summary>Sai da mesa (modo sandbox): arruma as bolas e some com taco/mira.</summary>
        public void Close()
        {
            Phase = Phase.Menu;
            Paused = false;
            Time.timeScale = 1f;
            Rack();
            cue.Visible = false;
            guide.Hide();
        }

        // ------------------------------------------------------------------ partida

        public void StartMatch(MatchConfig cfg)
        {
            if (Match != cfg) startPlayer = 0;
            Match = cfg;
            Rules.Reset();
            Rack();
            Paused = false;
            Time.timeScale = 1f;
            CurrentPlayer = startPlayer % cfg.players.Count;
            isBreak = true;
            ui?.ShowHud();
            ui?.Toast($"{CurrentSlot.name} dá a saída! Posicione a branca");
            BeginPlacing(true);
        }

        public void Rematch()
        {
            startPlayer++;
            StartMatch(Match);
        }

        public void RestartMatch() => StartMatch(Match);

        public void ToMenu()
        {
            Phase = Phase.Menu;
            Paused = false;
            Time.timeScale = 1f;
            Rack();
            cue.Visible = false;
            guide.Hide();
            cam.SetMode(CamMode.Menu);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ui?.ShowMain();
        }

        public void SetPaused(bool p)
        {
            if (Phase == Phase.Menu || Phase == Phase.GameOver) p = false;
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            ui?.ShowPause(p);
            if (p)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else waitRelease = true;
        }

        void Rack()
        {
            for (int i = 0; i < Phys.Count; i++)
            {
                Phys.onTable[i] = true;
                Phys.vel[i] = Vector2.zero;
            }
            Phys.events.Clear();
            trayCount = 0;

            var rng = new System.Random();
            var solids = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            var stripes = new List<int> { 9, 10, 11, 12, 13, 14, 15 };
            Shuffle(solids, rng);
            Shuffle(stripes, rng);
            // posições 0..14 do triângulo; 4 = centro (a 8); 10 e 14 = cantos de trás (uma de cada grupo)
            var slots = new int[15];
            slots[4] = 8;
            bool solidLeft = rng.Next(2) == 0;
            slots[10] = solidLeft ? solids[0] : stripes[0];
            slots[14] = solidLeft ? stripes[0] : solids[0];
            var rest = new List<int>();
            rest.AddRange(solids.GetRange(1, 6));
            rest.AddRange(stripes.GetRange(1, 6));
            Shuffle(rest, rng);
            int ri = 0;
            for (int k = 0; k < 15; k++)
                if (k != 4 && k != 10 && k != 14) slots[k] = rest[ri++];

            float dx = 2f * R * 0.8661f + 0.0005f;
            float dz = 2f * R + 0.0005f;
            var foot = table.FootSpot;
            int idx = 0;
            for (int row = 0; row < 5; row++)
                for (int j = 0; j <= row; j++)
                {
                    int n = slots[idx++];
                    Phys.pos[n] = new Vector2(foot.x + row * dx, (j - row * 0.5f) * dz);
                }
            Phys.pos[0] = table.HeadSpot;

            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] == null) continue;
                balls[i].transform.rotation = Random.rotation;
                balls[i].Place(table.ToWorld(Phys.pos[i]));
            }
        }

        static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ------------------------------------------------------------------ fases

        void BeginPlacing(bool kitchen)
        {
            Phase = Phase.Placing;
            kitchenOnly = kitchen;
            charging = false;
            Power = 0f;
            if (!Phys.onTable[0])
            {
                Phys.onTable[0] = true;
                Phys.vel[0] = Vector2.zero;
                Phys.pos[0] = table.HeadSpot;
            }
            if (kitchen && Phys.pos[0].x > table.HeadString) Phys.pos[0] = table.HeadSpot;
            Phys.pos[0] = FindFreeSpot(0, Phys.pos[0]);
            balls[0].Place(table.ToWorld(Phys.pos[0]));
            cue.Visible = false;
            cam.SetMode(CamMode.Top);
            waitRelease = true;
        }

        void BeginAiming()
        {
            Phase = Phase.Aiming;
            charging = false;
            chargeT = triggerPeak = 0f;
            Power = 0f;
            waitRelease = true;
            if (isBreak) aimAngle = 0f;
            else aimAngle = SuggestAngle();
            cam.SetMode(PreferTopView ? CamMode.Top : CamMode.Aim);
            UpdateAimVisuals();
        }

        /// <summary>Mira inicial: na bola mais próxima que o jogador pode acertar.</summary>
        float SuggestAngle()
        {
            var g = Rules.teamGroup[CurrentTeam];
            bool onEight = Rules.OnEight(CurrentTeam);
            float best = float.MaxValue;
            float ang = aimAngle;
            for (int n = 1; n < 16; n++)
            {
                if (!Phys.onTable[n]) continue;
                bool ok = onEight ? n == 8 : g == BallGroup.None ? n != 8 : PoolRules.GroupOf(n) == g;
                if (!ok) continue;
                var d = Phys.pos[n] - Phys.pos[0];
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    ang = Mathf.Atan2(d.y, d.x);
                }
            }
            return ang;
        }

        void Update()
        {
            if (Phase == Phase.Menu || Phase == Phase.GameOver) return;
            if (ui != null && ui.OptionsBlocking) return;

            var any = GameInput.Read(null);
            if (any.pause) SetPaused(!Paused);
            if (Paused) return;

            var inp = GameInput.Read(Match.DevicesFor(CurrentPlayer));
            if (inp.usedGamepad) LastUsedGamepad = true;
            else if (inp.usedKeyboard) LastUsedGamepad = false;

            if (inp.zoom != 0f) cam.Zoom(inp.zoom * Time.unscaledDeltaTime * 50f);

            switch (Phase)
            {
                case Phase.Placing: UpdatePlacing(inp); break;
                case Phase.Aiming: UpdateAiming(inp); break;
                case Phase.Simulating: UpdateSimulation(Mathf.Min(Time.deltaTime, 1f / 30f)); break;
            }
            UpdateCursor();
        }

        bool CurrentUsesKeyboard => Match.mode == ControlMode.Shared || CurrentSlot.UsesKeyboard;

        void UpdateCursor()
        {
            bool lockIt = Phase == Phase.Aiming && cam.Mode == CamMode.Aim && !Paused && CurrentUsesKeyboard;
            var want = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != want)
            {
                Cursor.lockState = want;
                Cursor.visible = !lockIt;
            }
        }

        void UpdatePlacing(FrameInput inp)
        {
            float dt = Time.deltaTime;
            var p = Phys.pos[0];
            if (inp.hasPointer && inp.pointerMoved && cam.cam != null &&
                table.RaycastSurface(cam.cam.ScreenPointToRay(inp.pointer), out var hit))
                p = hit;
            p += inp.move * placeSpeed * dt;

            float maxX = kitchenOnly ? table.HeadString : Phys.HalfL - R;
            p.x = Mathf.Clamp(p.x, -Phys.HalfL + R, maxX);
            p.y = Mathf.Clamp(p.y, -Phys.HalfW + R, Phys.HalfW - R);
            Phys.pos[0] = p;
            bool valid = !Phys.Overlaps(0, p);
            balls[0].Place(table.ToWorld(p));
            guide.ShowPlacement(table, Phys, p, valid);

            if (waitRelease)
            {
                if (!inp.chargeHeld) waitRelease = false;
                return;
            }
            if (inp.confirm && valid)
            {
                sfx.Chalk(table.ToWorld(p));
                BeginAiming();
            }
        }

        void UpdateAiming(FrameInput inp)
        {
            float dt = Time.deltaTime;
            if (inp.toggleView)
            {
                PreferTopView = !PreferTopView;
                cam.SetMode(PreferTopView ? CamMode.Top : CamMode.Aim);
            }

            if (!charging)
            {
                float speed = inp.fineModifier ? fineAimSpeedDeg * 2f : aimSpeedDeg;
                float rot = inp.aimRotate * speed + inp.aimFine * fineAimSpeedDeg;
                aimAngle -= rot * Mathf.Deg2Rad * dt;

                if (cam.Mode == CamMode.Aim && Cursor.lockState == CursorLockMode.Locked)
                    aimAngle -= inp.mouseDeltaX * mouseAimDegPerPixel * GameSettings.MouseSens * (inp.fineModifier ? 0.15f : 1f) * Mathf.Deg2Rad;
                else if (cam.Mode == CamMode.Top && inp.hasPointer && inp.pointerMoved && CurrentUsesKeyboard &&
                         table.RaycastSurface(cam.cam.ScreenPointToRay(inp.pointer), out var target))
                {
                    var d = target - Phys.pos[0];
                    if (d.sqrMagnitude > 1e-4f) aimAngle = Mathf.Atan2(d.y, d.x);
                }
            }

            if (waitRelease)
            {
                if (!inp.chargeHeld && inp.triggerPower < 0.05f) waitRelease = false;
            }
            else
            {
                if (inp.chargeHeld)
                {
                    charging = true;
                    chargeT += dt;
                    Power = Mathf.Clamp01(chargeT / chargeTime);
                }
                else if (inp.triggerPower > 0.05f)
                {
                    charging = true;
                    triggerPeak = Mathf.Max(triggerPeak, inp.triggerPower);
                    Power = triggerPeak;
                }
                else if (charging)
                {
                    if (Power > 0.02f)
                    {
                        Shoot();
                        return;
                    }
                    charging = false;
                    chargeT = triggerPeak = 0f;
                    Power = 0f;
                }

                if (inp.cancel && charging)
                {
                    charging = false;
                    chargeT = triggerPeak = 0f;
                    Power = 0f;
                    waitRelease = true;
                }
            }

            UpdateAimVisuals();
        }

        Vector2 AimDir2 => new(Mathf.Cos(aimAngle), Mathf.Sin(aimAngle));

        void UpdateAimVisuals()
        {
            var ballWorld = table.ToWorld(Phys.pos[0]);
            var dirWorld = table.DirToWorld(AimDir2);
            cue.Visible = true;
            cue.Pose(ballWorld, dirWorld, R, Power);
            cam.AimFocus = ballWorld;
            cam.AimDir = dirWorld;
            guide.Hide(); // sem auxílio de mira: no buteco é no olho
        }

        void Shoot()
        {
            float speed = Mathf.Lerp(minShotSpeed, maxShotSpeed, Mathf.Pow(Power, 1.6f));
            Phys.vel[0] = AimDir2 * speed;
            Phys.events.Clear();
            firstHit = -1;
            pocketedNow.Clear();
            simTime = 0f;
            var ballWorld = table.ToWorld(Phys.pos[0]);
            sfx.Cue(ballWorld, Power);
            cue.Strike(ballWorld, R);
            guide.Hide();
            Phase = Phase.Simulating;
            charging = false;
            chargeT = triggerPeak = 0f;
            Power = 0f;
            if (PreferTopView) cam.SetMode(CamMode.Top);
        }

        void UpdateSimulation(float dt)
        {
            simTime += dt;
            if (!PreferTopView && simTime > 0.4f && cam.Mode == CamMode.Aim) cam.SetMode(CamMode.Watch);

            bool moving = Phys.Simulate(dt);

            foreach (var e in Phys.events)
            {
                switch (e.type)
                {
                    case PhysEventType.BallBall:
                        if (firstHit < 0 && (e.a == 0 || e.b == 0)) firstHit = e.a == 0 ? e.b : e.a;
                        sfx.BallHit(table.ToWorld(Phys.pos[e.a]), e.speed);
                        break;
                    case PhysEventType.Cushion:
                        sfx.Cushion(table.ToWorld(Phys.pos[e.a]), e.speed);
                        break;
                    case PhysEventType.Pocket:
                    {
                        pocketedNow.Add(e.a);
                        var pocketWorld = table.ToWorld(Phys.pockets[e.b]);
                        sfx.Pocket(pocketWorld);
                        var dest = e.a == 0 ? pocketWorld + Vector3.down * 0.12f : table.TraySlot(trayCount++);
                        balls[e.a].SinkTo(pocketWorld, dest);
                        break;
                    }
                }
            }
            Phys.events.Clear();

            for (int i = 0; i < Phys.Count; i++)
            {
                if (!Phys.onTable[i]) continue;
                balls[i].Roll(table.ToWorld(Phys.pos[i]), table.DirToWorld(Phys.vel[i]), dt, R);
            }

            if (!moving || simTime > maxShotSeconds)
            {
                for (int i = 0; i < Phys.Count; i++) Phys.vel[i] = Vector2.zero;
                EndShot();
            }
        }

        void EndShot()
        {
            var report = new ShotReport
            {
                isBreak = isBreak,
                firstHit = firstHit,
                pocketed = new List<int>(pocketedNow),
            };
            var o = Rules.Evaluate(CurrentTeam, report);
            if (o.respotEight) RespotEight();
            // só deixa de ser saída quando a branca encosta em alguma bola
            if (firstHit >= 0) isBreak = false;

            if (o.winnerTeam >= 0)
            {
                Phase = Phase.GameOver;
                cue.Visible = false;
                guide.Hide();
                cam.SetMode(CamMode.Watch);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ui?.ShowGameOver(o.winnerTeam, o.message);
                return;
            }

            if (o.foul)
            {
                NextPlayer();
                ui?.Toast($"{o.message}\nVez de {CurrentSlot.name}");
                BeginPlacing(isBreak);
                return;
            }

            if (o.keepTurn)
            {
                ui?.Toast(o.message ?? "Boa! Joga de novo");
            }
            else
            {
                NextPlayer();
                ui?.Toast((o.message != null ? o.message + "\n" : "") + $"Vez de {CurrentSlot.name}");
            }
            BeginAiming();
        }

        // ------------------------------------------------------------------ API (IA, rede, testes)

        /// <summary>Executa a tacada da vez: ângulo em graus no plano da mesa (0 = +X) e força 0..1.</summary>
        public bool TryShoot(float angleDeg, float power)
        {
            if (Phase == Phase.Placing)
            {
                if (Phys.Overlaps(0, Phys.pos[0])) return false;
                BeginAiming();
            }
            if (Phase != Phase.Aiming) return false;
            aimAngle = angleDeg * Mathf.Deg2Rad;
            Power = Mathf.Clamp01(power);
            Shoot();
            return true;
        }

        /// <summary>Avança a simulação da tacada em andamento sem esperar os quadros.</summary>
        public void FastForward(float seconds, float step = 1f / 60f)
        {
            for (float t = 0f; t < seconds && Phase == Phase.Simulating; t += step)
                UpdateSimulation(step);
        }

        /// <summary>Posiciona a branca (bola na mão). Retorna false se a posição for inválida.</summary>
        public bool PlaceCueBall(Vector2 p)
        {
            if (Phase != Phase.Placing) return false;
            if (kitchenOnly && p.x > table.HeadString) return false;
            if (!Phys.InsidePlayArea(p, R) || Phys.Overlaps(0, p)) return false;
            Phys.pos[0] = p;
            balls[0].Place(table.ToWorld(p));
            return true;
        }

        void NextPlayer() => CurrentPlayer = (CurrentPlayer + 1) % Match.players.Count;

        void RespotEight()
        {
            Phys.onTable[8] = true;
            Phys.vel[8] = Vector2.zero;
            Phys.pos[8] = FindFreeSpot(8, table.FootSpot, Vector2.right);
            balls[8].Place(table.ToWorld(Phys.pos[8]));
            trayCount = Mathf.Max(0, trayCount - 1);
        }

        Vector2 FindFreeSpot(int self, Vector2 p, Vector2? preferDir = null)
        {
            if (!Phys.Overlaps(self, p) && Phys.InsidePlayArea(p, R)) return p;
            var dir = preferDir ?? Vector2.left;
            for (int k = 1; k < 200; k++)
            {
                float d = k * R * 0.5f;
                for (int a = 0; a < 8; a++)
                {
                    var q = p + (Vector2)(Quaternion.Euler(0, 0, a * 45f) * dir) * d;
                    if (Phys.InsidePlayArea(q, R) && !Phys.Overlaps(self, q)) return q;
                }
            }
            return p;
        }
    }
}
