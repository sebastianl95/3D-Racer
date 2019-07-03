using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaypointGizmos : MonoBehaviour
{
    public float size = 1f;
    private Transform[] waypoints;
    void OnDrawGizmos()
    {
        waypoints = gameObject.GetComponentsInChildren<Transform>();
        Vector3 last = waypoints[waypoints.Length - 1].position;
        for (int i = 1; i < waypoints.Length; i++)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(waypoints[i].position, size);
            Gizmos.DrawLine(last, waypoints[i].position);
            last = waypoints[i].position;
        }
    }
}
