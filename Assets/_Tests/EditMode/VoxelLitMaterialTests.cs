using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProjectB.Arena;
using ProjectB.Enemies;

namespace ProjectB.Tests.EditMode
{
    /// <summary>
    /// Hit flash, elite/boss rim and barrel fuse glow are driven through <c>ProjectB/VoxelLit</c> properties
    /// (<c>_HitFlash</c>, <c>_RimColor</c>) via MaterialPropertyBlock. A renderer on a stock URP shader silently
    /// ignores them (Slime / BatShooter / ExplosiveBarrel regression), so every enemy and barrel mesh must use VoxelLit.
    /// </summary>
    public class VoxelLitMaterialTests
    {
        private const string VoxelLitShaderName = "ProjectB/VoxelLit";

        [Test]
        public void EnemyAndBarrelPrefabs_UseVoxelLitOnAllMeshRenderers()
        {
            var failures = new StringBuilder();
            int checkedPrefabs = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Prefabs/Enemies", "Assets/_Prefabs/Arena" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                bool needsFlash = prefab.GetComponent<EnemyBase>() != null || prefab.GetComponent<ExplosiveBarrel>() != null;
                if (!needsFlash) continue;
                checkedPrefabs++;

                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Same exclusion as EnemyBase.CacheEnemyRenderers: telegraph decals are not flashed.
                    if (renderer.gameObject.name.Contains("Telegraph")) continue;

                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null || mat.shader == null || mat.shader.name != VoxelLitShaderName)
                        {
                            failures.AppendLine($"{path} / {renderer.name}: {(mat != null ? mat.name : "<null>")} ({(mat != null && mat.shader != null ? mat.shader.name : "no shader")})");
                        }
                    }
                }
            }

            Assert.Greater(checkedPrefabs, 10, "Expected to scan all enemy prefabs and the explosive barrel");
            Assert.IsEmpty(failures.ToString(), "Renderers without VoxelLit cannot hit-flash or show rim:\n" + failures);
        }
    }
}
