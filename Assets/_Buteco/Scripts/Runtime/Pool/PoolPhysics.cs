using System.Collections.Generic;
using UnityEngine;

namespace Buteco.Pool
{
    public struct Segment
    {
        public Vector2 a, b;
        public Segment(Vector2 a, Vector2 b) { this.a = a; this.b = b; }

        public Vector2 Closest(Vector2 p)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }
    }

    public enum PhysEventType { BallBall, Cushion, Pocket }

    public struct PhysEvent
    {
        public PhysEventType type;
        public int a, b;      // bolas (b = índice da caçapa em Pocket, -1 em Cushion)
        public float speed;   // velocidade normal do impacto
    }

    public struct AimHit
    {
        public bool hitBall;
        public int ball;
        public Vector2 contact;   // centro da branca no momento do contato
        public Vector2 objectDir; // para onde a bola atingida vai
        public Vector2 cueDir;    // para onde a branca desvia (linha tangente)
    }

    /// <summary>
    /// Simulação 2D da mesa (plano XZ da mesa → Vector2 x,y). Determinística, sem PhysX:
    /// facilita multiplayer online no futuro (basta sincronizar a tacada).
    /// </summary>
    public class PoolPhysics
    {
        public readonly float R;
        public readonly float HalfL, HalfW;
        public readonly int Count;

        public readonly Vector2[] pos;
        public readonly Vector2[] vel;
        public readonly bool[] onTable;

        public readonly List<Segment> cushions = new();
        public readonly List<Vector2> pockets = new();
        public readonly List<float> pocketRadius = new();
        public readonly List<PhysEvent> events = new();

        public float BallRestitution = 0.95f;
        public float CushionRestitution = 0.74f;
        public float RollDecel = 0.17f;   // m/s² (atrito de rolamento)
        public float DragK = 0.11f;       // 1/s   (perda proporcional à velocidade)
        public const float StopSpeed = 0.012f;
        const float SubDt = 1f / 720f;

        public PoolPhysics(int count, float radius, float halfLength, float halfWidth)
        {
            Count = count;
            R = radius;
            HalfL = halfLength;
            HalfW = halfWidth;
            pos = new Vector2[count];
            vel = new Vector2[count];
            onTable = new bool[count];
            BuildTable();
        }

        // ------------------------------------------------------------------ geometria

        public float CornerMouth => 2.9f * R;
        public float SideMouth => 2.3f * R;

        void BuildTable()
        {
            float hl = HalfL, hw = HalfW, cm = CornerMouth, sm = SideMouth, jaw = 2.6f * R;
            for (int s = -1; s <= 1; s += 2)
            {
                // tabelas longas (z = ±hw), interrompidas pela caçapa do meio
                float z = s * hw;
                AddRail(new Vector2(-hl + cm, z), new Vector2(-sm, z));
                AddRail(new Vector2(sm, z), new Vector2(hl - cm, z));
                AddRail(new Vector2(-sm, z), new Vector2(-sm, z + s * jaw));
                AddRail(new Vector2(sm, z), new Vector2(sm, z + s * jaw));
                AddRail(new Vector2(-hl + cm, z), new Vector2(-hl + cm, z) + new Vector2(-1, s).normalized * jaw);
                AddRail(new Vector2(hl - cm, z), new Vector2(hl - cm, z) + new Vector2(1, s).normalized * jaw);

                // tabelas curtas (x = ±hl)
                float x = s * hl;
                AddRail(new Vector2(x, -hw + cm), new Vector2(x, hw - cm));
                AddRail(new Vector2(x, hw - cm), new Vector2(x, hw - cm) + new Vector2(s, 1).normalized * jaw);
                AddRail(new Vector2(x, -hw + cm), new Vector2(x, -hw + cm) + new Vector2(s, -1).normalized * jaw);
            }

            // caçapas: 4 cantos + 2 do meio
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var mid = new Vector2(sx * (hl - cm * 0.5f), sz * (hw - cm * 0.5f));
                    pockets.Add(mid + new Vector2(sx, sz).normalized * (1.75f * R));
                    pocketRadius.Add(1.9f * R);
                }
            for (int s = -1; s <= 1; s += 2)
            {
                pockets.Add(new Vector2(0, s * (hw + 1.25f * R)));
                pocketRadius.Add(2.0f * R);
            }
        }

        void AddRail(Vector2 a, Vector2 b) => cushions.Add(new Segment(a, b));

        // ------------------------------------------------------------------ simulação

        public bool AnyMoving()
        {
            for (int i = 0; i < Count; i++)
                if (onTable[i] && vel[i].sqrMagnitude > 0f) return true;
            return false;
        }

        /// <summary>Avança a simulação. Eventos são acumulados em <see cref="events"/> (limpe você mesmo).</summary>
        public bool Simulate(float dt)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / SubDt));
            float h = dt / n;
            for (int k = 0; k < n; k++) SubStep(h);
            return AnyMoving();
        }

        void SubStep(float h)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!onTable[i]) continue;
                var v = vel[i];
                float sp = v.magnitude;
                if (sp <= 0f) continue;
                float dec = (RollDecel + DragK * sp) * h;
                if (sp - dec <= StopSpeed * 0.5f) v = Vector2.zero;
                else v *= (sp - dec) / sp;
                vel[i] = v;
                pos[i] += v * h;
            }

            float twoR = 2f * R;
            for (int i = 0; i < Count; i++)
            {
                if (!onTable[i]) continue;
                for (int j = i + 1; j < Count; j++)
                {
                    if (!onTable[j]) continue;
                    var d = pos[j] - pos[i];
                    float dist2 = d.sqrMagnitude;
                    if (dist2 >= twoR * twoR || dist2 < 1e-12f) continue;
                    float dist = Mathf.Sqrt(dist2);
                    var nrm = d / dist;
                    float overlap = twoR - dist;
                    pos[i] -= nrm * (overlap * 0.5f);
                    pos[j] += nrm * (overlap * 0.5f);
                    float vn = Vector2.Dot(vel[j] - vel[i], nrm);
                    if (vn < 0f)
                    {
                        float J = -(1f + BallRestitution) * vn * 0.5f;
                        vel[i] -= nrm * J;
                        vel[j] += nrm * J;
                        events.Add(new PhysEvent { type = PhysEventType.BallBall, a = i, b = j, speed = -vn });
                    }
                }
            }

            for (int i = 0; i < Count; i++)
            {
                if (!onTable[i]) continue;
                foreach (var seg in cushions)
                {
                    var c = seg.Closest(pos[i]);
                    var d = pos[i] - c;
                    float dist2 = d.sqrMagnitude;
                    if (dist2 >= R * R || dist2 < 1e-12f) continue;
                    float dist = Mathf.Sqrt(dist2);
                    var nrm = d / dist;
                    pos[i] = c + nrm * R;
                    float vn = Vector2.Dot(vel[i], nrm);
                    if (vn < 0f)
                    {
                        var vt = vel[i] - nrm * vn;
                        vel[i] = vt * 0.96f - nrm * (vn * CushionRestitution);
                        events.Add(new PhysEvent { type = PhysEventType.Cushion, a = i, b = -1, speed = -vn });
                    }
                }

                for (int p = 0; p < pockets.Count; p++)
                {
                    if ((pos[i] - pockets[p]).sqrMagnitude < pocketRadius[p] * pocketRadius[p])
                    {
                        Pocket(i, p);
                        break;
                    }
                }

                // rede de segurança: bola que escapou da mesa cai na caçapa mais próxima
                if (onTable[i] && (Mathf.Abs(pos[i].x) > HalfL + 3f * R || Mathf.Abs(pos[i].y) > HalfW + 3f * R))
                    Pocket(i, NearestPocket(pos[i]));
            }
        }

        void Pocket(int ball, int pocket)
        {
            events.Add(new PhysEvent { type = PhysEventType.Pocket, a = ball, b = pocket, speed = vel[ball].magnitude });
            onTable[ball] = false;
            vel[ball] = Vector2.zero;
            pos[ball] = pockets[pocket];
        }

        public int NearestPocket(Vector2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < pockets.Count; i++)
            {
                float d = (pockets[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Distância (com sinal ≥ 0) do ponto à tabela mais próxima.</summary>
        public float CushionDistance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var s in cushions) best = Mathf.Min(best, (p - s.Closest(p)).magnitude);
            return best;
        }

        public bool InsidePlayArea(Vector2 p, float margin)
        {
            return Mathf.Abs(p.x) <= HalfL - margin && Mathf.Abs(p.y) <= HalfW - margin;
        }

        public bool Overlaps(int self, Vector2 p)
        {
            for (int i = 0; i < Count; i++)
            {
                if (i == self || !onTable[i]) continue;
                if ((pos[i] - p).sqrMagnitude < (2f * R + 0.001f) * (2f * R + 0.001f)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ mira

        /// <summary>Traça a trajetória da bola <paramref name="cue"/> na direção dada até a primeira colisão.</summary>
        public AimHit CastBall(int cue, Vector2 dir, float maxDist = 5f)
        {
            dir.Normalize();
            var p = pos[cue];
            float bestT = maxDist;
            int bestBall = -1;
            float twoR = 2f * R;
            for (int i = 0; i < Count; i++)
            {
                if (i == cue || !onTable[i]) continue;
                var q = pos[i] - p;
                float along = Vector2.Dot(q, dir);
                if (along <= 0f) continue;
                float perp2 = q.sqrMagnitude - along * along;
                if (perp2 >= twoR * twoR) continue;
                float t = along - Mathf.Sqrt(twoR * twoR - perp2);
                if (t >= 0f && t < bestT) { bestT = t; bestBall = i; }
            }

            // sphere tracing contra as tabelas
            float tc = 0f;
            for (int it = 0; it < 256 && tc < bestT; it++)
            {
                var pt = p + dir * tc;
                float d = CushionDistance(pt) - R;
                if (d < 0.0005f) break;
                if (Mathf.Abs(pt.x) > HalfL + 4f * R || Mathf.Abs(pt.y) > HalfW + 4f * R) { tc = bestT + 1f; break; }
                tc += Mathf.Max(d, 0.0005f);
            }

            var hit = new AimHit { ball = -1 };
            if (bestBall >= 0 && bestT <= tc)
            {
                hit.hitBall = true;
                hit.ball = bestBall;
                hit.contact = p + dir * bestT;
                hit.objectDir = (pos[bestBall] - hit.contact).normalized;
                var tang = dir - hit.objectDir * Vector2.Dot(dir, hit.objectDir);
                hit.cueDir = tang.sqrMagnitude > 1e-6f ? tang.normalized : Vector2.zero;
            }
            else
            {
                hit.contact = p + dir * Mathf.Min(tc, maxDist);
            }
            return hit;
        }
    }
}
