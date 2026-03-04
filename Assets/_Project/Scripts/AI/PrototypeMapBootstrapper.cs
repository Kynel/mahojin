using System.Collections.Generic;
using DuckovProto.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuckovProto.AI
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class PrototypeMapBootstrapper : MonoBehaviour
    {
        [Header("Build")]
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private bool rebuildIfMapExists;
        [SerializeField] private int randomSeed = 2718;

        [Header("Terrain")]
        [SerializeField] private Vector3 terrainSize = new Vector3(200f, 20f, 200f);
        [SerializeField] private int heightmapResolution = 257;
        [SerializeField] private float playerSafeRadius = 15f;

        [Header("Spawns")]
        [SerializeField] private Vector2 playerSpawnXZ = new Vector2(0f, -6f);
        [SerializeField] private int enemySpawnPointCount = 10;
        [SerializeField] private float enemySpawnMinDistance = 18f;
        [SerializeField] private float maxSpawnSlope = 35f;

        [Header("Obstacles")]
        [SerializeField] private int rockLargeCount = 22;
        [SerializeField] private int wallChunkCount = 16;

        private const string MapRootName = "MapRoot";

        private Material obstacleMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != "Prototype_Arena")
            {
                return;
            }

            if (FindFirstObjectByType<PrototypeMapBootstrapper>() != null)
            {
                return;
            }

            GameObject root = new GameObject("PrototypeMapBootstrapper");
            root.AddComponent<PrototypeMapBootstrapper>();
        }

        private void Awake()
        {
            if (!buildOnAwake)
            {
                return;
            }

            BuildMap();
        }

        private void BuildMap()
        {
            Random.InitState(randomSeed);

            Transform existingRoot = GameObject.Find(MapRootName)?.transform;
            if (existingRoot != null)
            {
                Terrain existingTerrain = existingRoot.GetComponentInChildren<Terrain>();
                if (!rebuildIfMapExists && existingTerrain != null)
                {
                    ConfigureRuntimeSystems(existingRoot);
                    return;
                }

                Destroy(existingRoot.gameObject);
            }

            PrepareMaterials();

            GameObject mapRoot = new GameObject(MapRootName);
            Transform landmarksRoot = CreateChild(mapRoot.transform, "Landmarks");
            Transform obstaclesRoot = CreateChild(mapRoot.transform, "Obstacles");
            CreateChild(mapRoot.transform, "Props");
            Transform spawnRoot = CreateChild(mapRoot.transform, "SpawnPoints");

            Terrain terrain = CreateTerrain(mapRoot.transform);
            if (terrain == null)
            {
                Debug.LogError("[PrototypeMapBootstrapper] Terrain creation failed. Keeping legacy ground active.", this);
                EnableLegacyGround();
                return;
            }

            DisableLegacyGround();
            CreateLandmarks(terrain, landmarksRoot);
            CreateObstacles(terrain, obstaclesRoot);
            CreateBoundaryRocks(terrain, obstaclesRoot);

            Transform playerSpawn = CreatePlayerSpawn(terrain, spawnRoot);
            List<Transform> enemySpawns = CreateEnemySpawns(terrain, spawnRoot, playerSpawn.position);

            PlacePlayer(playerSpawn.position);
            ConfigureEnemySpawner(terrain, enemySpawns);
            DisableArenaWallBootstrapper();
        }

        private void ConfigureRuntimeSystems(Transform mapRoot)
        {
            Transform spawnRoot = mapRoot.Find("SpawnPoints");
            Terrain terrain = mapRoot.GetComponentInChildren<Terrain>();
            if (spawnRoot == null || terrain == null)
            {
                return;
            }

            List<Transform> enemySpawns = new List<Transform>();
            for (int i = 0; i < spawnRoot.childCount; i++)
            {
                Transform child = spawnRoot.GetChild(i);
                if (child.name.StartsWith("EnemySpawn_"))
                {
                    enemySpawns.Add(child);
                }
            }

            Transform playerSpawn = spawnRoot.Find("PlayerSpawn");
            if (playerSpawn != null)
            {
                PlacePlayer(playerSpawn.position);
            }

            ConfigureEnemySpawner(terrain, enemySpawns);
            DisableArenaWallBootstrapper();
        }

        private void DisableLegacyGround()
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
            {
                return;
            }

            ground.SetActive(false);
        }

        private void EnableLegacyGround()
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
            {
                return;
            }

            ground.SetActive(true);
        }

        private void PrepareMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                lit = Shader.Find("Standard");
            }

            obstacleMaterial = new Material(lit);
            obstacleMaterial.color = new Color(0.62f, 0.63f, 0.66f, 1f);
        }

        private Terrain CreateTerrain(Transform parent)
        {
            TerrainData data = new TerrainData
            {
                heightmapResolution = Mathf.Clamp(heightmapResolution, 129, 513),
                size = terrainSize
            };

            data.SetHeights(0, 0, GenerateHeights(data, playerSpawnXZ));
            data.terrainLayers = new[] { CreateTerrainLayer() };
            FillSingleLayerWeights(data);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = new Vector3(-terrainSize.x * 0.5f, 0f, -terrainSize.z * 0.5f);
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                terrainObject.layer = groundLayer;
            }

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.materialTemplate = null;
            terrain.Flush();
            return terrain;
        }

        private float[,] GenerateHeights(TerrainData data, Vector2 safeCenter)
        {
            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];

            Vector2[] hillCenters =
            {
                new Vector2(-52f, 38f),
                new Vector2(44f, -34f),
                new Vector2(58f, 48f),
            };

            float[] hillRadii = { 24f, 22f, 26f };
            float[] hillHeights = { 0.42f, 0.34f, 0.38f };

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (resolution - 1f);
                    float v = y / (resolution - 1f);

                    float wx = (u - 0.5f) * data.size.x;
                    float wz = (v - 0.5f) * data.size.z;

                    float fine = Mathf.PerlinNoise((wx + 300f) * 0.025f, (wz - 200f) * 0.025f) * 0.08f;
                    float broad = Mathf.PerlinNoise((wx - 120f) * 0.009f, (wz + 80f) * 0.009f) * 0.14f;
                    float h = 0.03f + fine + broad;

                    for (int i = 0; i < hillCenters.Length; i++)
                    {
                        float d = Vector2.Distance(new Vector2(wx, wz), hillCenters[i]);
                        float t = Mathf.Clamp01(1f - (d / hillRadii[i]));
                        float smooth = t * t * (3f - 2f * t);
                        h += smooth * hillHeights[i];
                    }

                    float edge = Mathf.Min(u, 1f - u, v, 1f - v);
                    float edgeRaise = Mathf.InverseLerp(0.18f, 0f, edge);
                    h += edgeRaise * 0.33f;

                    float safeDistance = Vector2.Distance(new Vector2(wx, wz), safeCenter);
                    float safeBlend = Mathf.InverseLerp(playerSafeRadius, 0f, safeDistance);
                    h = Mathf.Lerp(h, 0.055f, safeBlend);

                    heights[y, x] = Mathf.Clamp01(h);
                }
            }

            return heights;
        }

        private static TerrainLayer CreateTerrainLayer()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat
            };

            Color grass = new Color(0.34f, 0.5f, 0.29f, 1f);
            texture.SetPixels(new[] { grass, grass, grass, grass });
            texture.Apply();

            TerrainLayer layer = new TerrainLayer
            {
                diffuseTexture = texture,
                tileSize = new Vector2(24f, 24f)
            };
            return layer;
        }

        private static void FillSingleLayerWeights(TerrainData data)
        {
            int resolution = data.alphamapResolution;
            float[,,] maps = new float[resolution, resolution, 1];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    maps[y, x, 0] = 1f;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        private void CreateLandmarks(Terrain terrain, Transform root)
        {
            Vector3 center = new Vector3(2f, 0f, 6f);
            float ringRadius = 8f;
            int pillars = 8;

            GameObject ring = new GameObject("RuinRing");
            ring.transform.SetParent(root, false);

            for (int i = 0; i < pillars; i++)
            {
                float t = i / (float)pillars;
                float angle = t * Mathf.PI * 2f;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
                CreateWallChunk(ring.transform, $"RingPillar_{i:00}", terrain, pos, new Vector3(1.8f, 3.2f, 1.8f), Quaternion.Euler(0f, i * 360f / pillars, 0f));
            }

            CreateWallChunk(ring.transform, "RingCore", terrain, center, new Vector3(4.5f, 1.1f, 4.5f), Quaternion.identity);
            CreateRamp(root, terrain, new Vector3(-16f, 0f, 22f), 18f, 4.2f, 5f, -24f);
            CreateRamp(root, terrain, new Vector3(30f, 0f, -18f), 16f, 3.4f, 4f, 32f);
        }

        private void CreateObstacles(Terrain terrain, Transform root)
        {
            for (int i = 0; i < rockLargeCount; i++)
            {
                if (!TrySampleObstaclePosition(terrain, out Vector3 pos, 16f, 86f))
                {
                    continue;
                }

                Vector3 scale = new Vector3(Random.Range(2.1f, 4.8f), Random.Range(1.8f, 4.6f), Random.Range(2.1f, 4.8f));
                PrimitiveType shape = (i % 3 == 0) ? PrimitiveType.Capsule : PrimitiveType.Cube;
                CreatePrimitive(root, $"RockLarge_{i:00}", shape, pos, scale, Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-3f, 3f)));
            }

            for (int i = 0; i < wallChunkCount; i++)
            {
                if (!TrySampleObstaclePosition(terrain, out Vector3 pos, 18f, 80f))
                {
                    continue;
                }

                Vector3 scale = new Vector3(Random.Range(3f, 7f), Random.Range(1.8f, 3.6f), Random.Range(0.9f, 1.5f));
                Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                CreatePrimitive(root, $"WallChunk_{i:00}", PrimitiveType.Cube, pos, scale, rot);
            }
        }

        private void CreateBoundaryRocks(Terrain terrain, Transform root)
        {
            int boundaryCount = 24;
            float boundaryRadius = 96f;
            for (int i = 0; i < boundaryCount; i++)
            {
                float angle = (i / (float)boundaryCount) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * boundaryRadius + Random.Range(-3f, 3f),
                    0f,
                    Mathf.Sin(angle) * boundaryRadius + Random.Range(-3f, 3f));

                Vector3 scale = new Vector3(Random.Range(6f, 12f), Random.Range(4f, 9f), Random.Range(5f, 10f));
                CreatePrimitive(root, $"BoundaryRock_{i:00}", PrimitiveType.Cube, SampleAtGround(terrain, pos, scale.y * 0.5f), scale, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
        }

        private Transform CreatePlayerSpawn(Terrain terrain, Transform spawnRoot)
        {
            Transform playerSpawn = CreateChild(spawnRoot, "PlayerSpawn");
            Vector3 spawnPos = new Vector3(playerSpawnXZ.x, 0f, playerSpawnXZ.y);
            playerSpawn.position = SampleAtGround(terrain, spawnPos, 1.05f);
            return playerSpawn;
        }

        private List<Transform> CreateEnemySpawns(Terrain terrain, Transform spawnRoot, Vector3 playerPos)
        {
            List<Transform> spawns = new List<Transform>(enemySpawnPointCount);
            int attempts = enemySpawnPointCount * 8;
            for (int i = 0; i < attempts && spawns.Count < enemySpawnPointCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(42f, 88f);
                Vector3 candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                candidate = SampleAtGround(terrain, candidate, 1.02f);

                if (!IsSpawnPointValid(terrain, candidate, playerPos, spawns))
                {
                    continue;
                }

                Transform point = CreateChild(spawnRoot, $"EnemySpawn_{spawns.Count:00}");
                point.position = candidate;
                spawns.Add(point);
            }

            return spawns;
        }

        private bool IsSpawnPointValid(Terrain terrain, Vector3 candidate, Vector3 playerPos, List<Transform> existing)
        {
            Vector3 flatDelta = candidate - playerPos;
            flatDelta.y = 0f;
            if (flatDelta.sqrMagnitude < enemySpawnMinDistance * enemySpawnMinDistance)
            {
                return false;
            }

            float slope = GetSlopeAt(terrain, candidate);
            if (slope > maxSpawnSlope)
            {
                return false;
            }

            int wallMask = LayerMask.GetMask("Wall");
            if (Physics.CheckSphere(candidate + Vector3.up * 0.75f, 1.2f, wallMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            for (int i = 0; i < existing.Count; i++)
            {
                Vector3 delta = existing[i].position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < 16f)
                {
                    return false;
                }
            }

            return true;
        }

        private void PlacePlayer(Vector3 spawnPos)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                return;
            }

            player.transform.position = spawnPos;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            PlayerVitals vitals = player.GetComponent<PlayerVitals>();
            if (vitals != null)
            {
                vitals.ResetToFull();
            }
        }

        private void ConfigureEnemySpawner(Terrain terrain, List<Transform> spawnPoints)
        {
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
            if (spawner == null)
            {
                return;
            }

            spawner.ApplyMapContext(terrain, spawnPoints.ToArray(), enemySpawnMinDistance);
        }

        private void DisableArenaWallBootstrapper()
        {
            ArenaWallBootstrapper arenaWall = FindFirstObjectByType<ArenaWallBootstrapper>();
            if (arenaWall != null)
            {
                arenaWall.enabled = false;
            }
        }

        private bool TrySampleObstaclePosition(Terrain terrain, out Vector3 position, float minRadius, float maxRadius)
        {
            for (int i = 0; i < 24; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                if (d.sqrMagnitude < 0.01f)
                {
                    d = Vector2.right;
                }

                float radius = Random.Range(minRadius, maxRadius);
                Vector3 candidate = new Vector3(d.x * radius, 0f, d.y * radius);
                float slope = GetSlopeAt(terrain, candidate);
                if (slope > 38f)
                {
                    continue;
                }

                int wallMask = LayerMask.GetMask("Wall");
                Vector3 sample = SampleAtGround(terrain, candidate, 0f);
                if (Physics.CheckSphere(sample + Vector3.up * 0.8f, 1.6f, wallMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                position = sample;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private void CreateRamp(Transform root, Terrain terrain, Vector3 center, float length, float rise, float width, float yaw)
        {
            Vector3 basePos = SampleAtGround(terrain, center, 0f);
            Vector3 pos = basePos + Vector3.up * (rise * 0.5f + 0.25f);
            Vector3 scale = new Vector3(width, rise, length);
            Quaternion rot = Quaternion.Euler(-Mathf.Atan2(rise, length) * Mathf.Rad2Deg, yaw, 0f);
            CreatePrimitive(root, "Ramp", PrimitiveType.Cube, pos, scale, rot);
        }

        private void CreateWallChunk(Transform root, string name, Terrain terrain, Vector3 centerXZ, Vector3 scale, Quaternion rotation)
        {
            Vector3 pos = SampleAtGround(terrain, centerXZ, scale.y * 0.5f);
            CreatePrimitive(root, name, PrimitiveType.Cube, pos, scale, rotation);
        }

        private GameObject CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = scale;

            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0)
            {
                go.layer = wallLayer;
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = obstacleMaterial;
            }

            return go;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Vector3 SampleAtGround(Terrain terrain, Vector3 xz, float heightOffset)
        {
            if (terrain == null)
            {
                return xz + Vector3.up * heightOffset;
            }

            float y = terrain.SampleHeight(xz) + terrain.transform.position.y;
            return new Vector3(xz.x, y + heightOffset, xz.z);
        }

        private static float GetSlopeAt(Terrain terrain, Vector3 worldPos)
        {
            if (terrain == null)
            {
                return 0f;
            }

            TerrainData data = terrain.terrainData;
            Vector3 local = worldPos - terrain.transform.position;
            float u = Mathf.Clamp01(local.x / data.size.x);
            float v = Mathf.Clamp01(local.z / data.size.z);
            return data.GetSteepness(u, v);
        }
    }
}
