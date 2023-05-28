using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereObject : MonoBehaviour
{
    [SerializeField] Shader rayTracingShader;
    public Material rayTracingMaterial;
    public Color color;
    public Color emissionColor;
    public float emissionStrength = 0;

    public void UpdateProperties() {
        if (rayTracingMaterial == null) {
            rayTracingMaterial = new Material(rayTracingShader);
        }
    }

    // These structs match up with what is in RayTracer.shader
    public struct Sphere {
        public Vector3 position;
        public float radius;
        public RayTracingMaterial material;
    }

    public struct RayTracingMaterial {
        public Vector4 color;
        public Vector4 emissionColor;
        public float emissionStrength;
    };
}
