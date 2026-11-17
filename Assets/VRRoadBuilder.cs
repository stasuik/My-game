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
    private BuildableItem stretchGhostItem;

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
        foreach (var point in FindObjectsOfType<SnapPoint>())
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
    private bool TryGetSegmentBounds(GameObject prefab, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
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
        return found && (bounds.size.x > 0.0001f || bounds.size.z > 0.0001f);
    }

    private readonly Dictionary<GameObject, (Vector3 start, Vector3 end)> deckEnds =
        new Dictionary<GameObject, (Vector3 start, Vector3 end)>();

    private (Vector3 start, Vector3 end) GetDeckEnds(GameObject prefab, Bounds bounds)
    {
        if (deckEnds.TryGetValue(prefab, out var ends)) return ends;

        // The bridge deck reaches farther along Z than the rails. Sample only
        // vertices on the two terminal cross sections, excluding side geometry.
        var start = new List<Vector3>();
        var end = new List<Vector3>();
        var vertices = new List<Vector3>();
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 point = toRoot.MultiplyPoint3x4(vertex);
                vertices.Add(point);
                minZ = Mathf.Min(minZ, point.z);
                maxZ = Mathf.Max(maxZ, point.z);
            }
        }

        if (vertices.Count == 0)
        {
            minZ = bounds.min.z;
            maxZ = bounds.max.z;
        }
        float tolerance = (maxZ - minZ) * 0.0001f;
        foreach (Vector3 point in vertices)
        {
            if (point.z - minZ <= tolerance) start.Add(point);
            if (maxZ - point.z <= tolerance) end.Add(point);
        }
        ends = (GetDeckCenter(start, bounds, minZ), GetDeckCenter(end, bounds, maxZ));
        // Both bridge cross sections share the same profile. Keep the axis
        // parallel to Z so stretching preserves the transverse profile.
        Vector3 center = (ends.start + ends.end) * 0.5f;
        ends = (new Vector3(center.x, center.y, minZ),
            new Vector3(center.x, center.y, maxZ));
        deckEnds.Add(prefab, ends);
        return ends;
    }

    private static Vector3 GetDeckCenter(List<Vector3> section, Bounds bounds, float z)
    {
        if (section.Count == 0) return new Vector3(bounds.center.x, bounds.center.y, z);
        float minX = section[0].x;
        float maxX = minX;
        foreach (Vector3 point in section)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
        }
        float centerX = (minX + maxX) * 0.5f;
        float middleWidth = (maxX - minX) * 0.25f;
        float deckHeight = float.PositiveInfinity;
        foreach (Vector3 point in section)
            if (Mathf.Abs(point.x - centerX) <= middleWidth)
                deckHeight = Mathf.Min(deckHeight, point.y);
        if (float.IsPositiveInfinity(deckHeight)) deckHeight = bounds.center.y;
        return new Vector3(centerX, deckHeight, z);
    }

    public float GetSegmentLength(BuildableItem item)
    {
        if (item != null && item.finalPrefab != null && TryGetSegmentBounds(item.finalPrefab, out Bounds bounds))
        {
            Vector3 scale = item.finalPrefab.transform.localScale;
            if (item.useCustomSegmentEnds)
            {
                Vector3 delta = item.segmentEndLocal - item.segmentStartLocal;
                float customLength = item.segmentAxis == SegmentAxis.X ? Mathf.Abs(delta.x * scale.x) : Mathf.Abs(delta.z * scale.z);
                if (customLength > 0.0001f) return customLength;
            }
            return item.segmentAxis == SegmentAxis.X ? Mathf.Abs(bounds.size.x * scale.x) : Mathf.Abs(bounds.size.z * scale.z);
        }
        return Mathf.Max(item != null ? item.stepDistance : 1f, 0.01f);
    }

    private GameObject SpawnSegment(GameObject prefab, Vector3 a, Vector3 b,
        BuildableItem item, Transform parent, bool isGhost)
    {
        Vector3 start = a + Vector3.up * item.heightOffset;
        Vector3 end = b + Vector3.up * item.heightOffset;
        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f) return null;

        GameObject seg = CreateSegmentModel(prefab, item, parent);
        PositionSegment(seg, prefab, item, start, end);

        if (isGhost)
        {
            foreach (var col in seg.GetComponentsInChildren<Collider>()) col.enabled = false;
        }
        else
        {
            var segment = seg.GetComponent<RoadSegment>();
            if (segment == null) segment = seg.AddComponent<RoadSegment>();
            segment.Initialize(start, end, item.LanesCount);
            foreach (var r in seg.GetComponentsInChildren<MeshRenderer>())
                if (r.gameObject.GetComponent<Collider>() == null)
                    r.gameObject.AddComponent<MeshCollider>();
        }
        return seg;
    }

    private GameObject CreateSegmentModel(GameObject prefab, BuildableItem item, Transform parent)
    {
        if (item.LanesCount == 1 || !TryGetSegmentBounds(prefab, out Bounds bounds))
            return Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);

        // Repeat the single-lane geometry symmetrically about the shared snap axis.
        var root = new GameObject(prefab.name);
        root.transform.SetParent(parent, false);
        bool alongX = item.segmentAxis == SegmentAxis.X;
        Vector3 laneSpacing = alongX ? Vector3.forward * bounds.size.z : Vector3.right * bounds.size.x;
        for (int i = 0; i < item.LanesCount; i++)
        {
            GameObject lane = Instantiate(prefab, root.transform, false);
            lane.transform.localRotation = Quaternion.identity;
            lane.transform.localScale = Vector3.one;
            lane.transform.localPosition = laneSpacing * (i - (item.LanesCount - 1) * 0.5f);
        }
        return root;
    }

    private void PositionSegment(GameObject seg, GameObject prefab, BuildableItem item,
        Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        if (TryGetSegmentBounds(prefab, out Bounds bounds))
        {
            Vector3 scale = prefab.transform.localScale;
            bool xAxis = item.segmentAxis == SegmentAxis.X;
            Vector3 axis = xAxis ? Vector3.right : Vector3.forward;
            Quaternion offset = Quaternion.Euler(GetRotationOffset(item));
            Vector3 modelAxis = offset * axis;
            if (Vector3.Dot(modelAxis, Vector3.forward) < -0.001f)
                modelAxis = -modelAxis;
            // Rotate the prefab's longitudinal axis onto the construction direction.
            seg.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) *
                Quaternion.FromToRotation(modelAxis, Vector3.forward) * offset;
            Vector3 localStart = bounds.center;
            Vector3 localEnd = bounds.center;
            if (xAxis) { localStart.x = bounds.min.x; localEnd.x = bounds.max.x; }
            else { localStart.z = bounds.min.z; localEnd.z = bounds.max.z; }
            if (item.sampleDeckEnds && !xAxis)
                (localStart, localEnd) = GetDeckEnds(prefab, bounds);
            else
                localStart.y = localEnd.y = bounds.max.y;
            // Explicit points belong to the final model, not a potentially different ghost mesh.
            if (item.useCustomSegmentEnds && prefab == item.finalPrefab)
            {
                localStart = item.segmentStartLocal;
                localEnd = item.segmentEndLocal;
            }
            float length = xAxis ? Mathf.Abs((localEnd.x - localStart.x) * scale.x) : Mathf.Abs((localEnd.z - localStart.z) * scale.z);
            if (xAxis) scale.x *= direction.magnitude / Mathf.Max(length, 0.0001f);
            else scale.z *= direction.magnitude / Mathf.Max(length, 0.0001f);
            seg.transform.localScale = scale;
            if (Vector3.Dot(seg.transform.TransformVector(localEnd - localStart), direction) < 0f)
            {
                Vector3 tmp = localStart; localStart = localEnd; localEnd = tmp;
            }
            seg.transform.position += start - seg.transform.TransformPoint(localStart);
        }
        else
        {
            seg.transform.SetPositionAndRotation((start + end) * 0.5f, Quaternion.LookRotation(direction.normalized));
            Debug.LogWarning("No usable mesh bounds for " + prefab.name, prefab);
        }

    }

    public void DrawStretchGhost(Vector3 start, Vector3 end, BuildableItem item)
    {
        GameObject prefab = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (prefab == null) return;
        if (stretchGhost == null || stretchGhostItem != item)
        {
            ClearGhostRoad();
            ghostContainer = new GameObject("[GHOST_STRUCTURE]");
            stretchGhost = SpawnSegment(prefab, start, end, item, ghostContainer.transform, true);
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
        SnapPoint startTarget = null, SnapPoint endTarget = null)
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
                points[i], item, container.transform, false);
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
        stretchGhostItem = null;
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
