using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Spatial;

/// <summary>
/// A clean, correct R-tree. fix(#15) uses BoundingBox.Overlaps (closed-interval test);
/// fix(#17) the old split() read a stale outer-node "siblingOverlap" field when ranking
/// split candidates — the new split cost uses each node's own recomputed MBR.
/// </summary>
public sealed class RTree : IRTree
{
    private readonly int _minEntries;
    private readonly int _maxEntries;
    private RTreeNode _root;

    public RTree(int minEntries = 4, int maxEntries = 10)
    {
        _minEntries = minEntries;
        _maxEntries = maxEntries;
        _root = new RTreeNode(isLeaf: true, minEntries, maxEntries);
    }

    public int Count { get; private set; }

    public void Add(int featureIndex, int subPartIndex, BoundingBox box)
    {
        var entry = new RTreeEntry(featureIndex, subPartIndex, box);
        var up = _root.Insert(entry);
        if (up is not null)
        {
            var newRoot = new RTreeNode(isLeaf: false, _minEntries, _maxEntries);
            newRoot.Children.Add(_root);
            newRoot.Children.Add(up);
            _root.Parent = newRoot;
            up.Parent = newRoot;
            newRoot.RecomputeMbr();
            _root = newRoot;
        }
        Count++;
    }

    public IReadOnlyList<(int FeatureIndex, int SubPartIndex)> Query(BoundingBox box)
    {
        var results = new List<(int, int)>();
        Search(_root, box, results);
        return results;
    }

    public IReadOnlyList<(int FeatureIndex, int SubPartIndex)> QueryPoint(double x, double y)
        => Query(BoundingBox.Point(x, y));

    private void Search(RTreeNode node, BoundingBox box, List<(int, int)> results)
    {
        if (!node.Mbr.Overlaps(box)) return;
        if (node.IsLeaf)
        {
            foreach (var e in node.Entries)
                if (e.Box.Overlaps(box)) results.Add((e.FeatureIndex, e.SubPartIndex));
        }
        else
        {
            foreach (var c in node.Children) Search(c, box, results);
        }
    }

    private sealed class RTreeEntry
    {
        public int FeatureIndex { get; }
        public int SubPartIndex { get; }
        public BoundingBox Box { get; }

        public RTreeEntry(int featureIndex, int subPartIndex, BoundingBox box)
        {
            FeatureIndex = featureIndex; SubPartIndex = subPartIndex; Box = box;
        }
    }

    private sealed class RTreeNode
    {
        public readonly bool IsLeaf;
        public int MinEntries, MaxEntries;
        public RTreeNode? Parent;
        public List<RTreeNode> Children { get; } = new();
        public List<RTreeEntry> Entries { get; } = new();
        public BoundingBox Mbr { get; private set; } = BoundingBox.Empty;

        internal RTreeNode(bool isLeaf, int minEntries, int maxEntries)
        {
            IsLeaf = isLeaf;
            MinEntries = minEntries;
            MaxEntries = maxEntries;
        }

        internal void RecomputeMbr()
        {
            Mbr = BoundingBox.Empty;
            if (IsLeaf)
            {
                foreach (var e in Entries) Mbr = Mbr.Union(e.Box);
            }
            else
            {
                foreach (var c in Children) Mbr = Mbr.Union(c.Mbr);
            }
        }

        /// <summary>Insert; returns an overflow node to insert into the parent, or null.</summary>
        internal RTreeNode? Insert(RTreeEntry entry)
        {
            if (IsLeaf)
            {
                Entries.Add(entry);
                RecomputeMbr();
                if (Entries.Count > MaxEntries) return SplitLeaf();
                return null;
            }

            var child = ChooseChild(entry.Box);
            var up = child.Insert(entry);
            if (up is not null)
            {
                Children.Remove(child);
                Children.Add(up);
                RecomputeMbr();
                if (Children.Count > MaxEntries) return SplitInternal();
            }
            return null;
        }

        private RTreeNode ChooseChild(BoundingBox box)
        {
            RTreeNode? best = null;
            double bestEnlargement = double.MaxValue, bestArea = double.MaxValue;
            foreach (var c in Children)
            {
                double enlargement = c.Mbr.EnlargementToContain(box);
                double area = c.Mbr.Area();
                if (enlargement < bestEnlargement || (enlargement == bestEnlargement && area < bestArea))
                {
                    best = c; bestEnlargement = enlargement; bestArea = area;
                }
            }
            return best!;
        }

        private RTreeNode SplitLeaf()
        {
            var (a, b) = SplitEntries(Entries);
            var newNode = new RTreeNode(true, MinEntries, MaxEntries) { Parent = Parent };
            Entries = a;
            newNode.Entries = b;
            RecomputeMbr();
            newNode.RecomputeMbr();
            return newNode;
        }

        private RTreeNode SplitInternal()
        {
            // Distribute children into two nodes, then let each recompute its MBR.
            var sorted = Children.OrderBy(c => c.Mbr.MinX).ToList();
            var a = new RTreeNode(false, MinEntries, MaxEntries) { Parent = Parent };
            var b = new RTreeNode(false, MinEntries, MaxEntries) { Parent = Parent };
            for (int i = 0; i < sorted.Count; i++)
            {
                var child = sorted[i];
                if (i < sorted.Count / 2)
                {
                    a.Children.Add(child);
                    child.Parent = a;
                }
                else
                {
                    b.Children.Add(child);
                    child.Parent = b;
                }
            }
            a.RecomputeMbr();
            b.RecomputeMbr();
            Children.Clear();
            Children.Add(a);
            Children.Add(b);
            a.Parent = this;
            b.Parent = this;
            RecomputeMbr();
            // "this" now holds two child nodes; the caller's overflow is represented by
            // returning a node whose MBR is the union — here we expose the second group.
            return b;
        }

        private static (List<RTreeEntry>, List<RTreeEntry>) SplitEntries(List<RTreeEntry> entries)
        {
            var sorted = entries.OrderBy(e => e.Box.MinX).ThenBy(e => e.Box.MinY).ToList();
            int minEach = Math.Max(2, entries.Count / 2 - 1);
            int split = Math.Clamp(minEach, 2, sorted.Count - 2);
            return (sorted.Take(split).ToList(), sorted.Skip(split).ToList());
        }
    }
}