using UnityEngine;
using System.Collections.Generic;

public class VRRoadBuilder : MonoBehaviour
{
    public LayerMask terrainLayer;
    public Terrain terrain;
    public Vector3 modelRotationOffset = Vector3.zero;
    public GameObject bridgePillarPrefab;

    [Range(0.5f, 1f)] public float snapRadius = 0.75f;
    [Range(0f, 90f)] public float maxSnapTurnAngle = 30f;
    private GameObject ghostContainer;
    private GameObject stretchGhost;
    private RoadArc arcGhost;
    private BuildableItem stretchGhostItem;
    private int stretchGhostLanes;

    private LineRenderer snapIndicator;
    private Material snapIndicatorMaterial;

    public SnapPoint FindAimedSnapPoint(Ray ray, float maxDistance, SnapPoint exclude = null,
        System.Predicate<SnapPoint> isEligible = null)
    {
        SnapPoint nearest = null;
        float radius = Mathf.Clamp(snapRadius, 0.5f, 1f);
        float bestDistance = radius * radius;
        float bestDepth = maxDistance;
        foreach (SnapPoint point in SnapPoint.ActivePoints)
        {
            if (!point.Available || point == exclude || point.connectionType != SnapConnectionType.Roadway)
                continue;
            Vector3 delta = point.Position - ray.origin;
            float depth = Vector3.Dot(delta, ray.direction);
            if (depth <= 0f || depth > maxDistance) continue;
            float distance = (delta - ray.direction * depth).sqrMagnitude;
            if (distance > bestDistance || (distance == bestDistance && depth >= bestDepth)) continue;
            if (isEligible != null && !isEligible(point)) continue;
            nearest = point;
            bestDistance = distance;
            bestDepth = depth;
        }
        return nearest;
    }

    public void ShowSnapIndicator(SnapPoint point, bool locked = false)
    {
        if (point == null)
        {
            if (snapIndicator != null) snapIndicator.enabled = false;
            return;
        }

        if (snapIndicator == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            snapIndicatorMaterial = new Material(shader);
            var indicator = new GameObject("Road Snap Indicator");
            indicator.transform.SetParent(transform, false);
            snapIndicator = indicator.AddComponent<LineRenderer>();
            snapIndicator.sharedMaterial = snapIndicatorMaterial;
            snapIndicator.widthMultiplier = 0.035f;
            snapIndicator.positionCount = 7;
        }

        Color color = locked ? new Color(0.15f, 1f, 0.4f) : new Color(0.1f, 0.85f, 1f);
        if (snapIndicatorMaterial.HasProperty("_BaseColor"))
            snapIndicatorMaterial.SetColor("_BaseColor", color);
        snapIndicator.startColor = snapIndicator.endColor = color;
        Vector3 center = point.Position + Vector3.up * 0.09f;
        Vector3 forward = Vector3.ProjectOnPlane(point.Forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 tip = center + forward * 0.4f;
        Vector3 wing = center + forward * 0.18f;
        snapIndicator.SetPosition(0, center - right * 0.18f);
        snapIndicator.SetPosition(1, center + right * 0.18f);
        snapIndicator.SetPosition(2, center);
        snapIndicator.SetPosition(3, tip);
        snapIndicator.SetPosition(4, wing + right * 0.14f);
        snapIndicator.SetPosition(5, tip);
        snapIndicator.SetPosition(6, wing - right * 0.14f);
        snapIndicator.enabled = true;
    }

    private void OnDestroy()
    {
        if (snapIndicatorMaterial != null) Destroy(snapIndicatorMaterial);
    }

    public bool CanAlignSnap(Vector3 approach, Vector3 alignedDirection)
    {
        return approach.sqrMagnitude > 0.000001f &&
            Vector3.Angle(approach, alignedDirection) <= maxSnapTurnAngle;
    }

    public SnapPoint FindNearestSnapPoint(Vector3 position, SnapPoint exclude = null,
        Vector3? origin = null, Vector3 requiredForward = default, System.Predicate<SnapPoint> isEligible = null)
    {
        SnapPoint nearest = null;
        float radius = Mathf.Clamp(snapRadius, 0.5f, 1f);
        float best = radius * radius;
        foreach (var point in SnapPoint.ActivePoints)
        {
            if (point == exclude || !point.Available || point.connectionType != SnapConnectionType.Roadway) continue;
            if (origin.HasValue)
            {
                Vector3 direction = (point.Position - origin.Value).normalized;
                // A rigid straight segment needs a common axis with each attached end.
                if (!SnapPoint.HasMatchingAxis(direction, -point.Forward) ||
                    (requiredForward != Vector3.zero && !SnapPoint.HasMatchingAxis(direction, requiredForward)))
                    continue;
            }
            if (isEligible != null && !isEligible(point)) continue;
            float distance = (point.Position - position).sqrMagnitude;
            if (distance <= best) { best = distance; nearest = point; }
        }
        return nearest;
    }

    public float SampleTerrainHeight(Vector3 p)
    {
        if (terrain != null)
        {
            float h = terrain.SampleHeight(p);
            return terrain.transform.position.y + h;
        }
        Vector3 start = new Vector3(p.x, p.y + 200f, p.z);
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 400f, terrainLayer))
            return hit.point.y;
        return p.y;
    }

    private Vector3 GetNormal(Vector3 p)
    {
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 local = p - terrain.transform.position;
            Vector3 normalizedPos = new Vector3(
                local.x / terrain.terrainData.size.x,
                0,
                local.z / terrain.terrainData.size.z
            );
            if (normalizedPos.x >= 0 && normalizedPos.x <= 1 && normalizedPos.z >= 0 && normalizedPos.z <= 1)
                return terrain.terrainData.GetInterpolatedNormal(normalizedPos.x, normalizedPos.z);
        }
        Vector3 start = new Vector3(p.x, p.y + 50f, p.z);
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 100f, terrainLayer))
            return hit.normal;
        return Vector3.up;
    }

    private (Vector3 position, Quaternion rotation) CalculateSegmentTransform(
        Vector3 a, Vector3 b, BuildableItem item)
    {
        Vector3 xzA = new Vector3(a.x, 0, a.z);
        Vector3 xzB = new Vector3(b.x, 0, b.z);
        Vector3 midXZ = (xzA + xzB) * 0.5f;

        Vector3 pos;
        Vector3 slopeForward;

        if (item.snapToTerrain)
        {
            float hA = SampleTerrainHeight(a);
            float hB = SampleTerrainHeight(b);
            float hMid = SampleTerrainHeight(new Vector3(midXZ.x, 0, midXZ.z));

            pos = new Vector3(midXZ.x, hMid + item.heightOffset, midXZ.z);
            slopeForward = new Vector3(
                xzB.x - xzA.x, hB - hA, xzB.z - xzA.z
            ).normalized;
        }
        else
        {
            float midY = (a.y + b.y) * 0.5f;
            pos = new Vector3(midXZ.x, midY + item.heightOffset, midXZ.z);
            slopeForward = new Vector3(
                xzB.x - xzA.x, b.y - a.y, xzB.z - xzA.z
            ).normalized;
        }

        Vector3 up;
        if (item.snapToTerrain)
            up = GetNormal(new Vector3(midXZ.x, pos.y - item.heightOffset, midXZ.z));
        else
            up = Vector3.up;

        Vector3 right = Vector3.Cross(slopeForward, up).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(slopeForward, Vector3.up).normalized;
            up = Vector3.Cross(right, slopeForward).normalized;
        }
        else
        {
            up = Vector3.Cross(right, slopeForward).normalized;
        }

        Quaternion rot = Quaternion.LookRotation(slopeForward, up);
        rot *= Quaternion.Euler(GetRotationOffset(item));

        return (pos, rot);
    }

    private Vector3 GetRotationOffset(BuildableItem item)
    {
        if (item != null && item.rotationOffset != Vector3.zero)
            return item.rotationOffset;
        return modelRotationOffset;
    }

    // Mesh bounds are transformed into prefab-root space, so nested meshes and
    // non-centred pivots do not change the joint positions.
    private bool TryGetSegmentBounds(GameObject prefab, BuildableItem item, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || !IsDeckMesh(filter, item)) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(x, y, z));
                        Vector3 local = toRoot.MultiplyPoint3x4(corner);
                        if (!found) { bounds = new Bounds(local, Vector3.zero); found = true; }
                        else bounds.Encapsulate(local);
                    }
        }
        return found && bounds.size[item.LongitudinalAxis] > 0.0001f;
    }

    private static bool IsDeckMesh(MeshFilter filter, BuildableItem item)
    {
        return string.IsNullOrEmpty(item.deckMeshObject) || filter.name == item.deckMeshObject ||
            filter.sharedMesh.name == item.deckMeshObject;
    }

    private readonly Dictionary<(GameObject, BuildableItem), (Vector3 start, Vector3 end)> deckEnds =
        new Dictionary<(GameObject, BuildableItem), (Vector3 start, Vector3 end)>();

    private (Vector3 start, Vector3 end) GetDeckEnds(GameObject prefab, BuildableItem item, Bounds bounds)
    {
        var key = (prefab, item);
        if (deckEnds.TryGetValue(key, out var ends)) return ends;

        int along = item.LongitudinalAxis;
        Vector3 localUp = Quaternion.Inverse(Quaternion.Euler(GetRotationOffset(item))) * Vector3.up;
        int up = Mathf.Abs(localUp.y) > Mathf.Abs(localUp.z) ? 1 : 2;
        if (up == along) up = along == 1 ? 2 : 1;
        int across = 3 - along - up;

        Vector3 start = bounds.center;
        Vector3 end = bounds.center;
        start[up] = end[up] = localUp[up] >= 0 ? bounds.max[up] : bounds.min[up];
        start[along] = bounds.min[along];
        end[along] = bounds.max[along];

        if (item.useCustomSegmentEnds && prefab == item.finalPrefab)
            ends = (item.segmentStartLocal, item.segmentEndLocal);
        else if (item.sampleDeckEnds)
        {
            var vertices = new List<Vector3>();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable || !IsDeckMesh(filter, item)) continue;
                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                    vertices.Add(toRoot.MultiplyPoint3x4(vertex));
            }

            if (vertices.Count > 0)
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (Vector3 vertex in vertices)
                {
                    min = Mathf.Min(min, vertex[along]);
                    max = Mathf.Max(max, vertex[along]);
                }
                start[along] = min;
                end[along] = max;
                float tolerance = (max - min) * 0.0001f;
                start = GetDeckCenter(vertices, start, along, across, up, localUp[up], tolerance, item.useUpperDeckEdge);
                end = GetDeckCenter(vertices, end, along, across, up, localUp[up], tolerance, item.useUpperDeckEdge);
            }
            ends = (start, end);
        }
        else ends = (start, end);

        deckEnds.Add(key, ends);
        return ends;
    }

    private static Vector3 GetDeckCenter(List<Vector3> vertices, Vector3 point,
        int along, int across, int up, float upSign, float tolerance, bool upperEdge)
    {
        float left = float.PositiveInfinity, right = float.NegativeInfinity;
        foreach (Vector3 vertex in vertices)
        {
            if (Mathf.Abs(vertex[along] - point[along]) > tolerance) continue;
            left = Mathf.Min(left, vertex[across]);
            right = Mathf.Max(right, vertex[across]);
        }
        if (float.IsPositiveInfinity(left)) return point;

        point[across] = (left + right) * 0.5f;
        float height = upperEdge ? float.NegativeInfinity : float.PositiveInfinity;
        foreach (Vector3 vertex in vertices)
        {
            if (Mathf.Abs(vertex[along] - point[along]) > tolerance ||
                Mathf.Abs(vertex[across] - point[across]) > (right - left) * 0.25f) continue;
            height = upperEdge ? Mathf.Max(height, vertex[up] * upSign) : Mathf.Min(height, vertex[up] * upSign);
        }
        if (!float.IsInfinity(height)) point[up] = height * upSign;
        return point;
    }

    public float GetSegmentLength(BuildableItem item)
    {
        if (item != null && item.finalPrefab != null &&
            TryGetSegmentBounds(item.finalPrefab, item, out Bounds bounds))
        {
            var ends = GetDeckEnds(item.finalPrefab, item, bounds);
            return Mathf.Max(0.01f,
                Vector3.Scale(ends.end - ends.start, item.finalPrefab.transform.localScale).magnitude);
        }
        return Mathf.Max(item != null ? item.stepDistance : 1f, 0.01f);
    }

    private GameObject SpawnSegment(GameObject prefab, Vector3 a, Vector3 b,
        BuildableItem item, Transform parent, bool isGhost, int lanesCount = 1)
    {
        Vector3 start = a + Vector3.up * item.heightOffset;
        Vector3 end = b + Vector3.up * item.heightOffset;
        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f) return null;

        GameObject seg = CreateSegmentModel(prefab, item, parent, lanesCount);
        if (!string.IsNullOrEmpty(item.hiddenMeshObject))
            foreach (var filter in seg.GetComponentsInChildren<MeshFilter>())
                if (filter.gameObject != seg && filter.name == item.hiddenMeshObject)
                    filter.gameObject.SetActive(false);

        PositionSegment(seg, prefab, item, start, end);

        if (isGhost)
        {
            foreach (var col in seg.GetComponentsInChildren<Collider>()) col.enabled = false;
        }
        else
        {
            var segment = seg.GetComponent<RoadSegment>();
            if (segment == null) segment = seg.AddComponent<RoadSegment>();
            segment.Initialize(start, end, lanesCount);
            foreach (var r in seg.GetComponentsInChildren<MeshRenderer>())
                if (r.gameObject.GetComponent<Collider>() == null)
                    r.gameObject.AddComponent<MeshCollider>();
        }
        return seg;
    }

    private GameObject CreateSegmentModel(GameObject prefab, BuildableItem item, Transform parent, int lanesCount)
    {
        if (lanesCount == 1 || !TryGetSegmentBounds(prefab, item, out Bounds bounds))
            return Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);

        // Repeat the single-lane geometry symmetrically about the shared snap axis.
        var root = new GameObject(prefab.name);
        root.transform.SetParent(parent, false);
        Vector3 laneSpacing = Vector3.zero;
        int across = GetTransverseAxis(item);
        laneSpacing[across] = bounds.size[across];
        for (int i = 0; i < lanesCount; i++)
        {
            GameObject lane = Instantiate(prefab, root.transform, false);
            lane.transform.localRotation = Quaternion.identity;
            lane.transform.localScale = Vector3.one;
            lane.transform.localPosition = laneSpacing * (i - (lanesCount - 1) * 0.5f);
        }
        return root;
    }

    private void PositionSegment(GameObject seg, GameObject prefab, BuildableItem item,
        Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        if (TryGetSegmentBounds(prefab, item, out Bounds bounds))
        {
            Vector3 scale = prefab.transform.localScale;
            var (localStart, localEnd) = GetDeckEnds(prefab, item, bounds);
            int along = item.LongitudinalAxis;
            Quaternion offset = Quaternion.Euler(GetRotationOffset(item));
            Vector3 delta = Vector3.Scale(localEnd - localStart, scale);
            if (Vector3.Dot(offset * delta, Vector3.forward) < -0.001f)
            {
                (localStart, localEnd) = (localEnd, localStart);
                delta = -delta;
            }

            float transverseSquared = Mathf.Max(0f, delta.sqrMagnitude - delta[along] * delta[along]);
            float axialLength = Mathf.Sqrt(Mathf.Max(0.000001f, direction.sqrMagnitude - transverseSquared));
            scale[along] *= axialLength / Mathf.Max(Mathf.Abs(delta[along]), 0.0001f);
            Vector3 modelForward = offset * Vector3.Scale(localEnd - localStart, scale);
            seg.transform.rotation = Quaternion.LookRotation(direction, Vector3.up) *
                Quaternion.Inverse(Quaternion.LookRotation(modelForward, Vector3.up)) * offset;
            seg.transform.localScale = scale;
            seg.transform.position += start - seg.transform.TransformPoint(localStart);
        }
        else
        {
            seg.transform.SetPositionAndRotation((start + end) * 0.5f, Quaternion.LookRotation(direction.normalized));
            Debug.LogWarning("No usable mesh bounds for " + prefab.name, prefab);
        }

    }

    public float GetLaneWidth(BuildableItem item)
    {
        GameObject prefab = item.finalPrefab;
        if (prefab == null || !TryGetSegmentBounds(prefab, item, out Bounds bounds)) return 0f;
        int across = GetTransverseAxis(item);
        return Mathf.Abs(bounds.size[across] * prefab.transform.localScale[across]);
    }

    private int GetTransverseAxis(BuildableItem item)
    {
        Vector3 right = Quaternion.Inverse(Quaternion.Euler(GetRotationOffset(item))) * Vector3.right;
        int axis = -1;
        for (int i = 0; i < 3; i++)
            if (i != item.LongitudinalAxis && (axis < 0 || Mathf.Abs(right[i]) > Mathf.Abs(right[axis])))
                axis = i;
        // X-long assets map to forward with a yaw, moving their horizontal Z to world X.
        if (Mathf.Abs(right[axis]) < 0.01f)
        {
            Vector3 up = Quaternion.Inverse(Quaternion.Euler(GetRotationOffset(item))) * Vector3.up;
            axis = Mathf.Abs(up.y) > Mathf.Abs(up.z) ? 2 : 1;
        }
        return axis;
    }

    public void PlaceRoadArc(Vector3 origin, Vector3 forward, float length, float angle,
        BuildableItem item, int lanes, float laneWidth, SnapPoint startTarget, bool preview)
    {
        GameObject prefab = preview && item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (prefab == null) return;
        Vector3 start = origin + Vector3.up * item.heightOffset;
        if (!preview)
        {
            ClearGhostRoad();
            if (startTarget != null && (!startTarget.Available || !startTarget.MatchesEnd(start, forward)))
                return;
        }

        RoadArc arc = preview ? arcGhost : null;
        if (arc == null)
        {
            if (preview) ClearGhostRoad();
            var root = new GameObject(preview ? "[GHOST_ROAD_ARC]" : "Built_" + item.itemName + "_Arc",
                typeof(MeshFilter), typeof(MeshRenderer), typeof(RoadArc));
            arc = root.GetComponent<RoadArc>();
            var source = prefab.GetComponentInChildren<MeshRenderer>();
            if (source != null) root.GetComponent<MeshRenderer>().sharedMaterial = source.sharedMaterial;
            if (preview)
            {
                arcGhost = arc;
                ghostContainer = root;
            }
        }

        arc.Build(start, forward, length, angle, laneWidth, lanes, out Vector3 end, out Vector3 tangent);
        if (preview) return;

        var segment = arc.gameObject.AddComponent<RoadSegment>();
        segment.Initialize(start, end, forward, tangent, lanes);
        arc.gameObject.AddComponent<MeshCollider>().sharedMesh = arc.GetComponent<MeshFilter>().sharedMesh;
        if (segment.StartPoint.Connect(startTarget))
            segment.CloseSeam(segment.StartPoint, startTarget);
    }

    public void DrawStretchGhost(Vector3 start, Vector3 end, BuildableItem item, int lanesCount)
    {
        GameObject prefab = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (prefab == null) return;
        if (stretchGhost == null || stretchGhostItem != item || stretchGhostLanes != lanesCount)
        {
            ClearGhostRoad();
            ghostContainer = new GameObject("[GHOST_STRUCTURE]");
            stretchGhost = SpawnSegment(prefab, start, end, item, ghostContainer.transform, true, lanesCount);
            stretchGhostLanes = lanesCount;
            stretchGhostItem = item;
            return;
        }

        Vector3 offset = Vector3.up * item.heightOffset;
        PositionSegment(stretchGhost, prefab, item, start + offset, end + offset);
    }

    public void DrawGhostRoad(List<Vector3> points, BuildableItem item)
    {
        ClearGhostRoad();
        if (points.Count < 2) return;

        GameObject prefab = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (prefab == null) return;

        ghostContainer = new GameObject("[GHOST_STRUCTURE]");

        if (item.singleSpan)
        {
            SpawnSegment(
                prefab,
                points[0], points[points.Count - 1], item, ghostContainer.transform, true);
            if (!item.snapToTerrain && item.generatePillars)
                SpawnPillarsAlongSpan(points[0], points[points.Count - 1],
                    ghostContainer.transform, true, item);
            return;
        }

        for (int i = 1; i < points.Count; i++)
        {
            SpawnSegment(prefab, points[i - 1], points[i], item, ghostContainer.transform, true);
        }

        if (!item.snapToTerrain && item.generatePillars)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var (pos, _) = CalculateSegmentTransform(points[i - 1], points[i], item);
                SpawnPillar(pos, ghostContainer.transform, true, item);
            }
        }
    }

    public void BuildFinalRoad(List<Vector3> points, BuildableItem item,
        SnapPoint startTarget = null, SnapPoint endTarget = null, int lanesCount = 1)
    {
        ClearGhostRoad();
        if (points.Count < 2 || item.finalPrefab == null) return;
        if ((startTarget != null && !startTarget.Available) || (endTarget != null && !endTarget.Available))
            return;

        Vector3 offset = Vector3.up * item.heightOffset;
        int last = points.Count - 1;
        Vector3 firstDirection = points[item.singleSpan ? last : 1] - points[0];
        Vector3 lastDirection = points[last] - points[item.singleSpan ? 0 : last - 1];
        if ((startTarget != null && !startTarget.MatchesEnd(points[0] + offset, firstDirection)) ||
            (endTarget != null && !endTarget.MatchesEnd(points[last] + offset, -lastDirection)))
            return;

        GameObject container = new GameObject("Built_" + item.itemName);

        SnapPoint previous = startTarget;
        for (int i = 1; i < points.Count; i++)
        {
            if (item.singleSpan) i = points.Count - 1;
            GameObject built = SpawnSegment(item.finalPrefab, points[item.singleSpan ? 0 : i - 1],
                points[i], item, container.transform, false, lanesCount);
            if (built == null) continue;
            var segment = built.GetComponent<RoadSegment>();
            if (segment.StartPoint.Connect(previous))
                segment.CloseSeam(segment.StartPoint, previous);
            previous = segment.EndPoint;
        }
        if (previous != null && previous != startTarget && previous.Connect(endTarget))
            previous.GetComponentInParent<RoadSegment>().CloseSeam(previous, endTarget);

        if (item.singleSpan)
        {
            if (!item.snapToTerrain && item.generatePillars)
                SpawnPillarsAlongSpan(points[0], points[points.Count - 1], container.transform, false, item);
            return;
        }

        if (!item.snapToTerrain && item.generatePillars)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var (pos, _) = CalculateSegmentTransform(points[i - 1], points[i], item);
                SpawnPillar(pos, container.transform, false, item);
            }
        }
    }

    public void SetGhostColor(Color color)
    {
        if (ghostContainer == null) return;
        foreach (var r in ghostContainer.GetComponentsInChildren<MeshRenderer>())
            r.material.color = color;
    }

    public void ClearGhostRoad()
    {
        if (ghostContainer != null) Destroy(ghostContainer);
        ghostContainer = null;
        stretchGhost = null;
        arcGhost = null;
        stretchGhostItem = null;
        ShowSnapIndicator(null);
    }

    public float DistanceToNearestRoad(Vector3 position)
    {
        RoadSegment[] roads = FindObjectsOfType<RoadSegment>();
        float minDist = float.MaxValue;
        for (int i = 0; i < roads.Length; i++)
        {
            float dist = Vector3.Distance(position, roads[i].transform.position);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    private void SpawnPillar(Vector3 position, Transform parent, bool isGhost, BuildableItem item)
    {
        if (!Physics.Raycast(position, Vector3.down, out RaycastHit hit, 200f, terrainLayer))
            return;
        float d = hit.distance;
        if (d < item.minPillarHeight) return;

        Vector3 pPos = position + Vector3.down * (d * 0.5f);

        if (bridgePillarPrefab != null)
        {
            GameObject p = Instantiate(bridgePillarPrefab, pPos, Quaternion.identity, parent);
            Vector3 s = p.transform.localScale;
            p.transform.localScale = new Vector3(s.x, d, s.z);
            if (isGhost)
            {
                foreach (var col in p.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            else
            {
                foreach (var r in p.GetComponentsInChildren<MeshRenderer>())
                    if (r.gameObject.GetComponent<Collider>() == null)
                        r.gameObject.AddComponent<MeshCollider>();
            }
        }
        else
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.transform.SetParent(parent, false);
            p.transform.position = pPos;
            p.transform.localScale = new Vector3(item.pillarWidth, d * 0.5f, item.pillarWidth);
            p.name = isGhost ? "[GHOST_PILLAR]" : "Pillar";
            if (item.pillarMaterial != null && !isGhost)
                p.GetComponent<MeshRenderer>().material = item.pillarMaterial;
            if (isGhost)
            {
                Destroy(p.GetComponent<Collider>());
                SetTransparent(p);
            }
            else
            {
                Destroy(p.GetComponent<Collider>());
                p.AddComponent<MeshCollider>().convex = true;
            }
        }
    }

    private void SpawnPillarsAlongSpan(Vector3 start, Vector3 end,
        Transform parent, bool isGhost, BuildableItem item)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        int count = Mathf.Max(1, Mathf.FloorToInt(dist / item.stepDistance));
        for (int p = 1; p <= count; p++)
        {
            float t = (float)p / (count + 1);
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y += item.heightOffset;
            SpawnPillar(pos, parent, isGhost, item);
        }
    }

    private void SetTransparent(GameObject obj)
    {
        var r = obj.GetComponent<MeshRenderer>();
        if (r == null) return;
        Color c = r.material.color;
        c.a = 0.3f;
        r.material.color = c;
        r.material.SetFloat("_Mode", 2);
        r.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        r.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        r.material.SetInt("_ZWrite", 0);
        r.material.renderQueue = 3000;
        r.enabled = true;
    }
}
