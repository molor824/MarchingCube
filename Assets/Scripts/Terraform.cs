using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(MarchingCubeSetup), typeof(MeshCollider))]
public class Terraform : MonoBehaviour
{
    private MarchingCubeSetup _marching;
    private MeshCollider _collider;
    private Camera _main;

    [SerializeField] private float _mineRadius = 2;
    [SerializeField] private float _placeRadius = 2;
    [SerializeField] private float _mineSpeed = -2;
    [SerializeField] private float _placeSpeed = 2;
    [SerializeField] private float _maxDist = 100000;
    [SerializeField] private LayerMask _layerMask = 1;
    [SerializeField] private float _groundLevel = 1;

    private void Start()
    {
        _marching = GetComponent<MarchingCubeSetup>();
        _collider = GetComponent<MeshCollider>();
        _main = Camera.main;
        
        _marching.Start();

        var voxels = _marching.Voxels;
        var dimension = _marching.Dimension;
        
        Parallel.For(0, voxels.Length, i => {
            voxels[i] = i / dimension.x / dimension.z <= _groundLevel ? 0 : 1;
        });
        
        UpdateMesh();
    }

    void UpdateMesh()
    {
        var mesh = _marching.GenerateMesh();
        
        Physics.BakeMesh(mesh.GetInstanceID(), false);
        
        _collider.sharedMesh = mesh;
    }

    private void Update()
    {
        if (!Input.GetKey(KeyCode.Mouse0) && !Input.GetKey(KeyCode.Mouse1)) return;
        
        var mouseRay = _main.ScreenPointToRay(Input.mousePosition);
        var mine = Input.GetKey(KeyCode.Mouse0);
            
        if (Physics.Raycast(mouseRay, out var hitInfo, _maxDist, _layerMask, QueryTriggerInteraction.Ignore) && hitInfo.collider == _collider)
        {
            var voxels = _marching.Voxels;
            var dimension = _marching.Dimension;
            var radius = mine ? _mineRadius : _placeRadius;
            var speed = mine ? _mineSpeed : _placeSpeed;
            var delta = Time.deltaTime;

            Parallel.For(0, voxels.Length, i =>
            {
                var point = _marching.GetPoint(i);
                var dist = (point - hitInfo.point).magnitude;
                var min = radius - 1;
                var max = radius + 1;
                
                voxels[i] += Mathf.InverseLerp(max, min, dist) * speed * delta;
                voxels[i] = Mathf.Clamp01(voxels[i]);
            });
            
            UpdateMesh();
        }
    }
}