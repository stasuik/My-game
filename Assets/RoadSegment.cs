using System.Collections.Generic;
using UnityEngine;

public class RoadSegment : MonoBehaviour
{
    public SnapPoint StartPoint { get; private set; }
    public SnapPoint EndPoint { get; private set; }

    public int LanesCount { get; private set; } = 1;

    public void Initialize(Vector3 start, Vector3 end, int lanesCount = 1)
    {
        LanesCount = Mathf.Clamp(lanesCount, 1, 3);
        Vector3 direction = (end - start).normalized;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        StartPoint = CreatePoint("Snap Start", start, -direction);
        EndPoint = CreatePoint("Snap End", end, direction);
    }

    private const float SeamTolerance = 0.005f;
    private readonly HashSet<Mesh> ownedMeshes = new HashSet<Mesh>();

    public void CloseSeam(SnapPoint ownEnd, SnapPoint targetEnd)
    {
        var target = targetEnd.GetComponentInParent<RoadSegment>();
        if (target == null) return;

        Vector3 origin = targetEnd.Position;
        Vector3 normal = targetEnd.Forward;
        var referenceVertices = new List<Vector3>();
        foreach (var filter in target.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 world = filter.transform.TransformPoint(vertex);
                if (Mathf.Abs(Vector3.Dot(world - origin, normal)) <= SeamTolerance)
                    referenceVertices.Add(world);
            }
        }

        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            Vector3[] vertices = mesh.vertices;
            bool changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[i]);
                if (Mathf.Abs(Vector3.Dot(world - ownEnd.Position, normal)) > SeamTolerance)
                    continue;

                Vector3 projected = world - normal * Vector3.Dot(world - origin, normal);
                Vector3 fitted = projected;
                float bestDistance = SeamTolerance * SeamTolerance;
                foreach (Vector3 reference in referenceVertices)
                {
                    float distance = (reference - projected).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    fitted = reference;
                }

                if ((fitted - world).sqrMagnitude < 0.000000000001f) continue;
                vertices[i] = filter.transform.InverseTransformPoint(fitted);
                changed = true;
            }

            if (!changed) continue;
            Mesh edited;
            if (ownedMeshes.Contains(mesh))
                edited = mesh;
            else
            {
                edited = Instantiate(mesh);
                ownedMeshes.Add(edited);
                filter.sharedMesh = edited;
            }
            edited.vertices = vertices;
            edited.RecalculateBounds();

            foreach (var collider in filter.GetComponents<MeshCollider>())
            {
                if (collider.sharedMesh != mesh) continue;
                collider.sharedMesh = null;
                collider.sharedMesh = edited;
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes)
            Destroy(mesh);
    }

    private SnapPoint CreatePoint(string pointName, Vector3 position, Vector3 forward)
    {
        var point = new GameObject(pointName);
        point.transform.SetParent(transform, false);
        point.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
        return point.AddComponent<SnapPoint>();
    }
}
