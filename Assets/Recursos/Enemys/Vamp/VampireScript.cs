using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;

// IA do vampiro no estilo dos guardas de Thief / Dishonored:
// patrulha, cone de visão com medidor de detecção, investiga barulhos e persegue o player
public class VampireScript : MonoBehaviour
{
    public enum VampireStates
    {
        Patrulhando,
        Suspeito,
        Investigando,
        Perseguindo,
        Ragdoll,
        FicandoDePe,
        MortePermanente
    }
    public VampireStates EstadoAtual { get; private set; }
    // 0 = não viu nada, 1 = viu o player
    public float Deteccao { get; private set; }

    // Usados pelos gizmos do editor (VampiroGizmos)
    public bool VendoPlayer { get; private set; }
    public Transform Olhos => olhos;
    public Vector3 UltimaPosicaoConhecida => ultimaPosicaoConhecida;
    public Vector3 PontoInvestigar => pontoInvestigar;
    public Vector3 PosicaoDoPosto => posicaoInicial;
    public Quaternion RotacaoDoPosto => rotacaoInicial;

    public Transform playerTransform;

    [Header("Visão")]
    public float alcanceDaVisao = 15f;
    [Range(10f, 180f)] public float anguloDeVisao = 110f;
    [Tooltip("Mais perto que isso percebe o player na hora, mesmo fora do cone")]
    public float distanciaInstantanea = 1.5f;
    [Tooltip("Segundos para detectar o player de pé, no centro da visão, a meia distância")]
    public float tempoParaDetectar = 1.2f;
    [Tooltip("Segundos para o medidor cair de cheio a zero")]
    public float tempoParaEsquecer = 5f;
    [Tooltip("Segundos que continua sabendo onde o player está depois de perdê-lo de vista na perseguição")]
    public float memoriaAoPerseguir = 1.5f;

    [Header("Audição")]
    public float multiplicadorAudicao = 1f;

    [Header("Movimento")]
    public float velocidadePatrulha = 1.5f;
    public float velocidadeInvestigando = 2.2f;
    public float velocidadePerseguindo = 5.5f;
    public Transform[] pontosDePatrulha;
    public float esperaNoPonto = 3f;
    public float tempoProcurando = 6f;

    [Header("Ataque")]
    public float alcanceDoAtaque = 1.6f;
    public float intervaloDoAtaque = 1.5f;
    public UnityEvent aoAlcancarPlayer;

    [Header("Cair")]
    public float tempoMinimoCaido = 2.5f;
    public float tempoMaximoCaido = 8f;
    public bool morreuPraSempre;

    [Header("Olhar com a cabeça (Animation Rigging)")]
    public Rig rigOlhar;
    public Transform alvoOlhar;
    [Tooltip("Quão rápido a cabeça começa/para de seguir o alvo")]
    public float velocidadeVirarCabeca = 3f;
    [Tooltip("Quão rápido o ponto de olhar acompanha o player")]
    public float suavidadeOlhar = 8f;

    [Header("Debug")]
    public bool mostrarIndicador = true;

    private RagDollScript ragDoll;
    private NavMeshAgent agent;
    private Animator _animator;
    private Transform olhos;
    private CharacterController playerController;
    private PlayerMovment playerMovimento;
    private int mascaraVisao;

    private Vector3 posicaoInicial;
    private Quaternion rotacaoInicial;
    private int indicePatrulha;
    private float tempoNoEstado;
    private float tempoSemVer;
    private float proximoAtaque;
    private Vector3 ultimaPosicaoConhecida;
    private Vector3 pontoInvestigar;

    private TextMesh indicador;
    private Transform cabecaDoPlayer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        ragDoll = GetComponent<RagDollScript>();
        olhos = _animator.GetBoneTransform(HumanBodyBones.Head);

        if (playerTransform == null) playerTransform = FindObjectOfType<PlayerMovment>().transform;
        playerController = playerTransform.GetComponent<CharacterController>();
        Camera cameraDoPlayer = playerTransform.GetComponentInChildren<Camera>();
        cabecaDoPlayer = cameraDoPlayer != null ? cameraDoPlayer.transform : playerTransform;
        playerMovimento = playerTransform.GetComponent<PlayerMovment>();

        mascaraVisao = ~LayerMask.GetMask(LayerMask.LayerToName(gameObject.layer), "gun", "Ignore Raycast");
        posicaoInicial = transform.position;
        rotacaoInicial = transform.rotation;

        if (mostrarIndicador) CriarIndicador();
    }

    private void OnEnable()
    {
        ragDoll.Caiu += AoCair;
        Barulho.Emitido += AoOuvir;
    }
    private void OnDisable()
    {
        ragDoll.Caiu -= AoCair;
        Barulho.Emitido -= AoOuvir;
    }
    private void OnDestroy()
    {
        if (indicador != null) Destroy(indicador.gameObject);
    }

    void Update()
    {
        tempoNoEstado += Time.deltaTime;

        switch (EstadoAtual)
        {
            case VampireStates.Patrulhando: CasoPatrulhando(); break;
            case VampireStates.Suspeito: CasoSuspeito(); break;
            case VampireStates.Investigando: CasoInvestigando(); break;
            case VampireStates.Perseguindo: CasoPerseguindo(); break;
            case VampireStates.Ragdoll: CasoModoRagdoll(); break;
            case VampireStates.FicandoDePe: CasoFicandoDePé(); break;
        }

        if (agent.enabled)
            _animator.SetFloat("velocidade", agent.velocity.magnitude, 0.1f, Time.deltaTime);

        AtualizarOlhar();
    }

    // Vira a cabeça para o player quando está vendo ele (ou para onde o viu, quando desconfiado)
    private void AtualizarOlhar()
    {
        if (rigOlhar == null || alvoOlhar == null) return;

        bool ativo = EstadoAtual < VampireStates.Ragdoll;
        bool temAlvo = false;
        Vector3 ponto = Vector3.zero;
        if (ativo && VendoPlayer)
        {
            ponto = cabecaDoPlayer.position;
            temAlvo = true;
        }
        else if (EstadoAtual == VampireStates.Suspeito)
        {
            ponto = ultimaPosicaoConhecida + Vector3.up;
            temAlvo = true;
        }

        if (temAlvo)
        {
            // Se a cabeça estava solta, começa direto no alvo para não "varrer" o cenário
            alvoOlhar.position = rigOlhar.weight <= 0.01f ? ponto : Vector3.Lerp(alvoOlhar.position, ponto, suavidadeOlhar * Time.deltaTime);
        }
        rigOlhar.weight = Mathf.MoveTowards(rigOlhar.weight, temAlvo ? 1f : 0f, velocidadeVirarCabeca * Time.deltaTime);
    }

    private void LateUpdate()
    {
        AtualizarIndicador();
    }

    ///////////////////////////////////////////////////////////////
    /// Estados

    private void CasoPatrulhando()
    {
        bool vendo = AtualizarDeteccao();
        if (Deteccao >= 1f) { MudarEstado(VampireStates.Perseguindo); return; }
        if (vendo && Deteccao > 0.25f) { MudarEstado(VampireStates.Suspeito); return; }

        if (pontosDePatrulha != null && pontosDePatrulha.Length > 0)
        {
            Mover(pontosDePatrulha[indicePatrulha].position, velocidadePatrulha, 0.3f);
            if (Chegou())
            {
                // Espera um pouco em cada ponto antes de seguir
                if (tempoNoEstado >= esperaNoPonto)
                {
                    indicePatrulha = (indicePatrulha + 1) % pontosDePatrulha.Length;
                    tempoNoEstado = 0f;
                }
            }
            else tempoNoEstado = 0f;
        }
        else
        {
            // Guarda parado: volta para o posto e olha para a direção original
            Mover(posicaoInicial, velocidadePatrulha, 0.3f);
            if (Chegou())
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoInicial, 120f * Time.deltaTime);
        }
    }

    private void CasoSuspeito()
    {
        // Para e encara o que viu enquanto o medidor enche
        bool vendo = AtualizarDeteccao();
        Parar();
        OlharPara(ultimaPosicaoConhecida);

        if (Deteccao >= 1f) MudarEstado(VampireStates.Perseguindo);
        else if (!vendo && tempoNoEstado > 1f) Investigar(ultimaPosicaoConhecida);
        else if (Deteccao <= 0f) MudarEstado(VampireStates.Patrulhando);
    }

    private void CasoInvestigando()
    {
        bool vendo = AtualizarDeteccao();
        if (Deteccao >= 1f) { MudarEstado(VampireStates.Perseguindo); return; }
        if (vendo) pontoInvestigar = ultimaPosicaoConhecida;

        Mover(pontoInvestigar, velocidadeInvestigando, 1f);
        if (!Chegou())
        {
            tempoSemVer = 0f;
            return;
        }

        // Chegou no local: olha em volta procurando
        tempoSemVer += Time.deltaTime;
        transform.Rotate(Vector3.up, Mathf.Sin(tempoSemVer * 1.2f) * 90f * Time.deltaTime);
        if (tempoSemVer >= tempoProcurando) MudarEstado(VampireStates.Patrulhando);
    }

    private void CasoPerseguindo()
    {
        bool vendo = AtualizarDeteccao();
        if (vendo)
        {
            Deteccao = 1f;
            tempoSemVer = 0f;
        }
        else
        {
            // Memória curta: logo depois de perder de vista ainda sabe para onde o player foi
            tempoSemVer += Time.deltaTime;
            if (tempoSemVer < memoriaAoPerseguir) ultimaPosicaoConhecida = playerTransform.position;
        }

        Mover(ultimaPosicaoConhecida, velocidadePerseguindo, alcanceDoAtaque * 0.8f);

        float distancia = Vector3.Distance(transform.position, playerTransform.position);
        if (vendo && distancia <= alcanceDoAtaque)
        {
            OlharPara(playerTransform.position);
            if (Time.time >= proximoAtaque)
            {
                proximoAtaque = Time.time + intervaloDoAtaque;
                aoAlcancarPlayer.Invoke();
            }
        }

        // Perdeu o player de vista: vai procurar onde viu por último
        if (!vendo && tempoSemVer > memoriaAoPerseguir && (tempoSemVer > 4f || Chegou()))
        {
            Deteccao = 0.6f;
            Investigar(ultimaPosicaoConhecida);
        }
    }

    // Caso quando esta caido
    private void CasoModoRagdoll()
    {
        if (morreuPraSempre)
        {
            MudarEstado(VampireStates.MortePermanente);
            return;
        }

        // Levanta quando o corpo parar de rolar
        if (tempoNoEstado >= tempoMinimoCaido && (ragDoll.Parado || tempoNoEstado >= tempoMaximoCaido))
        {
            ragDoll.Levantar();
            MudarEstado(VampireStates.FicandoDePe);
        }
    }

    private void CasoFicandoDePé()
    {
        if (ragDoll.Levantando) return;

        // Levanta sabendo onde o player está
        ultimaPosicaoConhecida = playerTransform.position;
        Deteccao = 1f;
        MudarEstado(VampireStates.Perseguindo);
    }

    ///////////////////////////////////////////////////////////////
    /// Eventos

    private void AoCair()
    {
        VendoPlayer = false;
        if (rigOlhar != null) rigOlhar.weight = 0f;
        Deteccao = 1f;
        MudarEstado(VampireStates.Ragdoll);
    }

    private void AoOuvir(Vector3 posicao, float raio)
    {
        if (EstadoAtual >= VampireStates.Ragdoll) return;
        if (Vector3.Distance(transform.position, posicao) > raio * multiplicadorAudicao) return;

        if (EstadoAtual == VampireStates.Perseguindo)
        {
            if (tempoSemVer > 0f) ultimaPosicaoConhecida = posicao;
            return;
        }

        Deteccao = Mathf.Max(Deteccao, 0.5f);
        Investigar(posicao);
    }

    ///////////////////////////////////////////////////////////////
    /// Visão

    // Enche ou esvazia o medidor de detecção. Retorna se está vendo o player
    private bool AtualizarDeteccao()
    {
        VendoPlayer = PlayerVisivel(out float distancia, out bool central);
        if (VendoPlayer)
        {
            ultimaPosicaoConhecida = playerTransform.position;

            if (distancia <= distanciaInstantanea)
                Deteccao = 1f;
            else
            {
                // Mais rápido de perto, mais devagar na visão periférica e com o player agachado
                float fator = Mathf.Lerp(2f, 0.4f, distancia / alcanceDaVisao);
                if (!central) fator *= 0.5f;
                if (playerMovimento != null)
                {
                    if (playerMovimento.Agachado) fator *= 0.5f;
                    if (playerMovimento.VelocidadeAtual > 1f) fator *= 1.3f;
                }
                Deteccao += fator / tempoParaDetectar * Time.deltaTime;
            }
            Deteccao = Mathf.Clamp01(Deteccao);
            return true;
        }

        Deteccao = Mathf.Clamp01(Deteccao - Time.deltaTime / tempoParaEsquecer);
        return false;
    }

    private bool PlayerVisivel(out float distancia, out bool central)
    {
        distancia = 0f;
        central = false;

        // Testa a cabeça e o centro do corpo do player
        Vector3 centro = playerTransform.TransformPoint(playerController.center);
        Vector3 cabeca = centro + Vector3.up * (playerController.height * playerTransform.lossyScale.y * 0.4f);

        foreach (Vector3 ponto in new[] { cabeca, centro })
        {
            Vector3 direcao = ponto - olhos.position;
            float dist = direcao.magnitude;
            if (dist > alcanceDaVisao) continue;

            // Fora do cone, só percebe se o player estiver colado nele
            float angulo = Vector3.Angle(transform.forward, direcao);
            if (angulo > anguloDeVisao * 0.5f && dist > distanciaInstantanea) continue;

            if (Physics.Raycast(olhos.position, direcao, out RaycastHit hit, dist, mascaraVisao, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(playerTransform))
                continue;

            distancia = dist;
            central = angulo <= anguloDeVisao * 0.25f;
            return true;
        }
        return false;
    }

    ///////////////////////////////////////////////////////////////
    /// Auxiliares

    private void MudarEstado(VampireStates novo)
    {
        EstadoAtual = novo;
        tempoNoEstado = 0f;
        tempoSemVer = 0f;
    }

    private void Investigar(Vector3 ponto)
    {
        pontoInvestigar = ponto;
        MudarEstado(VampireStates.Investigando);
    }

    private void Mover(Vector3 destino, float velocidade, float distanciaParada)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;
        agent.speed = velocidade;
        agent.stoppingDistance = distanciaParada;
        if ((agent.destination - destino).sqrMagnitude > 0.25f) agent.SetDestination(destino);
    }

    private void Parar()
    {
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    private bool Chegou()
    {
        return agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    private void OlharPara(Vector3 ponto)
    {
        Vector3 direcao = ponto - transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direcao), 240f * Time.deltaTime);
    }

    ///////////////////////////////////////////////////////////////
    /// Indicador acima da cabeça ("?" suspeito, "!" alerta)

    private void CriarIndicador()
    {
        var go = new GameObject("Indicador_" + name);
        indicador = go.AddComponent<TextMesh>();
        indicador.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = indicador.font.material;
        indicador.fontSize = 64;
        indicador.characterSize = 0.03f;
        indicador.anchor = TextAnchor.MiddleCenter;
        indicador.fontStyle = FontStyle.Bold;
    }

    private void AtualizarIndicador()
    {
        if (indicador == null) return;

        bool alerta = EstadoAtual == VampireStates.Perseguindo;
        bool suspeito = !alerta && EstadoAtual < VampireStates.Ragdoll && Deteccao > 0.05f;
        indicador.gameObject.SetActive(alerta || suspeito);
        if (!alerta && !suspeito) return;

        indicador.text = alerta ? "!" : "?";
        indicador.color = alerta ? new Color(0.9f, 0.1f, 0.1f) : Color.Lerp(new Color(1f, 1f, 1f, 0.3f), new Color(1f, 0.75f, 0.1f), Deteccao);
        indicador.transform.position = olhos.position + Vector3.up * 0.6f;
        if (Camera.main != null) indicador.transform.rotation = Camera.main.transform.rotation;
    }
}
