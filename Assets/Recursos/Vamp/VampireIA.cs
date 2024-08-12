using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class VampireIA : MonoBehaviour
{
    NavMeshAgent agent;

    public Transform playerTransform;

    public bool perseguindoPlayer = false;

    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        if (perseguindoPlayer)
        {
            agent.destination = playerTransform.position;
        }
        
    }
}
