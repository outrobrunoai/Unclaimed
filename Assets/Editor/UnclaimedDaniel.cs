// Daniel's model (MPFB, exported by Blender/Daniel/scripts/daniel_exportar.py) and the Quaternius Universal Animation
// Library clips (CC0): both imported as Humanoid so Mecanim retargets the clips onto Daniel. Also renders review frames
// of each candidate clip (edit mode, preview scene) into Blender/Daniel/revisao/unity/.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class UnclaimedDaniel
{
    public const string Modelo = "Assets/Modelos/Daniel/Daniel.fbx";
    const string Texturas = "Assets/Modelos/Daniel/Texturas/";
    const string Materiais = "Assets/Modelos/Daniel/Materiais/";
    public static readonly string[] Bibliotecas = { "Assets/Animacoes/Quaternius/UAL1_Standard.fbx", "Assets/Animacoes/Quaternius/UAL2_Standard.fbx" };

    // FBX material name -> (base map, normal map, alpha-cut)
    static readonly Dictionary<string, (string cor, string normal, bool recorte)> Mapa = new Dictionary<string, (string, string, bool)>
    {
        { "Daniel.body", ("Aksel_Skin_diffuse.png", "Aksel_Skin_NRM.png", false) },
        { "Daniel.male_casualsuit05", ("male_casualsuit05_diffuse.png", "male_casualsuit05_normal.png", true) },
        { "Daniel.shoes03.001", ("shoes03_diffuse.png", null, false) },
        { "Daniel.eyebrow001", ("eyebrow001.png", null, true) },
        { "Daniel.eyelashes01", ("eyelashes01.png", null, true) },
        { "Daniel.high-poly", ("brown_eye.png", null, false) },
        { "Daniel.short02", ("short02_diffuse.png", null, true) },
    };

    [MenuItem("Tools/Unclaimed/Daniel/Configurar modelo e animações")]
    public static void Configurar()
    {
        foreach (string n in new[] { "Aksel_Skin_NRM.png", "male_casualsuit05_normal.png" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(Texturas + n);
            if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }

        Directory.CreateDirectory(Materiais);
        var mi = (ModelImporter)AssetImporter.GetAtPath(Modelo);
        mi.bakeAxisConversion = true;
        RecriarAvatar(mi);
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (var kv in Mapa)
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), Material(kv.Key, kv.Value));
        mi.SaveAndReimport();

        foreach (string b in Bibliotecas)
        {
            var ai = (ModelImporter)AssetImporter.GetAtPath(b);
            ai.bakeAxisConversion = true;      // Quaternius' Unity setup
            RecriarAvatar(ai);
            ai.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ai.importAnimation = true;
            ai.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = ai.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = NomeCurto(c.name);
                bool loop = c.name.EndsWith("_Loop");
                c.loopTime = loop;
                // In place: root rotation/height/XZ baked into the pose (the NavMeshAgent moves Daniel).
                c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = false;
                // Quaternius' setup: rotation based on body orientation, height on original, XZ on centre of mass.
                // (importer defaults for the root: body orientation / original height / centre of mass)
            }
            ai.clipAnimations = clips;
            ai.motionNodeName = RaizDoRig(b);
            ai.SaveAndReimport();
        }
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Modelo).OfType<Avatar>().FirstOrDefault();
        Debug.Log($"Unclaimed: Daniel configured. Avatar valid={(avatar != null && avatar.isValid)} human={(avatar != null && avatar.isHuman)}");
    }

    // Path of the rig's "root" bone (e.g. "Rig/root"), as the importer's motionNodeName expects.
    static string RaizDoRig(string asset)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            if (t.name == "root") return AnimationUtility.CalculateTransformPath(t, go.transform);
        return "";
    }

    // The importer keeps the avatar's stored skeleton across reimports (even after axis settings change): drop it so
    // "Create From This Model" builds the avatar again from the current bind pose.
    static void RecriarAvatar(ModelImporter mi)
    {
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.humanDescription = new HumanDescription();
        mi.SaveAndReimport();
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
    }

    // ------------------------------------------------------------------ animator + prefab

    const string Controlador = "Assets/Modelos/Daniel/Daniel.controller";
    const string Prefab = "Assets/Prefabs/Sistemas/Daniel (provisorio).prefab";

    // Locomotion by the agent's speed ("Velocidade", m/s; DanielPerseguidor: searching 2.2, investigating 3.2, chasing
    // 4.6). The clips are in place, so each one's playback rate is raised to roughly match its ground speed.
    static readonly (string clip, float limiar, float ritmo)[] Locomocao =
    {
        ("Idle_Loop", 0f, 1f), ("Walk_Formal_Loop", 2.2f, 1.35f), ("Walk_Loop", 3.2f, 1.6f), ("Jog_Fwd_Loop", 4.6f, 1.3f),
    };

    [MenuItem("Tools/Unclaimed/Daniel/Montar animator e prefab")]
    public static void MontarAnimatorEPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(Controlador) != null) AssetDatabase.DeleteAsset(Controlador);
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(Controlador);
        ctrl.AddParameter("Velocidade", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Confronto", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Agarrar", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var loco = sm.AddState("Locomocao");
        var arvore = new UnityEditor.Animations.BlendTree { name = "Locomocao", blendParameter = "Velocidade", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(arvore, ctrl);
        var filhos = new List<UnityEditor.Animations.ChildMotion>();
        foreach (var l in Locomocao) filhos.Add(new UnityEditor.Animations.ChildMotion { motion = Clipe(l.clip), threshold = l.limiar, timeScale = l.ritmo });
        arvore.children = filhos.ToArray();
        loco.motion = arvore;
        sm.defaultState = loco;

        // Standing in the archive doorway (Aparecer) with folded arms.
        var confronto = sm.AddState("Confronto"); confronto.motion = Clipe("Idle_FoldArms_Loop");
        var ida = loco.AddTransition(confronto); ida.hasExitTime = false; ida.duration = 0.3f;
        ida.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Confronto");
        var volta = confronto.AddTransition(loco); volta.hasExitTime = false; volta.duration = 0.25f;
        volta.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Confronto");

        // Capture: lunges with both arms out and stays there (the end screen takes over).
        var agarrar = sm.AddState("Agarrar"); agarrar.motion = Clipe("Push_Loop");
        var pega = sm.AddAnyStateTransition(agarrar); pega.hasExitTime = false; pega.duration = 0.12f; pega.canTransitionToSelf = false;
        pega.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Agarrar");
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var d = raiz.GetComponent<DanielPerseguidor>();
            Transform velho = raiz.transform.Find("Corpo");
            bool ativo = velho == null || velho.gameObject.activeSelf;
            if (velho != null) Object.DestroyImmediate(velho.gameObject);
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Modelo), raiz.transform);
            modelo.name = "Corpo";
            modelo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            modelo.SetActive(ativo);
            var anim = modelo.GetComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            d.corpo = modelo;
            d.animator = anim;
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        Debug.Log("Unclaimed: Daniel animator and prefab ready.");
    }

    static string NomeCurto(string n) { int i = n.LastIndexOf('|'); return i >= 0 ? n.Substring(i + 1) : n; }

    static Material Material(string nome, (string cor, string normal, bool recorte) d)
    {
        string path = Materiais + nome.Replace("Daniel.", "Daniel - ").Replace(".001", "") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Texturas + d.cor));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", nome == "Daniel.high-poly" ? 0.8f : 0.25f);
        if (d.normal != null)
        {
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Texturas + d.normal));
            m.EnableKeyword("_NORMALMAP");
        }
        m.SetFloat("_AlphaClip", d.recorte ? 1f : 0f);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Cull", d.recorte ? (float)CullMode.Off : (float)CullMode.Back);
        if (d.recorte) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(m);
        return m;
    }

    public static AnimationClip Clipe(string nome)
    {
        foreach (string b in Bibliotecas)
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(b).OfType<AnimationClip>())
                if (c.name == nome) return c;
        return null;
    }

    // ------------------------------------------------------------------ review frames

    // Renders `quadros` frames of each clip over its length, three-quarter view, to Blender/Daniel/revisao/unity/<clip>_NNN.png.
    // Play mode only (Animator poses a humanoid only there). One pose per real frame: skinned meshes are skinned once per
    // frame, so a pose is set on one editor update and read back from the camera's texture on the next.
    // Writes Blender/Daniel/revisao/unity/<clip>_NNN.png, then Temp/daniel_prev.txt with a summary.
    public static string RenderizarPrevias(string[] clipes, int quadros = 24, int largura = 400, int altura = 560, string modelo = Modelo,
                                           float camDist = 4.1f, float camAlvo = 0.9f, float camFov = 30f, string prefixo = "")
    {
        if (!Application.isPlaying) return "run in Play mode";
        string pasta = Path.GetFullPath("Blender/Daniel/revisao/unity");
        Directory.CreateDirectory(pasta);
        Vector3 o = new Vector3(0f, -500f, 0f);      // far below the map, which stays loaded
        var criados = new List<Object>();
        var rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

        var daniel = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelo)); criados.Add(daniel);
        daniel.transform.SetPositionAndRotation(o, Quaternion.identity);
        var anim = daniel.GetComponent<Animator>(); if (anim == null) anim = daniel.AddComponent<Animator>();
        anim.avatar = AssetDatabase.LoadAllAssetsAtPath(modelo).OfType<Avatar>().First();
        anim.runtimeAnimatorController = null; anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the imported default (CullUpdateTransforms) freezes it off-screen

        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); criados.Add(chao);
        chao.transform.localScale = Vector3.one * 3f; chao.transform.position = o;
        var mChao = new Material(Shader.Find("Universal Render Pipeline/Lit")); mChao.SetColor("_BaseColor", new Color(0.09f, 0.09f, 0.085f));
        chao.GetComponent<Renderer>().sharedMaterial = mChao;
        criados.Add(Luz("Chave", o + new Vector3(2f, 3f, 2f), new Color(1f, 0.93f, 0.82f), 18f));
        criados.Add(Luz("Recorte", o + new Vector3(-2f, 2.5f, -2f), new Color(0.75f, 0.85f, 1f), 10f));
        var cam = new GameObject("Camera").AddComponent<Camera>(); criados.Add(cam.gameObject);
        cam.targetTexture = rt; cam.fieldOfView = camFov; cam.farClipPlane = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.035f, 0.037f, 0.04f);
        cam.transform.position = o + new Vector3(camDist * 0.707f, camAlvo + 0.4f * camDist / 4.1f, camDist * 0.707f);
        cam.transform.LookAt(o + new Vector3(0f, camAlvo, 0f));

        var grafo = PlayableGraph.Create("PreviaDaniel");
        grafo.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var saida = AnimationPlayableOutput.Create(grafo, "Saida", anim);
        var tex = new Texture2D(largura, altura, TextureFormat.RGB24, false);
        var log = new System.Text.StringBuilder();

        // (clip, frame) jobs; -1 frame = nothing pending to read yet.
        var fila = new List<(string nome, int i)>();
        foreach (string n in clipes) for (int i = 0; i < quadros; i++) fila.Add((n, i));
        int k = 0; (string nome, int i) pendente = (null, -1);
        AnimationClipPlayable atual = default; string clipAtual = null;

        void Fim()
        {
            EditorApplication.update -= Passo;
            if (grafo.IsValid()) grafo.Destroy();
            foreach (Object c in criados) if (c != null) Object.Destroy(c);
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            File.WriteAllText("Temp/daniel_prev.txt", log.ToString());
        }
        void Passo()
        {
            try
            {
                if (!Application.isPlaying) { log.AppendLine("left Play"); Fim(); return; }
                if (pendente.nome != null)
                {
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, largura, altura), 0, 0); tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(pasta, prefixo + (modelo == Modelo ? "" : "ref_") + $"{pendente.nome}_{pendente.i:000}.png"), tex.EncodeToPNG());
                    pendente = (null, -1);
                }
                if (k >= fila.Count) { Fim(); return; }
                var job = fila[k++];
                AnimationClip clip = Clipe(job.nome);
                if (clip == null) { log.AppendLine("missing " + job.nome); return; }
                if (clipAtual != job.nome)
                {
                    if (atual.IsValid()) atual.Destroy();
                    atual = AnimationClipPlayable.Create(grafo, clip); atual.SetApplyFootIK(true);
                    saida.SetSourcePlayable(atual); clipAtual = job.nome;
                    log.AppendLine($"{job.nome} {clip.length:0.00}s");
                }
                daniel.transform.SetPositionAndRotation(o, Quaternion.identity);
                atual.SetTime(clip.length * job.i / quadros);
                grafo.Evaluate(0f);
                pendente = job;
            }
            catch (System.Exception e) { log.AppendLine(e.ToString()); Fim(); }
        }
        EditorApplication.update += Passo;
        return "started";
    }

    // Head/hips/feet heights after one evaluation of `clipe` at 30% (upright Daniel: head ~1.7 m, feet ~0.1 m).
    public static string Postura(string clipe, string modelo = Modelo)
    {
        const string ctrlPath = "Assets/Modelos/Daniel/_PosturaTemp.controller";
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.layers[0].stateMachine.AddState(clipe).motion = Clipe(clipe);
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelo));
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>();
            anim.avatar = AssetDatabase.LoadAllAssetsAtPath(modelo).OfType<Avatar>().First();
            anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;
            anim.Rebind();
            string Medir() => $"head={anim.GetBoneTransform(HumanBodyBones.Head).position.y:0.00} hips={anim.GetBoneTransform(HumanBodyBones.Hips).position.y:0.00} " +
                              $"footL={anim.GetBoneTransform(HumanBodyBones.LeftFoot).position.y:0.00} handL={anim.GetBoneTransform(HumanBodyBones.LeftHand).position.y:0.00} " +
                              $"fwd={anim.GetBoneTransform(HumanBodyBones.Hips).forward}";
            string antes = Medir();
            anim.Play(clipe, 0, 0.3f); anim.Update(0f);
            return $"rest[{antes}] anim[{Medir()}]";
        }
        finally { Object.DestroyImmediate(go); AssetDatabase.DeleteAsset(ctrlPath); }
    }

    // Point lights below the map (a directional light would also light the game scene).
    static GameObject Luz(string nome, Vector3 pos, Color cor, float intensidade)
    {
        var go = new GameObject(nome); go.transform.position = pos;
        var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = 12f; l.color = cor; l.intensity = intensidade;
        return go;
    }
}
