using UnityEngine;
using System.Collections.Generic;

public class VRRoadBuilder : MonoBehaviour
{
    [Header("��������� �����")]
    [Tooltip("���� ��������� (Terrain), �� �������� ����� ���������� �����")]
    public LayerMask terrainLayer;

    [Tooltip("�������������� ������� ������� � ��������� ����������� (�� ����������� ��� ������)")]
    public Vector3 modelRotationOffset = Vector3.zero;

    [Header("��������� ������")]
    [Tooltip("������ ������������ �������-�����, ������� ����� ����� ���� �� �����")]
    public GameObject bridgePillarPrefab;

    private GameObject ghostContainer;

    private Vector3 GetRotationOffset(BuildableItem item)
    {
        if (item != null && item.rotationOffset != Vector3.zero)
            return item.rotationOffset;
        return modelRotationOffset;
    }

    public void DrawGhostRoad(List<Vector3> points, BuildableItem item)
    {
        ClearGhostRoad();
        if (points.Count < 2) return;

        ghostContainer = new GameObject("[GHOST_STRUCTURE]");

        if (item.singleSpan)
        {
            DrawGhostSingleSpan(points[0], points[points.Count - 1], item);
            return;
        }

        GameObject prefabToUse = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (prefabToUse == null) return;

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 posA = points[i - 1];
            Vector3 posB = points[i];

            Vector3 direction = posB - posA;
            float actualDistance = direction.magnitude;
            if (actualDistance < 0.1f) continue;

            Vector3 spawnPos = Vector3.Lerp(posA, posB, 0.5f);
            spawnPos.y += item.heightOffset;

            Vector3 groundNormal = item.snapToTerrain ? GetTerrainNormalAt(spawnPos) : Vector3.up;

            Quaternion spawnRot = Quaternion.LookRotation(direction.normalized, groundNormal);
            spawnRot *= Quaternion.Euler(GetRotationOffset(item));

            GameObject ghostSeg = Instantiate(prefabToUse, spawnPos, spawnRot, ghostContainer.transform);

            foreach (var col in ghostSeg.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            if (i == points.Count - 1)
            {
                float scaleMultiplier = actualDistance / item.stepDistance;
                Vector3 currentScale = ghostSeg.transform.localScale;
                ghostSeg.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleMultiplier);
            }

            if (!item.snapToTerrain && item.generatePillars)
            {
                SpawnPillar(spawnPos, ghostContainer.transform, true, item);
            }
        }
    }

    private void DrawGhostSingleSpan(Vector3 start, Vector3 end, BuildableItem item)
    {
        GameObject prefabToUse = item.ghostPrefab != null ? item.ghostPrefab : item.finalPrefab;
        if (prefabToUse == null) return;

        Vector3 direction = end - start;
        float totalDistance = direction.magnitude;
        if (totalDistance < 0.1f) return;

        Vector3 midPoint = Vector3.Lerp(start, end, 0.5f);
        midPoint.y += item.heightOffset;

        Vector3 groundNormal = item.snapToTerrain ? GetTerrainNormalAt(midPoint) : Vector3.up;
        Quaternion spanRot = Quaternion.LookRotation(direction.normalized, groundNormal);
        spanRot *= Quaternion.Euler(GetRotationOffset(item));

        GameObject ghostSeg = Instantiate(prefabToUse, midPoint, spanRot, ghostContainer.transform);

        foreach (var col in ghostSeg.GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }

        float scaleMultiplier = totalDistance / item.stepDistance;
        Vector3 currentScale = ghostSeg.transform.localScale;
        ghostSeg.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleMultiplier);

        if (!item.snapToTerrain && item.generatePillars)
        {
            SpawnPillarsAlongSpan(start, end, ghostContainer.transform, true, item);
        }
    }

    public void BuildFinalRoad(List<Vector3> points, BuildableItem item)
    {
        ClearGhostRoad();
        if (points.Count < 2 || item.finalPrefab == null) return;

        GameObject finalContainer = new GameObject("Built_" + item.itemName);

        if (item.singleSpan)
        {
            BuildFinalSingleSpan(points[0], points[points.Count - 1], finalContainer, item);
            return;
        }

        for (int i = 1; i < points.Count; i++)
        {
            Vector3 posA = points[i - 1];
            Vector3 posB = points[i];

            Vector3 direction = posB - posA;
            float actualDistance = direction.magnitude;
            if (actualDistance < 0.1f) continue;

            Vector3 spawnPos = Vector3.Lerp(posA, posB, 0.5f);
            spawnPos.y += item.heightOffset;

            Vector3 groundNormal = item.snapToTerrain ? GetTerrainNormalAt(spawnPos) : Vector3.up;

            Quaternion spawnRot = Quaternion.LookRotation(direction.normalized, groundNormal);
            spawnRot *= Quaternion.Euler(GetRotationOffset(item));

            GameObject permanentSegment = Instantiate(item.finalPrefab, spawnPos, spawnRot, finalContainer.transform);

            if (i == points.Count - 1)
            {
                float scaleMultiplier = actualDistance / item.stepDistance;
                Vector3 currentScale = permanentSegment.transform.localScale;
                permanentSegment.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleMultiplier);
            }

            foreach (var renderer in permanentSegment.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.gameObject.GetComponent<Collider>() == null)
                {
                    renderer.gameObject.AddComponent<MeshCollider>();
                }
            }

            if (!item.snapToTerrain && item.generatePillars)
            {
                SpawnPillar(spawnPos, finalContainer.transform, false, item);
            }
        }
    }

    private void BuildFinalSingleSpan(Vector3 start, Vector3 end, GameObject container, BuildableItem item)
    {
        Vector3 direction = end - start;
        float totalDistance = direction.magnitude;
        if (totalDistance < 0.1f) return;

        Vector3 midPoint = Vector3.Lerp(start, end, 0.5f);
        midPoint.y += item.heightOffset;

        Vector3 groundNormal = item.snapToTerrain ? GetTerrainNormalAt(midPoint) : Vector3.up;
        Quaternion spanRot = Quaternion.LookRotation(direction.normalized, groundNormal);
        spanRot *= Quaternion.Euler(GetRotationOffset(item));

        GameObject bridgeSpan = Instantiate(item.finalPrefab, midPoint, spanRot, container.transform);

        float scaleMultiplier = totalDistance / item.stepDistance;
        Vector3 currentScale = bridgeSpan.transform.localScale;
        bridgeSpan.transform.localScale = new Vector3(currentScale.x, currentScale.y, currentScale.z * scaleMultiplier);

        foreach (var renderer in bridgeSpan.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.gameObject.GetComponent<Collider>() == null)
            {
                renderer.gameObject.AddComponent<MeshCollider>();
            }
        }

        if (!item.snapToTerrain && item.generatePillars)
        {
            SpawnPillarsAlongSpan(start, end, container.transform, false, item);
        }
    }

    /// <summary>
    /// ������� ���������� �������� �� �����
    /// </summary>
    public void ClearGhostRoad()
    {
        if (ghostContainer != null) Destroy(ghostContainer);
    }

    /// <summary>
    /// ����� ��� ������ � ������������� ������������ ���� ����� ������ ���� �� �����
    /// </summary>
    private void SpawnPillar(Vector3 segmentPosition, Transform parent, bool isGhost, BuildableItem item)
    {
        if (!Physics.Raycast(segmentPosition, Vector3.down, out RaycastHit hit, 200f, terrainLayer))
            return;

        float distanceToGround = hit.distance;
        if (distanceToGround < item.minPillarHeight) return;

        Vector3 pillarSpawnPos = segmentPosition + (Vector3.down * (distanceToGround / 2f));

        if (bridgePillarPrefab != null)
        {
            GameObject pillar = Instantiate(bridgePillarPrefab, pillarSpawnPos, Quaternion.identity, parent);
            Vector3 pillarScale = pillar.transform.localScale;
            pillar.transform.localScale = new Vector3(pillarScale.x, distanceToGround, pillarScale.z);

            if (isGhost)
            {
                foreach (var col in pillar.GetComponentsInChildren<Collider>()) Destroy(col);
            }
            else
            {
                foreach (var renderer in pillar.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.gameObject.GetComponent<Collider>() == null)
                        renderer.gameObject.AddComponent<MeshCollider>();
                }
            }
        }
        else
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = pillarSpawnPos;
            pillar.transform.localScale = new Vector3(item.pillarWidth, distanceToGround / 2f, item.pillarWidth);
            pillar.name = isGhost ? "[GHOST_PILLAR]" : "Pillar";

            if (item.pillarMaterial != null && !isGhost)
            {
                pillar.GetComponent<MeshRenderer>().material = item.pillarMaterial;
            }

            if (isGhost)
            {
                Destroy(pillar.GetComponent<Collider>());
                SetTransparent(pillar);
            }
            else
            {
                Destroy(pillar.GetComponent<Collider>());
                var mc = pillar.AddComponent<MeshCollider>();
                mc.convex = true;
            }
        }
    }

    private void SpawnPillarsAlongSpan(Vector3 start, Vector3 end, Transform parent, bool isGhost, BuildableItem item)
    {
        Vector3 direction = end - start;
        float totalDistance = direction.magnitude;

        int pillarCount = Mathf.FloorToInt(totalDistance / item.stepDistance);
        if (pillarCount < 1) pillarCount = 1;

        for (int p = 1; p <= pillarCount; p++)
        {
            float t = (float)p / (pillarCount + 1);
            Vector3 pillarPos = Vector3.Lerp(start, end, t);
            pillarPos.y += item.heightOffset;
            SpawnPillar(pillarPos, parent, isGhost, item);
        }
    }

    private void SetTransparent(GameObject obj)
    {
        var renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Color c = renderer.material.color;
            c.a = 0.3f;
            renderer.material.color = c;
            renderer.material.SetFloat("_Mode", 2);
            renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            renderer.material.SetInt("_ZWrite", 0);
            renderer.material.renderQueue = 3000;
            renderer.enabled = true;
        }
    }

    /// <summary>
    /// ��������������� ����� ��� ����������� ������� (�������) ����� ��� ������
    /// </summary>
    private Vector3 GetTerrainNormalAt(Vector3 originPos)
    {
        Vector3 rayStart = new Vector3(originPos.x, originPos.y + 50f, originPos.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, terrainLayer))
        {
            return hit.normal;
        }
        return Vector3.up;
    }
}