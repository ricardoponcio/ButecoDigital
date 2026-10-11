using System.Collections.Generic;
using Buteco.Pool;
using Buteco.Sandbox;
using Buteco.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Buteco.EditorTools
{
    /// <summary>Parte "sandbox" da cena: colisões, gente (dono, fregueses, roda de pagode), cadeiras e o freguês jogável.</summary>
    public static partial class ButecoBuilder
    {
        static readonly List<List<Transform>> streetChairs = new();
        static readonly List<Transform> stools = new();
        static GameObject counterBody;
        static Transform estufaNode;
        static PoolGame gameCache;
        static ButecoUI uiCache;
        static PoolCamera camCache;

        // =================================================================== colisão

        /// <summary>Colisor do tamanho da malha (o BoxCollider se ajusta sozinho aos bounds).</summary>
        static GameObject Solid(GameObject go)
        {
            go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>Parede invisível.</summary>
        static BoxCollider Blocker(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = Node(name, parent, center);
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            return c;
        }

        // =================================================================== gente

        struct Look
        {
            public Material skin, shirt, pants, hair, shoes;
            public int hairStyle;   // 0 curto, 1 careca, 2 boné, 3 black power, 4 coque
            public bool hat, mustache, apron, shorts, belly;
        }

        static readonly List<Material> skins = new();
        static readonly List<Material> shirts = new();

        static void CreatePeopleMaterials()
        {
            skins.Clear();
            shirts.Clear();
            Color[] skinColors =
            {
                new(0.33f, 0.20f, 0.13f), new(0.46f, 0.29f, 0.18f), new(0.62f, 0.42f, 0.28f),
                new(0.78f, 0.58f, 0.43f), new(0.93f, 0.76f, 0.63f),
            };
            for (int i = 0; i < skinColors.Length; i++) skins.Add(Lit($"Pele {i}", skinColors[i], 0.25f));
            Color[] shirtColors =
            {
                new(0.95f, 0.85f, 0.15f), new(0.10f, 0.45f, 0.25f), new(0.92f, 0.92f, 0.90f), new(0.15f, 0.25f, 0.55f),
                new(0.75f, 0.12f, 0.15f), new(0.95f, 0.50f, 0.20f), new(0.35f, 0.65f, 0.85f), new(0.55f, 0.30f, 0.55f),
            };
            for (int i = 0; i < shirtColors.Length; i++) shirts.Add(Lit($"Camisa {i}", shirtColors[i], 0.15f));
            Lit("Calça Jeans", new Color(0.18f, 0.25f, 0.42f), 0.15f);
            Lit("Calça Preta", new Color(0.07f, 0.07f, 0.08f), 0.15f);
            Lit("Calça Cáqui", new Color(0.62f, 0.55f, 0.40f), 0.15f);
            Lit("Calça Branca", new Color(0.92f, 0.91f, 0.88f), 0.15f);
            Lit("Cabelo Preto", new Color(0.05f, 0.04f, 0.035f), 0.3f);
            Lit("Cabelo Castanho", new Color(0.20f, 0.12f, 0.07f), 0.3f);
            Lit("Cabelo Grisalho", new Color(0.62f, 0.62f, 0.60f), 0.3f);
            Lit("Chapéu Palha", new Color(0.93f, 0.88f, 0.72f), 0.2f);
            Lit("Chinelo", new Color(0.15f, 0.45f, 0.30f), 0.3f);
            Lit("Tênis", new Color(0.9f, 0.9f, 0.9f), 0.3f);
            Lit("Pano Branco", new Color(0.96f, 0.96f, 0.94f), 0.1f);
            Lit("Espuma", new Color(1f, 0.97f, 0.88f), 0.3f);
            Lit("Couro Claro", new Color(0.93f, 0.88f, 0.76f), 0.2f);
        }

        static Look RandomLook()
        {
            return new Look
            {
                skin = Pick(skins),
                shirt = Pick(shirts),
                pants = Pick(new[] { M("Calça Jeans"), M("Calça Preta"), M("Calça Cáqui") }),
                hair = Chance(0.2f) ? M("Cabelo Grisalho") : Chance(0.5f) ? M("Cabelo Preto") : M("Cabelo Castanho"),
                shoes = Chance(0.6f) ? M("Chinelo") : M("Tênis"),
                hairStyle = rng.Next(5),
                shorts = Chance(0.4f),
                belly = Chance(0.3f),
                mustache = Chance(0.25f),
            };
        }

        /// <summary>Boneco low-poly articulado. seatHeight &lt; 0 = em pé.</summary>
        static NpcRig Person(Transform parent, string name, Vector3 pos, float yaw, Look look, float seatHeight)
        {
            var root = Node(name, parent, pos, yaw).transform;
            bool seated = seatHeight >= 0f;
            var hips = Node("Quadril", root, new Vector3(0, seated ? seatHeight + 0.045f : 0.92f, seated ? -0.05f : 0f)).transform;
            Box(hips, "Pélvis", Vector3.zero, new Vector3(0.32f, 0.16f, 0.2f), look.pants);

            Transform Leg(float side, out Transform knee)
            {
                var leg = Node(side < 0 ? "Perna E" : "Perna D", hips, new Vector3(side * 0.09f, -0.03f, 0)).transform;
                Box(leg, "Coxa", new Vector3(0, -0.2f, 0), new Vector3(0.14f, 0.44f, 0.15f), look.pants);
                knee = Node("Joelho", leg, new Vector3(0, -0.42f, 0)).transform;
                Box(knee, "Canela", new Vector3(0, -0.2f, 0), new Vector3(0.11f, 0.42f, 0.12f), look.shorts ? look.skin : look.pants);
                Box(knee, "Pé", new Vector3(0, -0.43f, 0.05f), new Vector3(0.11f, 0.06f, 0.24f), look.shoes);
                return leg;
            }
            var legL = Leg(-1f, out var kneeL);
            var legR = Leg(1f, out var kneeR);
            if (look.shorts)
            {
                // bermuda: a canela é pele, mas o joelho fica coberto
                Box(legL, "Bermuda", new Vector3(0, -0.36f, 0), new Vector3(0.145f, 0.12f, 0.155f), look.pants);
                Box(legR, "Bermuda", new Vector3(0, -0.36f, 0), new Vector3(0.145f, 0.12f, 0.155f), look.pants);
            }
            if (seated)
            {
                legL.localEulerAngles = new Vector3(-90f, -4f, 0);
                legR.localEulerAngles = new Vector3(-90f, 4f, 0);
                kneeL.localEulerAngles = kneeR.localEulerAngles = new Vector3(90f, 0, 0);
            }

            var spine = Node("Tronco", hips, new Vector3(0, 0.07f, 0)).transform;
            Box(spine, "Camisa", new Vector3(0, 0.25f, 0), new Vector3(0.36f, 0.5f, 0.21f), look.shirt);
            if (look.belly) Box(spine, "Barriga", new Vector3(0, 0.13f, 0.07f), new Vector3(0.33f, 0.26f, 0.12f), look.shirt);
            if (look.apron) Box(spine, "Avental", new Vector3(0, 0.1f, look.belly ? 0.135f : 0.11f), new Vector3(0.3f, 0.44f, 0.012f), M("Pano Branco"));

            var head = Node("Cabeça", spine, new Vector3(0, 0.5f, 0)).transform;
            Prism(head, "Pescoço", Vector3.zero, 6, 0.05f, 0.05f, 0.08f, look.skin);
            Ico(head, "Rosto", new Vector3(0, 0.15f, 0), 0.105f, look.skin, new Vector3(0.92f, 1.08f, 0.98f), 1);
            Box(head, "Olho", new Vector3(-0.034f, 0.17f, 0.094f), new Vector3(0.018f, 0.022f, 0.012f), M("Preto"));
            Box(head, "Olho", new Vector3(0.034f, 0.17f, 0.094f), new Vector3(0.018f, 0.022f, 0.012f), M("Preto"));
            Box(head, "Nariz", new Vector3(0, 0.14f, 0.1f), new Vector3(0.022f, 0.035f, 0.024f), look.skin);
            if (look.mustache) Box(head, "Bigode", new Vector3(0, 0.112f, 0.098f), new Vector3(0.065f, 0.014f, 0.014f), look.hair);
            if (look.hat)
            {
                Prism(head, "Aba", new Vector3(0, 0.215f, 0), 12, 0.17f, 0.17f, 0.012f, M("Chapéu Palha"));
                Prism(head, "Copa", new Vector3(0, 0.22f, 0), 10, 0.1f, 0.085f, 0.085f, M("Chapéu Palha"));
                Prism(head, "Fita", new Vector3(0, 0.226f, 0), 10, 0.101f, 0.098f, 0.022f, M("Preto"));
            }
            else
            {
                switch (look.hairStyle)
                {
                    case 0: Ico(head, "Cabelo", new Vector3(0, 0.185f, -0.015f), 0.105f, look.hair, new Vector3(0.97f, 0.78f, 1.0f), 1); break;
                    case 2:
                        Prism(head, "Boné", new Vector3(0, 0.19f, -0.005f), 10, 0.102f, 0.085f, 0.075f, look.shirt == shirts[0] ? shirts[3] : shirts[0]);
                        Box(head, "Aba do boné", new Vector3(0, 0.2f, 0.1f), new Vector3(0.15f, 0.012f, 0.11f), look.shirt == shirts[0] ? shirts[3] : shirts[0]);
                        break;
                    case 3: Ico(head, "Black power", new Vector3(0, 0.2f, -0.012f), 0.13f, look.hair, new Vector3(1.08f, 0.95f, 1.05f), 1); break;
                    case 4:
                        Ico(head, "Cabelo", new Vector3(0, 0.185f, -0.015f), 0.105f, look.hair, new Vector3(0.97f, 0.78f, 1.0f), 1);
                        Ico(head, "Coque", new Vector3(0, 0.235f, -0.08f), 0.048f, look.hair, null, 0);
                        break;
                }
            }

            Transform Arm(float side, out Transform elbow, out Transform hand)
            {
                var sh = Node(side < 0 ? "Ombro E" : "Ombro D", spine, new Vector3(side * 0.215f, 0.45f, 0)).transform;
                Box(sh, "Manga", new Vector3(0, -0.06f, 0), new Vector3(0.115f, 0.15f, 0.115f), look.shirt);
                Box(sh, "Braço", new Vector3(0, -0.16f, 0), new Vector3(0.085f, 0.27f, 0.085f), look.skin);
                elbow = Node("Cotovelo", sh, new Vector3(0, -0.29f, 0)).transform;
                Box(elbow, "Antebraço", new Vector3(0, -0.12f, 0), new Vector3(0.078f, 0.25f, 0.078f), look.skin);
                Ico(elbow, "Mão", new Vector3(0, -0.27f, 0), 0.045f, look.skin, null, 0);
                hand = Node("Na mão", elbow, new Vector3(0, -0.28f, 0)).transform;
                sh.localEulerAngles = new Vector3(0, 0, side * 4f);
                return sh;
            }
            var shL = Arm(-1f, out var elL, out _);
            var shR = Arm(1f, out var elR, out var handR);

            var rig = root.gameObject.AddComponent<NpcRig>();
            rig.hips = hips; rig.spine = spine; rig.head = head;
            rig.shoulderL = shL; rig.elbowL = elL; rig.shoulderR = shR; rig.elbowR = elR;
            rig.legL = legL; rig.kneeL = kneeL; rig.legR = legR; rig.kneeR = kneeR;
            rig.handPropR = handR;
            dynamicObjects.Add(root.gameObject);
            return rig;
        }

        static void Pose(NpcRig rig, Vector3 shL, Vector3 elL, Vector3 shR, Vector3 elR)
        {
            rig.shoulderL.localEulerAngles = shL;
            rig.elbowL.localEulerAngles = elL;
            rig.shoulderR.localEulerAngles = shR;
            rig.elbowR.localEulerAngles = elR;
        }

        // =================================================================== instrumentos

        /// <summary>Cavaquinho ou banjo: corpo na origem, braço para +X, tampo para +Z.</summary>
        static Transform StringInstrument(Transform parent, bool banjo)
        {
            var t = Node(banjo ? "Banjo" : "Cavaquinho", parent, Vector3.zero).transform;
            if (banjo)
            {
                Prism(t, "Aro", new Vector3(0, 0, -0.035f), 14, 0.12f, 0.12f, 0.06f, M("Inox"), new Vector3(90f, 0, 0));
                Prism(t, "Pele", new Vector3(0, 0, 0.026f), 14, 0.112f, 0.112f, 0.004f, M("Couro Claro"), new Vector3(90f, 0, 0));
            }
            else
            {
                Box(t, "Corpo", new Vector3(-0.02f, 0, 0), new Vector3(0.24f, 0.17f, 0.06f), M("Madeira Clara"));
                Box(t, "Cintura", new Vector3(0.1f, 0, 0), new Vector3(0.06f, 0.13f, 0.06f), M("Madeira Clara"));
                Prism(t, "Boca", new Vector3(0.03f, 0, 0.03f), 10, 0.025f, 0.025f, 0.002f, M("Preto"), new Vector3(90f, 0, 0));
            }
            Box(t, "Braço", new Vector3(0.27f, 0, 0.01f), new Vector3(0.3f, 0.035f, 0.025f), M("Madeira Escura"));
            Box(t, "Mão do braço", new Vector3(0.45f, 0, 0.005f), new Vector3(0.08f, 0.05f, 0.02f), M("Madeira Escura"));
            for (int i = -1; i <= 1; i += 2)
                Box(t, "Corda", new Vector3(0.18f, i * 0.007f, 0.034f), new Vector3(0.5f, 0.002f, 0.002f), M("Inox"), null, null, false);
            return t;
        }

        static Transform Drum(Transform parent, string name, float radius, float length, Material shell)
        {
            var t = Node(name, parent, Vector3.zero).transform;
            Prism(t, "Corpo", new Vector3(0, -length * 0.5f, 0), 12, radius, radius, length, shell);
            Prism(t, "Pele", new Vector3(0, length * 0.5f, 0), 12, radius * 0.97f, radius * 0.97f, 0.004f, M("Couro Claro"));
            Prism(t, "Aro", new Vector3(0, length * 0.5f - 0.015f, 0), 12, radius * 1.03f, radius * 1.03f, 0.02f, M("Inox"));
            return t;
        }

        /// <summary>Põe um objeto na pose (local ao <paramref name="frame"/>) e depois pendura em <paramref name="holder"/> mantendo a posição.</summary>
        static void Hold(Transform item, Transform frame, Vector3 pos, Quaternion rot, Transform holder)
        {
            item.SetParent(frame, false);
            item.localPosition = pos;
            item.localRotation = rot;
            if (holder != null && holder != frame) item.SetParent(holder, true);
        }

        static NpcRig Musician(Transform parent, RodaDePagode roda, Instrument inst, Vector3 chairPos, float yaw, Look look)
        {
            Chair(parent, chairPos, yaw, M("Plástico Branco"));
            var rig = Person(parent, $"Músico ({inst})", chairPos, yaw, look, 0.4575f);
            var spine = rig.spine;
            switch (inst)
            {
                case Instrument.Cavaquinho:
                case Instrument.Banjo:
                {
                    Pose(rig, new Vector3(-20f, 0, -10f), new Vector3(-80f, 0, 0), new Vector3(-10f, 0, -25f), new Vector3(-55f, 0, 0));
                    // braço do instrumento (+X local) para a esquerda e um pouco à frente; tampo (+Z) para a frente
                    var neckDir = new Vector3(-0.85f, 0.3f, 0.45f).normalized;
                    var face = (Vector3.forward - Vector3.Dot(Vector3.forward, neckDir) * neckDir).normalized;
                    var rot = Quaternion.LookRotation(face, Vector3.Cross(face, neckDir));
                    Hold(StringInstrument(parent, inst == Instrument.Banjo), spine, new Vector3(0.02f, 0.13f, 0.2f), rot, spine);
                    break;
                }
                case Instrument.Pandeiro:
                {
                    Pose(rig, new Vector3(-35f, 0, -5f), new Vector3(-80f, 0, 0), new Vector3(-35f, 0, -30f), new Vector3(-85f, 0, 0));
                    var pand = Node("Pandeiro", parent, Vector3.zero).transform;
                    Prism(pand, "Aro", new Vector3(0, -0.025f, 0), 14, 0.12f, 0.12f, 0.05f, M("Madeira"));
                    Prism(pand, "Pele", new Vector3(0, 0.025f, 0), 14, 0.115f, 0.115f, 0.003f, M("Couro Claro"));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        Prism(pand, "Platinela", new Vector3(Mathf.Cos(a) * 0.12f, -0.012f, Mathf.Sin(a) * 0.12f), 8, 0.018f, 0.018f, 0.012f, M("Inox"));
                    }
                    var normal = new Vector3(0.45f, 0.1f, 0.88f).normalized;
                    Hold(pand, spine, new Vector3(-0.12f, 0.35f, 0.42f), Quaternion.FromToRotation(Vector3.up, normal), rig.elbowL);
                    break;
                }
                case Instrument.Tanta:
                {
                    Pose(rig, new Vector3(-20f, 0, 0), new Vector3(-80f, 0, 0), new Vector3(-20f, 0, 0), new Vector3(-80f, 0, 0));
                    var d = Drum(parent, "Tantã", 0.13f, 0.55f, M("Plástico Vermelho"));
                    Hold(d, rig.hips, new Vector3(0.02f, 0.18f, 0.26f), Quaternion.Euler(0, 0, -90f), rig.hips);
                    break;
                }
                case Instrument.Repique:
                {
                    Pose(rig, new Vector3(-20f, 0, 0), new Vector3(-80f, 0, 0), new Vector3(-20f, 0, -15f), new Vector3(-80f, 0, 0));
                    var d = Drum(parent, "Repique de mão", 0.11f, 0.34f, M("Inox"));
                    Hold(d, rig.hips, new Vector3(0.04f, 0.2f, 0.26f), Quaternion.Euler(-35f, 0, 0), rig.hips);
                    break;
                }
            }
            var m = rig.gameObject.AddComponent<Buteco.Sandbox.Musician>();
            m.roda = roda;
            m.instrument = inst;
            return rig;
        }

        static NpcRig SeatedPatron(Transform parent, Transform chair, RodaDePagode roda, Look look, float seatHeight = 0.4575f)
        {
            var rig = Person(chair, "Freguês", Vector3.zero, 0f, look, seatHeight);
            rig.transform.SetParent(parent, true);
            Pose(rig, new Vector3(-20f, 0, 0), new Vector3(-70f, 0, 0), new Vector3(-20f, 0, 0), new Vector3(-70f, 0, 0));
            var glass = Glass(rig.handPropR, Vector3.zero, true);
            glass.localPosition = new Vector3(0, -0.02f, 0.03f);
            glass.localRotation = Quaternion.Euler(90f, 0, 0);
            var p = rig.gameObject.AddComponent<Patron>();
            p.roda = roda;
            return rig;
        }

        static Seat MakeSeat(Transform chair, string label, float eyeHeight, float standBack, bool atRoda = false)
        {
            var seat = chair.gameObject.AddComponent<Seat>();
            seat.label = label;
            seat.atRoda = atRoda;
            seat.lookPitch = atRoda ? 4f : 8f;
            seat.eye = Node("Olhos", chair, new Vector3(0, eyeHeight, -0.06f)).transform;
            seat.standPoint = Node("Levantar", chair, new Vector3(0, 0.05f, -standBack)).transform;
            if (chair.GetComponent<Collider>() == null)
            {
                var c = chair.gameObject.AddComponent<BoxCollider>();
                c.center = new Vector3(0, eyeHeight * 0.3f, 0);
                c.size = new Vector3(0.38f, eyeHeight * 0.6f, 0.38f);
            }
            return seat;
        }

        // =================================================================== montagem

        static void BuildSandbox(Transform world, PoolTable table)
        {
            CreatePeopleMaterials();
            var sb = Node("Sandbox", world, Vector3.zero).transform;

            // ---------------- limites do passeio (paredes invisíveis)
            var bounds = Node("Limites", sb, Vector3.zero).transform;
            Blocker(bounds, "Fim da rua (esq.)", new Vector3(-14f, 1.5f, -8.5f), new Vector3(0.4f, 3f, 10f));
            Blocker(bounds, "Fim da rua (dir.)", new Vector3(16f, 1.5f, -9f), new Vector3(0.4f, 3f, 8f));
            Blocker(bounds, "Atrás do bar", new Vector3(0.7f, 1.5f, 3.85f), new Vector3(11.4f, 3f, 0.3f));
            Blocker(bounds, "Topo da viela", new Vector3(7.9f, StairsY(30f) + 1.5f, 29.8f), new Vector3(3.2f, 3f, 0.4f));
            Blocker(bounds, "Fim do balcão", new Vector3(-1.1f, 0.6f, 3.0f), new Vector3(0.5f, 1.2f, 0.9f));

            // ---------------- a mesa de sinuca vira uma atração do passeio
            var tableCol = table.gameObject.AddComponent<BoxCollider>();
            tableCol.center = new Vector3(0, 0.42f, 0);
            tableCol.size = new Vector3(table.playLength + 0.38f, 0.84f, table.playWidth + 0.38f);
            table.gameObject.AddComponent<PoolSpot>().reach = 2.6f;

            // ---------------- a roda de pagode, no canto das mesas de plástico
            var rodaRoot = Node("Roda de pagode", sb, new Vector3(3.0f, 0, -0.9f)).transform;
            var rodaGo = Node("Som da roda", rodaRoot, new Vector3(0, 1.0f, 0));
            var src = rodaGo.AddComponent<AudioSource>();
            src.spatialBlend = 0.85f;
            src.volume = 0.8f;
            src.minDistance = 2.5f;
            src.maxDistance = 40f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.dopplerLevel = 0f;
            var roda = rodaGo.AddComponent<RodaDePagode>();
            roda.songs = new[]
            {
                new RodaDePagode.Song { title = "Samba de Esquina", clip = Clip("roda_samba_1"), bpm = 100f },
                new RodaDePagode.Song { title = "Fim de Tarde na Laje", clip = Clip("roda_samba_2"), bpm = 96f },
            };
            roda.applause = Clip("roda_aplausos");
            dynamicObjects.Add(rodaGo);

            PlasticTable(rodaRoot, new Vector3(0, 0, -0.36f), M("Plástico Amarelo"), 0f, false);
            PlasticTable(rodaRoot, new Vector3(0, 0, 0.36f), M("Plástico Amarelo"), 0f, false);

            var looks = new[]
            {
                new Look { skin = skins[0], shirt = shirts[2], pants = M("Calça Branca"), hair = M("Cabelo Preto"), shoes = M("Tênis"), hat = true },
                new Look { skin = skins[2], shirt = shirts[1], pants = M("Calça Jeans"), hair = M("Cabelo Preto"), shoes = M("Tênis"), hairStyle = 3 },
                new Look { skin = skins[1], shirt = shirts[0], pants = M("Calça Preta"), hair = M("Cabelo Preto"), shoes = M("Chinelo"), hairStyle = 2, belly = true },
                new Look { skin = skins[4], shirt = shirts[4], pants = M("Calça Cáqui"), hair = M("Cabelo Grisalho"), shoes = M("Tênis"), hairStyle = 1, mustache = true },
                new Look { skin = skins[3], shirt = shirts[6], pants = M("Calça Jeans"), hair = M("Cabelo Castanho"), shoes = M("Chinelo"), hairStyle = 4, shorts = true },
            };
            Musician(rodaRoot, roda, Instrument.Cavaquinho, new Vector3(-0.72f, 0, -0.35f), 90f, looks[0]);
            Musician(rodaRoot, roda, Instrument.Banjo, new Vector3(-0.72f, 0, 0.35f), 90f, looks[1]);
            Musician(rodaRoot, roda, Instrument.Pandeiro, new Vector3(0.72f, 0, -0.35f), -90f, looks[2]);
            Musician(rodaRoot, roda, Instrument.Tanta, new Vector3(0.72f, 0, 0.35f), -90f, looks[3]);
            Musician(rodaRoot, roda, Instrument.Repique, new Vector3(0, 0, 1.02f), 180f, looks[4]);
            // a cadeira vaga da roda é do jogador
            var freeChair = Chair(rodaRoot, new Vector3(0, 0, -1.12f), 0f, M("Plástico Branco"));
            MakeSeat(freeChair, "Sentar na roda", 1.16f, 0.65f, true);
            // copos e cerveja na mesa da roda
            Beer(rodaRoot, new Vector3(0.1f, 0.74f, -0.1f));
            Beer(rodaRoot, new Vector3(-0.12f, 0.74f, 0.45f));
            Glass(rodaRoot, new Vector3(-0.22f, 0.74f, -0.4f), true);
            Glass(rodaRoot, new Vector3(0.2f, 0.74f, -0.45f), true);
            Glass(rodaRoot, new Vector3(0.22f, 0.74f, 0.3f), false);
            Glass(rodaRoot, new Vector3(-0.2f, 0.74f, 0.2f), true);
            Glass(rodaRoot, new Vector3(0.05f, 0.74f, 0.75f), true);

            // ---------------- o dono do bar
            var tiao = Person(sb, "Seu Tião", new Vector3(1.4f, 0, 3.07f), 180f, new Look
            {
                skin = skins[1], shirt = shirts[2], pants = M("Calça Preta"), hair = M("Cabelo Grisalho"), shoes = M("Chinelo"),
                hairStyle = 0, mustache = true, apron = true, belly = true,
            }, -1f);
            var bartender = tiao.gameObject.AddComponent<Bartender>();
            var bottleProp = Beer(tiao.handPropR, Vector3.zero);
            bottleProp.localPosition = new Vector3(0, -0.02f, -0.1f);
            bottleProp.localRotation = Quaternion.Euler(90f, 0, 0);
            bartender.propBottle = bottleProp.gameObject;
            var coxProp = Node("Coxinha", tiao.handPropR, new Vector3(0, -0.02f, 0.02f)).transform;
            Prism(coxProp, "Massa", Vector3.zero, 6, 0.035f, 0f, 0.075f, M("Salgado"), new Vector3(90f, 0, 0));
            bartender.propCoxinha = coxProp.gameObject;
            var tiaoCol = tiao.gameObject.AddComponent<CapsuleCollider>();
            tiaoCol.center = new Vector3(0, 0.9f, 0);
            tiaoCol.height = 1.8f;
            tiaoCol.radius = 0.22f;
            tiao.gameObject.AddComponent<CounterService>().bartender = bartender;

            // ---------------- balcão: gelada, coxinha e caixa
            var geladaSvc = counterBody.AddComponent<CounterService>();
            geladaSvc.bartender = bartender;
            geladaSvc.reach = 2.4f;
            var estufaCol = estufaNode.gameObject.AddComponent<BoxCollider>();
            estufaCol.center = new Vector3(0, 0.22f, 0);
            estufaCol.size = new Vector3(0.92f, 0.44f, 0.47f);
            var coxSvc = estufaNode.gameObject.AddComponent<CounterService>();
            coxSvc.service = Service.Coxinha;
            coxSvc.bartender = bartender;
            coxSvc.reach = 2.4f;
            var caixa = Node("Caixa", counterBody.transform.parent, new Vector3(-1.95f, 1.06f, -0.05f), 15f).transform;
            Box(caixa, "Gaveta", new Vector3(0, 0.04f, 0), new Vector3(0.3f, 0.08f, 0.28f), M("Metal Escuro"));
            Box(caixa, "Corpo", new Vector3(0, 0.12f, 0.03f), new Vector3(0.28f, 0.1f, 0.2f), M("Plástico Branco"), new Vector3(-15f, 0, 0));
            Box(caixa, "Visor", new Vector3(0, 0.22f, 0.08f), new Vector3(0.14f, 0.06f, 0.03f), M("Janela TV"));
            Box(caixa, "Caderninho do fiado", new Vector3(0.24f, 0.01f, 0.02f), new Vector3(0.12f, 0.015f, 0.17f), M("Plástico Vermelho"), new Vector3(0, 20f, 0));
            var caixaCol = caixa.gameObject.AddComponent<BoxCollider>();
            caixaCol.center = new Vector3(0.05f, 0.12f, 0);
            caixaCol.size = new Vector3(0.5f, 0.26f, 0.32f);
            caixa.gameObject.AddComponent<CounterService>().service = Service.PagarConta;

            // ---------------- banquetas do balcão (uma já tem dono)
            for (int i = 0; i < stools.Count; i++)
            {
                var st = stools[i];
                var c = st.gameObject.AddComponent<BoxCollider>();
                c.center = new Vector3(0, 0.37f, 0);
                c.size = new Vector3(0.38f, 0.74f, 0.38f);
                if (i == stools.Count - 1)
                {
                    var look = RandomLook();
                    look.skin = skins[3];
                    SeatedPatron(sb, st, roda, look, 0.75f);
                }
                else MakeSeat(st, "Sentar no balcão", 1.45f, 0.55f);
            }

            // ---------------- fregueses nas mesas da calçada; as cadeiras vagas são sentáveis
            int[] patronsPerTable = { 2, 1, 1 };
            for (int t = 0; t < streetChairs.Count; t++)
            {
                var chairs = streetChairs[t];
                for (int i = 0; i < chairs.Count; i++)
                {
                    if (i < patronsPerTable[t]) SeatedPatron(sb, chairs[i], roda, RandomLook());
                    else MakeSeat(chairs[i], "Sentar", 1.16f, 0.6f);
                }
            }

            // ---------------- o freguês jogável
            var spawn = Node("Chegada", sb, new Vector3(0.0f, 0.05f, -5.5f), 0f).transform;
            var playerGo = new GameObject("Freguês (jogador)");
            playerGo.transform.position = spawn.position;
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0, 0.875f, 0);
            cc.stepOffset = 0.32f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            var stepsSrc = playerGo.AddComponent<AudioSource>();
            stepsSrc.playOnAwake = false;
            stepsSrc.spatialBlend = 0f;
            stepsSrc.clip = Clip("sfx_passo");
            stepsSrc.volume = 0.7f;
            var player = playerGo.AddComponent<ButecoPlayer>();
            player.cam = camCache.cam;
            player.steps = stepsSrc;
            player.hands = BuildHands(camCache.cam.transform);

            var sandbox = new GameObject("Buteco (sandbox)").AddComponent<ButecoSandbox>();
            sandbox.player = player;
            sandbox.pool = gameCache;
            sandbox.poolCam = camCache;
            sandbox.ui = uiCache;
            sandbox.roda = roda;
            sandbox.spawn = spawn;
            player.sandbox = sandbox;
            gameCache.sandbox = sandbox;
            uiCache.sandbox = sandbox;
        }

        static PlayerHands BuildHands(Transform cam)
        {
            var root = Node("Mãos", cam, Vector3.zero).transform;
            var hands = root.gameObject.AddComponent<PlayerHands>();
            var skin = skins[2];

            var glass = Node("Copo", root, new Vector3(0.2f, -0.23f, 0.42f)).transform;
            Prism(glass, "Vidro", Vector3.zero, 8, 0.028f, 0.034f, 0.095f, M("Copo Americano"), null, false);
            var liquid = Node("Cerveja", glass, new Vector3(0, 0.004f, 0)).transform;
            Prism(liquid, "Líquido", Vector3.zero, 8, 0.026f, 0.031f, 0.075f, M("Cerveja"), null, false);
            var foam = Node("Espuma", glass, new Vector3(0, 0.08f, 0)).transform;
            Prism(foam, "Colarinho", Vector3.zero, 8, 0.0305f, 0.031f, 0.008f, M("Espuma"), null, false);
            Box(glass, "Mão", new Vector3(0.036f, 0.035f, -0.005f), new Vector3(0.05f, 0.075f, 0.065f), skin, null, null, false);

            var bottle = Beer(root, new Vector3(-0.24f, -0.31f, 0.5f));
            bottle.name = "Garrafa";
            Box(bottle, "Mão", new Vector3(-0.03f, 0.1f, 0), new Vector3(0.05f, 0.08f, 0.085f), skin, null, null, false);

            var cox = Node("Coxinha", root, new Vector3(0.19f, -0.2f, 0.38f)).transform;
            Prism(cox, "Massa", new Vector3(0, -0.03f, 0), 6, 0.034f, 0.0f, 0.075f, M("Salgado"), null, false);
            Box(cox, "Mão", new Vector3(0.03f, -0.03f, 0), new Vector3(0.045f, 0.06f, 0.06f), skin, null, null, false);

            var handL = Ico(root, "Mão esquerda", new Vector3(-0.1f, -0.2f, 0.38f), 0.045f, skin, new Vector3(0.6f, 1.1f, 1f), 0).transform;
            var handR = Ico(root, "Mão direita", new Vector3(0.1f, -0.2f, 0.38f), 0.045f, skin, new Vector3(0.6f, 1.1f, 1f), 0).transform;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            hands.glass = glass;
            hands.liquid = liquid;
            hands.foam = foam;
            hands.liquidHeight = 0.075f;
            hands.bottle = bottle;
            hands.coxinha = cox;
            hands.handL = handL;
            hands.handR = handR;
            var src = root.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            hands.source = src;
            hands.sipClip = Clip("sfx_gole");
            hands.pourClip = Clip("sfx_servir");
            hands.openClip = Clip("sfx_garrafa");
            hands.biteClip = Clip("sfx_mordida");
            hands.clapClip = Clip("sfx_palma");
            root.gameObject.SetActive(false);
            return hands;
        }
    }
}
