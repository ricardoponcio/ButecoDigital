using UnityEngine;

namespace Buteco
{
    /// <summary>Balanço leve (roupas no varal, fios).</summary>
    public class Sway : MonoBehaviour
    {
        public float angle = 6f;
        public float speed = 1.3f;
        public Vector3 axis = Vector3.forward;
        Quaternion baseRot;
        float phase;

        void Start()
        {
            baseRot = transform.localRotation;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float a = Mathf.Sin(Time.time * speed + phase) * angle;
            transform.localRotation = baseRot * Quaternion.AngleAxis(a, axis);
        }
    }
}
