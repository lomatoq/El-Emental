using System.Collections;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class Gate1CaptureRenderingPlayModeTests
    {
        [UnityTest]
        public IEnumerator TransientDuelWiringRestoresProviderOverrideAndRegistry()
        {
            int registryCountBefore = DuelShadowCasterRegistry.Shared.Count;
            GameObject lightObject = new GameObject("Gate1 Test Light");
            GameObject player = CreateRenderable("Gate1 Test Player");
            GameObject opponent = CreateRenderable("Gate1 Test Opponent");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            player.transform.position = new Vector3(-1f, 0f, 0f);
            opponent.transform.position = new Vector3(1f, 0f, 0f);
            Gate1DuelShadowCaptureScope scope = null;
            try
            {
                Assert.That(Gate1DuelShadowCaptureScope.TryBegin(
                    light,
                    player.transform,
                    opponent.transform,
                    player.GetComponentsInChildren<Renderer>(true),
                    opponent.GetComponentsInChildren<Renderer>(true),
                    out scope,
                    out string failure), Is.True, failure);
                Assert.That(scope.BoundCasterCount, Is.EqualTo(2));
                Assert.That(DuelShadowCaptureOverride.IsActive, Is.True);
                Assert.That(DuelShadowBoundsProvider.Active, Is.Not.Null);
                Assert.That(DuelShadowCasterRegistry.Shared.Count,
                    Is.EqualTo(registryCountBefore + 2));

                scope.Dispose();
                scope = null;
                yield return null;

                Assert.That(DuelShadowCaptureOverride.IsActive, Is.False);
                Assert.That(DuelShadowBoundsProvider.Active, Is.Null);
                Assert.That(DuelShadowCasterRegistry.Shared.Count,
                    Is.EqualTo(registryCountBefore));
                Assert.That(player.GetComponent<DuelShadowCaster>(), Is.Null);
                Assert.That(opponent.GetComponent<DuelShadowCaster>(), Is.Null);
            }
            finally
            {
                scope?.Dispose();
                DestroyRenderable(player);
                DestroyRenderable(opponent);
                Object.Destroy(lightObject);
            }
        }

        [UnityTest]
        public IEnumerator RepeatedDisposeCannotReleaseAReplacementOverrideOwner()
        {
            DuelShadowRuntimeSettings settings = CaptureSettings();
            DuelShadowCaptureOverride.Token first = default;
            DuelShadowCaptureOverride.Token replacement = default;
            try
            {
                Assert.That(DuelShadowCaptureOverride.TryBegin(
                    in settings,
                    out first,
                    out string failure), Is.True, failure);
                first.Dispose();
                Assert.That(DuelShadowCaptureOverride.TryBegin(
                    in settings,
                    out replacement,
                    out failure), Is.True, failure);

                first.Dispose();
                yield return null;
                Assert.That(DuelShadowCaptureOverride.IsActive, Is.True,
                    "A stale restoration token must not clear a newer capture owner.");

                replacement.Dispose();
                Assert.That(DuelShadowCaptureOverride.IsActive, Is.False);
            }
            finally
            {
                replacement.Dispose();
                first.Dispose();
            }
        }

        private static GameObject CreateRenderable(string name)
        {
            var root = new GameObject(name);
            var mesh = new Mesh
            {
                name = name + " Mesh",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0f, 0.5f, 0f)
                },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Hidden/InternalErrorShader");
            renderer.sharedMaterial = new Material(shader)
            {
                name = name + " Material"
            };
            return root;
        }

        private static void DestroyRenderable(GameObject root)
        {
            if (root == null) return;
            MeshFilter filter = root.GetComponent<MeshFilter>();
            Renderer renderer = root.GetComponent<Renderer>();
            if (filter != null && filter.sharedMesh != null)
                Object.Destroy(filter.sharedMesh);
            if (renderer != null && renderer.sharedMaterial != null)
                Object.Destroy(renderer.sharedMaterial);
            Object.Destroy(root);
        }

        private static DuelShadowRuntimeSettings CaptureSettings()
        {
            return new DuelShadowRuntimeSettings(
                DuelShadowQuality.Resolve(DuelShadowQualityTier.Low),
                new DuelShadowClassificationSettings(0.45f, 0.8f),
                new DuelShadowStabilizationSettings(
                    12f, 160f, 1.5f, 4f, 0.5f, 1f, 0.2f, 1f, 1.5f),
                16,
                0.88f,
                0.8f,
                1.8f,
                DuelShadowDebugView.ShadowOnly);
        }
    }
}
