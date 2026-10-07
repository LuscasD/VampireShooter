using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Liga/desliga o ragdoll e faz a transição suave do ragdoll para a animação de levantar
public class RagDollScript : MonoBehaviour
{
    [Header("Levantar")]
    public AnimationClip clipDeCostas;
    public string estadoDeCostas = "standUp";
    public AnimationClip clipDeBrucos;
    public string estadoDeBrucos = "StandingUp";
    public float tempoDeTransicao = 0.5f;

    [Header("Física")]
    public float velocidadeParado = 0.3f;
    [Tooltip("Parte do impacto aplicada só no osso atingido; o resto empurra o corpo inteiro")]
    [Range(0f, 1f)] public float impactoNoOssoAtingido = 0.3f;
    [Tooltip("Quantos trancos ele dá para se soltar de uma estaca antes de levantar")]
    public int trancosParaSoltar = 3;

    public bool Ativo { get; private set; }
    public bool Levantando { get; private set; }
    public bool Parado => rbCintura == null || rbCintura.velocity.magnitude < velocidadeParado;

    public event Action Caiu;

    private class PoseDeLevantar
    {
        public Vector3[] posicoes;
        public Quaternion[] rotacoes;
        public Vector3 cintura;        // posição da cintura (espaço local do root) no primeiro frame
        public Vector3 direcaoCabeca;  // direção cintura -> cabeça no primeiro frame
    }

    private Rigidbody[] _ragdollRigidbodies;
    private EstacaHandler[] estacas;
    private Animator animRagDoll;
    private NavMeshAgent ragDollAgent;
    private Transform ossoDaCintura;
    private Transform ossoDaCabeca;
    private Rigidbody rbCintura;
    private float massaTotal;
    private Transform[] ossos;
    private PoseDeLevantar poseCostas;
    private PoseDeLevantar poseBrucos;
    private int mascaraChao;

    void Awake()
    {
        animRagDoll = GetComponent<Animator>();
        ragDollAgent = GetComponent<NavMeshAgent>();
        ossoDaCintura = animRagDoll.GetBoneTransform(HumanBodyBones.Hips);
        ossoDaCabeca = animRagDoll.GetBoneTransform(HumanBodyBones.Head);
        rbCintura = ossoDaCintura.GetComponent<Rigidbody>();
        ossos = ossoDaCintura.GetComponentsInChildren<Transform>();
        _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        estacas = GetComponentsInChildren<EstacaHandler>();
        mascaraChao = ~LayerMask.GetMask(LayerMask.LayerToName(gameObject.layer), "Player", "gun", "Ignore Raycast");

        // Física mais estável: menos tremedeira e sem atravessar o chão com impactos fortes
        foreach (var rb in _ragdollRigidbodies)
        {
            massaTotal += rb.mass;
            rb.solverIterations = 12;
            rb.angularDrag = Mathf.Max(rb.angularDrag, 0.6f);
            rb.drag = Mathf.Max(rb.drag, 0.05f);
        }
        foreach (var junta in GetComponentsInChildren<CharacterJoint>())
            junta.enableProjection = true;

        poseCostas = AmostrarPose(clipDeCostas);
        poseBrucos = AmostrarPose(clipDeBrucos);

        DesabilitarRagdoll();
    }

    /////////// Recebe impacto em uma area e derruba com o RagDoll
    public void ReceberImpacto(Vector3 force, Vector3 hitPoint)
    {
        StopAllCoroutines();
        Levantando = false;
        HabilitarRagdoll();

        // Empurra o corpo todo e dá um tranco extra onde a bala acertou
        foreach (var rb in _ragdollRigidbodies)
            rb.AddForce(force * (1f - impactoNoOssoAtingido) * (rb.mass / massaTotal), ForceMode.Impulse);
        rigidBodyAcertado(hitPoint).AddForceAtPosition(force * impactoNoOssoAtingido, hitPoint, ForceMode.Impulse);

        Caiu?.Invoke();
    }

    public void Levantar()
    {
        if (Ativo && !Levantando) StartCoroutine(RotinaLevantar());
    }

    private Rigidbody rigidBodyAcertado(Vector3 hitPoint)
    {
        Rigidbody rigidBodyMaisProxima = null;
        float distanciaMaisProxima = 0;

        foreach (var rigidbody in _ragdollRigidbodies)
        {
            float distancia = Vector3.Distance(rigidbody.position, hitPoint);

            if (rigidBodyMaisProxima == null || distancia < distanciaMaisProxima)
            {
                distanciaMaisProxima = distancia;
                rigidBodyMaisProxima = rigidbody;
            }
        }

        return rigidBodyMaisProxima;
    }

    ///////////////////////////////////////////////////////////////
    /// Controle de Ragdoll

    private void DesabilitarRagdoll()
    {
        foreach (var rigidBody in _ragdollRigidbodies)
        {
            rigidBody.isKinematic = true;
            // Interpolação em corpos cinemáticos animados causa tremedeira
            rigidBody.interpolation = RigidbodyInterpolation.None;
        }

        Ativo = false;
        animRagDoll.enabled = true;
    }

    private void HabilitarRagdoll()
    {
        foreach (var rigidBody in _ragdollRigidbodies)
        {
            rigidBody.isKinematic = false;
            rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        Ativo = true;
        animRagDoll.enabled = false;
        ragDollAgent.enabled = false;
    }

    ///////////////////////////////////////////////////////////////
    /// Levantar

    private IEnumerator RotinaLevantar()
    {
        Levantando = true;

        // Preso numa estaca (braço, perna...): se debate e arranca a parte presa antes de levantar
        if (EstaPreso())
        {
            for (int tranco = 0; tranco < trancosParaSoltar; tranco++)
            {
                Vector3 direcao = UnityEngine.Random.insideUnitSphere;
                direcao.y = Mathf.Abs(direcao.y);
                rbCintura.AddForce(direcao.normalized * 2.5f, ForceMode.VelocityChange);
                yield return new WaitForSeconds(0.3f);
            }
            foreach (var estaca in estacas)
            {
                Vector3 puxao = (ossoDaCintura.position - estaca.transform.position).normalized * 4f + Vector3.up;
                estaca.Soltar(puxao);
            }
            // Deixa o corpo cair livre antes de começar a levantar
            yield return new WaitForSeconds(0.8f);
        }

        // Barriga pra cima ou pra baixo decide qual animação usar
        bool deCostas = ossoDaCintura.forward.y > 0f;
        PoseDeLevantar alvo = deCostas ? poseCostas : poseBrucos;
        string estado = deCostas ? estadoDeCostas : estadoDeBrucos;

        foreach (var rigidBody in _ragdollRigidbodies)
            rigidBody.isKinematic = true;

        if (alvo != null)
        {
            AlinharAoQuadril(alvo);

            // Interpola os ossos da pose do ragdoll até o primeiro frame da animação
            Vector3[] posicoesIniciais = new Vector3[ossos.Length];
            Quaternion[] rotacoesIniciais = new Quaternion[ossos.Length];
            for (int i = 0; i < ossos.Length; i++)
            {
                posicoesIniciais[i] = ossos[i].localPosition;
                rotacoesIniciais[i] = ossos[i].localRotation;
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime / tempoDeTransicao)
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                for (int i = 0; i < ossos.Length; i++)
                {
                    ossos[i].localPosition = Vector3.Lerp(posicoesIniciais[i], alvo.posicoes[i], s);
                    ossos[i].localRotation = Quaternion.Slerp(rotacoesIniciais[i], alvo.rotacoes[i], s);
                }
                yield return null;
            }
        }

        DesabilitarRagdoll();
        animRagDoll.Play(estado, 0, 0f);

        // Espera a animação de levantar terminar
        yield return null;
        while (animRagDoll.GetCurrentAnimatorStateInfo(0).IsName(estado) && !animRagDoll.IsInTransition(0))
            yield return null;

        ragDollAgent.enabled = true;
        ragDollAgent.Warp(transform.position);
        Levantando = false;
    }

    private bool EstaPreso()
    {
        foreach (var estaca in estacas)
            if (estaca.Preso) return true;
        return false;
    }

    // Move e gira o root para que a pose inicial da animação caia em cima do corpo caído
    private void AlinharAoQuadril(PoseDeLevantar alvo)
    {
        Vector3 cintura = ossoDaCintura.position;
        Quaternion rotacaoCintura = ossoDaCintura.rotation;

        Vector3 direcaoCabeca = ossoDaCabeca.position - cintura;
        direcaoCabeca.y = 0f;
        if (direcaoCabeca.sqrMagnitude > 0.0001f && alvo.direcaoCabeca.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direcaoCabeca) * Quaternion.Inverse(Quaternion.LookRotation(alvo.direcaoCabeca));

        Vector3 deslocamento = transform.TransformVector(alvo.cintura);
        deslocamento.y = 0f;
        Vector3 posicao = cintura - deslocamento;

        // Garante que o personagem esta no chão
        if (Physics.Raycast(new Vector3(posicao.x, cintura.y + 0.5f, posicao.z), Vector3.down, out RaycastHit hit, 5f, mascaraChao, QueryTriggerInteraction.Ignore))
            posicao.y = hit.point.y;

        transform.position = posicao;
        ossoDaCintura.SetPositionAndRotation(cintura, rotacaoCintura);
    }

    // Guarda a pose do primeiro frame de uma animação de levantar
    private PoseDeLevantar AmostrarPose(AnimationClip clip)
    {
        if (clip == null) return null;

        Vector3 posicaoOriginal = transform.position;
        Quaternion rotacaoOriginal = transform.rotation;

        clip.SampleAnimation(gameObject, 0f);

        var pose = new PoseDeLevantar
        {
            posicoes = new Vector3[ossos.Length],
            rotacoes = new Quaternion[ossos.Length],
        };
        for (int i = 0; i < ossos.Length; i++)
        {
            pose.posicoes[i] = ossos[i].localPosition;
            pose.rotacoes[i] = ossos[i].localRotation;
        }
        pose.cintura = transform.InverseTransformPoint(ossoDaCintura.position);
        pose.direcaoCabeca = transform.InverseTransformPoint(ossoDaCabeca.position) - pose.cintura;
        pose.direcaoCabeca.y = 0f;

        transform.SetPositionAndRotation(posicaoOriginal, rotacaoOriginal);
        return pose;
    }
}
