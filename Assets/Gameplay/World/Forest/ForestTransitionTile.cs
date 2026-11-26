using UnityEngine;
using UnityEngine.Tilemaps;

namespace MysticWoods.Environment
{
    [CreateAssetMenu(menuName = "Mystic Woods/Forest path tile")]
    public sealed class ForestTransitionTile : TileBase
    {
        public Sprite sprite;

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    tilemap.RefreshTile(position + new Vector3Int(x, y, 0));
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            int mask = 0, diagonal = 0;
            if (tilemap.GetTile(position + Vector3Int.left) == this) mask |= 1;
            if (tilemap.GetTile(position + Vector3Int.right) == this) mask |= 2;
            if (tilemap.GetTile(position + Vector3Int.down) == this) mask |= 4;
            if (tilemap.GetTile(position + Vector3Int.up) == this) mask |= 8;
            if (tilemap.GetTile(position + new Vector3Int(-1, -1, 0)) == this) diagonal |= 1;
            if (tilemap.GetTile(position + new Vector3Int(1, -1, 0)) == this) diagonal |= 2;
            if (tilemap.GetTile(position + new Vector3Int(-1, 1, 0)) == this) diagonal |= 4;
            if (tilemap.GetTile(position + new Vector3Int(1, 1, 0)) == this) diagonal |= 8;
            tileData.sprite = sprite;
            tileData.color = new Color(mask / 15f, diagonal / 15f, 1, 1);
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
        }
    }
}
