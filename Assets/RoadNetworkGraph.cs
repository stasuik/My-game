using System.Collections.Generic;

namespace RoadBuilder.Core.Network
{
    public enum RoadEnd
    {
        Start,
        End
    }

    /// <summary>
    /// A junction viewed from one of its segment ends. An unconnected end forms its own junction.
    /// </summary>
    public readonly struct RoadJunction
    {
        internal RoadJunction(RoadSegment segment, RoadEnd end, SnapPoint point,
            RoadSegment connectedSegment, RoadEnd? connectedEnd)
        {
            Segment = segment;
            End = end;
            Point = point;
            ConnectedSegment = connectedSegment;
            ConnectedEnd = connectedEnd;
        }

        public RoadSegment Segment { get; }
        public RoadEnd End { get; }
        public SnapPoint Point { get; }
        public RoadSegment ConnectedSegment { get; }
        public RoadEnd? ConnectedEnd { get; }
    }

    /// <summary>
    /// Reads the live, undirected road graph from reciprocal SnapPoint connections.
    /// </summary>
    public static class RoadNetworkGraph
    {
        public static bool TryGetJunction(RoadSegment segment, RoadEnd end, out RoadJunction junction)
        {
            junction = default;
            if (segment == null || !segment.isActiveAndEnabled)
                return false;

            SnapPoint point;
            switch (end)
            {
                case RoadEnd.Start:
                    point = segment.StartPoint;
                    break;
                case RoadEnd.End:
                    point = segment.EndPoint;
                    break;
                default:
                    return false;
            }

            if (point == null || !point.isActiveAndEnabled)
                return false;

            SnapPoint peer = point.ConnectedTo;
            if (peer != null && peer.isActiveAndEnabled && peer.ConnectedTo == point)
            {
                RoadSegment neighbor = peer.GetComponentInParent<RoadSegment>();
                if (neighbor != null && neighbor != segment && neighbor.isActiveAndEnabled &&
                    neighbor.gameObject.scene == segment.gameObject.scene)
                {
                    if (peer == neighbor.StartPoint)
                    {
                        junction = new RoadJunction(segment, end, point, neighbor, RoadEnd.Start);
                        return true;
                    }

                    if (peer == neighbor.EndPoint)
                    {
                        junction = new RoadJunction(segment, end, point, neighbor, RoadEnd.End);
                        return true;
                    }
                }
            }

            junction = new RoadJunction(segment, end, point, null, null);
            return true;
        }

        /// <summary>Returns each direct neighbor once, even if it joins at both ends.</summary>
        public static IEnumerable<RoadSegment> GetNeighbors(RoadSegment segment)
        {
            RoadSegment startNeighbor = null;
            if (TryGetJunction(segment, RoadEnd.Start, out RoadJunction start))
            {
                startNeighbor = start.ConnectedSegment;
                if (startNeighbor != null)
                    yield return startNeighbor;
            }

            if (TryGetJunction(segment, RoadEnd.End, out RoadJunction end) &&
                end.ConnectedSegment != null && end.ConnectedSegment != startNeighbor)
                yield return end.ConnectedSegment;
        }

        /// <summary>Tests reachability in the current topology within one Unity scene.</summary>
        public static bool AreConnected(RoadSegment from, RoadSegment to)
        {
            if ((!TryGetJunction(from, RoadEnd.Start, out _) && !TryGetJunction(from, RoadEnd.End, out _)) ||
                (!TryGetJunction(to, RoadEnd.Start, out _) && !TryGetJunction(to, RoadEnd.End, out _)) ||
                from.gameObject.scene != to.gameObject.scene)
                return false;

            var visited = new HashSet<RoadSegment> { from };
            var pending = new Queue<RoadSegment>();
            pending.Enqueue(from);

            while (pending.Count > 0)
            {
                RoadSegment current = pending.Dequeue();
                if (current == to)
                    return true;

                foreach (RoadSegment neighbor in GetNeighbors(current))
                {
                    if (visited.Add(neighbor))
                        pending.Enqueue(neighbor);
                }
            }

            return false;
        }
    }
}
