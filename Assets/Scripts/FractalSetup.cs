using UnityEngine;
using UnityEngine.Experimental.Rendering;

[RequireComponent(typeof(MeshRenderer))]
public class FractalSetup : MonoBehaviour
{
    private RenderTexture _texture;
    private MeshRenderer _renderer;

    [SerializeField] private Vector2Int _dimension = new(1000, 1000);
    [SerializeField] private ComputeShader _shader;
    [SerializeField] private int _maxIter = 1000;
    [SerializeField] private float _maxDistance = 10;
    [SerializeField] private Color _emptyColor = Color.black;
    [SerializeField] private Color _fullColor = Color.white;
    [SerializeField] private Vector2 _position;
    [SerializeField] private float _scale = 1;
    [SerializeField] private float _moveSpeed = 10;
    [SerializeField] private float _zoomSpeed = 2;

    void NewTexture()
    {
        if (_texture) Destroy(_texture);
        
        _texture = new(_dimension.x, _dimension.y, GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.D32_SFloat_S8_UInt)
        {
            enableRandomWrite = true
        };
        
        _shader.SetTexture(0, "Result", _texture);
        _renderer.sharedMaterial.mainTexture = _texture;
    }
    private void Start()
    {
        _renderer = GetComponent<MeshRenderer>();
        
        NewTexture();
    }

    private void Update()
    {
        _scale += Input.GetAxisRaw("Zoom") * _zoomSpeed * _scale * Time.deltaTime;
        _position.y += Input.GetAxisRaw("Vertical") * _moveSpeed * Time.deltaTime * _scale;
        _position.x += Input.GetAxisRaw("Horizontal") * _moveSpeed * Time.deltaTime * _scale;
        
        if (_dimension.x != _texture.width || _dimension.y != _texture.height)
            NewTexture();
        
        _shader.SetInts("Dimension", _dimension.x, _dimension.y);
        _shader.SetInt("MaxIter", _maxIter);
        _shader.SetFloat("MaxDist", _maxDistance);
        _shader.SetFloats("Position", _position.x, _position.y);
        _shader.SetVector("EmptyColor", _emptyColor);
        _shader.SetVector("FullColor", _fullColor);
        _shader.SetFloat("Scale", _scale);
        
        _shader.GetKernelThreadGroupSizes(0, out var x, out var y, out var z);
        
        _shader.Dispatch(0, (_texture.width + (int)x - 1) / (int)x, Mathf.CeilToInt(_texture.height / (float)y), 1);
    }
}