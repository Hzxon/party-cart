using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [HideInInspector]
    public Waypoint next;

    [Header("Track Metadata")]
    public bool isSharpCorner;
    public bool hasItemBox;
    public bool isShortcut;

    [Header("Debug")]
    public float radius = 2f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);

        if (next != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, next.transform.position);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
