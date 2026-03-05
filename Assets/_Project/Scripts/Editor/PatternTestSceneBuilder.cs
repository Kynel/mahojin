#if UNITY_EDITOR
using DuckovProto.AI;
using DuckovProto.CameraSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DuckovProto.EditorTools
{
    public static class PatternTestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype_PatternTest.unity";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string EnemyPrefabPath = "Assets/_Project/Prefabs/Enemy.prefab";

        [MenuItem("DuckovProto/Build Pattern Test Scene")]
        public static void BuildFromMenu()
        {
            BuildScene();
        }

        public static void BuildFromCommandLine()
        {
            BuildScene();
        }

        private static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "Prototype_PatternTest";

            ConfigureLighting();
            GameObject ground = CreateGround();
            GameObject player = CreatePlayer();
            GameObject enemy = CreateEnemy();
            ConfigureCameraRig(player);

            Selection.activeGameObject = player != null ? player : ground;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PatternTestSceneBuilder] Scene saved: {ScenePath}");
        }

        private static void ConfigureLighting()
        {
            Light light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.1f;
            }
        }

        private static GameObject CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 1f, 8f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                ground.layer = groundLayer;
            }

            return ground;
        }

        private static GameObject CreatePlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PatternTestSceneBuilder] Missing player prefab: {PlayerPrefabPath}");
                return null;
            }

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.transform.rotation = Quaternion.identity;
            return player;
        }

        private static GameObject CreateEnemy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PatternTestSceneBuilder] Missing enemy prefab: {EnemyPrefabPath}");
                return null;
            }

            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.name = "Enemy_Pattern";
            enemy.transform.position = new Vector3(4f, 1f, 0f);
            enemy.transform.rotation = Quaternion.identity;

            EnemyChaser chaser = enemy.GetComponent<EnemyChaser>();
            if (chaser != null)
            {
                chaser.enabled = false;
            }

            PatternEnemyMover mover = enemy.GetComponent<PatternEnemyMover>();
            if (mover == null)
            {
                mover = enemy.AddComponent<PatternEnemyMover>();
            }

            SerializedObject moverSerialized = new SerializedObject(mover);
            moverSerialized.FindProperty("radius").floatValue = 4f;
            moverSerialized.FindProperty("angularSpeedDeg").floatValue = 55f;
            moverSerialized.FindProperty("faceMoveDirection").boolValue = true;
            moverSerialized.FindProperty("heightOffset").floatValue = 0f;
            moverSerialized.ApplyModifiedPropertiesWithoutUndo();

            return enemy;
        }

        private static void ConfigureCameraRig(GameObject player)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = Object.FindFirstObjectByType<Camera>();
            }

            if (mainCamera == null)
            {
                return;
            }

            GameObject rigObject = new GameObject("CameraRig");
            GameObject pivotObject = new GameObject("Pivot");
            pivotObject.transform.SetParent(rigObject.transform, false);

            FollowCameraRig rig = rigObject.AddComponent<FollowCameraRig>();
            if (player != null)
            {
                rigObject.transform.position = player.transform.position;
            }

            mainCamera.transform.SetParent(pivotObject.transform, false);
            mainCamera.transform.localPosition = new Vector3(0f, 0f, -20f);
            mainCamera.transform.localRotation = Quaternion.identity;
            mainCamera.tag = "MainCamera";
        }
    }
}
#endif
