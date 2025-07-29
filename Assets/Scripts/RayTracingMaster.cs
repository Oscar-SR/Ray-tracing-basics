using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class RayTracingMaster : MonoBehaviour
{
    public struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    }

    private const int INITIAL_MAX_BOUNCES = 8;

    [SerializeField] private ComputeShader rayTracingShader;
    [SerializeField] private Texture skyboxTexture;
    [SerializeField] private Light directionalLight;
    [SerializeField, Range(1, 16)] private int maxBounces = INITIAL_MAX_BOUNCES;

    [Header("Spheres")]
    [SerializeField] private int sphereSeed;
    [SerializeField] private Vector2 sphereRadius = new Vector2(3.0f, 8.0f);
    [SerializeField] private uint spheresMax = 100;
    [SerializeField] private float spherePlacementRadius = 100.0f;

    private RenderTexture _target;
    private Camera _camera;
    private float _lastFieldOfView;
    private uint _currentSample = 0;
    private Material _addMaterial;
    private int _lastMaxBounces = INITIAL_MAX_BOUNCES;
    private ComputeBuffer _sphereBuffer;
    private List<Transform> _transformsToWatch = new List<Transform>();

    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }
    private void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }

    private void SetUpScene()
    {
        Random.InitState(sphereSeed);
        List<Sphere> spheres = new List<Sphere>();

        // Add a number of random spheres
        for (int i = 0; i < spheresMax; i++)
        {
            Sphere sphere = new Sphere();

            // Radius and radius
            sphere.radius = sphereRadius.x + Random.value * (sphereRadius.y - sphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * spherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }

            // Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            // Add the sphere to the list
            spheres.Add(sphere);

            SkipSphere:
            continue;
        }

        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        
        // Assign to compute buffer
        if (spheres.Count > 0)
        {
            _sphereBuffer = new ComputeBuffer(spheres.Count, Marshal.SizeOf<Sphere>());
            _sphereBuffer.SetData(spheres);
        }
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        _transformsToWatch.Add(transform);
        _transformsToWatch.Add(directionalLight.transform);
    }

    private void Update()
    {
        if (_camera.fieldOfView != _lastFieldOfView)
        {
            // The field of view of the came has changed
            _currentSample = 0;
            _lastFieldOfView = _camera.fieldOfView;
        }

        foreach (Transform t in _transformsToWatch)
        {
            if (t.hasChanged)
            {
                // Somo transform to watch has changed
                _currentSample = 0;
                t.hasChanged = false;
            }
        }

        if (_lastMaxBounces != maxBounces)
        {
            // maxBounces has changed
            _currentSample = 0;
            _lastMaxBounces = maxBounces;
        }
    }

    private void SetShaderParameters()
    {
        rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayTracingShader.SetTexture(0, "_SkyboxTexture", skyboxTexture);
        rayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        Vector3 l = directionalLight.transform.forward;
        rayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, directionalLight.intensity));

        rayTracingShader.SetInt("_MaxBounces", maxBounces); // check if the maxbounces has changed??

        if (_sphereBuffer != null)
            rayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }
    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        rayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        rayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Blit the result texture to the screen
        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, destination, _addMaterial);
        _currentSample++;
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();

            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();

            _currentSample = 0;
        }
    }
}