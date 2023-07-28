using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MarchingCubeSetup : MonoBehaviour
{
    [SerializeField] private float _isoLevel = 0.5f;
    [SerializeField] private Vector3Int _dimension = new(2, 2, 2);
    [SerializeField] private ComputeShader _marchingCubeShader;
    [SerializeField] private float _vertexSnap = 0.000001f;

    private MeshFilter _mesh;

    private ComputeBuffer _triangleBuffer;
    private ComputeBuffer _voxelBuffer;
    private ComputeBuffer _countBuffer;

    private Dictionary<Vector3Int, int> _vertexDict = new();
    private List<Vector3> _updatedVertices = new();
    private List<int> _indices = new();

    private int[] _countArr = { 0 };
    private float[] _voxels;

    public float[] Voxels => _voxels;
    public Vector3Int Dimension => _dimension;

    public float IsoLevel
    {
        get => _isoLevel;
        set
        {
            _marchingCubeShader.SetFloat("isoLevel", value);
            _isoLevel = value;
        }
    }

    public Vector3 GetPoint(int index)
    {
        return new(index % _dimension.x, index / _dimension.x / _dimension.z, index / _dimension.x % _dimension.z);
    }

    public void NewVoxel(float[] voxels, Vector3Int dimension)
    {
        if (dimension.x * dimension.y * dimension.z != voxels.Length)
            throw new UnityException("Voxel length must be equal to dimension volume");

        _voxels = voxels;
        _dimension = dimension;

        _marchingCubeShader.SetInts("dimension", dimension.x, dimension.y, dimension.z);

        if (_voxelBuffer == null || voxels.Length != _voxelBuffer.count)
        {
            _voxelBuffer?.Release();
            _triangleBuffer?.Release();

            _voxelBuffer = new(voxels.Length, sizeof(float));
            _triangleBuffer = new(voxels.Length, sizeof(float) * 9, ComputeBufferType.Append);

            var indexLen = voxels.Length * 3;

            _marchingCubeShader.SetBuffer(0, "voxels", _voxelBuffer);
            _marchingCubeShader.SetBuffer(0, "triangles", _triangleBuffer);
        }
    }

    public float GetVoxel(int x, int y, int z)
    {
        return _voxels[x + z * _dimension.x + y * _dimension.x * _dimension.z];
    }

    public void SetVoxel(float val, int x, int y, int z)
    {
        _voxels[x + z * _dimension.x + y * _dimension.x * _dimension.z] = val;
    }

    public void Start()
    {
        if (_mesh) return; // called start already

        _mesh = GetComponent<MeshFilter>();

        _countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);

        NewVoxel(new float[_dimension.x * _dimension.y * _dimension.z], _dimension);

        IsoLevel = _isoLevel;
    }

    public Mesh GenerateMesh()
    {
        var mesh = _mesh.mesh;

        mesh.Clear();

        _triangleBuffer.SetCounterValue(0);
        _voxelBuffer.SetData(_voxels);

        _marchingCubeShader.GetKernelThreadGroupSizes(0, out var x, out var y, out var z);
        _marchingCubeShader.Dispatch(
            0,
            (_dimension.x + (int)x - 1) / (int)x,
            (_dimension.y + (int)y - 1) / (int)y,
            (_dimension.z + (int)z - 1) / (int)z
        );

        ComputeBuffer.CopyCount(_triangleBuffer, _countBuffer, 0);
        _countBuffer.GetData(_countArr);

        if (_countArr[0] == 0) return mesh;

        var indexLen = _countArr[0] * 3;

        var vertices = new Vector3[indexLen];

        _triangleBuffer.GetData(vertices, 0, 0, indexLen);
        
        _vertexDict.Clear();
        _updatedVertices.Clear();
        _indices.Clear();

        int i = 0;
        foreach (var vertex in vertices)
        {
            var snappedVertex = new Vector3Int(
                Mathf.RoundToInt(vertex.x / _vertexSnap),
                Mathf.RoundToInt(vertex.y / _vertexSnap),
                Mathf.RoundToInt(vertex.z / _vertexSnap)
            );
            if (_vertexDict.TryGetValue(snappedVertex, out var index))
            {
                _indices.Add(index);
                continue;
            }
            
            _indices.Add(i);
            _vertexDict.Add(snappedVertex, i);
            _updatedVertices.Add(vertex);
            
            i++;
        }

        mesh.SetVertices(_updatedVertices);
        mesh.SetIndices(_indices, MeshTopology.Triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        return mesh;
    }

    private void OnDestroy()
    {
        _countBuffer?.Release();
        _triangleBuffer?.Release();
        _voxelBuffer?.Release();
    }
}