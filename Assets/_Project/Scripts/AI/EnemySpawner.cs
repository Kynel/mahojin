using System.Collections.Generic;
using DuckovProto.Combat;
using UnityEngine;

namespace DuckovProto.AI
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool prioritizeSpawnPoints = true;
        [SerializeField] private int maxAlive = 5;
        [SerializeField] private int initialSpawnCount = 5;
        [SerializeField] private float spawnInterval = 0.25f;
        [SerializeField] private float randomSpawnRadius = 14f;
        [SerializeField] private float minDistanceFromPlayer = 5f;
        [SerializeField] private int maxSpawnAttempts = 12;
        [SerializeField] private Terrain terrain;
        [SerializeField] private LayerMask wallMask = 1 << 10; // Wall
        [SerializeField] private float maxTerrainSlope = 35f;
        [SerializeField] private float spawnCheckRadius = 0.8f;
        [SerializeField] private float terrainEdgePadding = 6f;
        [SerializeField] private float spawnHeightOffset = 1.02f;
        [SerializeField] private bool enableDebugLogs = true;

        private readonly List<GameObject> aliveEnemies = new List<GameObject>(16);
        private float spawnTimer;

        private void Start()
        {
            ResolveMapReferences();
            bool prefabUsable = IsEnemyPrefabUsable();
            Log($"Start: initialSpawnCount={initialSpawnCount}, maxAlive={maxAlive}, enemyPrefab={GetEnemyPrefabLabel()}, usable={prefabUsable}");
            int startCount = Mathf.Min(initialSpawnCount, Mathf.Max(0, maxAlive));
            for (int i = 0; i < startCount; i++)
            {
                TrySpawnOne();
            }
        }

        private void Update()
        {
            CleanupDead();

            if (aliveEnemies.Count >= maxAlive)
            {
                spawnTimer = 0f;
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;
                TrySpawnOne();
            }
        }

        private void TrySpawnOne()
        {
            if (aliveEnemies.Count >= maxAlive)
            {
                Log($"TrySpawnOne blocked: maxAlive reached ({aliveEnemies.Count}/{maxAlive})");
                return;
            }

            if (!TryGetSpawnPosition(out Vector3 position))
            {
                Log("TrySpawnOne failed: no valid spawn position found.");
                return;
            }

            bool prefabUsable = IsEnemyPrefabUsable();
            GameObject enemy = prefabUsable
                ? Instantiate(enemyPrefab, position, Quaternion.identity)
                : CreateRuntimeEnemy(position);
            aliveEnemies.Add(enemy);
            Log($"Spawned enemy at {position}. Alive={aliveEnemies.Count}/{maxAlive} via {(prefabUsable ? "prefab" : "runtime fallback")}");
        }

        public void ApplyMapContext(Terrain mapTerrain, Transform[] mapSpawnPoints, float minPlayerDistance = -1f)
        {
            terrain = mapTerrain != null ? mapTerrain : terrain;
            if (mapSpawnPoints != null && mapSpawnPoints.Length > 0)
            {
                spawnPoints = mapSpawnPoints;
            }

            if (minPlayerDistance > 0f)
            {
                minDistanceFromPlayer = minPlayerDistance;
            }
        }

        private bool TryGetSpawnPosition(out Vector3 position)
        {
            ResolveMapReferences();
            int attempts = Mathf.Max(1, maxSpawnAttempts);

            if (prioritizeSpawnPoints && spawnPoints != null && spawnPoints.Length > 0)
            {
                int start = Random.Range(0, spawnPoints.Length);
                int checks = Mathf.Max(spawnPoints.Length, attempts);
                for (int i = 0; i < checks; i++)
                {
                    Transform point = spawnPoints[(start + i) % spawnPoints.Length];
                    if (point != null)
                    {
                        Vector3 candidate = SnapToTerrain(point.position);
                        if (IsSpawnPositionUsable(candidate))
                        {
                            position = candidate;
                            return true;
                        }
                    }
                }
            }

            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate = GetFallbackSpawnPosition();
                if (IsSpawnPositionUsable(candidate))
                {
                    position = candidate;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        private bool IsTooCloseToPlayer(Vector3 position)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                return false;
            }

            Vector3 delta = player.transform.position - position;
            delta.y = 0f;
            return delta.sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer;
        }

        private bool IsSpawnPositionUsable(Vector3 position)
        {
            if (IsTooCloseToPlayer(position))
            {
                return false;
            }

            if (terrain != null)
            {
                if (!IsInsideTerrainBounds(position))
                {
                    return false;
                }

                if (GetTerrainSlope(position) > maxTerrainSlope)
                {
                    return false;
                }
            }

            if (Physics.CheckSphere(position + Vector3.up * 0.7f, spawnCheckRadius, wallMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
        }

        private void ResolveMapReferences()
        {
            if (terrain == null)
            {
                terrain = FindFirstObjectByType<Terrain>();
            }

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                return;
            }

            Transform mapSpawnRoot = GameObject.Find("MapRoot/SpawnPoints")?.transform;
            if (mapSpawnRoot == null)
            {
                return;
            }

            List<Transform> discovered = new List<Transform>();
            for (int i = 0; i < mapSpawnRoot.childCount; i++)
            {
                Transform child = mapSpawnRoot.GetChild(i);
                if (child != null && child.name.StartsWith("EnemySpawn_"))
                {
                    discovered.Add(child);
                }
            }

            if (discovered.Count > 0)
            {
                spawnPoints = discovered.ToArray();
            }
        }

        private Vector3 GetFallbackSpawnPosition()
        {
            if (terrain != null)
            {
                TerrainData data = terrain.terrainData;
                Vector3 origin = terrain.transform.position;
                float minX = origin.x + terrainEdgePadding;
                float maxX = origin.x + data.size.x - terrainEdgePadding;
                float minZ = origin.z + terrainEdgePadding;
                float maxZ = origin.z + data.size.z - terrainEdgePadding;
                Vector3 candidate = new Vector3(Random.Range(minX, maxX), 0f, Random.Range(minZ, maxZ));
                return SnapToTerrain(candidate);
            }

            Vector2 random = Random.insideUnitCircle * randomSpawnRadius;
            Vector3 center = transform.position;
            return new Vector3(center.x + random.x, center.y, center.z + random.y);
        }

        private Vector3 SnapToTerrain(Vector3 worldPosition)
        {
            if (terrain == null)
            {
                return worldPosition;
            }

            float y = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
            return new Vector3(worldPosition.x, y + spawnHeightOffset, worldPosition.z);
        }

        private bool IsInsideTerrainBounds(Vector3 position)
        {
            if (terrain == null)
            {
                return true;
            }

            TerrainData data = terrain.terrainData;
            Vector3 local = position - terrain.transform.position;
            return local.x >= terrainEdgePadding
                   && local.z >= terrainEdgePadding
                   && local.x <= data.size.x - terrainEdgePadding
                   && local.z <= data.size.z - terrainEdgePadding;
        }

        private float GetTerrainSlope(Vector3 position)
        {
            if (terrain == null)
            {
                return 0f;
            }

            TerrainData data = terrain.terrainData;
            Vector3 local = position - terrain.transform.position;
            float u = Mathf.Clamp01(local.x / data.size.x);
            float v = Mathf.Clamp01(local.z / data.size.z);
            return data.GetSteepness(u, v);
        }

        private void CleanupDead()
        {
            for (int i = aliveEnemies.Count - 1; i >= 0; i--)
            {
                if (aliveEnemies[i] == null)
                {
                    aliveEnemies.RemoveAt(i);
                    Log($"Enemy removed from list. Alive={aliveEnemies.Count}/{maxAlive}");
                }
            }
        }

        private void Log(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[EnemySpawner] {message}", this);
        }

        private bool IsEnemyPrefabUsable()
        {
            if (!enemyPrefab)
            {
                return false;
            }

            try
            {
                // Minimal validity check: can chase and can receive damage.
                return enemyPrefab.GetComponent<EnemyChaser>() != null
                       && enemyPrefab.GetComponent<Health>() != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private string GetEnemyPrefabLabel()
        {
            if (!enemyPrefab)
            {
                return "null_or_missing";
            }

            try
            {
                return $"'{enemyPrefab.name}'";
            }
            catch (MissingReferenceException)
            {
                return "missing_reference";
            }
        }

        private static GameObject CreateRuntimeEnemy(Vector3 position)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Enemy_Runtime";
            enemy.layer = 8; // Enemy
            enemy.transform.position = position;

            Rigidbody rb = enemy.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX
                             | RigidbodyConstraints.FreezeRotationZ;

            enemy.AddComponent<Health>();
            enemy.AddComponent<EnemyChaser>();
            enemy.AddComponent<EnemyContactDamage>();
            enemy.AddComponent<EnemyManaReward>();
            return enemy;
        }
    }
}
