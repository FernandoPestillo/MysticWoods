using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MysticWoods.Tests.World
{
    /// <summary>Guards the physics rules of the scenery prefabs (see Assets/Editor/Scenery/SceneryPhysics.cs).</summary>
    public sealed class SceneryPhysicsTests
    {
        private const string Prefabs = "Assets/Art/Environment/Prefabs";
        private const string Level = "Assets/Scenes/FaseMysticWoods.unity";
        private static readonly string[] Walkable =
            { "FernBush", "FlowerBush", "MushroomBush", "GlowShrooms", "BerryBush", "TallGrass", "Bramble", "Bellflowers", "StoneMedallion" };
        private static readonly string[] Gameplay = { "BossGate", "ArenaEntrySeal", "FinalSeal", "GoldKey", "LevelExit", "ArenaEntrance" };

        private static GameObject[] AllPrefabs() => AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        private static bool Fades(GameObject go) => go.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "TransparetObject");

        [Test]
        public void EnvironmentLayerExistsAndCollidesWithCharacters()
        {
            int layer = LayerMask.NameToLayer("Environment");
            Assert.That(layer, Is.GreaterThan(0), "Environment layer missing");
            Assert.IsFalse(Physics2D.GetIgnoreLayerCollision(layer, 0), "Environment must collide with Default (player and enemies)");
        }

        [Test]
        public void ObstaclesAreStaticFrictionlessAndOnEnvironmentLayer()
        {
            int layer = LayerMask.NameToLayer("Environment");
            foreach (var prefab in AllPrefabs().Where(p => !Gameplay.Contains(p.name)))
            {
                var solids = prefab.GetComponents<Collider2D>().Where(c => !c.isTrigger).ToArray();
                if (solids.Length == 0) continue;
                Assert.AreEqual(layer, prefab.layer, prefab.name + " layer");
                var body = prefab.GetComponent<Rigidbody2D>();
                Assert.IsNotNull(body, prefab.name + " needs a Rigidbody2D");
                Assert.AreEqual(RigidbodyType2D.Static, body.bodyType, prefab.name + " body type");
                foreach (var c in solids)
                {
                    Assert.IsNotNull(c.sharedMaterial, prefab.name + " collider without physics material");
                    Assert.AreEqual(0f, c.sharedMaterial.friction, prefab.name + " friction");
                }
            }
        }

        [Test]
        public void SolidShapesOnlyCoverTheGroundFootprint()
        {
            foreach (var prefab in AllPrefabs().Where(p => !Gameplay.Contains(p.name)))
            {
                var sprite = prefab.GetComponent<SpriteRenderer>()?.sprite;
                if (sprite == null) continue;
                Bounds b = sprite.bounds;
                foreach (var c in prefab.GetComponents<Collider2D>().Where(c => !c.isTrigger))
                {
                    float top = c switch
                    {
                        BoxCollider2D box => box.offset.y + box.size.y / 2,
                        CapsuleCollider2D cap => cap.offset.y + cap.size.y / 2,
                        CircleCollider2D circle => circle.offset.y + circle.radius,
                        _ => float.PositiveInfinity,
                    };
                    Assert.LessOrEqual(top, b.min.y + b.size.y * .35f, $"{prefab.name}: solid collider reaches into the canopy/body art");
                    Assert.GreaterOrEqual(c.offset.y, b.min.y, $"{prefab.name}: solid collider below the sprite");
                }
            }
        }

        [Test]
        public void FadingPropsHaveExactlyOneCanopyTrigger()
        {
            foreach (var prefab in AllPrefabs().Where(Fades))
                Assert.AreEqual(1, prefab.GetComponents<Collider2D>().Count(c => c.isTrigger), prefab.name + " canopy triggers");
        }

        [Test]
        public void LowVegetationIsWalkable()
        {
            foreach (var prefab in AllPrefabs().Where(p => Walkable.Contains(p.name)))
            {
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider2D>(true), prefab.name + " should be walkable");
                Assert.IsNull(prefab.GetComponent<Rigidbody2D>(), prefab.name + " should have no body");
            }
        }

        [Test]
        public void FlippedInstancesHaveMirroredColliders()
        {
            var scene = EditorSceneManager.OpenScene(Level, OpenSceneMode.Single);
            int checkedCount = 0;
            foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null || !AssetDatabase.GetAssetPath(source).StartsWith(Prefabs) || Gameplay.Contains(source.name)) continue;
                var renderer = t.GetComponent<SpriteRenderer>();
                var colliders = t.GetComponents<Collider2D>();
                var originals = source.GetComponents<Collider2D>();
                Assert.AreEqual(originals.Length, colliders.Length, t.name + " collider count");
                for (int i = 0; i < colliders.Length; i++)
                {
                    float expected = renderer != null && renderer.flipX ? -originals[i].offset.x : originals[i].offset.x;
                    Assert.AreEqual(expected, colliders[i].offset.x, 1e-4f, $"{t.name} collider {i} offset.x (flipX={renderer?.flipX})");
                    checkedCount++;
                }
            }
            Assert.Greater(checkedCount, 300, "expected the level's scenery instances");
        }
    }
}
