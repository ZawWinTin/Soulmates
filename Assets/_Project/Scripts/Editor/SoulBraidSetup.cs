using UnityEditor;
using UnityEngine;

// Installs the soul braid (SoulBraid.cs) on the stack's Heart object in Grid.prefab: two puff
// particle systems (behind and in front of the hug), the candy heart switched to pop on the knot
// instead of on its own timer, and a shorter LoveSmoke puff that only masks the slime→hug swap.
// Safe to re-run: the braid systems are rebuilt each time.
public static class SoulBraidSetup
{
    const string GridPath = "Assets/_Project/Prefabs/Grid.prefab";
    const int BackOrder = 19; // just under the hug (20)
    const int FrontOrder = 102; // over the LoveSmoke puff (100) and the heart (101)

    [MenuItem("Tools/Soulmates/Apply Soul Braid")]
    public static void Apply()
    {
        var grid = PrefabUtility.LoadPrefabContents(GridPath);
        try
        {
            var set = Find(grid.transform, "Player1+Player2");
            var heart = set != null && set.childCount > 2 ? set.GetChild(2) : null; // [2] = Heart
            var smoke = heart != null ? heart.Find("LoveSmoke") : null;
            var hearts = heart != null ? heart.Find("LoveHearts") : null;
            if (smoke == null || hearts == null)
            {
                Debug.LogWarning(
                    "[SoulBraidSetup.Apply] Heart with LoveSmoke + LoveHearts not found; run Apply Love Smoke first."
                );
                return;
            }
            var smokeMat = smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial;

            // Shorter, smaller puff: it only has to hide the swap now, the braid is the show.
            var ps = smoke.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)18) });

            // The heart pops when SoulBraid knots the wisps (Emit), not on its own burst.
            var hp = hearts.GetComponent<ParticleSystem>();
            var hmain = hp.main;
            hmain.duration = 3f;
            var hem = hp.emission;
            hem.enabled = false;

            // re-added each run so the prefab picks up SoulBraid's current default tuning
            var oldBraid = heart.GetComponent<SoulBraid>();
            if (oldBraid != null)
                Object.DestroyImmediate(oldBraid, true);
            var braid = heart.gameObject.AddComponent<SoulBraid>();
            braid.backPuffs = BuildPuffs(heart, "BraidBack", smokeMat, BackOrder);
            braid.frontPuffs = BuildPuffs(heart, "BraidFront", smokeMat, FrontOrder);
            braid.hearts = hp;

            PrefabUtility.SaveAsPrefabAsset(grid, GridPath);
            Debug.Log("[SoulBraidSetup.Apply] Soul braid installed on the stack's Heart.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(grid);
        }
    }

    // A puff system fed only by SoulBraid.Emit: each puff drifts up, swells and fades like smoke.
    static ParticleSystem BuildPuffs(Transform heart, string name, Material mat, int order)
    {
        var old = heart.Find(name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name, typeof(ParticleSystem));
        go.transform.SetParent(heart, false);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 3f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 400;

        var emission = ps.emission;
        emission.enabled = false; // SoulBraid emits
        var shape = ps.shape;
        shape.enabled = false;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        vel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.18f); // smoke keeps drifting up
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.8f;
        noise.scrollSpeed = 0.6f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f); // slow curl

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.9f)
        );

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.7f, 0.45f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sharedMaterial = mat;
        psr.sortingOrder = order;
        return ps;
    }

    static Transform Find(Transform t, string name)
    {
        if (t.name == name)
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var hit = Find(t.GetChild(i), name);
            if (hit != null)
                return hit;
        }
        return null;
    }
}
