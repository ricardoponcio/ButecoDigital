using System.Collections.Generic;
using UnityEngine;

namespace Buteco
{
    /// <summary>Gera malhas low-poly com sombreamento flat (vértices não compartilhados).</summary>
    public static class MeshKit
    {
        class Builder
        {
            public readonly List<Vector3> verts = new();
            public readonly List<Vector2> uvs = new();
            public readonly List<int> tris = new();

            // A face visível na Unity é a de normal Cross(b - a, c - a); o hint garante a orientação.
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector2 ua = default, Vector2 ub = default, Vector2 uc = default)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                {
                    (b, c) = (c, b);
                    (ub, uc) = (uc, ub);
                }
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            public void Quad(Vector3 origin, Vector3 u, Vector3 v, Vector3 outward, Vector2 uvSize)
            {
                var p0 = origin; var p1 = origin + u; var p2 = origin + u + v; var p3 = origin + v;
                var t0 = Vector2.zero; var t1 = new Vector2(uvSize.x, 0); var t2 = uvSize; var t3 = new Vector2(0, uvSize.y);
                Tri(p0, p1, p2, outward, t0, t1, t2);
                Tri(p0, p2, p3, outward, t0, t2, t3);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Caixa centrada na origem. UV em metros * uvPerMeter (bom para texturas que repetem).</summary>
        public static Mesh Box(Vector3 size, Vector2 uvPerMeter)
        {
            var b = new Builder();
            var h = size * 0.5f;
            float ux = uvPerMeter.x, uy = uvPerMeter.y;
            // ±Z (u = x, v = y)
            b.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.forward, new Vector2(size.x * ux, size.y * uy));
            b.Quad(new Vector3(h.x, -h.y, -h.z), new Vector3(-size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.back, new Vector2(size.x * ux, size.y * uy));
            // ±X (u = z, v = y)
            b.Quad(new Vector3(h.x, -h.y, h.z), new Vector3(0, 0, -size.z), new Vector3(0, size.y, 0), Vector3.right, new Vector2(size.z * ux, size.y * uy));
            b.Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), Vector3.left, new Vector2(size.z * ux, size.y * uy));
            // ±Y (u = x, v = z)
            b.Quad(new Vector3(-h.x, h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), Vector3.up, new Vector2(size.x * ux, size.z * uy));
            b.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, -size.z), Vector3.down, new Vector2(size.x * ux, size.z * uy));
            return b.Build($"Box_{size.x:0.###}x{size.y:0.###}x{size.z:0.###}");
        }

        /// <summary>Prisma/tronco de N lados com pivô no centro da base, crescendo em +Y.</summary>
        public static Mesh Prism(int sides, float rBottom, float rTop, float height, bool caps = true, float angleOffsetDeg = 0f)
        {
            var b = new Builder();
            float step = Mathf.PI * 2f / sides;
            float off = angleOffsetDeg * Mathf.Deg2Rad;
            var top = new Vector3(0, height, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = off + i * step, a1 = a0 + step;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                var b0 = d0 * rBottom; var b1 = d1 * rBottom;
                var t0 = d0 * rTop + top; var t1 = d1 * rTop + top;
                var outward = (d0 + d1).normalized;
                if (rTop > 0.0001f || rBottom > 0.0001f)
                {
                    if (rBottom > 0.0001f) b.Tri(b0, b1, t1, outward);
                    if (rTop > 0.0001f) b.Tri(b0, t1, t0, outward);
                }
                if (caps)
                {
                    if (rBottom > 0.0001f) b.Tri(Vector3.zero, b0, b1, Vector3.down);
                    if (rTop > 0.0001f) b.Tri(top, t0, t1, Vector3.up);
                }
            }
            return b.Build($"Prism_{sides}_{rBottom:0.###}_{rTop:0.###}_{height:0.###}");
        }

        /// <summary>Icosfera low-poly (flat) — boa para cabeças, lâmpadas, copas de árvore.</summary>
        public static Mesh IcoSphere(float radius, int subdivisions = 1)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            var f = new List<int[]>
            {
                new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
                new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
                new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1},
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int c)
                {
                    long key = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    v.Add(((v[a] + v[c]) * 0.5f).normalized);
                    cache[key] = v.Count - 1;
                    return v.Count - 1;
                }
                var nf = new List<int[]>();
                foreach (var tri in f)
                {
                    int a = Mid(tri[0], tri[1]), bb = Mid(tri[1], tri[2]), c = Mid(tri[2], tri[0]);
                    nf.Add(new[] { tri[0], a, c }); nf.Add(new[] { tri[1], bb, a });
                    nf.Add(new[] { tri[2], c, bb }); nf.Add(new[] { a, bb, c });
                }
                f = nf;
            }
            var b = new Builder();
            foreach (var tri in f)
            {
                var a = v[tri[0]] * radius; var bb = v[tri[1]] * radius; var c = v[tri[2]] * radius;
                b.Tri(a, bb, c, (a + bb + c).normalized);
            }
            return b.Build($"Ico_{radius:0.###}_{subdivisions}");
        }
    }
}
