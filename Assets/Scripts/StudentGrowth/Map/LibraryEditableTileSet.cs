using UnityEngine;
using UnityEngine.Tilemaps;

namespace StudentGrowth
{
    public sealed class LibraryEditableTileSet : ScriptableObject
    {
        [SerializeField] private Texture2D shellTexture;
        [SerializeField] private Texture2D propsTexture;
        [SerializeField] private Sprite[] shellSprites = new Sprite[0];
        [SerializeField] private TileBase[] shellTiles = new TileBase[0];
        [SerializeField] private Sprite[] propSprites = new Sprite[0];
        [SerializeField] private TileBase collisionTile;
        [SerializeField] private TileBase interactionTile;
        [SerializeField] private TileBase redRugTile;
        [SerializeField] private TileBase tealRugTile;

        public Texture2D ShellTexture => shellTexture;
        public Texture2D PropsTexture => propsTexture;
        public Sprite[] ShellSprites => shellSprites;
        public TileBase[] ShellTiles => shellTiles;
        public Sprite[] PropSprites => propSprites;
        public TileBase CollisionTile => collisionTile;
        public TileBase InteractionTile => interactionTile;
        public TileBase RedRugTile => redRugTile;
        public TileBase TealRugTile => tealRugTile;

        public void Configure(
            Texture2D newShellTexture,
            Texture2D newPropsTexture,
            Sprite[] newShellSprites,
            TileBase[] newShellTiles,
            Sprite[] newPropSprites,
            TileBase newCollisionTile,
            TileBase newInteractionTile,
            TileBase newRedRugTile,
            TileBase newTealRugTile)
        {
            shellTexture = newShellTexture;
            propsTexture = newPropsTexture;
            shellSprites = newShellSprites ?? new Sprite[0];
            shellTiles = newShellTiles ?? new TileBase[0];
            propSprites = newPropSprites ?? new Sprite[0];
            collisionTile = newCollisionTile;
            interactionTile = newInteractionTile;
            redRugTile = newRedRugTile;
            tealRugTile = newTealRugTile;
        }
    }
}
