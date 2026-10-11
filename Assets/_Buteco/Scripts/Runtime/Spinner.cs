using UnityEngine;

namespace Buteco
{
    /// <summary>Gira continuamente (ventilador de teto, roda de moto…).</summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 220f;

        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
