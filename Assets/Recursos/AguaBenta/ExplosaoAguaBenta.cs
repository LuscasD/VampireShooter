using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Parâmetros da explosão. Ficam no Player (ArremessarAguaBenta) para poder mudar em tempo real no Inspector.
[System.Serializable]
public class ConfigExplosao
{
    [Header("1 · Consulta espacial")]
    public float raio = 6f;
    [Tooltip("Camadas que podem ser candidatas (vampiros, player, objetos com física)")]
    public LayerMask camadasAlvo;

    [Header("2 · Distância → influência")]
    [Tooltip("Eixo X: distância / raio (0 = centro, 1 = borda). Eixo Y: influência.")]
    public AnimationCurve influenciaPorDistancia = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.4f, 0.85f), new Keyframe(1f, 0f));

    [Header("3 · Oclusão")]
    [Tooltip("Camadas que bloqueiam a explosão (paredes, blocos, caixas)")]
    public LayerMask camadasObstaculo;

    [Header("4 · Resposta")]
    [Tooltip("Abaixo disso a água benta não chega com força: o alvo só ouve a explosão")]
    [Range(0f, 1f)] public float influenciaMinima = 0.05f;
    [Tooltip("Vampiro com influência acima disso morre de vez. Entre o mínimo e este valor ele só cai.")]
    [Range(0f, 1f)] public float limiteLetal = 0.3f;
    public float forcaMaxima = 600f;
    [Range(0f, 80f)] public float anguloDeLancamento = 30f;
    public float forcaEmObjetos = 15f;
    public float tremorMaximo = 5f;
    [Tooltip("Vampiros dentro deste raio ouvem a explosão e vão investigar")]
    public float raioDoBarulho = 25f;

    [Header("5 · Debug")]
    [Tooltip("Segundos que o debug fica na tela. 0 = até a próxima explosão.")]
    public float duracaoDoDebug = 0f;
    public bool mostrarPainel = true;
}

// Explosão de água benta como um pipeline:
// 1. Consulta (OverlapSphere) → 2. Distância → 3. Oclusão (Raycast) → 4. Resposta → 5. Debug visual
public class ExplosaoAguaBenta : MonoBehaviour
{
    public enum TipoAlvo { Vampiro, Player, Objeto }

    // Tudo o que foi decidido para um alvo, guardado para o debug visual
    public class Registro
    {
        public string nome;
        public TipoAlvo tipo;
        public Vector3 ponto;
        public float distancia;
        public float fatorDistancia;
        public float visibilidade;
        public float influencia;
        public Vector3[] pontosTestados;
        public bool[] caminhoLivre;
        public Vector3[] pontoDoBloqueio;
        public Vector3 forca;
        public string resultado;
    }

    public ConfigExplosao config = new ConfigExplosao();
    public Light brilho;

    public readonly List<Registro> registros = new List<Registro>();
    public int collidersEncontrados;
    public Vector3 Centro { get; private set; }

    private static ExplosaoAguaBenta ultima;
    private float criadaEm;
    private float intensidadeBrilho;

    public static ExplosaoAguaBenta Criar(ExplosaoAguaBenta prefab, Vector3 posicao, ConfigExplosao config)
    {
        ExplosaoAguaBenta explosao = Instantiate(prefab, posicao, Quaternion.identity);
        explosao.config = config;
        explosao.Explodir();
        return explosao;
    }

    public void Explodir()
    {
        Centro = transform.position;
        criadaEm = Time.time;
        // Só o debug da explosão mais recente fica na tela
        if (ultima != null && ultima != this) Destroy(ultima.gameObject);
        ultima = this;
        registros.Clear();

        // ───────────── 1. CONSULTA ESPACIAL ─────────────
        // Pega todos os colliders dentro do raio. Um vampiro tem vários (um por osso do ragdoll),
        // então agrupa por alvo para cada um ser analisado uma vez só.
        Collider[] colliders = Physics.OverlapSphere(Centro, config.raio, config.camadasAlvo, QueryTriggerInteraction.Ignore);
        collidersEncontrados = colliders.Length;

        var alvos = new Dictionary<Object, TipoAlvo>();
        foreach (Collider collider in colliders)
        {
            if (IdentificarAlvo(collider, out Object alvo, out TipoAlvo tipo) && !alvos.ContainsKey(alvo))
                alvos.Add(alvo, tipo);
        }

        foreach (var par in alvos)
        {
            Registro registro = Analisar(par.Key, par.Value);
            Responder(par.Key, registro);
            registros.Add(registro);
        }

        // Quem está longe só ouve a explosão
        Barulho.Emitir(Centro, config.raioDoBarulho);

        if (brilho != null) intensidadeBrilho = brilho.intensity;
        if (config.duracaoDoDebug > 0f) Destroy(gameObject, config.duracaoDoDebug);
    }

    // Decide que tipo de alvo é cada collider
    private bool IdentificarAlvo(Collider collider, out Object alvo, out TipoAlvo tipo)
    {
        RagDollScript vampiro = collider.GetComponentInParent<RagDollScript>();
        if (vampiro != null) { alvo = vampiro; tipo = TipoAlvo.Vampiro; return true; }

        PlayerMovment player = collider.GetComponentInParent<PlayerMovment>();
        if (player != null) { alvo = player; tipo = TipoAlvo.Player; return true; }

        Rigidbody corpo = collider.attachedRigidbody;
        if (corpo != null && !corpo.isKinematic) { alvo = corpo; tipo = TipoAlvo.Objeto; return true; }

        alvo = null;
        tipo = TipoAlvo.Objeto;
        return false;
    }

    private Registro Analisar(Object alvo, TipoAlvo tipo)
    {
        var registro = new Registro { tipo = tipo, nome = ((Component)alvo).name };
        registro.pontosTestados = PontosDoAlvo(alvo, tipo);
        registro.ponto = registro.pontosTestados[0];

        // ───────────── 2. DISTÂNCIA → INFLUÊNCIA ─────────────
        registro.distancia = Vector3.Distance(Centro, registro.ponto);
        float distanciaNormalizada = Mathf.Clamp01(registro.distancia / config.raio);
        registro.fatorDistancia = Mathf.Clamp01(config.influenciaPorDistancia.Evaluate(distanciaNormalizada));

        // ───────────── 3. OCLUSÃO (RAYCAST) ─────────────
        // Um raio para cada parte do alvo. A fração de raios livres é a "visibilidade":
        // 1 = totalmente exposto, 0 = totalmente atrás de uma parede, no meio = cobertura parcial.
        int n = registro.pontosTestados.Length;
        registro.caminhoLivre = new bool[n];
        registro.pontoDoBloqueio = new Vector3[n];
        int livres = 0;
        for (int i = 0; i < n; i++)
        {
            Vector3 direcao = registro.pontosTestados[i] - Centro;
            float distancia = direcao.magnitude;
            bool bloqueado = Physics.Raycast(Centro, direcao / distancia, out RaycastHit hit, distancia, config.camadasObstaculo, QueryTriggerInteraction.Ignore)
                             && !PertenceAoAlvo(hit.collider, alvo);
            registro.caminhoLivre[i] = !bloqueado;
            registro.pontoDoBloqueio[i] = bloqueado ? hit.point : registro.pontosTestados[i];
            if (!bloqueado) livres++;
        }
        registro.visibilidade = (float)livres / n;

        registro.influencia = registro.fatorDistancia * registro.visibilidade;
        return registro;
    }

    // Partes de cada alvo que os raios de oclusão testam (a primeira é usada para a distância)
    private Vector3[] PontosDoAlvo(Object alvo, TipoAlvo tipo)
    {
        switch (tipo)
        {
            case TipoAlvo.Vampiro:
                Animator anim = ((Component)alvo).GetComponent<Animator>();
                return new[]
                {
                    anim.GetBoneTransform(HumanBodyBones.Chest).position,
                    anim.GetBoneTransform(HumanBodyBones.Head).position,
                    anim.GetBoneTransform(HumanBodyBones.Hips).position,
                    anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position,
                    anim.GetBoneTransform(HumanBodyBones.RightLowerLeg).position,
                };
            case TipoAlvo.Player:
                var player = (PlayerMovment)alvo;
                var cc = player.GetComponent<CharacterController>();
                Vector3 centro = player.transform.TransformPoint(cc.center);
                float meiaAltura = cc.height * player.transform.lossyScale.y * 0.4f;
                return new[] { centro, centro + Vector3.up * meiaAltura, centro - Vector3.up * meiaAltura };
            default:
                Bounds limites = ((Rigidbody)alvo).GetComponent<Collider>().bounds;
                return new[] { limites.center, limites.center + Vector3.up * limites.extents.y * 0.8f, limites.center - Vector3.up * limites.extents.y * 0.8f };
        }
    }

    private static bool PertenceAoAlvo(Collider collider, Object alvo)
    {
        return collider.transform.IsChildOf(((Component)alvo).transform);
    }

    // ───────────── 4. RESPOSTA NO JOGO ─────────────
    private void Responder(Object alvo, Registro registro)
    {
        // Direção em arco, para fora do centro da explosão
        Vector3 horizontal = registro.ponto - Centro;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude < 0.0001f) horizontal = Vector3.forward;
        horizontal.Normalize();
        Vector3 direcao = Quaternion.AngleAxis(-config.anguloDeLancamento, Vector3.Cross(Vector3.up, horizontal)) * horizontal;

        if (registro.influencia < config.influenciaMinima)
        {
            registro.resultado = registro.visibilidade <= 0f ? "Protegido (parede)" : "Fraco demais: só ouviu";
            return;
        }

        switch (registro.tipo)
        {
            case TipoAlvo.Vampiro:
                var ragdoll = (RagDollScript)alvo;
                var vampiro = ragdoll.GetComponent<VampireScript>();
                bool jaMorto = vampiro.morreuPraSempre;
                bool letal = registro.influencia >= config.limiteLetal;
                if (letal)
                {
                    // Água benta mata de vez: ele nunca mais levanta
                    vampiro.morreuPraSempre = true;
                    if (ragdoll.GetComponent<QueimaduraAguaBenta>() == null)
                        ragdoll.gameObject.AddComponent<QueimaduraAguaBenta>();
                }
                registro.forca = direcao * config.forcaMaxima * registro.influencia;
                ragdoll.ReceberImpacto(registro.forca, registro.ponto);
                registro.resultado = jaMorto ? "Corpo arremessado" : letal ? "MORTO (queimado)" : "Respingo: derrubado";
                break;

            case TipoAlvo.Player:
                var olhar = ((PlayerMovment)alvo).GetComponent<OlharPlayer>();
                float tremor = config.tremorMaximo * registro.influencia;
                olhar.AdicionarRecuo(tremor, Random.Range(-tremor, tremor));
                registro.resultado = "Tremor " + tremor.ToString("0.0") + "°";
                break;

            case TipoAlvo.Objeto:
                var corpo = (Rigidbody)alvo;
                registro.forca = direcao * config.forcaEmObjetos * registro.influencia;
                corpo.AddForceAtPosition(registro.forca, registro.ponto, ForceMode.Impulse);
                registro.resultado = "Empurrado";
                break;
        }
    }

    // ───────────── 5. DEBUG VISUAL ─────────────
    // Linhas que aparecem na Scene e na Game (com Gizmos ligados), redesenhadas a cada frame enquanto a explosão existe
    private void DesenharDebug(Registro registro)
    {
        for (int i = 0; i < registro.pontosTestados.Length; i++)
        {
            if (registro.caminhoLivre[i])
                Debug.DrawLine(Centro, registro.pontosTestados[i], CorDaInfluencia(registro.influencia));
            else
            {
                Debug.DrawLine(Centro, registro.pontoDoBloqueio[i], Color.red);
                Debug.DrawLine(registro.pontoDoBloqueio[i], registro.pontosTestados[i], new Color(1f, 0f, 0f, 0.25f));
            }
        }
        if (registro.forca != Vector3.zero)
            Debug.DrawRay(registro.ponto, registro.forca.normalized * (0.5f + 2f * registro.influencia), Color.magenta);
    }

    // Verde = influência forte, amarelo = média, laranja = fraca
    private static Color CorDaInfluencia(float influencia)
    {
        return influencia >= 0.5f ? Color.Lerp(Color.yellow, Color.green, (influencia - 0.5f) * 2f)
                                  : Color.Lerp(new Color(1f, 0.45f, 0f), Color.yellow, influencia * 2f);
    }

    private void Update()
    {
        foreach (Registro registro in registros) DesenharDebug(registro);

        // Clarão azulado que some rápido
        if (brilho != null)
            brilho.intensity = Mathf.Lerp(intensidadeBrilho, 0f, (Time.time - criadaEm) / 0.4f);
    }

    // Painel na tela com a decisão de cada alvo (para explicar no vídeo)
    private void OnGUI()
    {
        if (!config.mostrarPainel || ultima != this) return;

        var estilo = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };
        float altura = 48f + registros.Count * 22f;
        GUI.Box(new Rect(10, 10, 640, altura), GUIContent.none);
        GUI.Label(new Rect(18, 14, 630, 22), "<b>Água benta</b>  ·  Overlap: " + collidersEncontrados + " colliders → " + registros.Count + " alvos  ·  raio " + config.raio.ToString("0.0") + "m", estilo);
        GUI.Label(new Rect(18, 34, 630, 22), "<color=#aaaaaa>alvo · distância · fator dist × visibilidade = influência → resultado</color>", estilo);
        for (int i = 0; i < registros.Count; i++)
        {
            Registro r = registros[i];
            string cor = r.influencia <= 0f ? "#ff5555" : r.resultado.StartsWith("MORTO") ? "#66ff66" : "#ffdd55";
            GUI.Label(new Rect(18, 56 + i * 22, 630, 22),
                "<color=" + cor + ">" + r.nome + " (" + r.tipo + ") · " + r.distancia.ToString("0.0") + "m · " +
                r.fatorDistancia.ToString("0.00") + " × " + r.visibilidade.ToString("0.00") + " = " + r.influencia.ToString("0.00") +
                " → " + r.resultado + "</color>", estilo);
        }
    }

#if UNITY_EDITOR
    // Gizmos na Scene: raio, candidatos, raios de oclusão, direção da força e números de cada alvo
    private void OnDrawGizmos()
    {
        Vector3 centro = Application.isPlaying ? Centro : transform.position;

        Handles.color = new Color(0.4f, 0.8f, 1f, 0.06f);
        Handles.DrawSolidDisc(centro, Vector3.up, config.raio);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(centro, config.raio);
        Handles.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        Handles.DrawWireDisc(centro, Vector3.up, config.raioDoBarulho);

        foreach (Registro r in registros)
        {
            for (int i = 0; i < r.pontosTestados.Length; i++)
            {
                if (r.caminhoLivre[i])
                {
                    Handles.color = CorDaInfluencia(r.influencia);
                    Handles.DrawAAPolyLine(3f, centro, r.pontosTestados[i]);
                }
                else
                {
                    Handles.color = Color.red;
                    Handles.DrawAAPolyLine(3f, centro, r.pontoDoBloqueio[i]);
                    Handles.DrawDottedLine(r.pontoDoBloqueio[i], r.pontosTestados[i], 3f);
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(r.pontoDoBloqueio[i], Vector3.one * 0.12f);
                }
                Gizmos.color = r.caminhoLivre[i] ? Color.white : Color.red;
                Gizmos.DrawSphere(r.pontosTestados[i], 0.06f);
            }

            if (r.forca != Vector3.zero)
            {
                Handles.color = Color.magenta;
                Handles.ArrowHandleCap(0, r.ponto, Quaternion.LookRotation(r.forca), 0.5f + 2f * r.influencia, EventType.Repaint);
            }

            Handles.Label(r.ponto + Vector3.up * 0.6f,
                r.nome + "\n" + r.distancia.ToString("0.0") + "m  " + r.fatorDistancia.ToString("0.00") + " × " +
                r.visibilidade.ToString("0.00") + " = " + r.influencia.ToString("0.00") + "\n" + r.resultado);
        }

        Handles.Label(centro + Vector3.up * 0.3f, "Overlap: " + collidersEncontrados + " colliders → " + registros.Count + " alvos");
    }
#endif
}
