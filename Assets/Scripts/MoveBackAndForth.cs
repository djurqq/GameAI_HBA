using UnityEngine;

public class MoveBackAndForth : MonoBehaviour
{
    public Vector3 offset = new Vector3(5, 0, 0);
    public float speed = 2f;

    Vector3 start;

    void Start() => start = transform.position;

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        transform.position = Vector3.Lerp(start, start + offset, t);
    }
}
