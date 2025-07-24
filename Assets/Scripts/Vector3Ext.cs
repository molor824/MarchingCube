using UnityEngine;

public static class Vector3Ext
{
    public static Vector3 Divide(this Vector3 a, Vector3 b)
    {
        return new(a.x / b.x, a.y / b.y, a.z / b.z);
    }
}
