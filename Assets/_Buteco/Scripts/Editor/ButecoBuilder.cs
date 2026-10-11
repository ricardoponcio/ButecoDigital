using System.Collections.Generic;
using Buteco.Pool;
using Buteco.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Buteco.EditorTools
{
    /// <summary>
    /// Monta a cena inteira do Buteco Digital de forma procedural (low-poly).
    /// Menu: Buteco → Construir Cena. Pode rodar quantas vezes quiser: recria a cena do zero.
    /// </summary>
    public static partial class ButecoBuilder
    {
        const string Root = "Assets/_Buteco";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Art/Textures";
        const string AudioDir = Root + "/Audio";
        const string ScenePath = Root + "/Scenes/Buteco.unity";

        static System.Random rng;
        static readonly Dictionary<string, Mesh> meshCache = new();
        static Dictionary<string, Material> mats;
        static readonly List<Material> paints = new();
        static readonly List<Material> doors = new();
        static readonly List<Material> clothes = new();
        static readonly List<GameObject> dynamicObjects = new();

        static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static bool Chance(float p) => rng.NextDouble() < p;
        static T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

        [MenuItem("Buteco/Construir Cena")]
        public static void Build()
        {
            rng = new System.Random(2026);
            meshCache.Clear();
            dynamicObjects.Clear();
            streetChairs.Clear();
            stools.Clear();
            AssetDatabase.Refresh();
            ConfigureImports();
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var world = new GameObject("Cenário").transform;
            BuildLighting(world);
            var bar = Node("Buteco", world, Vector3.zero).transform;
            BuildBarShell(bar);
            BuildBarInterior(bar);
            BuildFacade(bar);
            BuildStreet(Node("Rua e Viela", world, Vector3.zero).transform);
            BuildNeighborhood(Node("Comunidade", world, Vector3.zero).transform);

            var table = BuildPoolTable(Node("Mesa de Sinuca", null, new Vector3(-1.0f, 0f, 0.25f)).transform);
            BuildGame(table);
            BuildSandbox(world, table);

            MarkStatic(world);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = list.ToArray();
            PlayerSettings.productName = "Buteco Digital";
            PlayerSettings.companyName = "Buteco Digital";
            // multiplayer local: o jogo não pode congelar quando outra janela pega o foco
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[Buteco] Cena construída: " + ScenePath);
        }

        // =================================================================== importação

        static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                bool changed = false;
                bool repeat = path.Contains("piso") || path.Contains("azulejo") || path.Contains("tijolo") || path.Contains("ball_");
                var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (ti.wrapMode != wrap) { ti.wrapMode = wrap; changed = true; }
                if (ti.anisoLevel != 4) { ti.anisoLevel = 4; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter ai) continue;
                var s = ai.defaultSampleSettings;
                bool longClip = path.Contains("musica") || path.Contains("amb_") || path.Contains("roda_samba");
                var load = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (s.loadType != load)
                {
                    s.loadType = load;
                    s.compressionFormat = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                    s.quality = 0.6f;
                    ai.defaultSampleSettings = s;
                    ai.SaveAndReimport();
                }
            }
        }

        // =================================================================== materiais

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

        static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
        }

        static Material Lit(string name, Color c, float smooth = 0.2f, Texture2D tex = null, float metallic = 0f, Color? emission = null, bool emissionFromTex = false)
        {
            var m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BaseMap", tex);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.SetTexture("_EmissionMap", emissionFromTex ? tex : null);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.SetTexture("_EmissionMap", null);
            }
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Material Glass(string name, Color c, float smooth = 0.95f)
        {
            var m = Lit(name, c, smooth);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Shader shader, Color c)
        {
            var m = LoadOrCreate(name, shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Material M(string name) => mats[name];

        static void CreateMaterials()
        {
            mats = new Dictionary<string, Material>();
            paints.Clear(); doors.Clear(); clothes.Clear();
            var white = Color.white;

            Lit("Piso", white, 0.3f, Tex("piso_caquinho"));
            Lit("Azulejo", white, 0.75f, Tex("azulejo"));
            Lit("Faixa", white, 0.75f, Tex("azulejo_faixa"));
            Lit("Parede Verde", new Color(0.55f, 0.78f, 0.70f), 0.1f);
            Lit("Forro", new Color(0.93f, 0.91f, 0.86f), 0.1f);
            Lit("Fachada", new Color(0.97f, 0.76f, 0.30f), 0.05f);
            Lit("Tijolo", white, 0.05f, Tex("tijolo"));
            Lit("Concreto", new Color(0.62f, 0.61f, 0.58f), 0.05f);
            Lit("Concreto Escuro", new Color(0.42f, 0.41f, 0.40f), 0.05f);
            Lit("Calçada", new Color(0.70f, 0.68f, 0.63f), 0.05f);
            Lit("Asfalto", new Color(0.20f, 0.20f, 0.22f), 0.2f);
            Lit("Terra", new Color(0.42f, 0.33f, 0.26f), 0.02f);
            Lit("Metal", new Color(0.55f, 0.56f, 0.58f), 0.5f, null, 0.6f);
            Lit("Inox", new Color(0.80f, 0.81f, 0.82f), 0.75f, null, 0.85f);
            Lit("Metal Escuro", new Color(0.16f, 0.16f, 0.18f), 0.4f, null, 0.5f);
            Lit("Madeira", new Color(0.45f, 0.25f, 0.12f), 0.35f);
            Lit("Madeira Escura", new Color(0.22f, 0.11f, 0.06f), 0.5f);
            Lit("Madeira Clara", new Color(0.86f, 0.68f, 0.42f), 0.45f);
            Lit("Feltro", new Color(0.03f, 0.30f, 0.15f), 0.0f);
            Lit("Feltro Tabela", new Color(0.025f, 0.24f, 0.12f), 0.0f);
            Lit("Caçapa", new Color(0.01f, 0.01f, 0.01f), 0.1f);
            Lit("Couro", new Color(0.25f, 0.13f, 0.07f), 0.3f);
            Lit("Pérola", new Color(0.95f, 0.93f, 0.85f), 0.8f);
            Lit("Plástico Vermelho", new Color(0.82f, 0.10f, 0.10f), 0.45f);
            Lit("Plástico Amarelo", new Color(0.98f, 0.76f, 0.08f), 0.45f);
            Lit("Plástico Branco", new Color(0.95f, 0.95f, 0.93f), 0.45f);
            Lit("Vinil Vermelho", new Color(0.60f, 0.07f, 0.07f), 0.6f);
            Lit("Geladeira", new Color(0.80f, 0.10f, 0.10f), 0.6f, null, 0.2f);
            Lit("Vidro Geladeira", new Color(0.6f, 0.8f, 1f), 0.9f, null, 0f, new Color(0.45f, 0.65f, 0.95f) * 1.1f);
            Glass("Vidro", new Color(0.8f, 0.9f, 1f, 0.22f));
            Glass("Copo Americano", new Color(0.9f, 0.95f, 1f, 0.35f));
            Lit("Cerveja", new Color(0.95f, 0.65f, 0.12f), 0.8f, null, 0f, new Color(0.3f, 0.15f, 0f));
            Lit("Garrafa Âmbar", new Color(0.32f, 0.14f, 0.04f), 0.85f);
            Lit("Garrafa Verde", new Color(0.10f, 0.35f, 0.12f), 0.85f);
            Lit("Garrafa Clara", new Color(0.82f, 0.86f, 0.78f), 0.85f);
            Lit("Garrafa Azul", new Color(0.10f, 0.20f, 0.50f), 0.85f);
            Lit("Rótulo", new Color(0.95f, 0.90f, 0.75f), 0.2f);
            Lit("Tampinha", new Color(0.85f, 0.70f, 0.20f), 0.6f, null, 0.7f);
            Lit("Salgado", new Color(0.85f, 0.48f, 0.18f), 0.3f);
            Lit("Pastel", new Color(0.95f, 0.76f, 0.36f), 0.3f);
            Lit("Prato", new Color(0.97f, 0.97f, 0.95f), 0.6f);
            Lit("Luz Quente", new Color(1f, 0.9f, 0.7f), 0.5f, null, 0f, new Color(1f, 0.72f, 0.40f) * 3.5f);
            Lit("Luz Fria", new Color(0.95f, 0.98f, 1f), 0.5f, null, 0f, new Color(0.85f, 0.95f, 1f) * 3.5f);
            Lit("Luz Poste", new Color(1f, 0.8f, 0.5f), 0.5f, null, 0f, new Color(1f, 0.55f, 0.20f) * 5f);
            Lit("Luz Mesa", new Color(1f, 0.95f, 0.85f), 0.5f, null, 0f, new Color(1f, 0.85f, 0.6f) * 4f);
            Lit("Janela Acesa", new Color(1f, 0.82f, 0.55f), 0.6f, null, 0f, new Color(1f, 0.66f, 0.35f) * 1.5f);
            Lit("Janela Acesa 2", new Color(1f, 0.92f, 0.8f), 0.6f, null, 0f, new Color(1f, 0.85f, 0.65f) * 1.1f);
            Lit("Janela TV", new Color(0.6f, 0.75f, 1f), 0.6f, null, 0f, new Color(0.45f, 0.6f, 1f) * 1.0f);
            Lit("Janela Escura", new Color(0.07f, 0.09f, 0.13f), 0.92f);
            Lit("Moldura", new Color(0.92f, 0.92f, 0.90f), 0.3f);
            Lit("Grade", new Color(0.15f, 0.15f, 0.17f), 0.4f, null, 0.5f);
            Lit("Tela TV", Color.black, 0.9f, null, 0f, new Color(0.2f, 0.8f, 0.3f) * 1.5f);
            Lit("Preto", new Color(0.03f, 0.03f, 0.03f), 0.4f);
            Lit("Borracha", new Color(0.06f, 0.06f, 0.06f), 0.2f);
            Lit("Placa", white, 0.3f, Tex("placa_buteco"), 0f, new Color(0.85f, 0.85f, 0.85f), true);
            Lit("Lousa", white, 0.1f, Tex("lousa_cardapio"));
            Lit("Cartaz Samba", white, 0.2f, Tex("cartaz_samba"));
            Lit("Cartaz Torneio", white, 0.2f, Tex("cartaz_torneio"));
            Lit("Caixa d'Água", new Color(0.15f, 0.38f, 0.72f), 0.4f);
            Lit("Caramelo", new Color(0.80f, 0.52f, 0.24f), 0.15f);
            Lit("Caramelo Claro", new Color(0.94f, 0.78f, 0.55f), 0.15f);
            Lit("Folha", new Color(0.18f, 0.50f, 0.20f), 0.3f);
            Lit("Folha Escura", new Color(0.10f, 0.33f, 0.15f), 0.3f);
            Lit("Laranja", new Color(0.95f, 0.45f, 0.10f), 0.4f);
            Lit("Moto", new Color(0.75f, 0.08f, 0.10f), 0.7f, null, 0.3f);
            Lit("Giz Azul", new Color(0.2f, 0.45f, 0.85f), 0.1f);
            Lit("Taco Ponta", new Color(0.25f, 0.45f, 0.85f), 0.1f);
            Lit("Taco Anel", new Color(0.95f, 0.95f, 0.9f), 0.6f);
            Unlit("Fio", Shader.Find("Universal Render Pipeline/Unlit"), new Color(0.03f, 0.03f, 0.03f));
            Unlit("Linha Mira", Shader.Find("Sprites/Default"), Color.white);

            Color[] paintColors =
            {
                new(0.96f, 0.78f, 0.30f), new(0.93f, 0.55f, 0.60f), new(0.45f, 0.72f, 0.88f), new(0.55f, 0.80f, 0.55f),
                new(0.96f, 0.60f, 0.30f), new(0.72f, 0.60f, 0.85f), new(0.95f, 0.92f, 0.82f), new(0.40f, 0.65f, 0.62f),
                new(0.85f, 0.35f, 0.30f), new(0.98f, 0.85f, 0.70f), new(0.35f, 0.55f, 0.80f), new(0.80f, 0.85f, 0.35f),
            };
            for (int i = 0; i < paintColors.Length; i++) paints.Add(Lit($"Pintura {i:00}", paintColors[i], 0.05f));
            Color[] doorColors = { new(0.15f, 0.30f, 0.60f), new(0.15f, 0.45f, 0.25f), new(0.40f, 0.22f, 0.10f), new(0.45f, 0.46f, 0.48f), new(0.70f, 0.15f, 0.12f) };
            for (int i = 0; i < doorColors.Length; i++) doors.Add(Lit($"Porta {i:00}", doorColors[i], 0.4f, null, 0.3f));
            Color[] clothColors = { new(0.9f, 0.2f, 0.2f), new(0.2f, 0.5f, 0.9f), new(1f, 0.85f, 0.2f), new(0.95f, 0.95f, 0.95f), new(0.3f, 0.75f, 0.35f), new(0.9f, 0.45f, 0.75f), new(1f, 0.55f, 0.1f) };
            for (int i = 0; i < clothColors.Length; i++) clothes.Add(Lit($"Pano {i:00}", clothColors[i], 0.1f));

            for (int n = 0; n < 16; n++) Lit($"Bola {n:00}", Color.white, 0.88f, Tex($"ball_{n:00}"));
        }

        // =================================================================== primitivas

        static GameObject Node(string name, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        static Mesh CachedMesh(string key, System.Func<Mesh> make)
        {
            if (!meshCache.TryGetValue(key, out var m))
            {
                m = make();
                meshCache[key] = m;
            }
            return m;
        }

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3? euler = null, bool shadows = true)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Vector3? euler = null, Vector2? uvPerMeter = null, bool shadows = true)
        {
            var uv = uvPerMeter ?? Vector2.one;
            string key = $"box{size.x:0.000}_{size.y:0.000}_{size.z:0.000}_{uv.x:0.00}_{uv.y:0.00}";
            var mesh = CachedMesh(key, () => MeshKit.Box(size, uv));
            return MeshObj(name, parent, mesh, mat, center, euler, shadows);
        }

        static GameObject Prism(Transform parent, string name, Vector3 basePos, int sides, float rBottom, float rTop, float height, Material mat, Vector3? euler = null, bool shadows = true)
        {
            string key = $"prism{sides}_{rBottom:0.0000}_{rTop:0.0000}_{height:0.0000}";
            var mesh = CachedMesh(key, () => MeshKit.Prism(sides, rBottom, rTop, height));
            return MeshObj(name, parent, mesh, mat, basePos, euler, shadows);
        }

        static GameObject Ico(Transform parent, string name, Vector3 center, float radius, Material mat, Vector3? scale = null, int sub = 1)
        {
            string key = $"ico{radius:0.000}_{sub}";
            var mesh = CachedMesh(key, () => MeshKit.IcoSphere(radius, sub));
            var go = MeshObj(name, parent, mesh, mat, center);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            return go;
        }

        /// <summary>Quad com a face visível apontando para <paramref name="facing"/> (espaço local do pai).</summary>
        static GameObject Quad(Transform parent, string name, Vector3 center, Vector2 size, Material mat, Vector3 facing)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
        {
            var go = Node(name, parent, pos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        static Light SpotLight(Transform parent, string name, Vector3 pos, Vector3 euler, Color color, float intensity, float range, float angle, bool shadows)
        {
            var go = Node(name, parent, pos);
            go.transform.localEulerAngles = euler;
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        /// <summary>Fio de poste com barriga (catenária aproximada).</summary>
        static void Wire(Transform parent, Vector3 a, Vector3 b, float sag, float width = 0.018f)
        {
            var go = new GameObject("Fio", typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = M("Fio");
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            const int n = 14;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                lr.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t)));
            }
        }

        // =================================================================== luz e atmosfera

        static void BuildLighting(Transform world)
        {
            var sunGo = Node("Sol de fim de tarde", world, new Vector3(0, 20, 0));
            sunGo.transform.rotation = Quaternion.Euler(9f, 25f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.60f, 0.36f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            var sky = LoadOrCreate("Céu Entardecer", Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_SunSizeConvergence", 4f);
            sky.SetFloat("_AtmosphereThickness", 1.45f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.45f, 0.62f));
            sky.SetColor("_GroundColor", new Color(0.30f, 0.24f, 0.24f));
            sky.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.40f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.36f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.11f, 0.10f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.58f, 0.46f, 0.52f);
            RenderSettings.fogDensity = 0.012f;

            var ls = new LightingSettings { name = "Buteco Lighting", bakedGI = false, realtimeGI = false };
            const string lsPath = Root + "/Scenes/ButecoLighting.lighting";
            AssetDatabase.DeleteAsset(lsPath);
            AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = ls;

            // pós-processamento
            const string volPath = Root + "/Scenes/ButecoVolume.asset";
            AssetDatabase.DeleteAsset(volPath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, volPath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(8f);
            color.saturation.Override(14f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);

            var volGo = Node("Pós-processamento", world, Vector3.zero);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        // =================================================================== o buteco

        const float X0 = -4.5f, X1 = 4.5f, Z0 = -3.5f, Z1 = 3.5f, H = 3.2f;

        static void BuildBarShell(Transform bar)
        {
            var shell = Node("Estrutura", bar, Vector3.zero).transform;
            var tileUV = new Vector2(1f / 0.6f, 1f / 0.6f);
            var faixaUV = new Vector2(1f / 0.32f, 1f / 0.08f);

            Solid(Box(shell, "Piso de caquinho", new Vector3(0, -0.05f, 0), new Vector3(9.4f, 0.1f, 7.4f), M("Piso"), null, new Vector2(0.5f, 0.5f)));
            Box(shell, "Forro", new Vector3(0, H + 0.05f, 0), new Vector3(9.4f, 0.1f, 7.4f), M("Forro"));

            // parede do fundo (z+) e esquerda (x-): meia parede de azulejo + faixa + tinta verde-água
            Solid(Box(shell, "Azulejo fundo", new Vector3(0, 0.7f, Z1 + 0.1f), new Vector3(9.4f, 1.4f, 0.2f), M("Azulejo"), null, tileUV));
            Box(shell, "Faixa fundo", new Vector3(0, 1.44f, Z1 - 0.006f), new Vector3(9.0f, 0.08f, 0.012f), M("Faixa"), null, faixaUV);
            Solid(Box(shell, "Parede fundo", new Vector3(0, 2.3f, Z1 + 0.1f), new Vector3(9.4f, 1.8f, 0.2f), M("Parede Verde")));
            Solid(Box(shell, "Azulejo lateral", new Vector3(X0 - 0.1f, 0.7f, 0), new Vector3(0.2f, 1.4f, 7.4f), M("Azulejo"), null, tileUV));
            Box(shell, "Faixa lateral", new Vector3(X0 + 0.006f, 1.44f, 0), new Vector3(0.012f, 0.08f, 7.0f), M("Faixa"), null, faixaUV);
            Solid(Box(shell, "Parede lateral", new Vector3(X0 - 0.1f, 2.3f, 0), new Vector3(0.2f, 1.8f, 7.4f), M("Parede Verde")));

            // esquina aberta: frente (z-) e lado direito (x+) com portas de aço recolhidas
            Solid(Box(shell, "Parede frente esq.", new Vector3(-4.15f, 1.6f, Z0 - 0.1f), new Vector3(1.1f, 3.2f, 0.2f), M("Fachada")));
            Solid(Box(shell, "Parede lado fundo", new Vector3(X1 + 0.1f, 1.6f, 2.85f), new Vector3(0.2f, 3.2f, 1.7f), M("Fachada")));
            Solid(Box(shell, "Pilar da esquina", new Vector3(X1 + 0.1f, 1.6f, Z0 - 0.1f), new Vector3(0.32f, 3.2f, 0.32f), M("Fachada")));
            Box(shell, "Viga frente", new Vector3(0.45f, 2.95f, Z0 - 0.1f), new Vector3(8.3f, 0.5f, 0.25f), M("Fachada"));
            Box(shell, "Viga lado", new Vector3(X1 + 0.1f, 2.95f, -0.75f), new Vector3(0.25f, 0.5f, 5.5f), M("Fachada"));
            Box(shell, "Porta de aço frente", new Vector3(0.45f, 2.62f, Z0 + 0.02f), new Vector3(8.0f, 0.2f, 0.14f), M("Metal"));
            Box(shell, "Porta de aço lado", new Vector3(X1 - 0.02f, 2.62f, -0.75f), new Vector3(0.14f, 0.2f, 5.3f), M("Metal"));
            foreach (float x in new[] { -3.55f, 4.3f })
                Box(shell, "Trilho porta", new Vector3(x, 1.3f, Z0 - 0.02f), new Vector3(0.05f, 2.6f, 0.06f), M("Metal"));
            foreach (float z in new[] { -3.3f, 1.95f })
                Box(shell, "Trilho porta", new Vector3(X1 + 0.02f, 1.3f, z), new Vector3(0.06f, 2.6f, 0.05f), M("Metal"));
            // rodapé/degrau da entrada
            Box(shell, "Soleira", new Vector3(0.45f, 0.01f, Z0 - 0.02f), new Vector3(8.1f, 0.02f, 0.12f), M("Concreto Escuro"));
        }

        static void BuildBarInterior(Transform bar)
        {
            var it = Node("Interior", bar, Vector3.zero).transform;

            // ---------------- balcão de azulejo com tampo inox
            var counter = Node("Balcão", it, new Vector3(1.4f, 0, 2.6f)).transform;
            counterBody = Solid(Box(counter, "Corpo", new Vector3(0, 0.5f, 0), new Vector3(4.6f, 1.0f, 0.55f), M("Azulejo"), null, new Vector2(1f / 0.6f, 1f / 0.6f)));
            Box(counter, "Tampo inox", new Vector3(0, 1.03f, -0.05f), new Vector3(4.8f, 0.06f, 0.7f), M("Inox"));
            Box(counter, "Rodapé", new Vector3(0, 0.05f, -0.29f), new Vector3(4.6f, 0.1f, 0.04f), M("Madeira Escura"));

            // estufa de salgados
            var estufa = Node("Estufa", counter, new Vector3(1.25f, 1.06f, -0.1f)).transform;
            estufaNode = estufa;
            Box(estufa, "Base", new Vector3(0, 0.025f, 0), new Vector3(0.9f, 0.05f, 0.45f), M("Inox"));
            Box(estufa, "Vidro", new Vector3(0, 0.23f, 0), new Vector3(0.9f, 0.36f, 0.45f), M("Vidro"), null, null, false);
            Box(estufa, "Tampa", new Vector3(0, 0.42f, 0), new Vector3(0.92f, 0.03f, 0.47f), M("Inox"));
            Box(estufa, "Prateleira", new Vector3(0, 0.22f, 0), new Vector3(0.86f, 0.01f, 0.41f), M("Inox"));
            for (int i = 0; i < 6; i++)
                Prism(estufa, "Coxinha", new Vector3(-0.33f + i * 0.13f, 0.05f, Rf(-0.08f, 0.08f)), 6, 0.035f, 0f, 0.075f, M("Salgado"));
            for (int i = 0; i < 4; i++)
                Box(estufa, "Pastel", new Vector3(-0.3f + i * 0.2f, 0.24f, Rf(-0.05f, 0.05f)), new Vector3(0.14f, 0.025f, 0.08f), M("Pastel"), new Vector3(0, Rf(-20, 20), 0));
            PointLight(estufa, "Luz da estufa", new Vector3(0, 0.36f, 0), new Color(1f, 0.75f, 0.4f), 0.6f, 0.9f);

            // copos e garrafas no balcão
            Beer(counter, new Vector3(-1.6f, 1.06f, -0.15f));
            Glass(counter, new Vector3(-1.45f, 1.06f, -0.2f), true);
            Glass(counter, new Vector3(-0.2f, 1.06f, -0.22f), true);
            Glass(counter, new Vector3(0.5f, 1.06f, -0.12f), false);
            Box(counter, "Paliteiro", new Vector3(0.2f, 1.1f, -0.1f), new Vector3(0.05f, 0.08f, 0.05f), M("Madeira Clara"));

            // ---------------- banquetas
            foreach (float x in new[] { -0.3f, 0.6f, 1.5f, 2.4f })
            {
                var st = Node("Banqueta", it, new Vector3(x, 0, 1.95f)).transform;
                stools.Add(st);
                Prism(st, "Pé", Vector3.zero, 6, 0.035f, 0.025f, 0.68f, M("Metal"));
                Prism(st, "Base", Vector3.zero, 8, 0.2f, 0.18f, 0.03f, M("Metal"));
                Prism(st, "Aro", new Vector3(0, 0.25f, 0), 8, 0.16f, 0.16f, 0.02f, M("Metal"));
                Prism(st, "Assento", new Vector3(0, 0.68f, 0), 10, 0.18f, 0.17f, 0.07f, M("Vinil Vermelho"));
            }

            // ---------------- prateleiras de garrafas
            var shelves = Node("Prateleiras", it, new Vector3(1.4f, 0, Z1 - 0.13f)).transform;
            string[] bottleMats = { "Garrafa Âmbar", "Garrafa Verde", "Garrafa Clara", "Garrafa Azul", "Garrafa Clara", "Garrafa Âmbar" };
            foreach (float y in new[] { 1.55f, 1.95f, 2.35f })
            {
                Box(shelves, "Prateleira", new Vector3(0, y, 0), new Vector3(4.4f, 0.03f, 0.25f), M("Madeira"));
                float x = -2.1f;
                while (x < 2.1f)
                {
                    float r = Rf(0.03f, 0.045f);
                    float h = Rf(0.2f, 0.32f);
                    var b = Node("Garrafa", shelves, new Vector3(x + r, y + 0.015f, Rf(-0.05f, 0.05f))).transform;
                    var mat = M(Pick(bottleMats));
                    Prism(b, "Corpo", Vector3.zero, 8, r, r, h, mat);
                    Prism(b, "Ombro", new Vector3(0, h, 0), 8, r, r * 0.35f, h * 0.18f, mat);
                    Prism(b, "Gargalo", new Vector3(0, h * 1.18f, 0), 6, r * 0.33f, r * 0.3f, h * 0.22f, mat);
                    if (Chance(0.7f)) Prism(b, "Rótulo", new Vector3(0, h * 0.3f, 0), 8, r * 1.04f, r * 1.04f, h * 0.35f, M("Rótulo"));
                    x += r * 2f + Rf(0.01f, 0.06f);
                }
            }
            for (int i = -2; i <= 2; i++)
                Box(shelves, "Mão-francesa", new Vector3(i * 1.0f, 1.9f, 0.08f), new Vector3(0.03f, 0.9f, 0.08f), M("Madeira Escura"));

            // rádio tocando pagode
            var radio = Node("Rádio", shelves, new Vector3(1.8f, 2.365f, 0)).transform;
            Box(radio, "Caixa", new Vector3(0, 0.1f, 0), new Vector3(0.34f, 0.2f, 0.13f), M("Moto"));
            Box(radio, "Alto-falante", new Vector3(-0.08f, 0.1f, -0.066f), new Vector3(0.13f, 0.13f, 0.005f), M("Preto"));
            Box(radio, "Dial", new Vector3(0.09f, 0.13f, -0.066f), new Vector3(0.11f, 0.05f, 0.005f), M("Luz Quente"));
            Prism(radio, "Antena", new Vector3(0.14f, 0.2f, 0), 4, 0.004f, 0.002f, 0.35f, M("Inox"), new Vector3(0, 0, -25f));
            // hoje o som é ao vivo: o rádio fica desligado (a trilha dele virou repertório da roda)

            // ---------------- geladeira de cerveja com porta de vidro e freezer
            var fridge = Node("Geladeira", it, new Vector3(-1.75f, 0, 3.08f)).transform;
            Solid(Box(fridge, "Corpo", new Vector3(0, 0.98f, 0), new Vector3(0.76f, 1.96f, 0.68f), M("Geladeira")));
            Box(fridge, "Porta de vidro", new Vector3(0, 1.0f, -0.345f), new Vector3(0.62f, 1.55f, 0.01f), M("Vidro Geladeira"), null, null, false);
            Box(fridge, "Logo", new Vector3(0, 1.88f, -0.345f), new Vector3(0.6f, 0.1f, 0.01f), M("Plástico Branco"));
            for (int row = 0; row < 4; row++)
                for (int c = 0; c < 6; c++)
                    Prism(fridge, "Long neck", new Vector3(-0.25f + c * 0.1f, 0.32f + row * 0.38f, -0.29f), 6, 0.028f, 0.012f, 0.24f, M("Garrafa Âmbar"), null, false);
            PointLight(fridge, "Luz da geladeira", new Vector3(0, 1.1f, -0.7f), new Color(0.6f, 0.8f, 1f), 0.8f, 1.8f);

            var freezer = Node("Freezer", it, new Vector3(-3.0f, 0, 3.1f)).transform;
            Solid(Box(freezer, "Corpo", new Vector3(0, 0.43f, 0), new Vector3(1.2f, 0.86f, 0.66f), M("Plástico Branco")));
            Box(freezer, "Tampa", new Vector3(0, 0.875f, 0), new Vector3(1.22f, 0.03f, 0.68f), M("Vidro Geladeira"));
            Box(freezer, "Placa sorvete", new Vector3(0, 0.55f, -0.335f), new Vector3(0.8f, 0.3f, 0.01f), M("Plástico Amarelo"));

            // ---------------- TV no alto, passando futebol
            var tv = Node("TV", it, new Vector3(-4.0f, 2.45f, 3.05f), 40f).transform;
            Box(tv, "Suporte", new Vector3(0, 0.35f, 0.1f), new Vector3(0.06f, 0.5f, 0.06f), M("Metal Escuro"));
            Box(tv, "Corpo", Vector3.zero, new Vector3(0.95f, 0.58f, 0.09f), M("Preto"));
            var screen = Box(tv, "Tela", new Vector3(0, 0, -0.047f), new Vector3(0.87f, 0.5f, 0.005f), M("Tela TV"), null, null, false);
            var tvLight = PointLight(tv, "Brilho", new Vector3(0, -0.2f, -0.6f), new Color(0.3f, 0.9f, 0.4f), 0.7f, 2.8f);
            var tvs = tv.gameObject.AddComponent<TvScreen>();
            tvs.screen = screen.GetComponent<Renderer>();
            tvs.glow = tvLight;
            dynamicObjects.Add(tv.gameObject);

            // ---------------- ventilador de teto
            var fan = Node("Ventilador", it, new Vector3(2.4f, H, -1.0f)).transform;
            Prism(fan, "Haste", new Vector3(0, -0.32f, 0), 6, 0.018f, 0.018f, 0.32f, M("Metal"));
            Prism(fan, "Motor", new Vector3(0, -0.42f, 0), 10, 0.11f, 0.09f, 0.12f, M("Plástico Branco"));
            var blades = Node("Pás", fan, new Vector3(0, -0.4f, 0)).transform;
            for (int i = 0; i < 3; i++)
            {
                var arm = Node("Pá", blades, Vector3.zero, i * 120f).transform;
                Box(arm, "Lâmina", new Vector3(0.38f, 0, 0), new Vector3(0.6f, 0.012f, 0.13f), M("Madeira Clara"), new Vector3(8f, 0, 0));
            }
            blades.gameObject.AddComponent<Spinner>().degreesPerSecond = 260f;
            dynamicObjects.Add(fan.gameObject);

            // ---------------- luminárias fluorescentes
            int tubeIndex = 0;
            foreach (var p in new[] { new Vector3(2.4f, H - 0.03f, 0.6f), new Vector3(1.4f, H - 0.03f, 2.9f), new Vector3(-3.5f, H - 0.03f, -1.6f) })
            {
                var lum = Node("Fluorescente", it, p).transform;
                Box(lum, "Calha", Vector3.zero, new Vector3(1.25f, 0.04f, 0.12f), M("Plástico Branco"));
                var tube = Box(lum, "Tubo", new Vector3(0, -0.035f, 0), new Vector3(1.2f, 0.03f, 0.03f), M("Luz Fria"), null, null, false);
                var l = PointLight(lum, "Luz", new Vector3(0, -0.2f, 0), new Color(0.88f, 0.95f, 1f), 1.6f, 5.5f);
                if (tubeIndex++ == 2)
                {
                    var fl = lum.gameObject.AddComponent<LightFlicker>();
                    fl.target = l;
                    fl.emissiveRenderer = tube.GetComponent<Renderer>();
                    fl.flickerChance = 0.08f;
                    dynamicObjects.Add(lum.gameObject);
                }
            }

            // (o canto das mesas de plástico agora é da roda de pagode — ver ButecoBuilder.Sandbox.cs)

            // ---------------- engradados
            var crates = Node("Engradados", it, new Vector3(4.05f, 0, 3.05f)).transform;
            for (int i = 0; i < 4; i++)
                Crate(crates, new Vector3(0, i * 0.3f, 0), i % 2 == 0 ? M("Plástico Amarelo") : M("Plástico Vermelho"), Rf(-8f, 8f), i == 3);
            Crate(crates, new Vector3(0, 0, -0.42f), M("Plástico Vermelho"), 5f, true);
            Blocker(crates, "Colisão engradados", new Vector3(0, 0.6f, -0.2f), new Vector3(0.6f, 1.2f, 0.9f));

            // ---------------- parede esquerda: taqueira, marcador, cardápio, cartazes
            var wall = Node("Parede da sinuca", it, new Vector3(X0, 0, 0)).transform;
            Box(wall, "Taqueira", new Vector3(0.03f, 1.2f, 0.25f), new Vector3(0.04f, 0.08f, 0.75f), M("Madeira Escura"));
            Box(wall, "Taqueira base", new Vector3(0.06f, 0.1f, 0.25f), new Vector3(0.1f, 0.04f, 0.75f), M("Madeira Escura"));
            for (int i = 0; i < 4; i++)
            {
                var c = Node("Taco na parede", wall, new Vector3(0.07f, 0.12f, -0.05f + i * 0.2f)).transform;
                c.localEulerAngles = new Vector3(0, 0, -4f);
                Prism(c, "Corpo", Vector3.zero, 8, 0.016f, 0.008f, 1.45f, i % 2 == 0 ? M("Madeira Clara") : M("Madeira"));
                Prism(c, "Cabo", Vector3.zero, 8, 0.017f, 0.016f, 0.5f, M("Madeira Escura"));
            }
            var score = Node("Marcador", wall, new Vector3(0.04f, 2.15f, 0.25f)).transform;
            Box(score, "Tábua", Vector3.zero, new Vector3(0.03f, 0.35f, 1.0f), M("Madeira"));
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? 0.07f : -0.07f;
                Prism(score, "Arame", new Vector3(0.03f, y, -0.48f), 4, 0.003f, 0.003f, 0.96f, M("Inox"), new Vector3(90f, 0, 0));
                int beads = 6 + rng.Next(5);
                for (int b = 0; b < 20; b++)
                {
                    float z = b < beads ? -0.45f + b * 0.03f : 0.45f - (19 - b) * 0.03f;
                    Ico(score, "Conta", new Vector3(0.03f, y, z), 0.014f, row == 0 ? M("Plástico Amarelo") : M("Plástico Vermelho"), null, 0);
                }
            }
            Box(wall, "Moldura lousa", new Vector3(0.02f, 1.85f, -1.6f), new Vector3(0.03f, 1.05f, 0.85f), M("Madeira"));
            Quad(wall, "Cardápio", new Vector3(0.04f, 1.85f, -1.6f), new Vector2(0.8f, 1.0f), M("Lousa"), Vector3.right);
            Quad(wall, "Cartaz roda de samba", new Vector3(0.012f, 1.95f, 1.45f), new Vector2(0.45f, 0.6f), M("Cartaz Samba"), Vector3.right);
            Quad(wall, "Cartaz torneio", new Vector3(0.012f, 1.95f, 2.15f), new Vector2(0.45f, 0.6f), M("Cartaz Torneio"), Vector3.right);

            // relógio no fundo
            Prism(it, "Relógio", new Vector3(-1.75f, 2.65f, Z1 - 0.01f), 16, 0.17f, 0.17f, 0.03f, M("Plástico Branco"), new Vector3(90f, 0, 0));
            Box(it, "Ponteiro", new Vector3(-1.75f, 2.68f, Z1 - 0.045f), new Vector3(0.01f, 0.11f, 0.005f), M("Preto"));
            Box(it, "Ponteiro", new Vector3(-1.72f, 2.64f, Z1 - 0.045f), new Vector3(0.08f, 0.01f, 0.005f), M("Preto"), new Vector3(0, 0, -20f));

            // plantinhas na entrada
            Plant(it, new Vector3(-3.3f, 0, Z0 + 0.35f));
            Plant(it, new Vector3(X1 - 0.35f, 0, 1.6f));
        }

        static Transform Beer(Transform parent, Vector3 pos)
        {
            var b = Node("Cerveja 600", parent, pos).transform;
            Prism(b, "Corpo", Vector3.zero, 8, 0.038f, 0.038f, 0.2f, M("Garrafa Âmbar"));
            Prism(b, "Ombro", new Vector3(0, 0.2f, 0), 8, 0.038f, 0.014f, 0.05f, M("Garrafa Âmbar"));
            Prism(b, "Gargalo", new Vector3(0, 0.25f, 0), 6, 0.014f, 0.013f, 0.06f, M("Garrafa Âmbar"));
            Prism(b, "Rótulo", new Vector3(0, 0.06f, 0), 8, 0.04f, 0.04f, 0.08f, M("Rótulo"));
            return b;
        }

        /// <summary>O clássico copo americano (com ou sem cerveja).</summary>
        static Transform Glass(Transform parent, Vector3 pos, bool full)
        {
            var g = Node("Copo americano", parent, pos).transform;
            if (full) Prism(g, "Cerveja", new Vector3(0, 0.004f, 0), 8, 0.026f, 0.031f, 0.075f, M("Cerveja"));
            Prism(g, "Vidro", Vector3.zero, 8, 0.028f, 0.034f, 0.095f, M("Copo Americano"), null, false);
            return g;
        }

        static List<Transform> PlasticTable(Transform parent, Vector3 pos, Material mat, float yaw, bool withChairs = true)
        {
            var chairs = new List<Transform>();
            var t = Node("Mesa de plástico", parent, pos, yaw).transform;
            Blocker(t, "Colisão mesa", new Vector3(0, 0.37f, 0), new Vector3(0.72f, 0.74f, 0.72f));
            Box(t, "Tampo", new Vector3(0, 0.72f, 0), new Vector3(0.7f, 0.035f, 0.7f), mat);
            Box(t, "Saia", new Vector3(0, 0.68f, 0), new Vector3(0.66f, 0.06f, 0.66f), mat);
            foreach (var c in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                Box(t, "Perna", new Vector3(c.x * 0.29f, 0.34f, c.y * 0.29f), new Vector3(0.05f, 0.68f, 0.05f), mat, new Vector3(c.y * 3f, 0, -c.x * 3f));
            Beer(t, new Vector3(Rf(-0.1f, 0.1f), 0.74f, Rf(-0.1f, 0.1f)));
            Glass(t, new Vector3(0.18f, 0.74f, 0.12f), Chance(0.7f));
            Glass(t, new Vector3(-0.16f, 0.74f, -0.15f), Chance(0.5f));
            if (Chance(0.6f))
            {
                Prism(t, "Prato", new Vector3(-0.12f, 0.74f, 0.15f), 12, 0.11f, 0.12f, 0.015f, M("Prato"));
                for (int i = 0; i < 5; i++)
                    Box(t, "Torresmo", new Vector3(-0.12f + Rf(-0.05f, 0.05f), 0.765f, 0.15f + Rf(-0.05f, 0.05f)), new Vector3(0.04f, 0.025f, 0.03f), M("Salgado"), new Vector3(0, Rf(0, 90), 0));
            }
            if (!withChairs) return chairs;
            for (int i = 0; i < 4; i++)
            {
                if (Chance(0.2f)) continue;
                float a = i * 90f + Rf(-18f, 18f);
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                chairs.Add(Chair(t, -dir * Rf(0.55f, 0.62f), a, mat));
            }
            return chairs;
        }

        static Transform Chair(Transform parent, Vector3 pos, float yaw, Material mat)
        {
            var c = Node("Cadeira de plástico", parent, pos, yaw).transform;
            var col = c.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.45f, -0.02f);
            col.size = new Vector3(0.46f, 0.9f, 0.46f);
            Box(c, "Assento", new Vector3(0, 0.44f, 0), new Vector3(0.42f, 0.035f, 0.42f), mat);
            foreach (var k in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                Box(c, "Perna", new Vector3(k.x * 0.18f, 0.22f, k.y * 0.18f), new Vector3(0.04f, 0.45f, 0.04f), mat, new Vector3(k.y * 5f, 0, -k.x * 5f));
            Box(c, "Encosto", new Vector3(0, 0.7f, -0.21f), new Vector3(0.42f, 0.38f, 0.035f), mat, new Vector3(-10f, 0, 0));
            Box(c, "Braço", new Vector3(0.21f, 0.6f, -0.02f), new Vector3(0.035f, 0.035f, 0.38f), mat);
            Box(c, "Braço", new Vector3(-0.21f, 0.6f, -0.02f), new Vector3(0.035f, 0.035f, 0.38f), mat);
            return c;
        }

        static void Crate(Transform parent, Vector3 pos, Material mat, float yaw, bool withBottles)
        {
            var c = Node("Engradado", parent, pos, yaw).transform;
            Box(c, "Fundo", new Vector3(0, 0.02f, 0), new Vector3(0.5f, 0.04f, 0.36f), mat);
            Box(c, "Lado", new Vector3(0, 0.15f, 0.17f), new Vector3(0.5f, 0.3f, 0.025f), mat);
            Box(c, "Lado", new Vector3(0, 0.15f, -0.17f), new Vector3(0.5f, 0.3f, 0.025f), mat);
            Box(c, "Lado", new Vector3(0.24f, 0.15f, 0), new Vector3(0.025f, 0.3f, 0.36f), mat);
            Box(c, "Lado", new Vector3(-0.24f, 0.15f, 0), new Vector3(0.025f, 0.3f, 0.36f), mat);
            if (!withBottles) return;
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 3; z++)
                {
                    if (Chance(0.15f)) continue;
                    Prism(c, "Garrafa", new Vector3(-0.18f + x * 0.12f, 0.04f, -0.11f + z * 0.11f), 6, 0.036f, 0.014f, 0.32f, M("Garrafa Âmbar"));
                }
        }

        static void Plant(Transform parent, Vector3 pos)
        {
            var p = Node("Comigo-ninguém-pode", parent, pos).transform;
            Blocker(p, "Colisão planta", new Vector3(0, 0.4f, 0), new Vector3(0.4f, 0.8f, 0.4f));
            Prism(p, "Lata de tinta", Vector3.zero, 10, 0.15f, 0.15f, 0.32f, M("Metal"));
            Prism(p, "Terra", new Vector3(0, 0.3f, 0), 10, 0.14f, 0.14f, 0.02f, M("Terra"));
            for (int i = 0; i < 7; i++)
            {
                var leaf = Node("Folha", p, new Vector3(0, 0.3f, 0), i * 51f + Rf(-10, 10)).transform;
                Box(leaf, "Lâmina", new Vector3(0, 0.22f, 0.1f), new Vector3(0.1f, 0.4f, 0.01f), i % 2 == 0 ? M("Folha") : M("Folha Escura"), new Vector3(Rf(20f, 40f), 0, 0));
            }
        }

        // =================================================================== fachada e andares de cima

        static void BuildFacade(Transform bar)
        {
            var f = Node("Fachada e casa de cima", bar, Vector3.zero).transform;

            // marquise com letreiro
            Box(f, "Marquise", new Vector3(0.45f, 3.35f, Z0 - 0.75f), new Vector3(8.6f, 0.12f, 1.3f), M("Concreto"));
            Box(f, "Marquise lado", new Vector3(X1 + 0.65f, 3.35f, -0.75f), new Vector3(1.1f, 0.12f, 5.4f), M("Concreto"));
            Box(f, "Letreiro", new Vector3(0.45f, 3.82f, Z0 - 1.3f), new Vector3(3.1f, 0.82f, 0.08f), M("Metal Escuro"));
            Quad(f, "Letreiro BUTECO DIGITAL", new Vector3(0.45f, 3.82f, Z0 - 1.345f), new Vector2(3.0f, 0.75f), M("Placa"), Vector3.back);
            SpotLight(f, "Luz do letreiro", new Vector3(0.45f, 3.3f, Z0 - 2.1f), new Vector3(-35f, 0, 0), new Color(1f, 0.85f, 0.6f), 2.5f, 3f, 60f, false);
            // lâmpadas sob a marquise
            foreach (var p in new[] { new Vector3(-2.2f, 3.2f, Z0 - 0.7f), new Vector3(2.8f, 3.2f, Z0 - 0.7f), new Vector3(X1 + 0.6f, 3.2f, -1.5f) })
            {
                Prism(f, "Bocal", p + Vector3.up * 0.06f, 6, 0.025f, 0.025f, 0.08f, M("Plástico Branco"));
                Ico(f, "Lâmpada", p, 0.05f, M("Luz Quente"));
                PointLight(f, "Luz da calçada", p + Vector3.down * 0.12f, new Color(1f, 0.72f, 0.42f), 1.8f, 6f);
            }

            // 2º andar (casa da família do dono), pintado
            Box(f, "2º andar", new Vector3(0, 4.7f, 0), new Vector3(9.4f, 2.6f, 7.4f), M("Pintura 02"));
            Box(f, "Laje 2º", new Vector3(0, 3.36f, 0), new Vector3(9.5f, 0.14f, 7.5f), M("Concreto"));
            Box(f, "Laje 3º", new Vector3(0, 6.06f, 0), new Vector3(9.6f, 0.14f, 7.6f), M("Concreto"));
            foreach (float x in new[] { -3.0f, -0.6f, 2.6f })
                Window(f, new Vector3(x, 4.7f, Z0 - 0.2f), Vector3.back, Chance(0.6f));
            foreach (float z in new[] { -1.8f, 1.6f })
                Window(f, new Vector3(X1 + 0.2f, 4.7f, z), Vector3.right, Chance(0.6f));
            // sacadinha com grade
            var bal = Node("Sacada", f, new Vector3(0.9f, 3.45f, Z0 - 0.75f)).transform;
            for (int i = 0; i <= 14; i++)
                Box(bal, "Barra", new Vector3(-1.4f + i * 0.2f, 0.45f, -0.55f), new Vector3(0.02f, 0.9f, 0.02f), M("Grade"));
            Box(bal, "Corrimão", new Vector3(0, 0.9f, -0.55f), new Vector3(2.9f, 0.04f, 0.04f), M("Grade"));
            Plant(bal, new Vector3(-1.1f, 0.06f, -0.25f));
            Clothesline(bal, new Vector3(-1.3f, 0.06f, 0.05f), new Vector3(1.3f, 0.06f, 0.05f), 1.6f);

            // 3º andar: puxadinho no tijolo
            Box(f, "3º andar", new Vector3(-1.2f, 7.4f, 0.8f), new Vector3(6.8f, 2.6f, 5.6f), M("Tijolo"), null, new Vector2(0.95f, 0.95f));
            Box(f, "Laje 4º", new Vector3(-1.2f, 8.76f, 0.8f), new Vector3(7.0f, 0.12f, 5.8f), M("Concreto"));
            Window(f, new Vector3(-2.6f, 7.4f, 0.8f - 2.8f), Vector3.back, true);
            Window(f, new Vector3(0.2f, 7.4f, 0.8f - 2.8f), Vector3.back, false);
            WaterTank(f, new Vector3(-2.5f, 8.82f, 2.0f));
            Rebar(f, new Vector3(2.0f, 8.82f, -2.0f));
            Rebar(f, new Vector3(2.0f, 8.82f, 3.4f));
            // terraço do 3º com mureta
            Box(f, "Mureta", new Vector3(4.66f, 6.4f, 0), new Vector3(0.12f, 0.6f, 7.4f), M("Pintura 02"));
            Box(f, "Mureta", new Vector3(3.45f, 6.4f, Z0 - 0.14f), new Vector3(2.5f, 0.6f, 0.12f), M("Pintura 02"));
            PlasticTable(f, new Vector3(3.45f, 6.13f, -1.5f), M("Plástico Branco"), 20f);
        }

        static void Window(Transform parent, Vector3 center, Vector3 facing, bool lit)
        {
            var w = Node("Janela", parent, center).transform;
            w.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            Box(w, "Moldura", new Vector3(0, 0, 0.0f), new Vector3(1.0f, 1.1f, 0.06f), M("Moldura"));
            string glass = lit ? (Chance(0.2f) ? "Janela TV" : Chance(0.5f) ? "Janela Acesa" : "Janela Acesa 2") : "Janela Escura";
            Box(w, "Vidro", new Vector3(0, 0, -0.035f), new Vector3(0.86f, 0.96f, 0.02f), M(glass), null, null, false);
            Box(w, "Divisória", new Vector3(0, 0, -0.05f), new Vector3(0.03f, 0.96f, 0.02f), M("Moldura"));
            if (Chance(0.45f))
                for (int i = 0; i < 5; i++)
                    Box(w, "Grade", new Vector3(-0.4f + i * 0.2f, 0, -0.08f), new Vector3(0.015f, 1.0f, 0.015f), M("Grade"));
            Box(w, "Peitoril", new Vector3(0, -0.58f, -0.06f), new Vector3(1.05f, 0.05f, 0.14f), M("Concreto"));
        }

        static void WaterTank(Transform parent, Vector3 pos)
        {
            var t = Node("Caixa d'água", parent, pos).transform;
            Prism(t, "Corpo", Vector3.zero, 10, 0.55f, 0.62f, 0.75f, M("Caixa d'Água"));
            Prism(t, "Tampa", new Vector3(0, 0.75f, 0), 10, 0.64f, 0.5f, 0.12f, M("Caixa d'Água"));
        }

        static void Rebar(Transform parent, Vector3 pos)
        {
            var r = Node("Vergalhões", parent, pos).transform;
            for (int i = 0; i < 4; i++)
                Prism(r, "Ferro", new Vector3((i % 2) * 0.2f, 0, (i / 2) * 0.2f), 4, 0.008f, 0.008f, Rf(0.5f, 0.9f), M("Metal Escuro"), new Vector3(Rf(-6, 6), 0, Rf(-6, 6)));
        }

        static void Clothesline(Transform parent, Vector3 a, Vector3 b, float height)
        {
            var c = Node("Varal", parent, Vector3.zero).transform;
            Prism(c, "Poste varal", a, 4, 0.015f, 0.015f, height, M("Metal"));
            Prism(c, "Poste varal", b, 4, 0.015f, 0.015f, height, M("Metal"));
            var wa = parent.TransformPoint(a + Vector3.up * height);
            var wb = parent.TransformPoint(b + Vector3.up * height);
            Wire(c, wa, wb, 0.06f, 0.008f);
            int n = 3 + rng.Next(4);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n + Rf(-0.04f, 0.04f);
                var p = Vector3.Lerp(a, b, t) + Vector3.up * (height - 0.06f * 4f * t * (1 - t));
                var piece = Node("Roupa", c, p).transform;
                piece.localRotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(0, 90, 0);
                var size = Chance(0.5f) ? new Vector3(0.45f, 0.55f, 0.01f) : new Vector3(0.3f, 0.35f, 0.01f);
                Box(piece, "Pano", new Vector3(0, -size.y * 0.5f, 0), size, Pick(clothes), null, null, false);
                var sw = piece.gameObject.AddComponent<Sway>();
                sw.axis = Vector3.right;
                sw.angle = Rf(4f, 10f);
                dynamicObjects.Add(piece.gameObject);
            }
        }

        // =================================================================== rua e viela

        static float StairsY(float z) => z < 4f ? -0.15f : Mathf.Min((z - 4f) / 0.32f, 30f) * 0.17f - 0.15f;

        static void BuildStreet(Transform root)
        {
            Solid(Box(root, "Chão", new Vector3(5f, -0.4f, 10f), new Vector3(160f, 0.5f, 160f), M("Asfalto")));
            Solid(Box(root, "Calçada do bar", new Vector3(1.0f, -0.075f, -4.85f), new Vector3(30f, 0.15f, 2.3f), M("Calçada"), null, new Vector2(0.5f, 0.5f)));
            Solid(Box(root, "Calçada lateral", new Vector3(5.6f, -0.075f, -0.85f), new Vector3(1.8f, 0.15f, 10.3f), M("Calçada")));
            Solid(Box(root, "Meio-fio", new Vector3(1.0f, -0.07f, -6.02f), new Vector3(30f, 0.16f, 0.06f), M("Concreto")));
            Solid(Box(root, "Calçada de lá", new Vector3(2f, -0.075f, -12.0f), new Vector3(50f, 0.15f, 2.0f), M("Calçada")));
            Box(root, "Faixa amarela", new Vector3(-6f, -0.148f, -8.5f), new Vector3(30f, 0.01f, 0.1f), M("Plástico Amarelo"), null, null, false);

            // viela em escadaria subindo o morro
            var viela = Node("Viela", root, new Vector3(7.9f, 0, 0)).transform;
            for (int k = 0; k < 30; k++)
            {
                float z = 4f + k * 0.32f;
                float top = (k + 1) * 0.17f - 0.15f;
                Solid(Box(viela, "Degrau", new Vector3(0, (top - 0.4f) * 0.5f, z + 0.16f), new Vector3(2.8f, top + 0.4f, 0.32f), M("Concreto")));
            }
            float landY = StairsY(14f);
            Solid(Box(viela, "Patamar", new Vector3(0, landY - 0.5f, 22f), new Vector3(2.8f, 1f, 16f), M("Concreto")));
            // corrimão no meio da escada
            for (int k = 0; k <= 30; k += 3)
            {
                float z = 4f + k * 0.32f;
                Prism(viela, "Poste corrimão", new Vector3(0, StairsY(z + 0.01f), z), 6, 0.025f, 0.025f, 0.9f, M("Metal"));
            }
            Wire(viela, viela.TransformPoint(new Vector3(0, StairsY(4.01f) + 0.9f, 4f)), viela.TransformPoint(new Vector3(0, StairsY(13.6f) + 0.9f, 13.6f)), 0.0f, 0.04f);

            // postes de concreto com luz de sódio
            var poles = new List<Vector3>
            {
                new(-7.5f, -0.15f, -6.3f), new(6.5f, 0f, -6.25f), new(9.0f, StairsY(8f), 8f), new(6.75f, StairsY(16f), 16f),
                new(-1.5f, -0.15f, -11.2f), new(14f, -0.15f, -11.2f),
            };
            for (int i = 0; i < poles.Count; i++)
            {
                var p = poles[i];
                var toStreet = p.z < -10f ? Vector3.forward : p.z < -5f ? Vector3.back : (p.x > 8f ? Vector3.left : Vector3.right);
                StreetPole(root, p, toStreet, i == 2);
            }
            // fiação entre os postes, e "gatos" para as casas
            for (int i = 0; i < poles.Count; i++)
                for (int j = i + 1; j < poles.Count; j++)
                {
                    if ((poles[i] - poles[j]).magnitude > 17f) continue;
                    for (int w = 0; w < 3; w++)
                        Wire(root, poles[i] + new Vector3(-0.5f + w * 0.5f, 6.25f, 0), poles[j] + new Vector3(-0.5f + w * 0.5f, 6.25f, 0), Rf(0.3f, 0.7f));
                }
            Wire(root, poles[1] + Vector3.up * 6.1f, new Vector3(X1 + 0.2f, 5.6f, -2.5f), 0.4f);
            Wire(root, poles[1] + Vector3.up * 6.1f, new Vector3(3.5f, 5.7f, Z0 - 0.2f), 0.5f);
            Wire(root, poles[0] + Vector3.up * 6.1f, new Vector3(-4.0f, 5.7f, Z0 - 0.2f), 0.6f);
            Wire(root, poles[2] + Vector3.up * 6.1f, new Vector3(9.4f, 5.8f, 4.5f), 0.3f);
            Wire(root, poles[2] + Vector3.up * 6.1f, new Vector3(6.4f, 5.6f, 6f), 0.4f);
            Wire(root, poles[4] + Vector3.up * 6.1f, new Vector3(-4f, 5.9f, -13.1f), 0.5f);
            Wire(root, poles[4] + Vector3.up * 6.1f, new Vector3(2f, 5.4f, -13.1f), 0.4f);

            // bandeirinhas atravessando a rua (do bar até a casa da frente)
            Bunting(root, new Vector3(-3.0f, 3.9f, -4.8f), new Vector3(-2.0f, 4.8f, -13.0f), 0.6f);
            Bunting(root, new Vector3(1.0f, 3.9f, -4.8f), new Vector3(3.0f, 4.6f, -13.0f), 0.7f);
            Bunting(root, new Vector3(4.6f, 3.9f, -4.6f), new Vector3(8.5f, 4.4f, -13.0f), 0.6f);
            Bunting(root, new Vector3(X1 + 1.2f, 3.6f, -1.0f), new Vector3(9.4f, 3.9f, -1.5f), 0.25f);

            // calçada: mesas, moto, orelhão, lixeira, cachorro
            streetChairs.Add(PlasticTable(root, new Vector3(-1.9f, 0f, -5.0f), M("Plástico Vermelho"), 15f));
            streetChairs.Add(PlasticTable(root, new Vector3(1.7f, 0f, -5.05f), M("Plástico Amarelo"), -10f));
            streetChairs.Add(PlasticTable(root, new Vector3(5.6f, 0f, -2.6f), M("Plástico Amarelo"), 0f));
            Motorcycle(root, new Vector3(-6.2f, -0.15f, -7.0f), 75f);
            Orelhao(root, new Vector3(-9.5f, 0f, -5.6f));
            Solid(Prism(root, "Lixeira", new Vector3(-3.6f, 0f, -5.6f), 8, 0.22f, 0.25f, 0.75f, M("Pintura 03")));
            Dog(root, new Vector3(3.9f, 0f, -4.35f), -35f);
            Plant(root, new Vector3(-4.3f, 0f, -4.0f));
        }

        static void StreetPole(Transform root, Vector3 basePos, Vector3 toStreet, bool flicker)
        {
            var p = Node("Poste", root, basePos).transform;
            p.localRotation = Quaternion.LookRotation(toStreet);
            Solid(Prism(p, "Concreto", Vector3.zero, 8, 0.13f, 0.08f, 7.0f, M("Concreto")));
            Box(p, "Cruzeta", new Vector3(0, 6.25f, 0), new Vector3(1.4f, 0.09f, 0.09f), M("Madeira"));
            for (int i = 0; i < 3; i++) Prism(p, "Isolador", new Vector3(-0.5f + i * 0.5f, 6.3f, 0), 6, 0.025f, 0.02f, 0.07f, M("Pérola"));
            Box(p, "Braço", new Vector3(0, 5.4f, 0.6f), new Vector3(0.06f, 0.06f, 1.2f), M("Metal"), new Vector3(-8f, 0, 0));
            Box(p, "Luminária", new Vector3(0, 5.5f, 1.2f), new Vector3(0.22f, 0.1f, 0.42f), M("Metal Escuro"));
            var lamp = Box(p, "Lâmpada", new Vector3(0, 5.44f, 1.2f), new Vector3(0.16f, 0.03f, 0.32f), M("Luz Poste"), null, null, false);
            var l = PointLight(p, "Luz", new Vector3(0, 5.2f, 1.2f), new Color(1f, 0.62f, 0.3f), 4.5f, 13f);
            if (Chance(0.4f)) Prism(p, "Transformador", new Vector3(0.0f, 4.6f, -0.3f), 10, 0.25f, 0.25f, 0.75f, M("Metal"));
            if (flicker)
            {
                var fl = p.gameObject.AddComponent<LightFlicker>();
                fl.target = l;
                fl.emissiveRenderer = lamp.GetComponent<Renderer>();
                fl.flickerChance = 0.12f;
                dynamicObjects.Add(p.gameObject);
            }
        }

        static void Bunting(Transform root, Vector3 a, Vector3 b, float sag)
        {
            var g = Node("Bandeirinhas", root, Vector3.zero).transform;
            Wire(g, a, b, sag, 0.01f);
            int n = Mathf.RoundToInt((b - a).magnitude / 0.32f);
            var along = (b - a).normalized;
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                var flag = Node("Bandeirinha", g, p).transform;
                flag.rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
                Prism(flag, "Pano", new Vector3(0, -0.01f, 0), 3, 0.1f, 0.1f, 0.003f, Pick(clothes), new Vector3(-90f, 0, 30f), false);
                var sw = flag.gameObject.AddComponent<Sway>();
                sw.axis = Vector3.right;
                sw.angle = Rf(5f, 12f);
                sw.speed = Rf(1.2f, 2.2f);
                dynamicObjects.Add(flag.gameObject);
            }
        }

        static void Motorcycle(Transform root, Vector3 pos, float yaw)
        {
            var m = Node("Moto", root, pos, yaw).transform;
            m.localEulerAngles = new Vector3(0, yaw, 6f);
            foreach (float z in new[] { -0.62f, 0.62f })
            {
                Prism(m, "Pneu", new Vector3(-0.05f, 0.3f, z), 12, 0.3f, 0.3f, 0.1f, M("Borracha"), new Vector3(0, 0, 90f));
                Prism(m, "Aro", new Vector3(-0.055f, 0.3f, z), 12, 0.18f, 0.18f, 0.11f, M("Inox"), new Vector3(0, 0, 90f));
            }
            Box(m, "Tanque", new Vector3(0, 0.75f, 0.15f), new Vector3(0.3f, 0.22f, 0.5f), M("Moto"));
            Box(m, "Banco", new Vector3(0, 0.8f, -0.32f), new Vector3(0.26f, 0.08f, 0.6f), M("Preto"));
            Box(m, "Motor", new Vector3(0, 0.45f, 0.05f), new Vector3(0.24f, 0.3f, 0.45f), M("Metal Escuro"));
            Blocker(m, "Colisão moto", new Vector3(0, 0.55f, -0.05f), new Vector3(0.6f, 1.1f, 1.9f));
            Box(m, "Garfo", new Vector3(0, 0.62f, 0.55f), new Vector3(0.06f, 0.65f, 0.06f), M("Inox"), new Vector3(-20f, 0, 0));
            Box(m, "Guidão", new Vector3(0, 0.97f, 0.45f), new Vector3(0.65f, 0.035f, 0.035f), M("Metal Escuro"));
            Box(m, "Farol", new Vector3(0, 0.88f, 0.6f), new Vector3(0.14f, 0.12f, 0.06f), M("Luz Quente"));
            Box(m, "Bauleto", new Vector3(0, 0.95f, -0.72f), new Vector3(0.4f, 0.3f, 0.32f), M("Plástico Vermelho"));
        }

        static void Orelhao(Transform root, Vector3 pos)
        {
            var o = Node("Orelhão", root, pos, -30f).transform;
            Solid(Prism(o, "Poste", Vector3.zero, 6, 0.05f, 0.05f, 2.2f, M("Metal")));
            Ico(o, "Concha", new Vector3(0, 1.95f, 0.2f), 0.5f, M("Laranja"), new Vector3(1.0f, 1.05f, 0.85f));
            Box(o, "Telefone", new Vector3(0, 1.5f, 0.0f), new Vector3(0.22f, 0.35f, 0.12f), M("Pintura 02"));
        }

        static void Dog(Transform root, Vector3 pos, float yaw)
        {
            var d = Node("Vira-lata caramelo", root, pos, yaw).transform;
            var body = Node("Corpo", d, Vector3.zero).transform;
            Box(body, "Barriga", new Vector3(0, 0.12f, 0), new Vector3(0.28f, 0.22f, 0.55f), M("Caramelo"), new Vector3(0, 0, 8f));
            Box(body, "Peito", new Vector3(0.02f, 0.1f, 0.2f), new Vector3(0.24f, 0.16f, 0.14f), M("Caramelo Claro"));
            body.gameObject.AddComponent<Breathe>();
            dynamicObjects.Add(body.gameObject);
            var head = Node("Cabeça", d, new Vector3(0.12f, 0.1f, 0.38f), 35f).transform;
            Box(head, "Crânio", Vector3.zero, new Vector3(0.18f, 0.15f, 0.2f), M("Caramelo"));
            Box(head, "Focinho", new Vector3(0, -0.02f, 0.13f), new Vector3(0.1f, 0.08f, 0.1f), M("Caramelo Claro"));
            Box(head, "Nariz", new Vector3(0, 0.0f, 0.185f), new Vector3(0.04f, 0.03f, 0.02f), M("Preto"));
            Box(head, "Orelha", new Vector3(0.07f, 0.06f, -0.03f), new Vector3(0.05f, 0.1f, 0.08f), M("Caramelo"), new Vector3(0, 0, -35f));
            Box(head, "Orelha", new Vector3(-0.07f, 0.06f, -0.03f), new Vector3(0.05f, 0.1f, 0.08f), M("Caramelo"), new Vector3(0, 0, 35f));
            Box(head, "Olho fechado", new Vector3(0.05f, 0.03f, 0.1f), new Vector3(0.04f, 0.008f, 0.01f), M("Preto"));
            Box(head, "Olho fechado", new Vector3(-0.05f, 0.03f, 0.1f), new Vector3(0.04f, 0.008f, 0.01f), M("Preto"));
            foreach (var p in new[] { new Vector3(0.12f, 0.03f, 0.3f), new Vector3(-0.05f, 0.03f, 0.32f), new Vector3(0.15f, 0.03f, -0.2f) })
                Box(d, "Pata", p, new Vector3(0.07f, 0.06f, 0.2f), M("Caramelo"));
            var tail = Node("Rabo", d, new Vector3(-0.08f, 0.1f, -0.27f), -60f).transform;
            Box(tail, "Rabo", new Vector3(0, 0, -0.12f), new Vector3(0.05f, 0.05f, 0.25f), M("Caramelo"));
            var sw = tail.gameObject.AddComponent<Sway>();
            sw.axis = Vector3.up;
            sw.angle = 12f;
            sw.speed = 0.7f;
            dynamicObjects.Add(tail.gameObject);
        }

        // =================================================================== a comunidade em volta

        static void BuildNeighborhood(Transform root)
        {
            // vizinhos à esquerda do bar, na mesma calçada (frente para a rua, z-)
            float x = -4.75f;
            while (x > -26f)
            {
                float w = Rf(3f, 5.5f);
                House(root, new Vector3(x - w * 0.5f, -0.1f, Z0 - 0.2f + Rf(-0.3f, 0.1f)), 0f, w, Rf(6f, 9f), 1 + rng.Next(3), 0.2f, x > -9f);
                x -= w + Rf(0f, 0.15f);
            }

            // do outro lado da rua (frente para z+)
            x = -24f;
            while (x < 28f)
            {
                float w = Rf(3f, 6f);
                House(root, new Vector3(x + w * 0.5f, -0.1f, -13.0f - Rf(0f, 0.4f)), 180f, w, Rf(6f, 9f), 1 + rng.Next(4), 0.2f, Chance(0.35f));
                x += w + Rf(0f, 0.2f);
            }

            // lado direito da viela (frente para x-), subindo junto com a escada
            float z = -5.7f;
            while (z < 30f)
            {
                float w = Rf(3.2f, 5f);
                float zc = z + w * 0.5f;
                float baseY = StairsY(zc - w * 0.5f);
                House(root, new Vector3(9.4f + Rf(0f, 0.3f), baseY, zc), 90f, w, Rf(6f, 9f), 1 + rng.Next(3), 4f, zc < 4f && Chance(0.5f));
                z += w + Rf(0f, 0.15f);
            }

            // lado esquerdo da viela, atrás do bar
            z = Z1 + 0.6f;
            while (z < 30f)
            {
                float w = Rf(3.2f, 5f);
                float zc = z + w * 0.5f;
                float baseY = StairsY(zc - w * 0.5f);
                House(root, new Vector3(6.4f - Rf(0f, 0.3f), baseY, zc), -90f, w, Rf(5f, 8f), 1 + rng.Next(3), 4f, false);
                z += w + Rf(0f, 0.15f);
            }

            // atrás do bar, subindo
            for (float zz = 6f; zz < 18f; zz += Rf(6f, 8f))
            {
                for (float xx = -24f; xx < 0f; xx += Rf(4f, 6f))
                {
                    float w = Rf(3.5f, 5.5f);
                    House(root, new Vector3(xx, StairsY(zz) * 0.7f + Rf(0f, 0.6f), zz), Rf(-6f, 6f), w, Rf(5f, 7f), 1 + rng.Next(3), 4f, false);
                }
            }

            // o morro ao fundo: casinhas empilhadas até o horizonte
            float hillZ0 = 16f;
            float Hill(float zz) => 4.8f + (zz - hillZ0) * 0.48f;
            var hill = Node("Morro", root, Vector3.zero).transform;
            float slope = Mathf.Atan(0.48f) * Mathf.Rad2Deg;
            Box(hill, "Encosta", new Vector3(5f, Hill(40f) - 1.0f, 40f), new Vector3(110f, 2f, 58f), M("Terra"), new Vector3(-slope, 0, 0));
            for (float zz = hillZ0 + 2f; zz < 62f; zz += Rf(4.5f, 6.5f))
            {
                for (float xx = -45f; xx < 50f; xx += Rf(4f, 7f))
                {
                    if (Mathf.Abs(xx - 7.9f) < 2.5f && zz < 32f) continue; // continuação da viela
                    float w = Rf(3.5f, 6f);
                    House(root, new Vector3(xx, Hill(zz) + Rf(-0.4f, 0.4f), zz), Rf(-10f, 10f), w, Rf(5f, 7f), 1 + rng.Next(3), 4.5f, false, zz > 35f);
                }
            }
            // algumas árvores no morro
            for (int i = 0; i < 14; i++)
            {
                float zz = Rf(hillZ0, 55f), xx = Rf(-40f, 45f);
                Tree(hill, new Vector3(xx, Hill(zz), zz));
            }
        }

        static void Tree(Transform parent, Vector3 pos)
        {
            var t = Node("Árvore", parent, pos).transform;
            float h = Rf(2.5f, 4.5f);
            Prism(t, "Tronco", Vector3.zero, 6, 0.18f, 0.12f, h, M("Madeira"));
            Ico(t, "Copa", new Vector3(0, h + 0.6f, 0), Rf(1.2f, 2.0f), Chance(0.5f) ? M("Folha") : M("Folha Escura"), new Vector3(1f, Rf(0.7f, 1f), 1f), 0);
        }

        /// <summary>Casa de alvenaria: frente em z=0 (local) olhando para -z; cresce para +z.</summary>
        static void House(Transform parent, Vector3 pos, float yaw, float w, float d, int floors, float foundation, bool shop, bool simple = false)
        {
            var h = Node("Casa", parent, pos, yaw).transform;
            const float fh = 2.7f;
            var paint = Pick(paints);
            bool allBrick = Chance(0.3f);
            for (int f = 0; f < floors; f++)
            {
                var mat = allBrick || (f > 0 && Chance(0.35f)) ? M("Tijolo") : (f > 0 && Chance(0.3f) ? Pick(paints) : paint);
                bool isBrick = mat == M("Tijolo");
                float bottom = f == 0 ? -foundation : f * fh;
                float top = (f + 1) * fh;
                var floorBox = Box(h, $"Andar {f}", new Vector3(0, (bottom + top) * 0.5f, d * 0.5f), new Vector3(w, top - bottom, d), mat, null, isBrick ? new Vector2(0.95f, 0.95f) : (Vector2?)null);
                if (f == 0 && !simple) Solid(floorBox);
                Box(h, "Laje", new Vector3(0, top, d * 0.5f - 0.04f), new Vector3(w + 0.1f, 0.12f, d + 0.1f), M("Concreto"));
            }
            float roof = floors * fh;

            if (!simple)
            {
                for (int f = 0; f < floors; f++)
                {
                    int count = Mathf.Max(1, Mathf.FloorToInt((w - 0.4f) / 1.6f));
                    for (int i = 0; i < count; i++)
                    {
                        float x = -w * 0.5f + (i + 0.5f) * w / count;
                        if (f == 0 && i == 0)
                        {
                            if (shop) ShopFront(h, w);
                            else Door(h, new Vector3(x, 0, 0));
                            continue;
                        }
                        if (f == 0 && shop) continue;
                        Window(h, new Vector3(x, f * fh + 1.45f, -0.03f), Vector3.back, Chance(0.45f));
                    }
                    if (f > 0 && Chance(0.18f))
                        Box(h, "Ar-condicionado", new Vector3(w * 0.5f - 0.5f, f * fh + 2.1f, -0.15f), new Vector3(0.7f, 0.45f, 0.28f), M("Plástico Branco"));
                    if (f > 0 && Chance(0.25f))
                    {
                        Box(h, "Sacada", new Vector3(0, f * fh + 0.05f, -0.45f), new Vector3(w * 0.6f, 0.1f, 0.9f), M("Concreto"));
                        Box(h, "Mureta", new Vector3(0, f * fh + 0.5f, -0.88f), new Vector3(w * 0.6f, 0.8f, 0.06f), Chance(0.5f) ? paint : M("Tijolo"), null, new Vector2(0.95f, 0.95f));
                        if (Chance(0.5f)) Clothesline(h, new Vector3(-w * 0.28f, f * fh + 0.1f, -0.5f), new Vector3(w * 0.28f, f * fh + 0.1f, -0.5f), 1.5f);
                    }
                }
            }
            else
            {
                // casas distantes: só janelas emissivas, baratinhas
                for (int f = 0; f < floors; f++)
                    if (Chance(0.6f))
                        Box(h, "Janela", new Vector3(Rf(-w * 0.3f, w * 0.3f), f * fh + 1.45f, -0.03f), new Vector3(0.8f, 0.9f, 0.04f), Chance(0.55f) ? M("Janela Acesa") : M("Janela Escura"), null, null, false);
            }

            if (Chance(0.6f)) WaterTank(h, new Vector3(Rf(-w * 0.25f, w * 0.25f), roof + 0.06f, Rf(d * 0.3f, d * 0.7f)));
            if (Chance(0.35f)) Rebar(h, new Vector3(w * 0.5f - 0.3f, roof + 0.06f, 0.3f));
            if (Chance(0.3f))
            {
                Box(h, "Mureta laje", new Vector3(0, roof + 0.35f, 0.02f), new Vector3(w, 0.6f, 0.12f), M("Tijolo"), null, new Vector2(0.95f, 0.95f));
            }
            if (!simple && Chance(0.2f))
                Clothesline(h, new Vector3(-w * 0.3f, roof + 0.06f, d * 0.4f), new Vector3(w * 0.3f, roof + 0.06f, d * 0.4f), 1.4f);
            if (Chance(0.12f)) Prism(h, "Antena", new Vector3(0, roof, d * 0.5f), 4, 0.015f, 0.01f, 1.8f, M("Metal"));
        }

        static void Door(Transform h, Vector3 pos)
        {
            Box(h, "Porta", pos + new Vector3(0, 1.05f, -0.03f), new Vector3(0.9f, 2.1f, 0.06f), Pick(doors));
            Box(h, "Batente", pos + new Vector3(0, 2.13f, -0.04f), new Vector3(1.0f, 0.06f, 0.08f), M("Concreto"));
            Box(h, "Degrau", pos + new Vector3(0, 0.05f, -0.2f), new Vector3(1.0f, 0.1f, 0.3f), M("Concreto"));
            if (Chance(0.4f)) Box(h, "Número", pos + new Vector3(0.6f, 1.7f, -0.035f), new Vector3(0.18f, 0.12f, 0.01f), M("Plástico Branco"));
        }

        /// <summary>Comércio no térreo: mercadinho, salão, oficina… porta de aço meio aberta.</summary>
        static void ShopFront(Transform h, float w)
        {
            float sw = Mathf.Min(w - 0.4f, 3.2f);
            Box(h, "Porta de aço", new Vector3(0, 2.0f, -0.04f), new Vector3(sw, 1.4f, 0.06f), M("Metal"));
            Box(h, "Vão", new Vector3(0, 0.65f, -0.02f), new Vector3(sw, 1.3f, 0.04f), M("Janela Acesa 2"), null, null, false);
            Box(h, "Toldo", new Vector3(0, 2.75f, -0.5f), new Vector3(sw + 0.3f, 0.06f, 1.0f), Pick(clothes), new Vector3(-12f, 0, 0));
            Box(h, "Placa", new Vector3(0, 3.05f, -0.06f), new Vector3(sw * 0.8f, 0.35f, 0.05f), Pick(paints));
            PointLight(h, "Luz da loja", new Vector3(0, 1.2f, -0.8f), new Color(1f, 0.85f, 0.65f), 1.0f, 3.5f);
        }

        // =================================================================== mesa de sinuca

        static PoolTable BuildPoolTable(Transform root)
        {
            var table = root.gameObject.AddComponent<PoolTable>();
            var phys = table.CreatePhysics();
            float hl = table.HalfL, hw = table.HalfW, R = table.ballRadius, s = table.surfaceHeight;
            const float cush = 0.04f, frame = 0.13f;

            var geo = Node("Geometria", root, Vector3.zero).transform;
            Box(geo, "Feltro", new Vector3(0, s - 0.015f, 0), new Vector3(2 * hl + 0.14f, 0.03f, 2 * hw + 0.14f), M("Feltro"));

            // tabelas (borrachas) seguindo exatamente os segmentos da física
            foreach (var seg in phys.cushions)
            {
                var dir = seg.b - seg.a;
                float len = dir.magnitude;
                var dn = dir / len;
                bool horizontal = Mathf.Abs(dn.y) < 1e-3f, vertical = Mathf.Abs(dn.x) < 1e-3f;
                bool onBoundary = (horizontal && Mathf.Abs(Mathf.Abs(seg.a.y) - hw) < 1e-4f) || (vertical && Mathf.Abs(Mathf.Abs(seg.a.x) - hl) < 1e-4f);
                Vector2 outN;
                if (onBoundary) outN = horizontal ? new Vector2(0, Mathf.Sign(seg.a.y)) : new Vector2(Mathf.Sign(seg.a.x), 0);
                else
                {
                    var pocket = phys.pockets[phys.NearestPocket(seg.b)];
                    var perp = new Vector2(-dn.y, dn.x);
                    float side = dn.x * (pocket - seg.a).y - dn.y * (pocket - seg.a).x;
                    outN = side > 0 ? -perp : perp;
                }
                var c = (seg.a + seg.b) * 0.5f + outN * (cush * 0.5f);
                float yaw = Mathf.Atan2(-dn.y, dn.x) * Mathf.Rad2Deg;
                Box(geo, "Tabela", new Vector3(c.x, s + 0.019f, c.y), new Vector3(len + 0.004f, 0.038f, cush), M("Feltro Tabela"), new Vector3(0, yaw, 0));
            }

            // moldura de madeira
            float fz = hw + cush + frame * 0.5f, fx = hl + cush + frame * 0.5f;
            Box(geo, "Moldura", new Vector3(0, s + 0.015f, fz), new Vector3(2 * (hl + cush + frame), 0.05f, frame), M("Madeira Escura"));
            Box(geo, "Moldura", new Vector3(0, s + 0.015f, -fz), new Vector3(2 * (hl + cush + frame), 0.05f, frame), M("Madeira Escura"));
            Box(geo, "Moldura", new Vector3(fx, s + 0.015f, 0), new Vector3(frame, 0.05f, 2 * hw + 2 * cush), M("Madeira Escura"));
            Box(geo, "Moldura", new Vector3(-fx, s + 0.015f, 0), new Vector3(frame, 0.05f, 2 * hw + 2 * cush), M("Madeira Escura"));
            Box(geo, "Saia", new Vector3(0, s - 0.09f, 0), new Vector3(2 * (hl + cush + frame), 0.16f, 2 * (hw + cush + frame)), M("Madeira"));

            // caçapas: buraco no feltro + buraco na moldura
            for (int i = 0; i < phys.pockets.Count; i++)
            {
                var p = phys.pockets[i];
                Prism(geo, "Caçapa", new Vector3(p.x, s, p.y), 16, phys.pocketRadius[i] + 0.008f, phys.pocketRadius[i] + 0.008f, 0.0025f, M("Caçapa"), null, false);
                Prism(geo, "Boca da caçapa", new Vector3(p.x, s + 0.039f, p.y), 16, 0.064f, 0.064f, 0.0015f, M("Caçapa"), null, false);
                Prism(geo, "Couro", new Vector3(p.x, s - 0.12f, p.y), 12, 0.05f, 0.075f, 0.12f, M("Couro"));
            }

            // diamantes
            for (int i = 1; i < 8; i++)
            {
                if (i == 4) continue;
                float x = -hl + i * (2 * hl / 8f);
                foreach (float z in new[] { fz, -fz })
                    Box(geo, "Diamante", new Vector3(x, s + 0.041f, z), new Vector3(0.014f, 0.003f, 0.014f), M("Pérola"), new Vector3(0, 45, 0), null, false);
            }
            for (int j = 1; j < 4; j++)
            {
                float z = -hw + j * (2 * hw / 4f);
                foreach (float x in new[] { fx, -fx })
                    Box(geo, "Diamante", new Vector3(x, s + 0.041f, z), new Vector3(0.014f, 0.003f, 0.014f), M("Pérola"), new Vector3(0, 45, 0), null, false);
            }

            // pernas
            foreach (var lp in new[] { new Vector2(hl - 0.05f, hw), new Vector2(-hl + 0.05f, hw), new Vector2(hl - 0.05f, -hw), new Vector2(-hl + 0.05f, -hw), new Vector2(0, hw), new Vector2(0, -hw) })
            {
                Prism(geo, "Perna", new Vector3(lp.x, 0, lp.y), 8, 0.07f, 0.085f, s - 0.17f, M("Madeira Escura"));
                Prism(geo, "Pé", new Vector3(lp.x, 0, lp.y), 8, 0.09f, 0.08f, 0.05f, M("Madeira Escura"));
            }

            // canaleta onde as bolas encaçapadas aparecem
            var trayRoot = Node("Canaleta", root, new Vector3(-0.3f, s - 0.1f, -(hw + cush + frame + 0.045f))).transform;
            Box(trayRoot, "Fundo", new Vector3(0.43f, -0.004f, 0), new Vector3(0.95f, 0.012f, 0.075f), M("Madeira"));
            Box(trayRoot, "Borda", new Vector3(0.43f, 0.02f, -0.04f), new Vector3(0.95f, 0.05f, 0.012f), M("Madeira"));
            var slot = Node("Primeira posição", trayRoot, new Vector3(0, R + 0.002f, 0), 90f).transform;
            table.tray = slot;

            // giz azul na moldura
            Box(geo, "Giz", new Vector3(hl - 0.3f, s + 0.052f, fz), new Vector3(0.022f, 0.022f, 0.022f), M("Giz Azul"));
            Box(geo, "Giz", new Vector3(-hl + 0.4f, s + 0.052f, -fz), new Vector3(0.022f, 0.022f, 0.022f), M("Giz Azul"));

            // luminária de sinuca
            var lamp = Node("Luminária", root, new Vector3(0, 1.78f, 0)).transform;
            Box(lamp, "Caixa", Vector3.zero, new Vector3(1.4f, 0.16f, 0.4f), M("Pintura 07"));
            Box(lamp, "Friso", new Vector3(0, -0.07f, 0), new Vector3(1.42f, 0.03f, 0.42f), M("Madeira Escura"));
            Box(lamp, "Difusor", new Vector3(0, -0.085f, 0), new Vector3(1.3f, 0.01f, 0.32f), M("Luz Mesa"), null, null, false);
            foreach (float x in new[] { -0.5f, 0.5f })
                Box(lamp, "Corrente", new Vector3(x, (H - 1.78f) * 0.5f, 0), new Vector3(0.012f, H - 1.78f, 0.012f), M("Metal"), null, null, false);
            for (int i = -1; i <= 1; i++)
                SpotLight(lamp, "Luz da mesa", new Vector3(i * 0.48f, -0.1f, 0), new Vector3(90f, 0, 0), new Color(1f, 0.9f, 0.75f), 1.7f, 3.0f, 105f, i == 0);

            // bolas
            var ballsRoot = Node("Bolas", root, Vector3.zero).transform;
            var balls = new PoolBall[16];
            for (int n = 0; n < 16; n++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = n == 0 ? "Bola branca" : $"Bola {n}";
                go.transform.SetParent(ballsRoot, false);
                go.transform.localScale = Vector3.one * (2f * R);
                go.GetComponent<MeshRenderer>().sharedMaterial = M($"Bola {n:00}");
                var b = go.AddComponent<PoolBall>();
                b.number = n;
                balls[n] = b;
            }
            ballsCache = balls;

            // taco
            var cueGo = Node("Taco", root, new Vector3(0, s + 0.3f, 0)).transform;
            CuePiece(cueGo, 0f, 0.012f, 0.0062f, 0.0062f, M("Taco Ponta"));
            CuePiece(cueGo, 0.012f, 0.022f, 0.0065f, 0.0066f, M("Taco Anel"));
            CuePiece(cueGo, 0.034f, 0.9f, 0.0066f, 0.0115f, M("Madeira Clara"));
            CuePiece(cueGo, 0.934f, 0.02f, 0.0118f, 0.012f, M("Taco Anel"));
            CuePiece(cueGo, 0.954f, 0.5f, 0.012f, 0.0155f, M("Madeira Escura"));
            CuePiece(cueGo, 1.454f, 0.015f, 0.0155f, 0.0155f, M("Borracha"));
            cueCache = cueGo.gameObject.AddComponent<CueStick>();

            return table;
        }

        static PoolBall[] ballsCache;
        static CueStick cueCache;

        static void CuePiece(Transform cue, float start, float length, float rTip, float rButt, Material m)
        {
            // prisma cresce em +Y; girando -90° em X ele cresce para -Z (atrás da ponta)
            Prism(cue, "Peça", new Vector3(0, 0, -start), 10, rTip, rButt, length, m, new Vector3(-90f, 0, 0));
        }

        // =================================================================== jogo, câmera e UI

        static void BuildGame(PoolTable table)
        {
            var camGo = new GameObject("Câmera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 300f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var pc = camGo.AddComponent<PoolCamera>();
            pc.table = table;
            pc.cam = cam;
            pc.hideInTopView = table.transform.Find("Luminária").GetComponentsInChildren<Renderer>();
            camGo.transform.position = pc.menuPosition;
            camGo.transform.LookAt(pc.menuLookAt);

            var guideGo = new GameObject("Mira");
            var guide = guideGo.AddComponent<AimGuide>();
            guide.mainLine = Line(guideGo.transform, "Linha", 0.004f);
            guide.ghostCircle = Line(guideGo.transform, "Bola fantasma", 0.003f);
            guide.objectLine = Line(guideGo.transform, "Saída da bola", 0.004f);
            guide.cueLine = Line(guideGo.transform, "Saída da branca", 0.003f);

            var gameGo = new GameObject("Jogo");
            var fx = gameGo.AddComponent<AudioFx>();
            fx.ballClick = Clip("sfx_bola");
            fx.cushion = Clip("sfx_tabela");
            fx.cueHit = Clip("sfx_taco");
            fx.pocket = Clip("sfx_cacapa");
            fx.chalk = Clip("sfx_giz");
            fx.transform.position = table.Center;

            var amb = new GameObject("Som da viela", typeof(AudioSource));
            amb.transform.position = new Vector3(7.5f, 1.5f, -4f);
            var asrc = amb.GetComponent<AudioSource>();
            asrc.clip = Clip("amb_viela");
            asrc.loop = true;
            asrc.playOnAwake = true;
            asrc.volume = 0.45f;
            asrc.spatialBlend = 0.4f;
            asrc.maxDistance = 40f;

            var ui = gameGo.AddComponent<ButecoUI>();
            var game = gameGo.AddComponent<PoolGame>();
            game.table = table;
            game.balls = ballsCache;
            game.cue = cueCache;
            game.guide = guide;
            game.cam = pc;
            game.sfx = fx;
            game.ui = ui;
            ui.game = game;
            gameCache = game;
            uiCache = ui;
            camCache = pc;
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");

        static LineRenderer Line(Transform parent, string name, float width)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = M("Linha Mira");
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            return lr;
        }

        // =================================================================== otimização

        static void MarkStatic(Transform root)
        {
            var dynamicSet = new HashSet<Transform>();
            foreach (var go in dynamicObjects)
                if (go != null)
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) dynamicSet.Add(t);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (dynamicSet.Contains(t)) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }
    }
}
