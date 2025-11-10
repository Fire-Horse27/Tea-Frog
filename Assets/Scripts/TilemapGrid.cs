using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class TilemapGrid : MonoBehaviour
{
    [Header("Tilemaps")]
    [Tooltip("Tiles here are considered blocking (walls, obstacles).")]
    public Tilemap collisionTilemap;   // tiles here block movement
    [Tooltip("Tiles here represent floor/walkable areas.")]
    public Tilemap walkableTilemap;    // tiles here are potentially walkable

    [Header("Debug")]
    public bool drawDebug = false;
    public Color debugWalkableColor = new Color(0f, 1f, 0f, 0.15f);
    public Color debugBlockedColor = new Color(1f, 0f, 0f, 0.15f);

    private Dictionary<Vector3Int, bool> walkable = new Dictionary<Vector3Int, bool>();
    private BoundsInt unionBounds;

    void OnEnable()
    {
        BuildGrid();
    }

    // Call this if you edit tilemaps at runtime or in the editor and want to recompute.
    public void RebuildGrid()
    {
        BuildGrid();
    }

    public void BuildGrid()
    {
        walkable.Clear();
        if (collisionTilemap == null && walkableTilemap == null)
        {
            Debug.LogWarning("TilemapGrid: assign at least one tilemap.");
            return;
        }

        // compute bounds that include both tilemaps
        BoundsInt b = new BoundsInt();
        bool first = true;
        if (collisionTilemap != null)
        {
            if (first) { b = collisionTilemap.cellBounds; first = false; }
            else b = UnionBounds(b, collisionTilemap.cellBounds);
        }
        if (walkableTilemap != null)
        {
            if (first) { b = walkableTilemap.cellBounds; first = false; }
            else b = UnionBounds(b, walkableTilemap.cellBounds);
        }

        unionBounds = b;

        for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);

                bool hasWalkable = (walkableTilemap != null) && walkableTilemap.HasTile(cell);
                bool hasCollision = (collisionTilemap != null) && collisionTilemap.HasTile(cell);

                // decide: tile is walkable only if it's marked as floor AND not blocked by collision
                bool isWalkable = hasWalkable && !hasCollision;

                walkable[cell] = isWalkable;
            }
    }

    // helper: union of bounds
    private BoundsInt UnionBounds(BoundsInt a, BoundsInt b)
    {
        int xMin = Mathf.Min(a.xMin, b.xMin);
        int yMin = Mathf.Min(a.yMin, b.yMin);
        int xMax = Mathf.Max(a.xMax, b.xMax);
        int yMax = Mathf.Max(a.yMax, b.yMax);
        return new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
    }

    public bool IsWalkable(Vector3Int cell)
    {
        if (walkable.TryGetValue(cell, out bool w)) return w;
        return false; // outside union considered non-walkable
    }

    public Vector3 CellCenterWorld(Vector3Int cell)
    {
        // prefer walkableTilemap for world conversion if available, else collisionTilemap
        if (walkableTilemap != null) return walkableTilemap.CellToWorld(cell) + walkableTilemap.tileAnchor;
        if (collisionTilemap != null) return collisionTilemap.CellToWorld(cell) + collisionTilemap.tileAnchor;
        return (Vector3)cell;
    }

    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        // prefer walkable tilemap for consistent rounding
        if (walkableTilemap != null) return walkableTilemap.WorldToCell(worldPos);
        if (collisionTilemap != null) return collisionTilemap.WorldToCell(worldPos);
        return Vector3Int.FloorToInt(worldPos);
    }

    public List<Vector3Int> GetNeighbors(Vector3Int cell)
    {
        var neighbors = new List<Vector3Int>(4);
        Vector3Int[] deltas = { new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0) };
        foreach (var d in deltas)
        {
            var n = cell + d;
            if (walkable.ContainsKey(n) && walkable[n]) neighbors.Add(n);
        }
        return neighbors;
    }

    // optional debug: draw colored quads for walkable/blocked tiles
    void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;
        if (unionBounds.size == Vector3Int.zero) BuildGrid();
        if (unionBounds.size == Vector3Int.zero) return;

        foreach (var kv in walkable)
        {
            Vector3Int cell = kv.Key;
            bool w = kv.Value;
            Vector3 center = CellCenterWorld(cell);
            Vector3 size = Vector3.one * (walkableTilemap != null ? walkableTilemap.cellSize.x : 1f);
            Gizmos.color = w ? debugWalkableColor : debugBlockedColor;
            Gizmos.DrawCube(center + new Vector3(0.5f * size.x, 0.5f * size.y, 0f) - new Vector3(0.5f * size.x, 0.5f * size.y, 0f), size * 0.9f);
        }
    }
}
