using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class MapFacingInteractionUtilityTests
    {
        private GameObject[] createdObjects = new GameObject[0];

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdObjects.Length; i++)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects = new GameObject[0];
        }

        [Test]
        public void FacingTargetInsideRangeCanInteract()
        {
            Rect range = new Rect(-1f, -1f, 2f, 2f);

            bool canInteract = MapFacingInteractionUtility.IsInsideRangeAndFacing(
                range,
                Vector2.zero,
                Vector2.up,
                new Vector2(0f, 0.75f));

            Assert.IsTrue(canInteract);
        }

        [Test]
        public void BackToTargetCannotInteract()
        {
            Rect range = new Rect(-1f, -1f, 2f, 2f);

            bool canInteract = MapFacingInteractionUtility.IsInsideRangeAndFacing(
                range,
                Vector2.zero,
                Vector2.down,
                new Vector2(0f, 0.75f));

            Assert.IsFalse(canInteract);
        }

        [Test]
        public void OutsideRangeCannotInteractEvenWhenFacing()
        {
            Rect range = new Rect(-1f, -1f, 2f, 2f);

            bool canInteract = MapFacingInteractionUtility.IsInsideRangeAndFacing(
                range,
                new Vector2(0f, -2f),
                Vector2.up,
                Vector2.zero);

            Assert.IsFalse(canInteract);
        }

        [Test]
        public void DominantInputAxisBecomesCardinalFacing()
        {
            Assert.AreEqual(Vector2.right, MapFacingInteractionUtility.ToCardinal(new Vector2(2f, 1f), Vector2.down));
            Assert.AreEqual(Vector2.down, MapFacingInteractionUtility.ToCardinal(new Vector2(0.1f, -0.9f), Vector2.up));
        }

        [Test]
        public void MapInteractionTargetRequiresFacingWhenEnabled()
        {
            GameObject targetObject = new GameObject("FacingTarget");
            createdObjects = new[] { targetObject };
            targetObject.transform.position = new Vector3(0f, 1f, 0f);
            MapInteractionTarget target = targetObject.AddComponent<MapInteractionTarget>();
            target.interactionRange = 2f;
            target.requireFacing = true;

            Assert.IsTrue(target.CanSelect(Vector2.zero, Vector2.up));
            Assert.IsFalse(target.CanSelect(Vector2.zero, Vector2.down));
        }

        [Test]
        public void PlayerFacingInteractorSelectsHighestPriorityFacingTarget()
        {
            GameObject interactorObject = new GameObject("PlayerFacingInteractor");
            GameObject lowPriorityObject = new GameObject("LowPriorityTarget");
            GameObject highPriorityObject = new GameObject("HighPriorityTarget");
            createdObjects = new[] { interactorObject, lowPriorityObject, highPriorityObject };

            lowPriorityObject.transform.position = new Vector3(0f, 0.5f, 0f);
            highPriorityObject.transform.position = new Vector3(0f, 0.75f, 0f);

            MapInteractionTarget lowPriority = lowPriorityObject.AddComponent<MapInteractionTarget>();
            lowPriority.interactionRange = 2f;
            lowPriority.priority = 0;
            MapInteractionTarget highPriority = highPriorityObject.AddComponent<MapInteractionTarget>();
            highPriority.interactionRange = 2f;
            highPriority.priority = 10;

            PlayerFacingInteractor interactor = interactorObject.AddComponent<PlayerFacingInteractor>();
            interactor.targets = new[] { lowPriority, highPriority };
            interactor.SetFacingDirection(Vector2.up);

            Assert.AreEqual(highPriority, interactor.RefreshCurrentTarget(Vector2.zero));
        }

        [Test]
        public void LowerYSortsAboveHigherY()
        {
            int highObjectOrder = MapObjectYSorter.CalculateSortingOrder(1f, 1000, 100f);
            int lowObjectOrder = MapObjectYSorter.CalculateSortingOrder(-1f, 1000, 100f);

            Assert.Greater(lowObjectOrder, highObjectOrder);
        }
    }
}
