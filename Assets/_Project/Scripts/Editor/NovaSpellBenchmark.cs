using UnityEditor;
using UnityEngine;
using DuckovProto.Spells;
using DuckovProto.Combat;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Unity.Profiling;
using System;

public class NovaSpellBenchmark
{
    [MenuItem("Tools/Benchmark NovaSpell")]
    public static void RunBenchmark()
    {
        // Setup
        GameObject casterGo = new GameObject("Caster");
        NovaSpell nova = casterGo.AddComponent<NovaSpell>();

        // Add some dummy enemies
        int enemyCount = 100;
        GameObject[] enemies = new GameObject[enemyCount];
        for (int i = 0; i < enemyCount; i++)
        {
            enemies[i] = new GameObject($"Enemy_{i}");
            enemies[i].transform.position = casterGo.transform.position + UnityEngine.Random.insideUnitSphere * 2.5f;
            enemies[i].layer = 8; // Enemy layer
            var collider = enemies[i].AddComponent<SphereCollider>();
            enemies[i].AddComponent<Health>();
        }

        int iterations = 1000;

        // Warmup
        nova.Cast(casterGo.transform);

        // Run
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startMemory = GC.GetTotalMemory(true);
        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            nova.Cast(casterGo.transform);
        }

        sw.Stop();
        long endMemory = GC.GetTotalMemory(false);

        Debug.Log($"[NovaBenchmark] Iterations: {iterations}");
        Debug.Log($"[NovaBenchmark] Time Elapsed: {sw.ElapsedMilliseconds} ms");
        Debug.Log($"[NovaBenchmark] Estimated Allocation: {endMemory - startMemory} bytes");

        // Cleanup
        GameObject.DestroyImmediate(casterGo);
        foreach (var e in enemies)
        {
            GameObject.DestroyImmediate(e);
        }
    }
}
