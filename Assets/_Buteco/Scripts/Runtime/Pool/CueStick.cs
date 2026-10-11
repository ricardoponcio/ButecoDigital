using UnityEngine;

namespace Buteco.Pool
{
    /// <summary>Taco: pivô na ponta, corpo se estende para -Z local.</summary>
    public class CueStick : MonoBehaviour
    {
        public float elevationDeg = 7f;
        public float maxPullBack = 0.28f;

        float strikeT = -1f;
        Vector3 strikeFrom, strikeTo;

        public bool Visible
        {
            get => gameObject.activeSelf;
            set { if (gameObject.activeSelf != value) gameObject.SetActive(value); }
        }

        /// <summary>Posiciona o taco atrás da bola.</summary>
        public void Pose(Vector3 ballCenter, Vector3 dir, float radius, float power)
        {
            if (strikeT >= 0f) return;
            dir.y = 0f; dir.Normalize();
            var fwd = Quaternion.AngleAxis(elevationDeg, Vector3.Cross(Vector3.up, dir)) * dir;
            float gap = 0.012f + power * maxPullBack;
            transform.SetPositionAndRotation(ballCenter - fwd * (radius + gap), Quaternion.LookRotation(fwd, Vector3.up));
        }

        /// <summary>Animação da tacada (a ponta avança até a bola).</summary>
        public void Strike(Vector3 ballCenter, float radius)
        {
            strikeFrom = transform.position;
            strikeTo = ballCenter - transform.forward * (radius * 0.6f);
            strikeT = 0f;
        }

        void Update()
        {
            if (strikeT < 0f) return;
            strikeT += Time.deltaTime;
            const float hit = 0.06f, hold = 0.35f;
            if (strikeT < hit)
                transform.position = Vector3.Lerp(strikeFrom, strikeTo, strikeT / hit);
            else if (strikeT > hold)
            {
                strikeT = -1f;
                Visible = false;
            }
        }
    }
}
