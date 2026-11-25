using UnityEngine;

// Constant-radius centerline with a cross section perpendicular to its tangent.
public sealed class RoadArc : MonoBehaviour
{
    private Mesh mesh;

    public void Build(Vector3 start, Vector3 forward, float length, float angle, float laneWidth,
        int lanes, out Vector3 end, out Vector3 endForward)
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "Road Arc" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        transform.position = start;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float radians = angle * Mathf.Deg2Rad;
        float radius = length / radians;
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

        endForward = (forward * Mathf.Cos(radians) + right * Mathf.Sin(radians)).normalized;
        end = start + radius * (forward * Mathf.Sin(radians) + right * (1f - Mathf.Cos(radians)));
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
