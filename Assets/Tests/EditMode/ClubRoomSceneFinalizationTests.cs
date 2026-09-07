using System;
using System.Linq;
using NUnit.Framework;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public sealed class ClubRoomSceneFinalizationTests
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/ClubRoomMapScene.unity";
        private const string FinalSpriteRoot = "Assets/Art/New2_0/Club/Final/Facilities";

        [SetUp]
        public void OpenClubScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [Test]
        public void SpawnIsSeparatedFromFacingExitAndStaticCollision()
        {
            GameObject spawn = GameObject.Find("ClubEntrySpawn");
            GameObject player = GameObject.Find("PlayerRpg");
            GameObject exit = GameObject.Find("ClubExitDoor");
            GameObject collision = GameObject.Find("Collision");
            Assert.NotNull(spawn);
            Assert.NotNull(player);
            Assert.NotNull(exit);
            Assert.NotNull(collision.transform.Find("DoorCollision"));

            MapInteractionTarget exitTarget = exit.GetComponent<MapInteractionTarget>();
            Assert.IsTrue(exitTarget.requireFacing);
            Assert.LessOrEqual(exitTarget.interactionRange, 0.8f);
            Assert.Greater(Vector2.Distance(spawn.transform.position, exit.transform.position),
                exitTarget.interactionRange + 0.1f);

            Collider2D playerCollider = player.GetComponent<Collider2D>();
            player.transform.position = spawn.transform.position;
            Physics2D.SyncTransforms();
            Assert.IsFalse(collision.GetComponentsInChildren<Collider2D>(true)
                .Any(item => item.bounds.Intersects(playerCollider.bounds)));
        }

        [Test]
        public void EditableFacilitiesUseNormalizedScaleOneSprites()
        {
            SpriteRenderer[] renderers = GameObject.Find("EditableClubFacilities")
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Concat(GameObject.Find("EditableClubWallDecor")
                    .GetComponentsInChildren<SpriteRenderer>(true))
                .ToArray();
            Assert.AreEqual(31, renderers.Length);
            foreach (SpriteRenderer renderer in renderers)
            {
                Assert.AreEqual(Vector3.one, renderer.transform.lossyScale, renderer.name);
                string path = AssetDatabase.GetAssetPath(renderer.sprite);
                StringAssert.StartsWith(FinalSpriteRoot, path, renderer.name);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.NotNull(importer, path);
                Assert.AreEqual(64f, importer.spritePixelsPerUnit, 0.001f, path);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed,
                    importer.textureCompression, path);
            }
        }

        [Test]
        public void MeetingTableUsesFourShortFacingEdgeInteractions()
        {
            MapInteractionTarget[] targets = UnityEngine.Object
                .FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Include)
                .Where(target => target.interactionPoint != null
                    && target.interactionPoint.actionId == "club_meeting")
                .ToArray();
            Assert.AreEqual(4, targets.Length);
            Assert.IsTrue(targets.All(target => target.requireFacing));
            Assert.IsTrue(targets.All(target => target.interactionRange <= 0.9f));
        }

        [Test]
        public void ExistingColliderAndYSortSystemsRemainIntact()
        {
            GameObject facilities = GameObject.Find("EditableClubFacilities");
            Assert.GreaterOrEqual(facilities.GetComponentsInChildren<Collider2D>(true).Length, 15);
            Assert.GreaterOrEqual(facilities.GetComponentsInChildren<MapObjectYSorter>(true).Length, 15);
            FullscreenMapSceneController controller = UnityEngine.Object
                .FindAnyObjectByType<FullscreenMapSceneController>();
            Assert.NotNull(controller);
            Assert.AreEqual("location_club", controller.mapId);
            Assert.GreaterOrEqual(controller.interactionTargets.Length, 7);
        }
    }
}
