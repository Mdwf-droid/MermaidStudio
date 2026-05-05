using MermaidStudio.Domain.Diagrams;
using MermaidStudio.Domain.States;

namespace MermaidStudio.Application.Layout;

public sealed class StateDiagramLayoutService
{
    private const double BaseX = 120;
    private const double BaseY = 90;
    private const double SiblingGap = 90;
    private const double RootGap = 150;
    private const double LevelGap = 95;

    public void ApplyLayout(DiagramDocument document)
    {
        if (document.Kind != DiagramKind.StateDiagram)
            throw new InvalidOperationException("Le document fourni n'est pas un State Diagram.");

        if (document.StateNodes.Count == 0)
            return;

        var orderedNodes = document.StateNodes
            .Select((node, index) => new { Node = node, Index = index })
            .ToList();

        var orderIndex = orderedNodes.ToDictionary(
            x => x.Node.Id.Value,
            x => x.Index,
            StringComparer.Ordinal);

        var stateById = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            n => n,
            StringComparer.Ordinal);

        var sizeById = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            EstimateNodeSize,
            StringComparer.Ordinal);

        var outgoing = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        var incoming = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var transition in document.StateTransitions)
        {
            var sourceId = transition.SourceStateId.Value;
            var targetId = transition.TargetStateId.Value;

            if (!outgoing.ContainsKey(sourceId) || !incoming.ContainsKey(targetId))
                continue;

            if (!outgoing[sourceId].Contains(targetId, StringComparer.Ordinal))
                outgoing[sourceId].Add(targetId);

            if (!incoming[targetId].Contains(sourceId, StringComparer.Ordinal))
                incoming[targetId].Add(sourceId);
        }

        foreach (var key in outgoing.Keys.ToList())
        {
            outgoing[key] = outgoing[key]
                .OrderBy(id => orderIndex[id], Comparer<int>.Default)
                .ToList();
        }

        foreach (var key in incoming.Keys.ToList())
        {
            incoming[key] = incoming[key]
                .OrderBy(id => orderIndex[id], Comparer<int>.Default)
                .ToList();
        }

        // Racines : Start d'abord, puis états sans prédécesseur, sinon fallback stable
        var roots = document.StateNodes
            .Where(n => n.Kind == StateNodeKind.Start)
            .OrderBy(n => orderIndex[n.Id.Value], Comparer<int>.Default)
            .Select(n => n.Id.Value)
            .ToList();

        roots.AddRange(document.StateNodes
            .Where(n => incoming[n.Id.Value].Count == 0 && n.Kind != StateNodeKind.Start)
            .OrderBy(n => orderIndex[n.Id.Value], Comparer<int>.Default)
            .Select(n => n.Id.Value)
            .Where(id => !roots.Contains(id, StringComparer.Ordinal)));

        if (roots.Count == 0)
        {
            roots.Add(document.StateNodes
                .OrderBy(n => orderIndex[n.Id.Value], Comparer<int>.Default)
                .First().Id.Value);
        }

        // Construction d'une forêt directrice sans cycle :
        // un parent est assigné au premier parcours stable.
        var parentById = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => (string?)null,
            StringComparer.Ordinal);

        var visitQueue = new Queue<string>(roots);
        var discovered = new HashSet<string>(roots, StringComparer.Ordinal);

        while (visitQueue.Count > 0)
        {
            var current = visitQueue.Dequeue();

            foreach (var child in outgoing[current])
            {
                if (!discovered.Contains(child))
                {
                    discovered.Add(child);
                    parentById[child] = current;
                    visitQueue.Enqueue(child);
                }
            }
        }

        // Fallback pour composants non atteints / cycles isolés
        foreach (var nodeId in document.StateNodes
                     .OrderBy(n => orderIndex[n.Id.Value], Comparer<int>.Default)
                     .Select(n => n.Id.Value))
        {
            if (discovered.Contains(nodeId))
                continue;

            roots.Add(nodeId);
            discovered.Add(nodeId);
            visitQueue.Enqueue(nodeId);

            while (visitQueue.Count > 0)
            {
                var current = visitQueue.Dequeue();

                foreach (var child in outgoing[current])
                {
                    if (!discovered.Contains(child))
                    {
                        discovered.Add(child);
                        parentById[child] = current;
                        visitQueue.Enqueue(child);
                    }
                }
            }
        }

        // Dédup roots en ordre stable
        roots = roots
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => orderIndex[id], Comparer<int>.Default)
            .ToList();

        var childrenById = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var kv in parentById)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
                childrenById[kv.Value!].Add(kv.Key);
        }

        foreach (var key in childrenById.Keys.ToList())
        {
            childrenById[key] = childrenById[key]
                .OrderBy(id => orderIndex[id], Comparer<int>.Default)
                .ToList();
        }

        var depthById = document.StateNodes.ToDictionary(
            n => n.Id.Value,
            _ => 0,
            StringComparer.Ordinal);

        void AssignDepth(string id, int depth)
        {
            depthById[id] = Math.Max(depthById[id], depth);

            foreach (var child in childrenById[id])
                AssignDepth(child, depth + 1);
        }

        foreach (var root in roots)
            AssignDepth(root, 0);

        var subtreeSpanById = new Dictionary<string, double>(StringComparer.Ordinal);

        double ComputeSubtreeSpan(string id)
        {
            if (subtreeSpanById.TryGetValue(id, out var cached))
                return cached;

            var selfWidth = sizeById[id].Width;
            var children = childrenById[id];

            if (children.Count == 0)
            {
                subtreeSpanById[id] = selfWidth;
                return selfWidth;
            }

            double totalChildrenWidth = 0.0;
            for (int i = 0; i < children.Count; i++)
            {
                if (i > 0)
                    totalChildrenWidth += SiblingGap;

                totalChildrenWidth += ComputeSubtreeSpan(children[i]);
            }

            var span = Math.Max(selfWidth, totalChildrenWidth);
            subtreeSpanById[id] = span;
            return span;
        }

        foreach (var root in roots)
            ComputeSubtreeSpan(root);

        var centerXById = new Dictionary<string, double>(StringComparer.Ordinal);

        void AssignCenters(string id, double left)
        {
            var children = childrenById[id];

            if (children.Count == 0)
            {
                centerXById[id] = left + subtreeSpanById[id] / 2.0;
                return;
            }

            double currentLeft = left;
            foreach (var child in children)
            {
                AssignCenters(child, currentLeft);
                currentLeft += subtreeSpanById[child] + SiblingGap;
            }

            var first = children.First();
            var last = children.Last();
            centerXById[id] = (centerXById[first] + centerXById[last]) / 2.0;
        }

        double rootLeft = BaseX;
        foreach (var root in roots)
        {
            AssignCenters(root, rootLeft);
            rootLeft += subtreeSpanById[root] + RootGap;
        }

        foreach (var nodeId in document.StateNodes
                     .OrderBy(n => orderIndex[n.Id.Value], Comparer<int>.Default)
                     .Select(n => n.Id.Value))
        {
            if (!centerXById.ContainsKey(nodeId))
            {
                centerXById[nodeId] = rootLeft + sizeById[nodeId].Width / 2.0;
                rootLeft += sizeById[nodeId].Width + RootGap;
            }
        }

        var maxDepth = depthById.Values.DefaultIfEmpty(0).Max();

        var maxHeightByDepth = Enumerable.Range(0, maxDepth + 1)
            .ToDictionary(
                depth => depth,
                depth => document.StateNodes
                    .Where(n => depthById[n.Id.Value] == depth)
                    .Select(n => sizeById[n.Id.Value].Height)
                    .DefaultIfEmpty(60)
                    .Max());

        var yByDepth = new Dictionary<int, double>();
        double currentY = BaseY;
        for (int depth = 0; depth <= maxDepth; depth++)
        {
            yByDepth[depth] = currentY;
            currentY += maxHeightByDepth[depth] + LevelGap;
        }

        foreach (var node in document.StateNodes)
        {
            var id = node.Id.Value;
            var size = sizeById[id];
            var depth = depthById[id];

            node.X = centerXById[id] - size.Width / 2.0;
            node.Y = yByDepth[depth];
        }

        // Raffinement : léger recentrage de End sous la “branche finale” si possible
        var endNodes = document.StateNodes.Where(n => n.Kind == StateNodeKind.End).ToList();
        foreach (var endNode in endNodes)
        {
            var predecessors = incoming[endNode.Id.Value];
            if (predecessors.Count == 0)
                continue;

            var avgCenter = predecessors
                .Select(id => centerXById.TryGetValue(id, out var c) ? c : centerXById[endNode.Id.Value])
                .Average();

            endNode.X = avgCenter - sizeById[endNode.Id.Value].Width / 2.0;
        }
    }

    private static (double Width, double Height) EstimateNodeSize(StateNode node)
    {
        return node.Kind switch
        {
            StateNodeKind.Start => (34, 34),
            StateNodeKind.End => (44, 44),
            _ => EstimateNormalStateSize(node.Label)
        };
    }

    private static (double Width, double Height) EstimateNormalStateSize(string label)
    {
        const double minWidth = 140;
        const double minHeight = 58;
        const double maxTextWidth = 170;
        const double horizontalPadding = 28;
        const double verticalPadding = 20;
        const double estimatedCharWidth = 7.2;
        const double estimatedLineHeight = 18.0;

        var text = string.IsNullOrWhiteSpace(label) ? "State" : label.Trim();
        var estimatedTextWidth = Math.Min(maxTextWidth, Math.Max(40, text.Length * estimatedCharWidth));
        var estimatedLines = Math.Max(1, (int)Math.Ceiling((text.Length * estimatedCharWidth) / maxTextWidth));
        var estimatedTextHeight = estimatedLines * estimatedLineHeight;

        var width = Math.Max(minWidth, estimatedTextWidth + horizontalPadding);
        var height = Math.Max(minHeight, estimatedTextHeight + verticalPadding);

        return (width, height);
    }
}
