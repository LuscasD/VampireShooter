using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DebugIAVamp : MonoBehaviour
{

    public bool velocity;
    public bool desiredVelocity;
    public bool path;

   NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }


    void OnDrawGizmos()
    {
        if (velocity)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + agent.velocity);
        }

        if (desiredVelocity)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + agent.desiredVelocity);
        }

        if (path)
        {
            Gizmos.color = Color.yellow;
            var agentPath = agent.path;
            Vector3 prevCorner = transform.position;
            foreach(var corner in agentPath.corners)
            {
                Gizmos.DrawLine(prevCorner, corner);
                Gizmos.DrawSphere(corner, 0.1f);

                prevCorner = corner;
            }
        }
    }
}

