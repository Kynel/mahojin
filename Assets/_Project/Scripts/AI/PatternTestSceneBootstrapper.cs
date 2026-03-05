using DuckovProto.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuckovProto.AI
{
    public static class PatternTestSceneBootstrapper
    {
        private const string SceneName = "Prototype_PatternTest";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name != SceneName)
            {
                return;
            }

            ApplyPatternOverrides();
        }

        private static void ApplyPatternOverrides()
        {
            DisableRuntimeSystems();
            EnsurePatternEnemyMover();
        }

        private static void DisableRuntimeSystems()
        {
            EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null)
                {
                    spawners[i].enabled = false;
                }
            }

            PrototypeMapBootstrapper[] mapBootstrappers = Object.FindObjectsByType<PrototypeMapBootstrapper>(FindObjectsSortMode.None);
            for (int i = 0; i < mapBootstrappers.Length; i++)
            {
                if (mapBootstrappers[i] != null)
                {
                    mapBootstrappers[i].enabled = false;
                }
            }

            ArenaWallBootstrapper[] wallBootstrappers = Object.FindObjectsByType<ArenaWallBootstrapper>(FindObjectsSortMode.None);
            for (int i = 0; i < wallBootstrappers.Length; i++)
            {
                if (wallBootstrappers[i] != null)
                {
                    wallBootstrappers[i].enabled = false;
                }
            }
        }

        private static void EnsurePatternEnemyMover()
        {
            EnemyChaser[] chasers = Object.FindObjectsByType<EnemyChaser>(FindObjectsSortMode.None);
            for (int i = 0; i < chasers.Length; i++)
            {
                if (chasers[i] != null)
                {
                    chasers[i].enabled = false;
                }
            }

            PatternEnemyMover mover = Object.FindFirstObjectByType<PatternEnemyMover>();
            if (mover != null)
            {
                return;
            }

            Health[] enemies = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                Health health = enemies[i];
                if (health == null)
                {
                    continue;
                }

                if (health.gameObject.layer != LayerMask.NameToLayer("Enemy"))
                {
                    continue;
                }

                health.gameObject.AddComponent<PatternEnemyMover>();
                break;
            }
        }
    }
}
