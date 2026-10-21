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

    // Route debug meshes are only needed while inspecting a selected waypoint.
    // Keeping this out of OnDrawGizmos avoids drawing all 403 nodes every Scene View repaint.
    private void OnDrawGizmosSelected()
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

}
