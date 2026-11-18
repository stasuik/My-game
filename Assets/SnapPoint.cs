using UnityEngine;

public enum SnapConnectionType { Roadway }

// Forward points outwards from the segment, towards a connecting segment.
public class SnapPoint : MonoBehaviour
{
    public SnapConnectionType connectionType = SnapConnectionType.Roadway;
    public SnapPoint ConnectedTo { get; private set; }
    public Vector3 Position => transform.position;
    public Vector3 Forward => transform.forward;
    public bool Available => isActiveAndEnabled && ConnectedTo == null;

    private const float PositionTolerance = 0.001f;
    private const float AxisTolerance = 0.001f;

    public static bool HasMatchingAxis(Vector3 first, Vector3 second)
    {
        return (first.normalized - second.normalized).sqrMagnitude <= AxisTolerance * AxisTolerance
            && first.sqrMagnitude > 0.000001f && second.sqrMagnitude > 0.000001f;
    }

    public bool MatchesEnd(Vector3 position, Vector3 segmentDirection)
    {
        return (Position - position).sqrMagnitude <= PositionTolerance * PositionTolerance &&
            HasMatchingAxis(Forward, segmentDirection);
    }

    public bool Connect(SnapPoint other)
    {
        if (other == null || other == this || !Available || !other.Available ||
            connectionType != other.connectionType || !MatchesEnd(other.Position, -other.Forward)) return false;
        ConnectedTo = other;
        other.ConnectedTo = this;
        return true;
    }

    private void OnDisable()
    {
        if (ConnectedTo != null)
        {
            ConnectedTo.ConnectedTo = null;
            ConnectedTo = null;
        }
    }
}
