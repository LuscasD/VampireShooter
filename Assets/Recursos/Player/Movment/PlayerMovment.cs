using System.Collections;
using UnityEngine;

// Movimentação em primeira pessoa no estilo imersivo (Thief / Dishonored):
// aceleração suave, correr, agachar, pular e escalar bordas (mantle)
[RequireComponent(typeof(CharacterController))]
public class PlayerMovment : MonoBehaviour
{
    [Header("Velocidade (m/s)")]
    public float velocidadeAndando = 4.5f;
    public float velocidadeCorrendo = 7.5f;
    public float velocidadeAgachado = 2.2f;
    public float aceleracao = 35f;
    public float desaceleracao = 45f;
    [Range(0f, 1f)] public float controleNoAr = 0.3f;

    [Header("Pulo e gravidade")]
    public float alturaDoPulo = 1.2f;
    public float gravidade = -22f;
    public float coyoteTime = 0.12f;
    public float bufferDePulo = 0.15f;

    [Header("Agachar")]
    public float alturaAgachado = 1.1f;
    public float velocidadeAgachar = 6f;
    public Transform modelo;

    [Header("Escalar bordas")]
    public float alturaMaximaEscalada = 2.4f;
    public float alcanceEscalada = 0.7f;
    public float duracaoEscalada = 0.45f;

    [Header("Barulho (raio em que os inimigos ouvem)")]
    public float raioBarulhoCorrendo = 8f;
    public float raioBarulhoQueda = 6f;

    private CharacterController controller;
    private int mascaraCenario;

    private Vector3 velocidadeHorizontal;
    private float velocidadeVertical;

    private float alturaEmPe;
    private float alturaAtual;
    private Vector3 centroOriginal;
    private Vector3 escalaModelo;
    private Vector3 posicaoModelo;

    private float ultimoTempoNoChao = -10f;
    private float ultimoPedidoDePulo = -10f;
    private bool querAgachar;
    private float proximoPassoBarulhento;

    public bool NoChao { get; private set; }
    public bool Agachado { get; private set; }
    public bool Correndo { get; private set; }
    public bool Escalando { get; private set; }
    public float VelocidadeAtual => velocidadeHorizontal.magnitude;
    // Diferença entre a altura atual e a altura em pé (negativa quando agachado)
    public float DiferencaDeAltura => alturaAtual - alturaEmPe;

    // Controlados pela arma
    public float MultiplicadorExterno { get; set; } = 1f;
    public bool PodeCorrer { get; set; } = true;

    public event System.Action<float> Aterrissou;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        alturaEmPe = controller.height;
        alturaAtual = alturaEmPe;
        centroOriginal = controller.center;
        if (modelo != null)
        {
            escalaModelo = modelo.localScale;
            posicaoModelo = modelo.localPosition;
        }

        mascaraCenario = Camadas.SemPlayer;
    }

    public void Pular()
    {
        ultimoPedidoDePulo = Time.time;
    }

    public void AlternarAgachar()
    {
        querAgachar = !querAgachar;
    }

    public void Movment(Vector2 input, bool correr, bool puloSegurado)
    {
        if (Escalando) return;

        float dt = Time.deltaTime;
        input = Vector2.ClampMagnitude(input, 1f);

        // Chão
        bool estavaNoChao = NoChao;
        NoChao = controller.isGrounded;
        if (NoChao)
        {
            ultimoTempoNoChao = Time.time;
            if (!estavaNoChao)
            {
                Aterrissou?.Invoke(-velocidadeVertical);
                if (-velocidadeVertical > 8f) Barulho.Emitir(transform.position, raioBarulhoQueda);
            }
        }

        // Correr cancela o agachar
        bool querCorrer = correr && PodeCorrer && input.y > 0.1f;
        if (querCorrer && querAgachar) querAgachar = false;

        AtualizarAltura(dt);

        Correndo = querCorrer && !Agachado;

        // Pulo / escalar
        bool pediuPulo = Time.time - ultimoPedidoDePulo <= bufferDePulo;
        if ((pediuPulo || (!NoChao && puloSegurado && input.y > 0.1f)) && TentarEscalar())
        {
            ultimoPedidoDePulo = -10f;
            return;
        }
        if (pediuPulo && Time.time - ultimoTempoNoChao <= coyoteTime)
        {
            if (Agachado) querAgachar = false;
            velocidadeVertical = Mathf.Sqrt(2f * alturaDoPulo * -gravidade);
            ultimoPedidoDePulo = -10f;
            ultimoTempoNoChao = -10f;
            NoChao = false;
        }

        // Velocidade horizontal com aceleração
        float alvo = Agachado ? velocidadeAgachado : Correndo ? velocidadeCorrendo : velocidadeAndando;
        Vector3 desejada = (transform.right * input.x + transform.forward * input.y) * alvo * MultiplicadorExterno;
        float taxa = desejada.sqrMagnitude > velocidadeHorizontal.sqrMagnitude ? aceleracao : desaceleracao;
        if (!NoChao) taxa *= controleNoAr;
        velocidadeHorizontal = Vector3.MoveTowards(velocidadeHorizontal, desejada, taxa * dt);

        // Gravidade
        if (NoChao && velocidadeVertical < 0f)
            velocidadeVertical = -2f;
        else
            velocidadeVertical += gravidade * dt;

        CollisionFlags flags = controller.Move((velocidadeHorizontal + Vector3.up * velocidadeVertical) * dt);
        if ((flags & CollisionFlags.Above) != 0 && velocidadeVertical > 0f) velocidadeVertical = 0f;

        // Passos correndo fazem barulho; andar agachado é silencioso
        if (Correndo && NoChao && VelocidadeAtual > velocidadeAndando && Time.time >= proximoPassoBarulhento)
        {
            Barulho.Emitir(transform.position, raioBarulhoCorrendo);
            proximoPassoBarulhento = Time.time + 0.35f;
        }
    }

    private void AtualizarAltura(float dt)
    {
        // Só levanta se tiver espaço acima da cabeça
        Agachado = querAgachar || (alturaAtual < alturaEmPe - 0.01f && !TemEspaco(PosicaoDosPes(), alturaEmPe));

        float alvo = Agachado ? alturaAgachado : alturaEmPe;
        alturaAtual = Mathf.MoveTowards(alturaAtual, alvo, velocidadeAgachar * dt);
        AplicarAltura();
    }

    private void AplicarAltura()
    {
        // Mantém os pés no mesmo lugar ao mudar a altura
        float pes = centroOriginal.y - alturaEmPe / 2f;
        controller.height = alturaAtual;
        controller.center = new Vector3(centroOriginal.x, pes + alturaAtual / 2f, centroOriginal.z);

        if (modelo != null)
        {
            modelo.localScale = new Vector3(escalaModelo.x, escalaModelo.y * alturaAtual / alturaEmPe, escalaModelo.z);
            modelo.localPosition = posicaoModelo + Vector3.up * (controller.center.y - centroOriginal.y);
        }
    }

    private Vector3 PosicaoDosPes()
    {
        float escala = transform.lossyScale.y;
        return transform.position + Vector3.up * ((controller.center.y - controller.height / 2f) * escala);
    }

    private float RaioMundo => controller.radius * transform.lossyScale.x;

    // Verifica se uma cápsula com a altura (local) indicada cabe com os pés nesse ponto
    private bool TemEspaco(Vector3 pes, float alturaLocal)
    {
        float raio = RaioMundo * 0.9f;
        float altura = alturaLocal * transform.lossyScale.y;
        Vector3 baixo = pes + Vector3.up * (raio + 0.1f);
        Vector3 cima = pes + Vector3.up * Mathf.Max(altura - raio, raio + 0.1f);
        return !Physics.CheckCapsule(baixo, cima, raio, mascaraCenario, QueryTriggerInteraction.Ignore);
    }

    private bool TentarEscalar()
    {
        float escala = transform.lossyScale.y;
        float raio = RaioMundo;
        Vector3 pes = PosicaoDosPes();
        Vector3 frente = transform.forward;
        float degrau = controller.stepOffset * escala;

        // 1. Precisa ter uma parede na frente (na altura do joelho ou do peito)
        float distancia = raio + alcanceEscalada;
        RaycastHit parede;
        if (!Physics.Raycast(pes + Vector3.up * (degrau + 0.05f), frente, out parede, distancia, mascaraCenario, QueryTriggerInteraction.Ignore) &&
            !Physics.Raycast(pes + Vector3.up * (controller.height * escala * 0.6f), frente, out parede, distancia, mascaraCenario, QueryTriggerInteraction.Ignore))
            return false;

        // 2. Procura o topo da borda logo depois da parede
        Vector3 pontoNaBorda = parede.point - new Vector3(parede.normal.x, 0f, parede.normal.z).normalized * (raio * 0.8f);
        Vector3 origem = new Vector3(pontoNaBorda.x, pes.y + alturaMaximaEscalada + raio, pontoNaBorda.z);
        RaycastHit topo;
        if (!Physics.SphereCast(origem, raio * 0.4f, Vector3.down, out topo, alturaMaximaEscalada + raio, mascaraCenario, QueryTriggerInteraction.Ignore))
            return false;

        float alturaBorda = topo.point.y - pes.y;
        if (alturaBorda <= degrau || alturaBorda > alturaMaximaEscalada || topo.normal.y < 0.7f)
            return false;

        // 3. Precisa caber em cima (em pé ou agachado) e ter espaço para subir
        Vector3 destino = new Vector3(pontoNaBorda.x, topo.point.y + 0.02f, pontoNaBorda.z);
        bool agacharNoTopo;
        if (TemEspaco(destino, alturaEmPe)) agacharNoTopo = false;
        else if (TemEspaco(destino, alturaAgachado)) agacharNoTopo = true;
        else return false;

        Vector3 pesNoAlto = new Vector3(pes.x, destino.y, pes.z);
        if (!TemEspaco(pesNoAlto, Agachado || agacharNoTopo ? alturaAgachado : alturaEmPe))
            return false;

        StartCoroutine(Escalar(pes, destino, agacharNoTopo));
        return true;
    }

    private IEnumerator Escalar(Vector3 pesInicio, Vector3 pesDestino, bool agacharNoTopo)
    {
        Escalando = true;
        velocidadeHorizontal = Vector3.zero;
        velocidadeVertical = 0f;

        if (agacharNoTopo)
        {
            querAgachar = true;
            Agachado = true;
            alturaAtual = alturaAgachado;
            AplicarAltura();
        }

        Vector3 offset = transform.position - PosicaoDosPes();
        Vector3 subida = new Vector3(pesInicio.x, pesDestino.y, pesInicio.z);
        controller.enabled = false;

        // Sobe primeiro e depois avança, como puxando o corpo para cima da borda
        float tempoSubida = duracaoEscalada * 0.65f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / tempoSubida)
        {
            transform.position = Vector3.Lerp(pesInicio, subida, Mathf.SmoothStep(0f, 1f, t)) + offset;
            yield return null;
        }
        float tempoAvanco = duracaoEscalada - tempoSubida;
        for (float t = 0f; t < 1f; t += Time.deltaTime / tempoAvanco)
        {
            transform.position = Vector3.Lerp(subida, pesDestino, Mathf.SmoothStep(0f, 1f, t)) + offset;
            yield return null;
        }
        transform.position = pesDestino + offset;

        controller.enabled = true;
        Escalando = false;
        NoChao = true;
        Aterrissou?.Invoke(3f);
    }
}
