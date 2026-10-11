using UnityEngine;

namespace Buteco.Pool
{
    public enum CamMode { Menu, Aim, Top, Watch }

    /// <summary>Câmera da sinuca: vitrine (menu), mira atrás da branca, vista de cima e acompanhamento.</summary>
    public class PoolCamera : MonoBehaviour
    {
        public PoolTable table;
        public Camera cam;

        [Header("Menu — vista da rua")]
        public Vector3 menuPosition = new(7.2f, 2.3f, -9.5f);
        public Vector3 menuLookAt = new(-0.5f, 1.1f, -0.5f);
        public float menuSway = 0.6f;

        [Header("Mira")]
        public float aimDistance = 0.85f;
        public float aimHeight = 0.32f;
        public float minDistance = 0.35f, maxDistance = 1.8f;

        [Header("Vista de cima")]
        public float topHeight = 1.85f;
        [Tooltip("Escondidos na vista de cima (ex.: a luminária que fica entre a câmera e a mesa).")]
        public Renderer[] hideInTopView;

        public CamMode Mode { get; private set; } = CamMode.Menu;
        public Vector3 AimFocus { get; set; }
        public Vector3 AimDir { get; set; } = Vector3.right;

        Vector3 velocity;
        float menuT;
        bool snapNext;

        void Reset()
        {
            cam = GetComponent<Camera>();
        }

        public void SetMode(CamMode m, bool snap = false)
        {
            Mode = m;
            snapNext |= snap;
        }

        public void Zoom(float amount)
        {
            if (Mode == CamMode.Aim)
                aimDistance = Mathf.Clamp(aimDistance * (1f - amount * 0.02f), minDistance, maxDistance);
            else if (Mode == CamMode.Top)
                topHeight = Mathf.Clamp(topHeight * (1f - amount * 0.015f), 1.0f, 3.2f);
        }

        void LateUpdate()
        {
            if (table == null) return;
            float dt = Time.unscaledDeltaTime;
            bool showOverhead = Mode != CamMode.Top || transform.position.y < table.Center.y + 0.95f;
            if (hideInTopView != null)
                foreach (var r in hideInTopView)
                    if (r != null && r.enabled != showOverhead) r.enabled = showOverhead;
            Vector3 targetPos;
            Quaternion targetRot;
            float smooth;

            switch (Mode)
            {
                case CamMode.Aim:
                {
                    var dir = AimDir; dir.y = 0f; dir.Normalize();
                    float h = aimHeight * (aimDistance / 0.85f) * 0.8f + 0.06f;
                    targetPos = AimFocus - dir * aimDistance + Vector3.up * h;
                    var look = AimFocus + dir * (0.35f + aimDistance * 0.4f);
                    targetRot = Quaternion.LookRotation(look - targetPos, Vector3.up);
                    smooth = 0.07f;
                    break;
                }
                case CamMode.Top:
                {
                    targetPos = table.Center + table.transform.up * topHeight;
                    targetRot = table.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
                    smooth = 0.25f;
                    break;
                }
                case CamMode.Watch:
                {
                    targetPos = table.Center + table.transform.rotation * new Vector3(-0.25f, 1.45f, -1.75f);
                    targetRot = Quaternion.LookRotation(table.Center + Vector3.down * 0.15f - targetPos, Vector3.up);
                    smooth = 0.45f;
                    break;
                }
                default:
                {
                    menuT += dt;
                    var sway = new Vector3(Mathf.Sin(menuT * 0.11f), Mathf.Sin(menuT * 0.07f) * 0.25f, Mathf.Cos(menuT * 0.09f) * 0.4f) * menuSway;
                    targetPos = menuPosition + sway;
                    targetRot = Quaternion.LookRotation(menuLookAt - targetPos, Vector3.up);
                    smooth = 0.9f;
                    break;
                }
            }

            if (snapNext)
            {
                transform.SetPositionAndRotation(targetPos, targetRot);
                velocity = Vector3.zero;
                snapNext = false;
                return;
            }
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smooth, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-dt / Mathf.Max(0.02f, smooth * 0.6f)));
        }
    }
}
