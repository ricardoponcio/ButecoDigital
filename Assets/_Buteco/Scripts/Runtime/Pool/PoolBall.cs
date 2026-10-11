using UnityEngine;

namespace Buteco.Pool
{
    public class PoolBall : MonoBehaviour
    {
        public int number;

        Vector3 trayTarget;
        float sinkT = -1f;
        Vector3 sinkFrom;

        public bool IsCue => number == 0;

        /// <summary>Posiciona e gira a bola rolando sem deslizar.</summary>
        public void Roll(Vector3 worldPos, Vector3 worldVel, float dt, float radius)
        {
            sinkT = -1f;
            gameObject.SetActive(true);
            transform.position = worldPos;
            float sp = worldVel.magnitude;
            if (sp > 1e-5f)
            {
                var axis = Vector3.Cross(Vector3.up, worldVel / sp);
                transform.Rotate(axis, sp * dt / radius * Mathf.Rad2Deg, Space.World);
            }
        }

        public void Place(Vector3 worldPos)
        {
            sinkT = -1f;
            gameObject.SetActive(true);
            transform.position = worldPos;
        }

        /// <summary>Animação curtinha de cair na caçapa e ir para a canaleta.</summary>
        public void SinkTo(Vector3 pocketWorld, Vector3 tray)
        {
            sinkFrom = pocketWorld;
            trayTarget = tray;
            sinkT = 0f;
            transform.position = pocketWorld;
        }

        void Update()
        {
            if (sinkT < 0f) return;
            sinkT += Time.deltaTime;
            if (sinkT < 0.25f)
            {
                transform.position = sinkFrom + Vector3.down * (sinkT / 0.25f) * 0.08f;
            }
            else if (sinkT < 0.9f)
            {
                float k = Mathf.SmoothStep(0, 1, (sinkT - 0.25f) / 0.65f);
                transform.position = Vector3.Lerp(sinkFrom + Vector3.down * 0.08f, trayTarget, k);
            }
            else
            {
                transform.position = trayTarget;
                sinkT = -1f;
            }
        }
    }
}
