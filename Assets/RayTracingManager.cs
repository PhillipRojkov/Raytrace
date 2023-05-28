using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracingManager : MonoBehaviour {
    [SerializeField] bool useShaderInSceneView;
    [SerializeField] bool resetHistory;
    [SerializeField] bool environmentLighting;
    [SerializeField] Shader rayTracingShader;
    [SerializeField] int maxBounceCount = 1;
    [SerializeField] int numRaysPerPixel = 1;
    [SerializeField] int numRenderedFrames = 1;
    [SerializeField] Color skyColorHorizon;
    [SerializeField] Color skyColorZenith;
    [SerializeField] Color groundColor;
    Vector3 sunLightdirection;
    [SerializeField] float sunFocus;
    [SerializeField] float sunIntensity;
    [SerializeField] GameObject sunObject;
    Material rayTracingMaterial;

    private Camera myCamera;

    private ComputeBuffer sphereBuffer;
    private ComputeBuffer triangleBuffer;

    private int frame = 0;
    private RenderTexture pastFrame;

    // Called after each camera has finished rendering into the src texture
    void OnRenderImage(RenderTexture src, RenderTexture target) {
        frame++;
        if (Camera.current != Camera.main && useShaderInSceneView) {
            myCamera = Camera.current;
            
            if (rayTracingMaterial == null) {
                rayTracingMaterial = new Material(rayTracingShader);
            }
            if (pastFrame == null || resetHistory) {
                pastFrame = new RenderTexture(target);
                resetHistory = false;
            }

            UpdateCameraParams(myCamera);

            // Push to render target
            Graphics.Blit(null, target, rayTracingMaterial);
            
            // Capture the current frame
            Graphics.Blit(target, pastFrame);

            if (sphereBuffer != null) {
                sphereBuffer.Release();
            }
            if (triangleBuffer != null) {
                triangleBuffer.Release();
            }
        } else {
            Graphics.Blit(src, target);
        }
    }

    void UpdateCameraParams(Camera cam) {
        float planeHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;

        sunLightdirection = Quaternion.Euler(sunObject.transform.rotation.eulerAngles) * Vector3.forward;
        sunLightdirection.Normalize();

        rayTracingMaterial.SetVector("ViewParams", new Vector3(planeWidth, planeHeight, cam.nearClipPlane));
        rayTracingMaterial.SetMatrix("CamLocalToWorldMatrix", cam.transform.localToWorldMatrix);
        rayTracingMaterial.SetInteger("EnvironmentLighting", environmentLighting ? 1 : 0);
        rayTracingMaterial.SetInt("MaxBounceCount", maxBounceCount);
        rayTracingMaterial.SetInt("NumRaysPerPixel", numRaysPerPixel);
        rayTracingMaterial.SetInt("NumRenderedFrames", numRenderedFrames);
        rayTracingMaterial.SetInt("Frame", frame);
        rayTracingMaterial.SetColor("SkyColorHorizon", skyColorHorizon);
        rayTracingMaterial.SetColor("SkyColorZenith", skyColorZenith);
        rayTracingMaterial.SetColor("GroundColor", groundColor);
        rayTracingMaterial.SetVector("SunLightDirection", sunLightdirection);
        rayTracingMaterial.SetFloat("SunFocus", sunFocus);
        rayTracingMaterial.SetFloat("SunIntensity", sunIntensity);
        rayTracingMaterial.SetTexture("_MainTexOld", pastFrame);

        BufferSpheres();
        BufferMeshes();
    }

    void BufferSpheres() {
        // Get all game objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        List<SphereObject> sphereObjects = new List<SphereObject>();

        // Loop through all game objects
        foreach (GameObject obj in allObjects) {
            // Check if the object has a SphereObject component attached to it
            SphereObject sphereObj = obj.GetComponent<SphereObject>();
            if (sphereObj != null) {
                sphereObj.UpdateProperties();
                sphereObjects.Add(sphereObj);
            }
        }

        // Copy the sphere data from the SphereObject components into the buffer
        List<SphereObject.Sphere> spheres = new List<SphereObject.Sphere>();
        foreach (SphereObject sphereObj in sphereObjects) {
            SphereObject.Sphere sphere = new SphereObject.Sphere();
            sphere.position = sphereObj.transform.position;
            sphere.radius = sphereObj.transform.localScale.x / 2;
            sphere.material.color = sphereObj.color;
            sphere.material.emissionColor = sphereObj.emissionColor;
            sphere.material.emissionStrength = sphereObj.emissionStrength;
            spheres.Add(sphere);
        }

        if (spheres.Count > 0) {
            sphereBuffer = new ComputeBuffer(sphereObjects.Count, sizeof(float) * 13, ComputeBufferType.Structured);
            sphereBuffer.SetData(spheres.ToArray());

            // Set the buffer on the ray tracing shader
            rayTracingMaterial.SetBuffer("spheres", sphereBuffer);
            rayTracingMaterial.SetInteger("numSpheres", spheres.Count);
        }
    }
    void BufferMeshes() {
        // Get all game objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        List<MeshObject> meshObjects = new List<MeshObject>();

        // Loop through all game objects
        foreach (GameObject obj in allObjects) {
            // Check if the object has a SphereObject component attached to it
            MeshObject meshObj = obj.GetComponent<MeshObject>();
            if (meshObj != null) {
                meshObj.UpdateProperties();
                meshObjects.Add(meshObj);
            }
        }

        // Copy mesh object from the MeshObject components into the buffer of ALL triangles
        List<MeshObject.Triangle> triangles = new List<MeshObject.Triangle>();
        foreach (MeshObject meshObj in meshObjects) {
            triangles.AddRange(meshObj.triangles);
        }

        if (triangles.Count > 0) {
            triangleBuffer = new ComputeBuffer(triangles.Count, sizeof(float) * 21, ComputeBufferType.Structured);
            triangleBuffer.SetData(triangles.ToArray());

            rayTracingMaterial.SetBuffer("triangles", triangleBuffer);
            rayTracingMaterial.SetInt("numTriangles", triangles.Count);
        }
    }
}