using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class VampireScript : MonoBehaviour
{
    //Outros Scripts
    private RagDollScript ragDoll;
    private AudioManager audioManager;

    //Estados do vampiro
    public enum VampireStates
    {
        SeguindoPlayer,
        Ragdoll,
        FicandoDePe,
        MortePermanente
    }
    public VampireStates estadoAtual = VampireStates.SeguindoPlayer;

    //Referencias Gerais
    private Animator _animator;

    //Referencias De IA
    NavMeshAgent agent;
    public Transform playerTransform;
    public float maxDistance = 1.0f;
    public float tempoParaLevantar = 2;

    [SerializeField] string _standUpStateName;

    public bool morreuPraSempre;

    // Start is called before the first frame update
    void Start()
    {
        audioManager = FindObjectOfType<AudioManager>();
        agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        ragDoll = GetComponent<RagDollScript>();
    }

    void Update()
    {
        switch (estadoAtual)
        {
            case VampireStates.SeguindoPlayer:
                CasoSeguirPlayer();
                break;
            case VampireStates.Ragdoll:
                CasoModoRagdoll();
                break;
            case VampireStates.FicandoDePe:
                CasoFicandoDePé();
                break;
            case VampireStates.MortePermanente:
                CasoMortePermanente();
                break;
        }
    }

    // Caso quando esta seguindo player
    private void CasoSeguirPlayer()
    {
        _animator.SetBool("runing", true);

       // audioManager.Play("vampiroViuPlayer");

        float sqDistance = (playerTransform.position - agent.destination).magnitude;
        if (sqDistance > maxDistance * maxDistance)
        {
            agent.destination = playerTransform.position;
        }
    }


    // Caso quando esta caido
    private void CasoModoRagdoll()
    {
        tempoParaLevantar -= Time.deltaTime;
         
        if(tempoParaLevantar <= 0 && morreuPraSempre == false)
        {
            ragDoll.AlinharPosição();

            estadoAtual = VampireStates.FicandoDePe;
            ragDoll.DisabiltarRagdoll();

            _animator.Play(_standUpStateName);
        }
        if(morreuPraSempre == true)
        {
            estadoAtual = VampireStates.MortePermanente;
        }
      
    }

   private void CasoFicandoDePé()
    {
        if(_animator.GetCurrentAnimatorStateInfo(0).IsName(_standUpStateName) == false)
        {
           // audioManager.Play("vampiroLevanta");
            estadoAtual = VampireStates.SeguindoPlayer;
        }
    }


    private void CasoMortePermanente()
    {
        
    }

}
