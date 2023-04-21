using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MarchingCubeSetup : MonoBehaviour
{
    [SerializeField] private float _isoLevel = 0.5f;
    [SerializeField] private Vector3Int _dimension = new(2, 2, 2);
    [SerializeField] private ComputeShader _marchingCubeShader;
    [SerializeField] private ComputeShader _mergeShader;

    private MeshFilter _mesh;
    
    private ComputeBuffer _triangleBuffer;
    private ComputeBuffer _voxelBuffer;
    private ComputeBuffer _countBuffer;
    private ComputeBuffer _verticesBuffer;
    private ComputeBuffer _duplicateVerticesBuffer;
    private ComputeBuffer _indicesBuffer;
    
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
    
    void OnDrawGizmosSelected()
    {
        for (var i = 0; i < _voxels.Length; i++)
        {
            Gizmos.color = Color.Lerp(Color.black, Color.white, _voxels[i]);
            Gizmos.DrawSphere(GetPoint(i), 0.05f);
        }
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
            _verticesBuffer?.Release();
            _duplicateVerticesBuffer?.Release();
            _indicesBuffer?.Release();
            
            _voxelBuffer = new(voxels.Length, sizeof(float));
            _triangleBuffer = new(voxels.Length, sizeof(float) * 9, ComputeBufferType.Append);

            var indexLen = voxels.Length * 3;
            
            _verticesBuffer = new(indexLen, sizeof(float) * 3);
            _duplicateVerticesBuffer = new(indexLen, sizeof(int));
            _indicesBuffer = new(indexLen, sizeof(int));
            
            _marchingCubeShader.SetBuffer(0, "voxels", _voxelBuffer);
            _marchingCubeShader.SetBuffer(0, "triangles", _triangleBuffer);

            if (_mergeShader)
            {
                _mergeShader.SetBuffer(0, "indices", _indicesBuffer);
                _mergeShader.SetBuffer(0, "vertices", _verticesBuffer);
                _mergeShader.SetBuffer(0, "duplicateVertices", _duplicateVerticesBuffer);
            }
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
        
        var indices = new int[indexLen];
        var vertices = new Vector3[indexLen];
        
        _triangleBuffer.GetData(vertices, 0, 0, indexLen);
        _verticesBuffer.SetData(vertices);

        if (!_mergeShader)
        {
            for (int i = 0; i < indexLen; i++) indices[i] = i;
            
            mesh.vertices = vertices;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateNormals();
            
            return mesh;
        }
        
        _mergeShader.SetInt("indexLen", indexLen);
        _mergeShader.GetKernelThreadGroupSizes(0, out x, out _, out _);
        _mergeShader.Dispatch(0, (indexLen + (int)x - 1) / (int)x, 1, 1);
        
        _indicesBuffer.GetData(indices);
        
        ComputeBuffer.CopyCount(_duplicateVerticesBuffer, _countBuffer, 0);
        _countBuffer.GetData(_countArr);

        var duplicates = new int[_countArr[0]];
        
        _duplicateVerticesBuffer.GetData(duplicates, 0, 0, _countArr[0]);
        
        foreach (var duplicate in duplicates)
        {
            for (var i = duplicate; i < indexLen; i++)
            {
                if (i < indexLen - 1) vertices[i] = vertices[i + 1];
                indices[i]--;
            }
        }
        
        mesh.vertices = vertices.AsSpan(0, vertices.Length - duplicates.Length).ToArray();
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.RecalculateNormals();

        return mesh;
    }

    private void OnDestroy()
    {
        _countBuffer?.Release();
        _triangleBuffer?.Release();
        _voxelBuffer?.Release();
    }
}