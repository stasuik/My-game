using UnityEngine;
using System.Collections.Generic;

public class VRRoadBuilder : MonoBehaviour
{
    public LayerMask terrainLayer;
    public Terrain terrain;
    public Vector3 modelRotationOffset = Vector3.zero;
    public GameObject bridgePillarPrefab;

    private GameObject ghostContainer;

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

    private GameObject SpawnSegment(GameObject prefab, Vector3 a, Vector3 b,
        BuildableItem item, Transform parent, bool isGhost)
    {
        var (pos, rot) = CalculateSegmentTransform(a, b, item);

        float xzDist = Vector2.Distance(
            new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        float stretch = xzDist / Mathf.Max(item.stepDistance, 0.01f);

        GameObject seg = Instantiate(prefab, pos, rot, parent);

        Vector3 s = seg.transform.localScale;
        seg.transform.localScale = new Vector3(s.x, s.y, s.z * stretch);

        if (isGhost)
        {
            foreach (var col in seg.GetComponentsInChildren<Collider>())
                Destroy(col);
        }
        else
        {
            seg.AddComponent<RoadSegment>();
            foreach (var r in seg.GetComponentsInChildren<MeshRenderer>())
                if (r.gameObject.GetComponent<Collider>() == null)
                    r.gameObject.AddComponent<MeshCollider>();
        }

        return seg;
    }

    public void DrawGhostRoad(List<Vector3> points, BuildableItem item)
    {
        ClearGhostRoad();
        if (points.Count < 2) return;

        ghostContainer = new GameObject("[GHOST_STRUCTURE]");

        if (item.singleSpan)
        {
            SpawnSegment(
                item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab,
                points[0], points[points.Count - 1], item, ghostContainer.transform, true);
            if (!item.snapToTerrain && item.generatePillars)
                SpawnPillarsAlongSpan(points[0], points[points.Count - 1],
                    ghostContainer.transform, true, item);
            return;
        }

        GameObject prefab = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (prefab == null) return;

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

    public void BuildFinalRoad(List<Vector3> points, BuildableItem item)
    {
        ClearGhostRoad();
        if (points.Count < 2 || item.finalPrefab == null) return;

        GameObject container = new GameObject("Built_" + item.itemName);

        if (item.singleSpan)
        {
            SpawnSegment(item.finalPrefab, points[0], points[points.Count - 1],
                item, container.transform, false);
            if (!item.snapToTerrain && item.generatePillars)
                SpawnPillarsAlongSpan(points[0], points[points.Count - 1],
                    container.transform, false, item);
            return;
        }

        for (int i = 1; i < points.Count; i++)
        {
            SpawnSegment(item.finalPrefab, points[i - 1], points[i],
                item, container.transform, false);
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
