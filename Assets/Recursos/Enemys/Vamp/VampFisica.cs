using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class VampFisica : MonoBehaviour
    //// Código que controla os comportamentos do inimigo 
    ///e tambem sua fisica e ragdoll
{
    [SerializeField]
    private Camera _camera;

    [SerializeField]
    private string ficarEmPeFrenteStateName;
    [SerializeField]
    private string ficarEmPeTrazStateName;

    [SerializeField]
    private string clipNameLevantarDeFrente;

    [SerializeField]
    private string clipNameLevantarDeTraz;

    [SerializeField]
    private float tempoParaReiniciarBone;

    private Animator _animator;

    // A posição dos ossos do personagem
    private class BoneTransform
    {
        public Vector3 Position { get; set; }
        
        public Quaternion Rotation { get; set; }
    }

    //Variaveis da IA
    //Define os estados possiveis para o Inimigo
    private enum EnemyState
    {
        Idle,
        correndoAtéPlayer,
        Ragdoll,
        impaled,
        ficandoDePe,
        resetarBones
                   
    }
    private EnemyState estadoAtual = EnemyState.Idle;

    private Rigidbody[] ragdolls;
  
    public float tempoMinimoParaAcordar, tempoMaximoParaAcordar;
    private float tempoParaAcordarTotal;

    private Transform cintura;

    public bool morteFinal = false;

    private BoneTransform[] ficandoEmPeFrenteTransform;
    private BoneTransform[] ficandoEmPeTrazTransform;
    private bool estaViradoParaFrente;

    private BoneTransform[] raggdolTransform;
    private Transform[] bones;
    private float elapseResetBoneTime;





    VampireIA iA;
    NavMeshAgent agent;

    public Transform playerTransform;

    public bool viuPlayer;
    public bool conseguePerseguir = false;

    public float maxTime = 1.0f;
    public float maxDistance = 1.0f;

    float timer = 0.0f;

    // Start is called before the first frame update
    void Awake()
    {

        iA = GetComponent<VampireIA>();
        ragdolls = GetComponentsInChildren<Rigidbody>();
        _animator = GetComponent<Animator>();

        cintura = _animator.GetBoneTransform(HumanBodyBones.Hips);
        bones = cintura.GetComponentsInChildren<Transform>();

        ficandoEmPeFrenteTransform = new BoneTransform[bones.Length];
        ficandoEmPeTrazTransform = new BoneTransform[bones.Length];
        raggdolTransform = new BoneTransform[bones.Length];

        for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
        {
            ficandoEmPeFrenteTransform[boneIndex] = new BoneTransform();
            ficandoEmPeTrazTransform[boneIndex] = new BoneTransform();
            raggdolTransform[boneIndex] = new BoneTransform();
        }

        PopulateAnimationStartBoneTransform(clipNameLevantarDeFrente, ficandoEmPeFrenteTransform);
        PopulateAnimationStartBoneTransform(clipNameLevantarDeTraz, ficandoEmPeTrazTransform);


        DesligarORaggdoll();

        cintura = _animator.GetBoneTransform(HumanBodyBones.Hips);
    }

    // Update is called once per frame
    void Update()
    {
        switch (estadoAtual)
        {
            case EnemyState.Idle:
                idleBehaviour();
                break;
            case EnemyState.Ragdoll:
                RagdollBehaviour();
                break;
            case EnemyState.ficandoDePe:
                levantandoBehavior();
                break;
            case EnemyState.resetarBones:
                ResetarBoneBehavior();
                break;
        }
    }
    
    public void TriggerRagdoll(Vector3 force, Vector3 hitPoint)
    {
        if(estadoAtual == EnemyState.impaled)
        {
            return;
        }

        AbilitarORaggdoll();

        Rigidbody rigAtingido = rigidBodyAcertado(hitPoint);

        rigAtingido.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
        estadoAtual = EnemyState.Ragdoll;
        iA.conseguePerseguir = false;
        Debug.Log("esta no raggdol state");

        // Pega algum numero entre o tempo de acordar maximo e o minimo
        tempoParaAcordarTotal = Random.Range(tempoMinimoParaAcordar, tempoMaximoParaAcordar);
    }

    //Calcula a posição e pega o compontente RigidBody do objeto atingido
    private Rigidbody rigidBodyAcertado(Vector3 hitPoint)
    {
        Rigidbody rigidBodyMaisProxima = null;
        float distanciaMaisProxima = 0;

        foreach (var rigidbody in ragdolls)
        {
            float distancia = Vector3.Distance(rigidbody.position, hitPoint);

            if(rigidBodyMaisProxima == null || distancia < distanciaMaisProxima)
            {
                distanciaMaisProxima = distancia;
                rigidBodyMaisProxima = rigidbody;
            }
        }

        return rigidBodyMaisProxima;

    }

    //Habilita os compontentes do inimigo e retira o estado Raggdoll
    public void DesligarORaggdoll() 
    { 
        foreach (var rigs in ragdolls) 
        {
            rigs.isKinematic = true;
        }
        _animator.enabled = true;

    }

    public void AbilitarORaggdoll() 
    {
        _animator.SetBool("runing", false);
        foreach (var rigs in ragdolls)
        {
            rigs.isKinematic = false;
        }
        _animator.enabled = false;
    }
    
    //Inicio do alinhamaento do personagem quando se Levanta
    private void AlinharCinturaComPosição()
    {
        Vector3 posiçãoOriginalDaCintura = cintura.position;
        transform.position = cintura.position;

        Vector3 positionOffset = GetStandUpBoneTransforms()[0].Position;
        positionOffset.y = 0;
        positionOffset = transform.rotation * positionOffset;
        transform.position -= positionOffset;

        if(Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo))
        {
            transform.position = new Vector3(transform.position.x, hitInfo.point.y, transform.position.z);
        }

        cintura.position = posiçãoOriginalDaCintura;
    }

    private void AlinharCinturaComRotacao()
    {
        Vector3 posOriginalDaCintura = cintura.position;
        Quaternion rotOriginalDaCintura = cintura.rotation;

        Vector3 direcaoDesejada = cintura.up;

        if (estaViradoParaFrente)
        {
            direcaoDesejada *= -1;
        }
        direcaoDesejada.y = 0;
        direcaoDesejada.Normalize();

        Quaternion fromToRotation = Quaternion.FromToRotation(transform.forward, direcaoDesejada);
        transform.rotation *= fromToRotation;

        cintura.position = posOriginalDaCintura;
        cintura.rotation = rotOriginalDaCintura;
    }

    //Final do alinhamaento do personagem quando se Levanta

    //Comportamentos

    //Comportamento parado
    private void idleBehaviour()
    {

    }

    //o comportamento que acontece quando o personagem levanta
    private void levantandoBehavior()
    {

        if (_animator.GetCurrentAnimatorStateInfo(0).IsName(ficarEmPeStateName()) == false)
        {
            estadoAtual = EnemyState.Idle;
        }
    }

    //o comportamento quando esta no chão
    private void RagdollBehaviour()
    {
        //Isso não acontece se o inimigo sofre uma morte final
        if (!morteFinal)
        {
            tempoParaAcordarTotal -= Time.deltaTime;

            if(tempoParaAcordarTotal <= 0)
            {
                estaViradoParaFrente = cintura.forward.y > 0;

                AlinharCinturaComPosição();
                AlinharCinturaComRotacao();

                PopulateBoneTransform(raggdolTransform);

                estadoAtual = EnemyState.resetarBones;
                elapseResetBoneTime = 0;
            }
       }
        
    }

    private void ResetarBoneBehavior()
    {
        elapseResetBoneTime += Time.deltaTime;
        float elapsePercentage = elapseResetBoneTime / tempoParaReiniciarBone;

        BoneTransform[] ficandoEmPeBoneTransform = GetStandUpBoneTransforms();

        for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
        {
            bones[boneIndex].localPosition = Vector3.Lerp(
                raggdolTransform[boneIndex].Position,
                ficandoEmPeBoneTransform[boneIndex].Position,
                elapsePercentage);

            bones[boneIndex].localRotation = Quaternion.Lerp(
                raggdolTransform[boneIndex].Rotation,
                ficandoEmPeBoneTransform[boneIndex].Rotation,
                elapsePercentage);
        }

        if(elapsePercentage >= 1)
        {
            estadoAtual = EnemyState.ficandoDePe;
            DesligarORaggdoll();

            _animator.Play(ficarEmPeStateName(), 0, 0);
        }


    }

    public void Impale()
    {
        // define que sofre uma morte final e que esta no estado de Impalado
        //Debug.log("Chamou o estado impaldo");
        morteFinal = true;
        estadoAtual = EnemyState.impaled;
    }

    //Fim comportamentos

    private void PopulateBoneTransform(BoneTransform[] boneTransforms)
    {
        for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
        {
            boneTransforms[boneIndex].Position = bones[boneIndex].localPosition;
            boneTransforms[boneIndex].Rotation = bones[boneIndex].localRotation;
        }
    }

    private void PopulateAnimationStartBoneTransform(string nomeDoClipe, BoneTransform[] boneTransforms)
    {

        Vector3 postionBeforSampling = transform.position;
        Quaternion rotationBeforeSampling = transform.rotation;

      foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
        {
            if(clip.name == nomeDoClipe)
            {
                clip.SampleAnimation(gameObject, 0);
                PopulateBoneTransform(boneTransforms);
                break;
            }
        }

        transform.position = postionBeforSampling;
        transform.rotation = rotationBeforeSampling;

    }

    private string ficarEmPeStateName()
    {
        return estaViradoParaFrente ? ficarEmPeFrenteStateName : ficarEmPeTrazStateName;
    }

    // Descobre onde esta cada bone quando o personagem for levantar
    private BoneTransform[] GetStandUpBoneTransforms()
    {
        return estaViradoParaFrente ? ficandoEmPeFrenteTransform : ficandoEmPeTrazTransform;
    }

}
