using System.Collections.Generic;
using UnityEngine;

// Constant-radius centerline with a cross section perpendicular to its tangent.
public sealed class RoadArc : MonoBehaviour
{
    private Mesh mesh;
    private readonly List<SourceMesh> sourceMeshes = new List<SourceMesh>();
    private float sourceLength;

    private sealed class SourceMesh
    {
        public Mesh mesh;
        public Vector3[] straightVertices;
        public Vector3[] bentVertices;
        public Matrix4x4 fromArc;
    }

    public void SetSourceModel(GameObject model, float length)
    {
        sourceLength = length;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            Mesh original = filter.sharedMesh;
            if (original == null || !original.isReadable) continue;

            Matrix4x4 toArc = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3[] straight = original.vertices;
            for (int i = 0; i < straight.Length; i++)
                straight[i] = toArc.MultiplyPoint3x4(straight[i]);

            Mesh copy = Instantiate(original);
            copy.MarkDynamic();
            filter.sharedMesh = copy;
            sourceMeshes.Add(new SourceMesh
            {
                mesh = copy,
                straightVertices = straight,
                bentVertices = new Vector3[straight.Length],
                fromArc = toArc.inverse
            });
        }
    }

    public void Build(Vector3 start, Vector3 forward, float length, float angle, float laneWidth,
        int lanes, out Vector3 end, out Vector3 endForward)
    {
        transform.position = start;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float radians = angle * Mathf.Deg2Rad;
        float radius = length / radians;
        float endSine = Mathf.Sin(radians);
        float endCosine = Mathf.Cos(radians);
        endForward = (forward * endCosine + right * endSine).normalized;
        end = start + radius * (forward * endSine + right * (1f - endCosine));

        if (sourceLength > 0f)
        {
            BendSourceMeshes(forward, right, radians, radius);
            return;
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "Road Arc" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        int sections = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(length / 0.5f, Mathf.Abs(angle) / 5f)));
        int rowSize = lanes * 2;
        var vertices = new Vector3[(sections + 1) * rowSize];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[sections * lanes * 6];

        for (int section = 0; section <= sections; section++)
        {
            float t = (float)section / sections;
            float theta = radians * t;
            float sine = Mathf.Sin(theta);
            float cosine = Mathf.Cos(theta);
            Vector3 center = radius * (forward * sine + right * (1f - cosine));
            Vector3 across = right * cosine - forward * sine;
            // The road texture runs along U, with its edges at V = 0 and V = 1.
            float textureU = length * t / laneWidth;
            for (int lane = 0; lane < lanes; lane++)
            {
                int index = section * rowSize + lane * 2;
                float lateral = (lane - lanes * 0.5f) * laneWidth;
                vertices[index] = center + across * lateral;
                vertices[index + 1] = center + across * (lateral + laneWidth);
                uv[index] = new Vector2(textureU, 0f);
                uv[index + 1] = new Vector2(textureU, 1f);

                if (section == sections) continue;
                int triangle = (section * lanes + lane) * 6;
                triangles[triangle] = index;
                triangles[triangle + 1] = index + rowSize;
                triangles[triangle + 2] = index + 1;
                triangles[triangle + 3] = index + 1;
                triangles[triangle + 4] = index + rowSize;
                triangles[triangle + 5] = index + rowSize + 1;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void BendSourceMeshes(Vector3 forward, Vector3 right, float radians, float radius)
    {
        foreach (var source in sourceMeshes)
        {
            for (int i = 0; i < source.straightVertices.Length; i++)
            {
                Vector3 vertex = source.straightVertices[i];
                float theta = radians * (vertex.z / sourceLength);
                float sine = Mathf.Sin(theta);
                float cosine = Mathf.Cos(theta);
                Vector3 center = radius * (forward * sine + right * (1f - cosine));
                Vector3 across = right * cosine - forward * sine;
                Vector3 position = center + across * vertex.x + Vector3.up * vertex.y;
                source.bentVertices[i] = source.fromArc.MultiplyPoint3x4(position);
            }

            source.mesh.vertices = source.bentVertices;
            source.mesh.RecalculateNormals();
            source.mesh.RecalculateTangents();
            source.mesh.RecalculateBounds();
        }
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        foreach (var source in sourceMeshes)
            Destroy(source.mesh);
    }
}
