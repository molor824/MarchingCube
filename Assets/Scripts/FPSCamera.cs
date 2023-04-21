using System;
using UnityEngine;

public class FPSCamera : MonoBehaviour
{
    [SerializeField] private float _sensitivity = 2;
    [SerializeField] private float _speed = 5;

    private bool _locked = false;

    private void Start()
    {
        SwitchLock();
    }

    void SwitchLock()
    {
        _locked = !_locked;
        Cursor.lockState = _locked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void Update()
    {
        if (_locked)
        {
            var mouseX = Input.GetAxis("Mouse X") * _sensitivity;
            var mouseY = -Input.GetAxis("Mouse Y") * _sensitivity;

            var euler = transform.eulerAngles;
            euler.y += mouseX;
            euler.x += mouseY;

            var dist = Mathf.DeltaAngle(0, euler.x);
            if (Mathf.Abs(dist) > 90)
                euler.x = Mathf.Sign(dist) * 90;

            transform.eulerAngles = euler;
        }
        
        var axis = (transform.forward * Input.GetAxisRaw("Vertical") + transform.right * Input.GetAxisRaw("Horizontal") +
                    transform.up * Input.GetAxisRaw("Upwards")) * _speed * Time.deltaTime;
        
        transform.Translate(axis, Space.World);

        if (Input.GetKeyDown(KeyCode.Escape)) SwitchLock();
    }
}