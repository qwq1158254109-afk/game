using UnityEngine;

namespace StudentGrowth
{
    public enum MapInteractionType
    {
        LocationAction,
        Dialogue,
        Shop,
        MiniGame,
        CardBattle,
        Boss,
        ReturnToMainUi,
        EnterLocationScene
    }

    public class MapInteractionPoint : MonoBehaviour
    {
        public string locationId;
        public string actionId;
        public MapInteractionType interactionType = MapInteractionType.LocationAction;
        public string displayName;
        public string description;
        public bool requiresUnlockedLocation = true;

        public LocationActionExecutionRequest CreateExecutionRequest(LocationActionSource source)
        {
            return new LocationActionExecutionRequest
            {
                source = source,
                locationId = locationId,
                actionId = actionId,
                requiresUnlockedLocation = requiresUnlockedLocation
            };
        }
    }
}
