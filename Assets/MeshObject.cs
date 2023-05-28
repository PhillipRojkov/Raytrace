using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshObject : MonoBehaviour
{
    [SerializeField] Shader rayTracingShader;
    [SerializeField] bool drawDebug;
    public Material rayTracingMaterial;
    public Color color;
    public Color emissionColor;
    public float emissionStrength = 0;
    public List<Triangle> triangles;
    public RayMesh rayMesh;
    public bool flipNormals;

    public void UpdateProperties() {
        if (rayTracingMaterial == null) {
            rayTracingMaterial = new Material(rayTracingShader);
        }

        if (triangles == null) {
            triangles = new List<Triangle>();
        }
        triangles.Clear();

        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        Matrix4x4 transformMatrix = transform.localToWorldMatrix;
        rayMesh = new RayMesh();
        rayMesh.bounds = mesh.bounds.extents;

        for (int i = 0; i < mesh.triangles.Length; i += 3) {
            Triangle tri = new Triangle();

            tri.a = mesh.vertices[mesh.triangles[i]];
            tri.b = mesh.vertices[mesh.triangles[i + 1]];
            tri.c = mesh.vertices[mesh.triangles[i + 2]];

            tri.a = transformMatrix.MultiplyPoint(tri.a);
            tri.b = transformMatrix.MultiplyPoint(tri.b);
            tri.c = transformMatrix.MultiplyPoint(tri.c);

            if (flipNormals) {
                tri.c = tri.a;
                tri.a = transformMatrix.MultiplyPoint(mesh.vertices[mesh.triangles[i + 2]]);
            }

            tri.norm = Vector3.Cross(tri.b - tri.a, tri.c - tri.a); // I have no clue why you need to divide by 2 here and normalizing will break things

            // Transform triangle properties by the position, rotation, and scale of the object
            tri.material.color = color;
            tri.material.emissionColor = emissionColor;
            tri.material.emissionStrength = emissionStrength;
            triangles.Add(tri);
        }
    }

    public void OnDrawGizmos() {
        if (drawDebug) {
            foreach (Triangle tri in triangles) {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(tri.a, 0.05f);
                Gizmos.DrawSphere(tri.b, 0.05f);
                Gizmos.DrawSphere(tri.c, 0.05f);
                Gizmos.DrawRay(tri.a, tri.norm);
                Gizmos.DrawRay(tri.b, tri.norm);
                Gizmos.DrawRay(tri.c, tri.norm);
                Gizmos.DrawLine(tri.a, tri.b);
                Gizmos.DrawLine(tri.b, tri.c);
                Gizmos.DrawLine(tri.c, tri.a);
            }
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(rayMesh.bounds, 0.05f);
        }
    }


    // These structs match up with what is in RayTracer.shader
    public struct RayMesh {
        public List<Triangle> triangles;
        public Vector3 bounds;
    };

    public struct Triangle {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
        public Vector3 norm;
        public RayTracingMaterial material;
    };
    public struct RayTracingMaterial {
        public Vector4 color;
        public Vector4 emissionColor;
        public float emissionStrength;
    };
}
