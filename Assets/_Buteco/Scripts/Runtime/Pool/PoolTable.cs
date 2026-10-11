using UnityEngine;

namespace Buteco.Pool
{
    /// <summary>Especificação da mesa e conversão entre o espaço 2D da física e o mundo.</summary>
    public class PoolTable : MonoBehaviour
    {
        [Header("Medidas (metros)")]
        public float playLength = 2.10f;
        public float playWidth = 1.05f;
        public float ballRadius = 0.027f;
        public float surfaceHeight = 0.80f;

        [Tooltip("Onde as bolas encaçapadas ficam expostas (canaleta lateral).")]
        public Transform tray;

        public float HalfL => playLength * 0.5f;
        public float HalfW => playWidth * 0.5f;

        /// <summary>Ponto de saída (head spot) — lado -X.</summary>
        public Vector2 HeadSpot => new(-HalfL * 0.5f, 0f);
        /// <summary>Ponto do triângulo (foot spot) — lado +X.</summary>
        public Vector2 FootSpot => new(HalfL * 0.5f, 0f);
        public float HeadString => -HalfL * 0.5f;

        public PoolPhysics CreatePhysics(int balls = 16) => new(balls, ballRadius, HalfL, HalfW);

        public Vector3 ToWorld(Vector2 p, float lift = 0f) =>
            transform.TransformPoint(new Vector3(p.x, surfaceHeight + ballRadius + lift, p.y));

        public Vector3 DirToWorld(Vector2 d) => transform.TransformDirection(new Vector3(d.x, 0f, d.y));

        public Vector2 ToTable(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return new Vector2(l.x, l.z);
        }

        public Vector2 DirToTable(Vector3 worldDir)
        {
            var l = transform.InverseTransformDirection(worldDir);
            return new Vector2(l.x, l.z);
        }

        public Vector3 Center => transform.TransformPoint(new Vector3(0, surfaceHeight, 0));

        /// <summary>Raio da câmera/ponteiro → ponto no plano do feltro.</summary>
        public bool RaycastSurface(Ray ray, out Vector2 p)
        {
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, surfaceHeight + ballRadius, 0)));
            if (plane.Raycast(ray, out float t))
            {
                p = ToTable(ray.GetPoint(t));
                return true;
            }
            p = default;
            return false;
        }

        public Vector3 TraySlot(int index)
        {
            if (tray == null) return ToWorld(new Vector2(HalfL + 0.3f, 0));
            return tray.TransformPoint(new Vector3(0f, 0f, index * ballRadius * 2.05f));
        }

        void OnDrawGizmosSelected()
        {
            var phys = CreatePhysics();
            Gizmos.color = Color.yellow;
            foreach (var s in phys.cushions) Gizmos.DrawLine(ToWorld(s.a), ToWorld(s.b));
            Gizmos.color = Color.red;
            for (int i = 0; i < phys.pockets.Count; i++) Gizmos.DrawWireSphere(ToWorld(phys.pockets[i]), phys.pocketRadius[i]);
        }
    }
}
