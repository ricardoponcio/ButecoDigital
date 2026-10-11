using Buteco.Players;
using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>Freguês em primeira pessoa: anda, olha, senta e interage com o buteco.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class ButecoPlayer : MonoBehaviour
    {
        public ButecoSandbox sandbox;
        public Camera cam;
        public PlayerHands hands;
        public AudioSource steps;

        [Header("Movimento")]
        public float eyeHeight = 1.62f;
        public float walkSpeed = 1.7f;
        public float runSpeed = 3.4f;
        public float gravity = 14f;
        public float lookPitchLimit = 80f;
        public float seatYawLimit = 115f;

        [Header("Interação")]
        public float rayLength = 3f;

        public bool Active { get; private set; }
        public Seat CurrentSeat { get; private set; }
        public Interactable Focus { get; private set; }
        public string FocusPrompt { get; private set; }
        public bool LastUsedGamepad { get; private set; }
        public float Yaw => yaw;

        // a conta do freguês
        public int Geladas { get; private set; }
        public int Coxinhas { get; private set; }
        public float Conta => Geladas * PrecoGelada + Coxinhas * PrecoCoxinha;
        public const float PrecoGelada = 12f, PrecoCoxinha = 7f;
        /// <summary>0..1 — sobe com as geladas e baixa com o tempo; dá um balanço leve na câmera.</summary>
        public float Alegria { get; private set; }

        CharacterController cc;
        float yaw, pitch, vy;
        float bobT, lastStepSign;
        Vector3 blendPos;
        Quaternion blendRot;
        float blendT = 1f, blendDur = 0.5f;
        float poolFov = 50f;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (hands != null) hands.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ estado

        public void Activate(Vector3 feet, float yawDeg, float blendSeconds)
        {
            cc.enabled = false;
            transform.position = feet;
            cc.enabled = true;
            yaw = yawDeg;
            pitch = 0f;
            vy = 0f;
            Activate(blendSeconds);
        }

        /// <summary>Volta a controlar a câmera partindo da pose atual dela.</summary>
        public void Activate(float blendSeconds)
        {
            if (!Active) poolFov = cam.fieldOfView;
            Active = true;
            BlendFromCamera(blendSeconds);
            if (hands != null) hands.gameObject.SetActive(true);
            LockCursor(true);
        }

        public void Deactivate()
        {
            if (Active) cam.fieldOfView = poolFov;
            Active = false;
            Focus = null;
            FocusPrompt = null;
            if (hands != null) hands.gameObject.SetActive(false);
            LockCursor(false);
        }

        public static void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        void BlendFromCamera(float seconds)
        {
            if (cam == null) return;
            blendPos = cam.transform.position;
            blendRot = cam.transform.rotation;
            blendDur = Mathf.Max(0.01f, seconds);
            blendT = seconds > 0f ? 0f : 1f;
        }

        public void Sit(Seat seat)
        {
            if (seat == null || seat.Occupied) return;
            CurrentSeat = seat;
            seat.Occupied = true;
            cc.enabled = false;
            var f = seat.eye.forward; f.y = 0f;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            pitch = seat.lookPitch;
            BlendFromCamera(0.45f);
            sandbox?.OnSeatChanged(seat);
        }

        public void Stand()
        {
            if (CurrentSeat == null) return;
            var seat = CurrentSeat;
            seat.Occupied = false;
            CurrentSeat = null;
            transform.position = seat.standPoint != null ? seat.standPoint.position : seat.transform.position + seat.transform.forward * 0.6f;
            var f = seat.standPoint != null ? seat.standPoint.forward : seat.transform.forward;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            cc.enabled = true;
            vy = 0f;
            BlendFromCamera(0.35f);
            sandbox?.OnSeatChanged(null);
        }

        // ------------------------------------------------------------------ conta

        public void AddGelada()
        {
            Geladas++;
        }

        public void AddCoxinha() => Coxinhas++;

        /// <summary>Cada gole deixa o freguês um tiquinho mais alegre.</summary>
        public void Sip() => Alegria = Mathf.Clamp01(Alegria + 0.035f);

        public void PayTab()
        {
            Geladas = 0;
            Coxinhas = 0;
        }

        // ------------------------------------------------------------------ quadro a quadro

        void Update()
        {
            if (!Active || Time.timeScale == 0f) return;
            float dt = Time.deltaTime;
            var inp = GameInput.ReadExplore(dt);
            if (inp.usedGamepad) LastUsedGamepad = true;
            else if (inp.usedKeyboard) LastUsedGamepad = false;

            if (inp.pause)
            {
                sandbox?.SetPaused(true);
                return;
            }

            // olhar
            yaw += inp.look.x;
            pitch = Mathf.Clamp(pitch - inp.look.y, -lookPitchLimit, lookPitchLimit);
            if (CurrentSeat != null)
            {
                var f = CurrentSeat.eye.forward;
                float seatYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                yaw = seatYaw + Mathf.Clamp(Mathf.DeltaAngle(seatYaw, yaw), -seatYawLimit, seatYawLimit);
            }

            if (CurrentSeat == null) Move(inp, dt);
            else if (inp.back)
            {
                Stand();
                return;
            }

            Alegria = Mathf.Max(0f, Alegria - dt * 0.004f);

            // interação: o que está na mira?
            Focus = null;
            FocusPrompt = null;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, rayLength, ~0, QueryTriggerInteraction.Collide))
            {
                var it = hit.collider.GetComponentInParent<Interactable>();
                if (it != null && hit.distance <= it.reach)
                {
                    var p = it.Prompt(this);
                    if (!string.IsNullOrEmpty(p))
                    {
                        Focus = it;
                        FocusPrompt = p;
                    }
                }
            }
            if (inp.interact && Focus != null)
            {
                Focus.Interact(this);
                return;
            }

            if (hands != null) hands.Tick(this, inp, dt);
        }

        void Move(ExploreInput inp, float dt)
        {
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            float speed = inp.run ? runSpeed : walkSpeed;
            var v = (fwd * inp.move.y + right * inp.move.x) * speed;
            if (cc.isGrounded && vy < 0f) vy = -1f;
            vy -= gravity * dt;
            v.y = vy;
            cc.Move(v * dt);

            // passos e balanço da cabeça
            var flat = cc.velocity; flat.y = 0f;
            float sp = flat.magnitude;
            if (sp > 0.2f && cc.isGrounded)
            {
                bobT += dt * sp * 2.3f;
                float s = Mathf.Sign(Mathf.Sin(bobT));
                if (s != lastStepSign && steps != null && steps.clip != null)
                {
                    steps.pitch = Random.Range(0.85f, 1.1f);
                    steps.PlayOneShot(steps.clip, Mathf.Lerp(0.25f, 0.5f, sp / runSpeed));
                }
                lastStepSign = s;
            }
            else bobT = Mathf.MoveTowards(bobT, Mathf.Round(bobT / Mathf.PI) * Mathf.PI, dt * 4f);
        }

        void LateUpdate()
        {
            if (!Active || cam == null) return;
            Vector3 pos;
            float bob = GameSettings.HeadBob ? 1f : 0f;
            if (CurrentSeat != null) pos = CurrentSeat.eye.position;
            else pos = transform.position + Vector3.up * (eyeHeight + (Mathf.Abs(Mathf.Sin(bobT)) * 0.035f - 0.02f) * bob);

            float t = Time.time;
            float roll = Mathf.Sin(t * 0.6f) * Alegria * 2.2f * bob;
            float sway = Mathf.Sin(t * 0.45f) * Alegria * 1.2f * bob;
            var rot = Quaternion.Euler(pitch + sway * 0.5f, yaw + sway, roll);

            if (blendT < 1f)
            {
                blendT = Mathf.Min(1f, blendT + Time.unscaledDeltaTime / blendDur);
                float k = Mathf.SmoothStep(0f, 1f, blendT);
                pos = Vector3.Lerp(blendPos, pos, k);
                rot = Quaternion.Slerp(blendRot, rot, k);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, GameSettings.Fov, k);
            }
            else cam.fieldOfView = GameSettings.Fov;
            cam.transform.SetPositionAndRotation(pos, rot);
        }
    }
}
