using UnityEngine;

namespace Buteco.Sandbox
{
    /// <summary>O dono do bar atrás do balcão: busca a gelada no freezer (ou a coxinha na estufa) e entrega no balcão.</summary>
    [RequireComponent(typeof(NpcRig))]
    public class Bartender : MonoBehaviour
    {
        public string displayName = "Seu Tião";
        [Tooltip("Linha atrás do balcão (z do mundo) e limites em x.")]
        public float lineZ = 3.07f;
        public float minX = -0.6f, maxX = 3.4f;
        public float coolerX = 0.1f, estufaX = 2.65f;
        public float speed = 1.3f;
        public GameObject propBottle, propCoxinha;

        public bool Busy => state != State.Idle;

        enum State { Idle, GoFetch, Fetch, GoServe, Serve }
        State state;
        HeldItem order;
        ButecoPlayer customer;
        float targetX, stateT, walkT, yaw = 180f;
        NpcRig rig;

        void Awake()
        {
            rig = GetComponent<NpcRig>();
            if (propBottle) propBottle.SetActive(false);
            if (propCoxinha) propCoxinha.SetActive(false);
        }

        public bool Order(HeldItem item, ButecoPlayer player)
        {
            if (Busy) return false;
            order = item;
            customer = player;
            targetX = item == HeldItem.Gelada ? coolerX : estufaX;
            Go(State.GoFetch);
            return true;
        }

        void Go(State s)
        {
            state = s;
            stateT = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateT += dt;
            var p = transform.position;
            bool walking = false;
            float faceYaw = 180f; // de frente pro salão

            switch (state)
            {
                case State.GoFetch:
                case State.GoServe:
                {
                    float dx = targetX - p.x;
                    if (Mathf.Abs(dx) < 0.03f)
                    {
                        if (state == State.GoFetch) Go(State.Fetch);
                        else Go(State.Serve);
                        break;
                    }
                    walking = true;
                    faceYaw = dx > 0f ? 90f : -90f;
                    p.x += Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), speed * dt);
                    break;
                }
                case State.Fetch:
                    if (stateT > 0.5f)
                    {
                        if (order == HeldItem.Gelada && propBottle) propBottle.SetActive(true);
                        if (order == HeldItem.Coxinha && propCoxinha) propCoxinha.SetActive(true);
                    }
                    if (stateT > 1.0f)
                    {
                        targetX = Mathf.Clamp(customer != null ? customer.transform.position.x : p.x, minX, maxX);
                        Go(State.GoServe);
                    }
                    break;
                case State.Serve:
                    if (stateT > 0.55f)
                    {
                        if (propBottle) propBottle.SetActive(false);
                        if (propCoxinha) propCoxinha.SetActive(false);
                        Deliver();
                        Go(State.Idle);
                    }
                    break;
            }

            p.z = lineZ;
            transform.position = p;
            yaw = Mathf.MoveTowardsAngle(yaw, faceYaw, dt * 540f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Animate(walking, dt);
        }

        void Deliver()
        {
            if (customer == null) return;
            if (order == HeldItem.Gelada)
            {
                customer.hands.GiveGelada();
                customer.AddGelada();
                customer.sandbox?.Toast($"{displayName}: Ó a gelada, trincando!");
            }
            else
            {
                customer.hands.GiveCoxinha();
                customer.AddCoxinha();
                customer.sandbox?.Toast($"{displayName}: Coxinha saindo, quentinha!");
            }
            customer = null;
        }

        void Animate(bool walking, float dt)
        {
            float t = Time.time;
            if (walking)
            {
                walkT += dt * 7.5f;
                float s = Mathf.Sin(walkT);
                rig.Legs(s * 24f, -s * 24f, Mathf.Max(0f, -s) * 30f, Mathf.Max(0f, s) * 30f);
                rig.Body(Mathf.Abs(Mathf.Cos(walkT)) * 0.025f, Vector3.zero, Vector3.zero);
                rig.ArmL(new Vector3(-s * 18f, 0, 0), new Vector3(-15f, 0, 0));
                if (state == State.GoServe) rig.ArmR(new Vector3(-35f, 0, 0), new Vector3(-50f, 0, 0));
                else rig.ArmR(new Vector3(s * 18f, 0, 0), new Vector3(-15f, 0, 0));
                return;
            }
            rig.Legs(0f, 0f, 0f, 0f);
            switch (state)
            {
                case State.Fetch:
                {
                    float k = Mathf.Sin(Mathf.Clamp01(stateT / 1.0f) * Mathf.PI);
                    if (order == HeldItem.Gelada)
                    {
                        // abaixa até o freezer embaixo do balcão
                        rig.Body(-k * 0.12f, new Vector3(k * 45f, 0, 0), new Vector3(-k * 20f, 0, 0));
                        rig.ArmR(new Vector3(-k * 50f, 0, 0), new Vector3(-k * 10f, 0, 0));
                        rig.ArmL(new Vector3(-k * 30f, 0, 0), Vector3.zero);
                    }
                    else
                    {
                        rig.Body(0f, new Vector3(k * 10f, 0, 0), new Vector3(k * 10f, 0, 0));
                        rig.ArmR(new Vector3(-k * 70f, 0, 0), new Vector3(-k * 15f, 0, 0));
                        rig.ArmL(Vector3.zero, Vector3.zero);
                    }
                    break;
                }
                case State.Serve:
                {
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateT / 0.35f));
                    rig.Body(0f, new Vector3(k * 12f, 0, 0), new Vector3(k * 6f, 0, 0));
                    rig.ArmR(new Vector3(-k * 75f, 0, 0), new Vector3(-(1f - k) * 50f, 0, 0));
                    rig.ArmL(Vector3.zero, Vector3.zero);
                    break;
                }
                default:
                {
                    // passando o pano no balcão
                    float w = t * 2.2f;
                    rig.Body(0f, new Vector3(8f, 0, 0), new Vector3(10f, Mathf.Sin(t * 0.25f) * 25f, 0));
                    rig.ArmR(new Vector3(-50f + Mathf.Sin(w) * 6f, Mathf.Cos(w) * 12f, 0), new Vector3(-30f, 0, 0));
                    rig.ArmL(new Vector3(-20f, 0, 0), new Vector3(-20f, 0, 0));
                    break;
                }
            }
        }
    }
}
