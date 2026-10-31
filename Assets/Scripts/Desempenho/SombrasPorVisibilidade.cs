using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Suspende sombras de luzes adicionais fora de vista, desliga a lua no interior sem aberturas visíveis e,
/// com a câmera dentro do prédio, encurta a distância das sombras da lua.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class SombrasPorVisibilidade : MonoBehaviour
{
    [Min(0f)] public float margem = 1.5f;
    [Min(0)] public int quadrosDeEspera = 10;
    [Min(0f)] public float alturaDaAbertura = 2.4f;
    // Dentro do prédio, a lua só precisa de sombra perto da câmera: as quatro cascatas até 50 m somavam ~7000 casters
    // (~3 ms no Editor) no canto oeste da recepção. Medido em 2026-10-19 contra o original em 623 poses (tempo
    // congelado, ruído 0): 30 m difere em até 0,006% dos pixels, 20 m em até 0,09% e 12 m em até 1,6% (vistas longas
    // pelas aberturas, onde a luz da lua sem sombra clareia o fundo do interior). 12 m fica como alternativa.
    [Min(1f)] public float distanciaDeSombraInterna = 20f;

    Light[] luzes;
    Light[] luas;
    bool[] estadosOriginaisLua;
    MapaAbrigoChuva mapa;
    int quadrosSemLua;
    bool luaLigada = true;
    LightShadows[] sombrasOriginais;
    BoundingSphere[] esferas;
    int[] ultimoFrameVisivel;
    EspelhoPlanar[] espelhos;
    CullingGroup grupo;
    Camera cameraAlvo;
    readonly Plane[] planos = new Plane[6];
    readonly Plane[] planosAnteriores = new Plane[6];
    Plane[][] planosDosEspelhos;
    int espelhosAtivos;
    bool temPlanosAnteriores;
    UniversalRenderPipelineAsset pipeline;
    float distanciaOriginal;
    bool interno;

    public int Gerenciadas => luzes == null ? 0 : luzes.Length;
    /// <summary>Aberturas do prédio para fora (vãos achados no mapa de abrigo da chuva).</summary>
    public int Aberturas { get; private set; }
    /// <summary>Até onde (m, na horizontal) a lua entra por uma abertura: alturaDaAbertura / tan(elevação).</summary>
    public float AlcanceDaLua { get; private set; }
    public bool LuaDesligada
    {
        get
        {
            if (luaLigada || luas == null) return false;
            for (int i = 0; i < luas.Length; i++)
                if (luas[i] != null && !luas[i].enabled) return true;
            return false;
        }
    }
    public int ComSombraDesligada
    {
        get
        {
            int quantidade = 0;
            if (luzes != null)
                for (int i = 0; i < luzes.Length; i++)
                    if (luzes[i] != null && luzes[i].shadows == LightShadows.None) quantidade++;
            return quantidade;
        }
    }

    void OnEnable()
    {
        Light[] todas = FindObjectsByType<Light>(FindObjectsInactive.Include);
        int quantidade = 0;
        int quantidadeLuas = 0;
        foreach (Light luz in todas)
        {
            if ((luz.type == LightType.Spot || luz.type == LightType.Point) && luz.shadows != LightShadows.None)
                quantidade++;
            if (luz.type == LightType.Directional && luz.enabled) quantidadeLuas++;
        }

        luas = new Light[quantidadeLuas];
        estadosOriginaisLua = new bool[quantidadeLuas];
        int indiceLua = 0;
        foreach (Light luz in todas)
        {
            if (luz.type != LightType.Directional || !luz.enabled) continue;
            luas[indiceLua] = luz;
            estadosOriginaisLua[indiceLua] = luz.enabled;
            indiceLua++;
        }
        ChuvaLeve chuva = FindAnyObjectByType<ChuvaLeve>();
        mapa = chuva != null ? chuva.mapa : null;
        List<BoundingSphere> regioesDaLua = CalcularRegioesDaLua();
        quadrosSemLua = 0;
        luaLigada = true;

        luzes = new Light[quantidade];
        sombrasOriginais = new LightShadows[quantidade];
        esferas = new BoundingSphere[quantidade + regioesDaLua.Count];
        ultimoFrameVisivel = new int[esferas.Length];
        int indice = 0;
        foreach (Light luz in todas)
        {
            if ((luz.type != LightType.Spot && luz.type != LightType.Point) || luz.shadows == LightShadows.None) continue;
            luzes[indice] = luz;
            sombrasOriginais[indice] = luz.shadows;
            esferas[indice] = new BoundingSphere(luz.transform.position, luz.range + Mathf.Max(0f, margem));
            ultimoFrameVisivel[indice] = int.MinValue;
            indice++;
        }
        for (int i = 0; i < regioesDaLua.Count; i++)
        {
            esferas[quantidade + i] = regioesDaLua[i];
            ultimoFrameVisivel[quantidade + i] = int.MinValue;
        }

        espelhos = FindObjectsByType<EspelhoPlanar>(FindObjectsInactive.Include);
        cameraAlvo = Camera.main;
        temPlanosAnteriores = false;
        grupo = new CullingGroup { targetCamera = cameraAlvo };
        // O grupo mantém referência ao array: atualizar seus elementos dispensa realocação.
        grupo.SetBoundingSpheres(esferas);
        grupo.SetBoundingSphereCount(esferas.Length);
    }

    void OnDisable()
    {
        RestaurarDistanciaDeSombra();
        RestaurarSombras();
        if (luas != null)
            for (int i = 0; i < luas.Length; i++)
                if (luas[i] != null && luas[i].enabled != estadosOriginaisLua[i])
                    luas[i].enabled = estadosOriginaisLua[i];
        luaLigada = true;
        grupo?.Dispose();
        grupo = null;
        cameraAlvo = null;
        temPlanosAnteriores = false;
    }

    void LateUpdate()
    {
        Camera principal = Camera.main;
        if (cameraAlvo != principal)
        {
            cameraAlvo = principal;
            grupo.targetCamera = principal;
            temPlanosAnteriores = false;
        }

        bool seguranca = principal == null;
        // Um espelho planar ativo desenha a cena de outro ponto (câmera do reflexo, frustum já recortado na janela do
        // espelho): luzes e aberturas que só ela vê também precisam ficar. Sem teste de oclusão (conservador).
        if (planosDosEspelhos == null || planosDosEspelhos.Length != espelhos.Length)
        {
            planosDosEspelhos = new Plane[espelhos.Length][];
            for (int i = 0; i < planosDosEspelhos.Length; i++) planosDosEspelhos[i] = new Plane[6];
        }
        espelhosAtivos = 0;
        foreach (EspelhoPlanar espelho in espelhos)
        {
            Camera reflexo = espelho != null ? espelho.CameraDoReflexo : null;
            if (reflexo == null) continue;
            GeometryUtility.CalculateFrustumPlanes(reflexo, planosDosEspelhos[espelhosAtivos++]);
        }

        if (principal != null) GeometryUtility.CalculateFrustumPlanes(principal, planos);
        int quadro = Time.frameCount;
        for (int i = 0; i < luzes.Length; i++)
        {
            Light luz = luzes[i];
            if (luz == null) continue;
            Vector3 centro = luz.transform.position;
            float raio = luz.range + Mathf.Max(0f, margem);
            if (!esferas[i].position.Equals(centro) || esferas[i].radius != raio)
                esferas[i] = new BoundingSphere(centro, raio);

            if (seguranca)
            {
                ultimoFrameVisivel[i] = quadro;
                AplicarSombra(i, sombrasOriginais[i]);
                continue;
            }
            if (!luz.isActiveAndEnabled) continue;

            bool dentroAgora = IntersectaFrustum(esferas[i], planos);
            bool dentroAntes = temPlanosAnteriores && IntersectaFrustum(esferas[i], planosAnteriores);
            // A oclusão é do frame anterior; contornar paredes pode atrasar a reativação em um frame.
            bool ocluida = dentroAntes && !grupo.IsVisible(i);
            bool precisa = (dentroAgora && !ocluida) || NoFrustumDeUmReflexo(esferas[i]);
            if (precisa) ultimoFrameVisivel[i] = quadro;
            // A conversão evita overflow no sentinela de luzes ainda não vistas.
            bool manter = (long)quadro - ultimoFrameVisivel[i] <= Mathf.Max(0, quadrosDeEspera);
            AplicarSombra(i, manter ? sombrasOriginais[i] : LightShadows.None);
        }

        bool aberturaVisivel = false;
        for (int i = luzes.Length; i < esferas.Length; i++)
        {
            bool precisa = seguranca;
            if (!seguranca)
            {
                bool dentroAgora = IntersectaFrustum(esferas[i], planos);
                bool dentroAntes = temPlanosAnteriores && IntersectaFrustum(esferas[i], planosAnteriores);
                bool ocluida = dentroAntes && !grupo.IsVisible(i);
                precisa = (dentroAgora && !ocluida) || NoFrustumDeUmReflexo(esferas[i]);
            }
            if (precisa) ultimoFrameVisivel[i] = quadro;
            if ((long)quadro - ultimoFrameVisivel[i] <= Mathf.Max(0, quadrosDeEspera)) aberturaVisivel = true;
        }

        bool temMapa = mapa != null && mapa.profundidadeInterna != null && mapa.profundidadeInterna.Length > 0;
        float profundidade = principal != null && temMapa ? mapa.ProfundidadeInterna(principal.transform.position) : 0f;
        bool dentro = profundidade > 0.3f;
        AtualizarDistanciaDeSombra(!seguranca && profundidade > (interno ? 0.3f : 0.5f));
        bool precisaLua = seguranca || !dentro || aberturaVisivel;
        int espera = Mathf.Max(0, quadrosDeEspera);
        if (precisaLua) quadrosSemLua = 0;
        else if (quadrosSemLua < espera) quadrosSemLua++;
        luaLigada = precisaLua || quadrosSemLua < espera;
        for (int i = 0; i < luas.Length; i++)
            if (luas[i] != null && luas[i].enabled != luaLigada) luas[i].enabled = luaLigada;

        if (principal != null)
            for (int i = 0; i < planos.Length; i++) planosAnteriores[i] = planos[i];
        temPlanosAnteriores = principal != null;
    }

    // Por onde a lua pode aparecer vista de dentro: o vão de cada abertura para fora (ver o exterior exige ver o vão)
    // e, quando a luz entra por ele, a faixa de piso e parede que ela ilumina — um prisma da largura do vão que
    // avança na direção horizontal da luz até AlcanceDaLua. Cada trecho vira uma esfera que o envolve.
    List<BoundingSphere> CalcularRegioesDaLua()
    {
        var regioes = new List<BoundingSphere>();
        Aberturas = 0;
        AlcanceDaLua = 0f;
        if (mapa == null || mapa.profundidadeInterna == null) return regioes;
        var vaos = new List<Vector3>();
        var interior = new List<Vector3>();
        for (int z = 0; z < mapa.altura; z++)
            for (int x = 0; x < mapa.largura; x++)
            {
                float profundidade = mapa.ProfundidadeNaCelula(x, z);
                if (profundidade > 0f && profundidade <= 0.3f) vaos.Add(mapa.CentroCelula(x, z, 0f));
                else if (profundidade > 0.3f && profundidade <= 1.5f) interior.Add(mapa.CentroCelula(x, z, 0f));
            }

        float h = alturaDaAbertura;
        // Flood fill por distância: células do mesmo vão ficam a menos de 2 m umas das outras.
        var visitadas = new bool[vaos.Count];
        var fila = new int[vaos.Count];
        for (int i = 0; i < vaos.Count; i++)
        {
            if (visitadas[i]) continue;
            int inicio = 0, fim = 0;
            fila[fim++] = i;
            visitadas[i] = true;
            Vector3 soma = Vector3.zero;
            while (inicio < fim)
            {
                Vector3 c = vaos[fila[inicio++]];
                soma += c;
                for (int j = 0; j < vaos.Count; j++)
                {
                    if (visitadas[j] || (vaos[j] - c).sqrMagnitude > 4f) continue;
                    visitadas[j] = true;
                    fila[fim++] = j;
                }
            }
            Vector3 centro = soma / fim;
            float meiaLargura = 0f;
            for (int k = 0; k < fim; k++) meiaLargura = Mathf.Max(meiaLargura, Vector3.Distance(vaos[fila[k]], centro));
            meiaLargura += mapa.celula;
            Aberturas++;

            // Esfera que envolve o vão inteiro (largura x altura).
            float raio = Mathf.Sqrt(meiaLargura * meiaLargura + h * h / 4f) + 0.3f;
            Vector3 meio = centro + Vector3.up * (h / 2f);
            regioes.Add(new BoundingSphere(meio, raio));

            // Para dentro: das células do vão para as logo atrás dele.
            Vector3 somaDentro = Vector3.zero;
            int nDentro = 0;
            foreach (Vector3 c in interior)
                if ((c - centro).sqrMagnitude <= 6.25f) { somaDentro += c; nDentro++; }
            if (nDentro == 0) continue;
            Vector3 paraDentro = somaDentro / nDentro - centro;
            paraDentro.y = 0f;
            paraDentro.Normalize();

            foreach (Light lua in luas)
            {
                Vector3 rumo = lua.transform.forward;
                float elevacao = Mathf.Max(5f, Mathf.Asin(Mathf.Clamp01(-rumo.y)) * Mathf.Rad2Deg);
                rumo.y = 0f;
                if (rumo.sqrMagnitude < 1e-4f) rumo = paraDentro;   // lua a pino: cai logo atrás do vão
                rumo.Normalize();
                if (Vector3.Dot(rumo, paraDentro) <= 0.05f) continue;   // a luz sai por este vão, não entra
                float alcance = h / Mathf.Tan(elevacao * Mathf.Deg2Rad) + 0.5f;
                AlcanceDaLua = Mathf.Max(AlcanceDaLua, alcance);
                for (float d = raio; d < alcance + raio; d += raio)
                    regioes.Add(new BoundingSphere(meio + rumo * d, raio));
            }
        }
        return regioes;
    }

    bool NoFrustumDeUmReflexo(BoundingSphere esfera)
    {
        for (int k = 0; k < espelhosAtivos; k++)
            if (IntersectaFrustum(esfera, planosDosEspelhos[k])) return true;
        return false;
    }

    static bool IntersectaFrustum(BoundingSphere esfera, Plane[] planosDoFrustum)
    {
        for (int i = 0; i < planosDoFrustum.Length; i++)
            if (planosDoFrustum[i].GetDistanceToPoint(esfera.position) < -esfera.radius) return false;
        return true;
    }

    // O valor do asset do pipeline é trocado em tempo de execução e devolvido ao desligar (no Editor o asset é o mesmo
    // do projeto, então nunca fica alterado depois do Play Mode). A entrada pede 0,2 m a mais que a saída para não
    // alternar parado na borda.
    void AtualizarDistanciaDeSombra(bool dentro)
    {
        if (pipeline == null)
        {
            pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            distanciaOriginal = pipeline.shadowDistance;
        }
        interno = dentro && distanciaDeSombraInterna < distanciaOriginal;
        float alvo = interno ? distanciaDeSombraInterna : distanciaOriginal;
        if (!Mathf.Approximately(pipeline.shadowDistance, alvo)) pipeline.shadowDistance = alvo;
    }

    void RestaurarDistanciaDeSombra()
    {
        if (pipeline != null && !Mathf.Approximately(pipeline.shadowDistance, distanciaOriginal))
            pipeline.shadowDistance = distanciaOriginal;
        pipeline = null;
        interno = false;
    }

    void AplicarSombra(int indice, LightShadows sombra)
    {
        if (luzes[indice].shadows != sombra) luzes[indice].shadows = sombra;
    }

    void RestaurarSombras()
    {
        if (luzes == null) return;
        for (int i = 0; i < luzes.Length; i++)
            if (luzes[i] != null) AplicarSombra(i, sombrasOriginais[i]);
    }
}
