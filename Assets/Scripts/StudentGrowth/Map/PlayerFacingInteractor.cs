using UnityEngine;

namespace StudentGrowth
{
    public sealed class PlayerFacingInteractor : MonoBehaviour
    {
        public MapInteractionController mapInteractionController;
        public MapInteractionTarget[] targets = new MapInteractionTarget[0];
        public Vector2 facingDirection = Vector2.down;

        public MapInteractionTarget currentTarget { get; private set; }

        public void SetFacingDirection(Vector2 direction)
        {
            facingDirection = MapFacingInteractionUtility.ToCardinal(direction, facingDirection);
        }

        public MapInteractionTarget RefreshCurrentTarget(Vector2 playerPosition)
        {
            MapInteractionTarget best = null;
            float bestDistance = float.MaxValue;
            int bestPriority = int.MinValue;
            if (targets == null)
            {
                currentTarget = null;
                return currentTarget;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                MapInteractionTarget target = targets[i];
                if (target == null || !target.CanSelect(playerPosition, facingDirection))
                {
                    continue;
                }

                float distance = ((Vector2)target.transform.position - playerPosition).sqrMagnitude;
                if (target.priority > bestPriority || target.priority == bestPriority && distance < bestDistance)
                {
                    best = target;
                    bestPriority = target.priority;
                    bestDistance = distance;
                }
            }

            currentTarget = best;
            return currentTarget;
        }

        public LocationActionExecutionResult TryInteractCurrent()
        {
            if (currentTarget == null || mapInteractionController == null)
            {
                return new LocationActionExecutionResult
                {
                    success = false,
                    source = LocationActionSource.Map,
                    errorCode = "MissingFacingInteractionTarget",
                    message = "没有面向可互动对象。"
                };
            }

            return mapInteractionController.TryInteract(currentTarget);
        }
    }
}
