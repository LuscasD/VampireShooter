using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class VampireIA : MonoBehaviour
{
    NavMeshAgent agent;

    public Transform playerTransform;

    public bool viuPlayer;
    public bool conseguePerseguir = false;
    private Animator _animator;

    public float maxTime = 1.0f;
    public float maxDistance = 1.0f;

    float timer = 0.0f;

    private enum IAVampireEstate
    {
        parado,
        derrubado,
        patrulhando,
        correndoAtéPlayer,


    }

    IAVampireEstate estadoAtualIA = IAVampireEstate.parado;

    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
    }

    // Update uma vez por frame 
    void Update()
    {

        switch (estadoAtualIA)
        {
            case IAVampireEstate.parado:
                ComportamentoParado();
                break;
            case IAVampireEstate.correndoAtéPlayer:
                ComportamentoPerseguir();
                break;
        }

        if (viuPlayer)
        {
            estadoAtualIA = IAVampireEstate.correndoAtéPlayer;
        }

        if (agent.remainingDistance >= agent.stoppingDistance)
        {
            _animator.SetBool("runing", true);
        }

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            _animator.SetBool("runing", false);
        }

        void ComportamentoParado()
        {
            _animator.SetBool("runing", false);
        }

        void ComportamentoPerseguir()
        {
            if (conseguePerseguir)
            {
                _animator.SetBool("runing", true);
                // inicia o timer para seguir o player
                timer -= Time.deltaTime;
                if (timer < 0.0f)
                {
                    float sqDistance = (playerTransform.position - agent.destination).magnitude;
                    if (sqDistance > maxDistance * maxDistance)
                    {
                        agent.destination = playerTransform.position;
                    }
                    timer = maxTime;
                }

            }
        }

    }
}
