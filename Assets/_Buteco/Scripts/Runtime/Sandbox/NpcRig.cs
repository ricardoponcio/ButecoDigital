using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>Esqueleto simples dos bonecos low-poly (montado pelo ButecoBuilder). Guarda a pose base para animar por cima.</summary>
    public class NpcRig : MonoBehaviour
    {
        public Transform hips, spine, head;
        public Transform shoulderL, elbowL, shoulderR, elbowR;
        public Transform legL, kneeL, legR, kneeR;
        public Transform handPropR;

        Quaternion bHips, bSpine, bHead, bShL, bElL, bShR, bElR, bLegL, bKnL, bLegR, bKnR;
        Vector3 bHipsPos;
        bool captured;

        void Awake() => Capture();

        public void Capture()
        {
            if (captured) return;
            captured = true;
            bHipsPos = hips.localPosition;
            bHips = hips.localRotation;
            bSpine = spine.localRotation;
            bHead = head.localRotation;
            bShL = shoulderL.localRotation; bElL = elbowL.localRotation;
            bShR = shoulderR.localRotation; bElR = elbowR.localRotation;
            bLegL = legL.localRotation; bKnL = kneeL.localRotation;
            bLegR = legR.localRotation; bKnR = kneeR.localRotation;
        }

        public void Body(float hipsLift, Vector3 spineEuler, Vector3 headEuler)
        {
            hips.localPosition = bHipsPos + Vector3.up * hipsLift;
            spine.localRotation = bSpine * Quaternion.Euler(spineEuler);
            head.localRotation = bHead * Quaternion.Euler(headEuler);
        }

        public void ArmL(Vector3 shoulder, Vector3 elbow)
        {
            shoulderL.localRotation = bShL * Quaternion.Euler(shoulder);
            elbowL.localRotation = bElL * Quaternion.Euler(elbow);
        }

        public void ArmR(Vector3 shoulder, Vector3 elbow)
        {
            shoulderR.localRotation = bShR * Quaternion.Euler(shoulder);
            elbowR.localRotation = bElR * Quaternion.Euler(elbow);
        }

        public void Legs(float swingL, float swingR, float kneeLBend, float kneeRBend)
        {
            legL.localRotation = bLegL * Quaternion.Euler(swingL, 0, 0);
            legR.localRotation = bLegR * Quaternion.Euler(swingR, 0, 0);
            kneeL.localRotation = bKnL * Quaternion.Euler(kneeLBend, 0, 0);
            kneeR.localRotation = bKnR * Quaternion.Euler(kneeRBend, 0, 0);
        }
    }
}
