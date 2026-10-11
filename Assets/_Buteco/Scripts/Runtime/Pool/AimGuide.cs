using UnityEngine;

namespace Buteco.Pool
{
    /// <summary>Marcador da bola na mão (a linha de mira foi aposentada: no buteco é no olho).</summary>
    public class AimGuide : MonoBehaviour
    {
        public LineRenderer mainLine;
        public LineRenderer ghostCircle;
        public LineRenderer objectLine;
        public LineRenderer cueLine;
        public Color okColor = new(1f, 1f, 1f, 0.75f);
        public Color badColor = new(1f, 0.3f, 0.25f, 0.9f);

        const int CircleSegments = 28;

        public void Hide()
        {
            Set(mainLine, false); Set(ghostCircle, false); Set(objectLine, false); Set(cueLine, false);
        }

        static void Set(LineRenderer lr, bool on)
        {
            if (lr != null && lr.gameObject.activeSelf != on) lr.gameObject.SetActive(on);
        }

        public void ShowPlacement(PoolTable table, PoolPhysics phys, Vector2 at, bool valid)
        {
            Set(mainLine, false); Set(objectLine, false); Set(cueLine, false);
            Circle(table, at, phys.R * 1.25f, -phys.R * 0.9f, valid ? okColor : badColor);
        }

        void Circle(PoolTable table, Vector2 c, float r, float lift, Color color)
        {
            Set(ghostCircle, true);
            ghostCircle.loop = true;
            ghostCircle.positionCount = CircleSegments;
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / CircleSegments;
                ghostCircle.SetPosition(i, table.ToWorld(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, lift));
            }
            Tint(ghostCircle, color);
        }

        static void Tint(LineRenderer lr, Color c)
        {
            lr.startColor = c;
            lr.endColor = c;
        }
    }
}
