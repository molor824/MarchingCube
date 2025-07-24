using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MarchingCubeMesh : MonoBehaviour
{
    [SerializeField]
    ComputeShader _computeShader;
    [SerializeField]
    Vector3Int _dimension;

    Vector3Int _threadGroup;

    MeshFilter _meshFilter;
    ComputeBuffer _voxelBuffer, _vertexBuffer, _indexBuffer, _countBuffer;
    int _mainKernelIndex;

    static Vector3Int _numThreads => new(4, 4, 4);

    void Start()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _threadGroup = Vector3Int.CeilToInt(((Vector3)_dimension).Divide((Vector3)_numThreads));

        if (_dimension == Vector3Int.zero)
        {
            Debug.LogWarning("Dimension is zero! Component will not be run!");
            enabled = false;
        }
        else
        {
            _countBuffer = new(1, sizeof(int), ComputeBufferType.Counter);
            var pxSize = _dimension.x * (_dimension.y + 1) * (_dimension.z + 1);
            var pySize = (_dimension.x + 1) * _dimension.y * (_dimension.z + 1);
            var pzSize = (_dimension.x + 1) * (_dimension.y + 1) * _dimension.z;
            _indexBuffer = new(_dimension.x * _dimension.y * _dimension.z * 15, sizeof(int), ComputeBufferType.Append);
            _vertexBuffer = new(pxSize + pySize + pzSize, sizeof(float) * 3, ComputeBufferType.Structured);
            _voxelBuffer = new((_dimension.x + 1) * (_dimension.y + 1) * (_dimension.z + 1) / 4, sizeof(int), ComputeBufferType.Structured);
            _computeShader.SetInts("Dimension", _dimension.x, _dimension.y, _dimension.z);
            _mainKernelIndex = _computeShader.FindKernel("CSMain");
        }
    }
    void RunShader()
    {
        _indexBuffer.SetCounterValue(0);
        _computeShader.SetBuffer(_mainKernelIndex, "voxels", _voxelBuffer);
        _computeShader.SetBuffer(_mainKernelIndex, "vertices", _vertexBuffer);
        _computeShader.SetBuffer(_mainKernelIndex, "indices", _indexBuffer);

        _computeShader.Dispatch(_mainKernelIndex, _threadGroup.x, _threadGroup.y, _threadGroup.z);
    }
    void ApplyMesh()
    {
        ComputeBuffer.CopyCount(_indexBuffer, _countBuffer, 0);
        var indexCountArr = new int[1];
        _countBuffer.GetData(indexCountArr);
        var indexCount = indexCountArr[0];

        var vertices = new float[_vertexBuffer.count];
        var indices = new int[indexCount];
    }
    void Update()
    {

    }
}
