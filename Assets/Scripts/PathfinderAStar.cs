using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathfinderAStar : MonoBehaviour
{
    public TilemapGrid grid;

    public List<Vector3> FindPath(Vector3 fromWorld, Vector3 toWorld)
    {
        var start = grid.WorldToCell(fromWorld);
        var goal = grid.WorldToCell(toWorld);

        var closed = new HashSet<Vector3Int>();
        var open = new PriorityQueue(); // small internal priority queue below
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, int>();
        gScore[start] = 0;

        open.Enqueue(start, Heuristic(start, goal));

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current == goal) return ReconstructPath(cameFrom, current);
            closed.Add(current);

            foreach (var neighbor in grid.GetNeighbors(current))
            {
                if (closed.Contains(neighbor)) continue;
                int tentativeG = gScore[current] + 1; // uniform cost

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    int f = tentativeG + Heuristic(neighbor, goal);
                    if (!open.Contains(neighbor)) open.Enqueue(neighbor, f);
                    else open.UpdatePriority(neighbor, f);
                }
            }
        }
        // no path
        return new List<Vector3>();
    }

    int Heuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan for grid
    }

    List<Vector3> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
    {
        var path = new List<Vector3Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        // convert to world positions (cell centers)
        return path.Select(cell => grid.CellCenterWorld(cell)).ToList();
    }

    // tiny priority queue (min-heap behavior using SortedDictionary for simplicity)
    class PriorityQueue
    {
        private SortedDictionary<int, Queue<Vector3Int>> dict = new SortedDictionary<int, Queue<Vector3Int>>();
        private HashSet<Vector3Int> set = new HashSet<Vector3Int>();
        public int Count
        {
            get
            {
                int c = 0; foreach (var q in dict.Values) c += q.Count; return c;
            }
        }
        public void Enqueue(Vector3Int item, int priority)
        {
            if (!dict.TryGetValue(priority, out var q)) { q = new Queue<Vector3Int>(); dict[priority] = q; }
            q.Enqueue(item); set.Add(item);
        }
        public Vector3Int Dequeue()
        {
            var key = dict.Keys.First();
            var q = dict[key];
            var v = q.Dequeue();
            if (q.Count == 0) dict.Remove(key);
            set.Remove(v);
            return v;
        }
        public bool Contains(Vector3Int item) => set.Contains(item);
        // Update is noop here — we just enqueue again (could cause duplicates but gScore check prevents issues)
        public void UpdatePriority(Vector3Int item, int newPriority) { Enqueue(item, newPriority); }
    }
}

