// Blender-authored exterior fittings. Build reusable assets and patch only their visual instances.
// Existing lights, colliders, gameplay components and baked structural meshes are never rebuilt here.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class UnclaimedExteriorFinishes
{
    public const string Folder = "Assets/Modelos/Entorno/Acabamentos";
    const string ScenePath = "Assets/Scenes/PrototipoNecroterio.unity";
    const string Marker = "Acabamentos externos v1";
    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";
    public static bool Available(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)) != null;

    [MenuItem("Tools/Unclaimed/Build Exterior Finishes Prefabs")]
    public static void Build() => BuildAssets(null);

    // Approved direction A: update only the main sign, retaining its prefab/texture GUIDs.
    [MenuItem("Tools/Unclaimed/Build Main Sign Only")]
    public static void BuildMainSign() => BuildAssets("Placa_Fachada");

    // Signage review: signs and the intercom only; their instances update through the prefabs.
    [MenuItem("Tools/Unclaimed/Build Exterior Signs Only")]
    public static void BuildSigns() => BuildAssets("Placa_Fachada","Placa_Atendimento","Placa_Numero","Placa_NaoEstacione","Placa_Guarita","Interfone_Externo");

    // Materials owned by one sign family; a selective build rebuilds these and reuses every other shared material.
    static readonly HashSet<string> SignMaterials=new HashSet<string>{"Chapa_Letreiro","PVC_Fundo","Acrilico_Borda","Inox"};

    static void BuildAssets(params string[] only)
    {
        if(only!=null && only.Length==0)only=null;
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before building assets.");
        Ensure(Folder,"Materiais");Ensure(Folder,"Prefabs");Ensure(Folder,"Malhas");
        var manifest=JObject.Parse(File.ReadAllText(Folder+"/assets.json"));
        var materials=new Dictionary<string,Material>();
        var colors=(JObject)manifest["materials"];
        foreach (var entry in ((JObject)manifest["assets"]).Properties())
        {
            if(only!=null && !only.Contains(entry.Name))continue;
            string name=entry.Name, path=Folder+"/"+name+".fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null)throw new InvalidOperationException("FBX not imported: "+path);
            bool changed=importer.importAnimation || importer.importCameras || importer.importLights || !importer.bakeAxisConversion ||
                importer.globalScale!=1 || importer.generateSecondaryUV || importer.isReadable || importer.meshCompression!=ModelImporterMeshCompression.Off ||
                importer.materialImportMode!=ModelImporterMaterialImportMode.ImportStandard || importer.materialLocation!=ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.bakeAxisConversion=true;
            importer.globalScale=1;importer.generateSecondaryUV=false;importer.isReadable=false;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
            importer.importNormals=ModelImporterNormals.Import;
            if(changed)importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root=UnityEngine.Object.Instantiate(model);root.name=name;
            try {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats=r.sharedMaterials;
                    for(int i=0;i<mats.Length;i++){
                        string slot=mats[i].name;
                        if(!materials.ContainsKey(slot)){
                            // Shared screw materials already exist; leave unrelated families untouched.
                            var existing=only!=null && !slot.StartsWith("Print_") && !slot.StartsWith("Face_") && !SignMaterials.Contains(slot)
                                ? AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materiais/"+slot+".mat") : null;
                            materials[slot]=existing!=null?existing:MaterialFor(slot,colors);
                        }
                        mats[i]=materials[slot];
                    }
                    r.sharedMaterials=mats;r.lightProbeUsage=LightProbeUsage.BlendProbes;
                    r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
                    r.shadowCastingMode=name.StartsWith("Placa_") || name=="Interfone_Externo" || name.StartsWith("Cadeado_") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    if(r is MeshRenderer mr)mr.receiveGI=ReceiveGI.LightProbes;
                }
                // No detailed colliders: the approved wall/planter/gate colliders remain authoritative.
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath(name));
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        if(only!=null){
            AssetDatabase.SaveAssets();
            Debug.Log("Unclaimed: rebuilt selectively ("+string.Join(", ",only)+"); instances update through their prefabs. No exterior regeneration.");
            return;
        }
        // Shared mesh, restrained finish variation for the second installed unit.
        var basePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Condensadora_Split"));
        var variant=(GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        try {
            var original=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materiais/Chapa_Clara.mat");
            string agedPath=Folder+"/Materiais/Chapa_Clara_Usada.mat";
            var aged=AssetDatabase.LoadAssetAtPath<Material>(agedPath);
            if(aged==null){aged=new Material(original);AssetDatabase.CreateAsset(aged,agedPath);}
            aged.SetColor("_BaseColor",new Color(.65f,.66f,.61f));aged.SetFloat("_Smoothness",.20f);EditorUtility.SetDirty(aged);
            foreach(var r in variant.GetComponentsInChildren<Renderer>(true)){
                var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)if(mats[i]==original)mats[i]=aged;r.sharedMaterials=mats;
            }
            PrefabUtility.SaveAsPrefabAsset(variant,PrefabPath("Condensadora_Split_Usada"));
        }finally{UnityEngine.Object.DestroyImmediate(variant);}
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: 13 Blender exterior finish prefabs built; existing asset GUIDs retained.");
    }

    static Material MaterialFor(string name,JObject colors)
    {
        string path=Folder+"/Materiais/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        var spec=colors[name] as JArray;
        Color color=Color.white;float metal=0,smooth=.2f;
        if(spec!=null){var c=(JArray)spec[0];color=new Color((float)c[0],(float)c[1],(float)c[2]);metal=(float)spec[1];smooth=(float)spec[2];}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
        if(name=="Print_Placa_Fachada" || name=="Chapa_Letreiro"){
            m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",.16f);
            m.DisableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        m.SetFloat("_Surface",0);m.SetFloat("_AlphaClip",0);m.SetFloat("_Cull",2);
        string texture=name.StartsWith("Print_")?name.Substring(6):name=="Folha_Espada"?"Espada_Folha":name=="Folha_Filodendro"?"Filodendro_Folha":name=="Face_Interfone"?"Interfone_Identificacao":null;
        if(texture!=null){
            string tp=Folder+"/Texturas/"+texture+".png";
            var ti=AssetImporter.GetAtPath(tp) as TextureImporter;
            if(ti!=null){
                bool change=ti.wrapMode!=TextureWrapMode.Clamp || ti.maxTextureSize<4096 || ti.textureCompression!=TextureImporterCompression.Uncompressed;
                ti.wrapMode=TextureWrapMode.Clamp;ti.maxTextureSize=4096;ti.textureCompression=TextureImporterCompression.Uncompressed;
                ti.alphaSource=TextureImporterAlphaSource.None;ti.mipMapsPreserveCoverage=false;ti.anisoLevel=4;
                if(change)ti.SaveAndReimport();
            }
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            // Optional weathering maps from Blender/Entorno/Acabamentos/scripts (placas_comum.py):
            // <texture>_Mascara = metallic (R) / smoothness (A), <texture>_Normal = tangent-space normal.
            var mask=ImportMap(Folder+"/Texturas/"+texture+"_Mascara.png",false);
            m.SetTexture("_MetallicGlossMap",mask);m.SetFloat("_SmoothnessTextureChannel",0);
            if(mask!=null){m.EnableKeyword("_METALLICSPECGLOSSMAP");m.SetFloat("_Smoothness",1);}else m.DisableKeyword("_METALLICSPECGLOSSMAP");
            var normal=ImportMap(Folder+"/Texturas/"+texture+"_Normal.png",true);
            m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",1);
            if(normal!=null)m.EnableKeyword("_NORMALMAP");else m.DisableKeyword("_NORMALMAP");
        }
        bool foliage=name.StartsWith("Folha_") || name=="Caule";
        m.SetFloat("_SpecularHighlights",foliage?0:1);
        if(foliage)m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");else m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.DisableKeyword("_ALPHATEST_ON");m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if(foliage)Wind(m,name);
        EditorUtility.SetDirty(m);return m;
    }

    static Texture2D ImportMap(string path,bool normal)
    {
        var ti=AssetImporter.GetAtPath(path) as TextureImporter;
        if(ti==null)return null;
        var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        if(ti.textureType!=type || ti.sRGBTexture || ti.wrapMode!=TextureWrapMode.Clamp || ti.textureCompression!=TextureImporterCompression.Uncompressed || ti.maxTextureSize<4096){
            ti.textureType=type;ti.sRGBTexture=false;ti.wrapMode=TextureWrapMode.Clamp;ti.maxTextureSize=4096;
            ti.textureCompression=TextureImporterCompression.Uncompressed;ti.alphaSource=normal?TextureImporterAlphaSource.None:TextureImporterAlphaSource.FromInput;
            ti.anisoLevel=4;ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Planter ornamentals sway with the shared vegetation wind (Unclaimed/Vegetacao, height-based weights: these meshes
    // carry no wind vertex colours). Stiffness per species: sansevieria leaves are rigid and barely nod; philodendron
    // leaves on long petioles move more. Amplitudes in metres per unit of wind, reference height = plant height.
    static void Wind(Material m,string name)
    {
        var shader=Shader.Find("Unclaimed/Vegetacao");
        if(shader==null)return;
        m.shader=shader;
        bool sword=name=="Folha_Espada";
        m.SetFloat("_AlphaClip",0);m.SetFloat("_Cull",2);m.DisableKeyword("_ALPHATEST_ON");
        m.SetFloat("_UsarNormal",0);m.DisableKeyword("_NORMALMAP");m.SetFloat("_Translucidez",0.15f);
        m.SetFloat("_SpecularHighlights",0);m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections",0);m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.SetFloat("_VentoSemCor",1);
        m.SetFloat("_VentoAltura",sword?0.85f:0.7f);
        m.SetFloat("_VentoTronco",sword?0.008f:0.02f);
        m.SetFloat("_VentoRamos",sword?0.004f:0.03f);
        m.SetFloat("_VentoFolhas",sword?0.0015f:0.01f);
        m.SetFloat("_VentoFolhaSemCor",name=="Caule"?0.2f:1f);
        m.SetFloat("_VentoFrequencia",sword?0.5f:0.6f);
        m.enableInstancing=true;
        m.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    public static GameObject Place(string asset,Transform parent,Vector3 pos,Vector3 normal,string name=null,float scale=1)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(asset));
        if(prefab==null)throw new InvalidOperationException("Missing exterior finish prefab "+asset);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
        Undo.RegisterCreatedObjectUndo(go,"Place exterior finish");
        go.name=name??asset;go.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(normal));
        go.transform.localScale=Vector3.one*scale;return go;
    }

    [MenuItem("Tools/Unclaimed/Apply Exterior Finishes Only")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!=ScenePath)throw new InvalidOperationException("Open PrototipoNecroterio in Edit Mode.");
        var top=scene.GetRootGameObjects().First(g=>g.name=="Unclaimed - Necroterio (planta)").transform;
        var outside=top.Find("Area externa");
        if(outside.Find(Marker)!=null)throw new InvalidOperationException("Exterior finishes already applied; rebuild the individual prefab to revise.");
        // Complete preflight before touching any scene object.
        foreach(string asset in new[]{"Condutor_Pluvial_DN100","Agua_Jardim_DN25","Interfone_Externo","Condensadora_Split","Cadeado_Chave_Corrente","Placa_Fachada","Placa_Atendimento","Placa_Numero","Placa_NaoEstacione","Placa_Guarita","Espada_Ornamental_A","Espada_Ornamental_B","Filodendro_Ornamental"})
            if(!Available(asset))throw new InvalidOperationException("Build prefabs first: "+asset);
        var facade=outside.Find("Fachada");var fences=outside.Find("Fechamentos");var drainage=outside.Find("Drenagem");
        var install=facade.Find("Instalacoes da fachada").GetComponent<MeshFilter>();
        var gates=fences.Find("Gradis e portoes").GetComponent<MeshFilter>();
        if(install.sharedMesh==null || gates.sharedMesh==null)throw new InvalidOperationException("Original merged meshes missing.");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Apply exterior visual finishes");
        // Remove triangles only when all three vertices are inside one of these old component envelopes.
        var boxes=new[]{
            Box(new Vector3(-9.7f,1.3f,-12.505f),new Vector3(.125f,.205f,.065f)),
            new Bounds(new Vector3(3.81f,1.19f,-9.2f),new Vector3(.46f,2.4f,.86f)),
            new Bounds(new Vector3(3.81f,1.19f,-7.9f),new Vector3(.46f,2.4f,.86f)),
            Box(new Vector3(3.72f,.62f,-10f),new Vector3(.42f,1.35f,.82f))
        };
        Trim(install,boxes,"Instalacoes_fachada_preservadas");
        var lockPoints=new[]{new Vector3(-11.4f,1.05f,-19.03f),new Vector3(-16.6f,1.1f,-8.04f),new Vector3(6.97f,1.05f,-14.62f),new Vector3(18f,1.1f,5.21f)};
        Trim(gates,lockPoints.Select(p=>Box(p+Vector3.down*.025f,new Vector3(.19f,.17f,.19f))).ToArray(),"Gradis_portoes_sem_cadeados_placeholder");
        var oldPipe=drainage.Find("Condutores pluviais");Undo.DestroyObjectImmediate(oldPipe.gameObject);
        var pipeGroup=new GameObject("Condutores pluviais");Undo.RegisterCreatedObjectUndo(pipeGroup,"Replace downpipes");pipeGroup.transform.SetParent(drainage,false);
        var positions=new[]{new Vector3(-14.6f,0,-12.475f),new Vector3(-6.75f,0,-12.475f),new Vector3(3.3f,0,-10.35f),new Vector3(3.6f,0,-6),new Vector3(-14.85f,0,-4.2f),new Vector3(-13.1f,0,.5f),new Vector3(0,0,9.1f),new Vector3(14.6f,0,-3.6f)};
        var normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.right,Vector3.left,Vector3.left,Vector3.forward,Vector3.back};
        for(int i=0;i<positions.Length;i++)Place("Condutor_Pluvial_DN100",pipeGroup.transform,positions[i],normals[i],"Condutor DN100 "+(i+1));
        Place("Interfone_Externo",facade,new Vector3(-9.7f,1.3f,-12.475f),Vector3.back,"Interfone externo");
        for(int i=0;i<2;i++)Place(i==0?"Condensadora_Split":"Condensadora_Split_Usada",facade,new Vector3(3.6f,1.8f,i==0?-9.2f:-7.9f),Vector3.right,"Condensadora split "+(i+1));
        Place("Agua_Jardim_DN25",facade,new Vector3(3.6f,0,-10f),Vector3.right,"Agua - torneira e mangueira");
        for(int i=0;i<lockPoints.Length;i++)Place("Cadeado_Chave_Corrente",fences,lockPoints[i]+(i==1||i==3?Vector3.forward*.027f:Vector3.zero),i==2?Vector3.left:Vector3.back,"Cadeado de chave - portao "+(i+1));
        PatchSign(facade,"Placa - identificacao","Placa_Fachada",new Vector3(-11.375f,3.08f,-12.475f),Vector3.back);
        PatchSign(facade,"Placa - atendimento","Placa_Atendimento",new Vector3(-9.95f,1.6f,-12.475f),Vector3.back);
        PatchSign(facade,"Placa - numero","Placa_Numero",new Vector3(-12.55f,1.55f,-19.15f),Vector3.back);
        PatchSign(facade,"Placa - nao estacione","Placa_NaoEstacione",new Vector3(20.1f,.33f,-19.1f),Vector3.back);
        PatchSign(outside.Find("Patio de manobra"),"Placa - portaria","Placa_Guarita",new Vector3(13.4f,2.3f,-17.6f),Vector3.right);
        int swords=0,ornamentals=0;
        var shrubs=outside.Find("Vegetacao/Arbustos e bananeiras");
        foreach(var t in shrubs.Cast<Transform>().ToArray()){
            bool sword=t.name=="Espada_Sao_Jorge";
            bool broad=t.name.StartsWith("Arbusto_Redondo") && t.position.z>-11.9f && t.position.z< -11.3f && t.position.x> -6.4f && t.position.x<3.6f;
            if(!sword && !broad)continue;
            var go=Place(sword?(swords++%2==0?"Espada_Ornamental_A":"Espada_Ornamental_B"):"Filodendro_Ornamental",shrubs,t.position,t.forward,sword?"Espada-de-sao-jorge ornamental":"Filodendro ornamental");
            go.transform.rotation=t.rotation;go.transform.localScale=t.localScale;
            if(broad)ornamentals++;
            Undo.DestroyObjectImmediate(t.gameObject);
        }
        var marker=new GameObject(Marker);Undo.RegisterCreatedObjectUndo(marker,"Finish marker");marker.transform.SetParent(outside,false);
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Unclaimed: exterior finishes applied locally: 8 downpipes, water kit, intercom, 2 condensers, 5 signs, 4 key padlocks, "+swords+" swords and "+ornamentals+" broad-leaf plants. Lights and colliders unchanged.");
    }

    public static void RefineInstalled()
    {
        if(Application.isPlaying || SceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Edit Mode only.");
        var outside=GameObject.Find("Unclaimed - Necroterio (planta)/Area externa").transform;
        var fences=outside.Find("Fechamentos");
        foreach(var name in new[]{"Cadeado de chave - portao 2","Cadeado de chave - portao 4"}){
            var t=fences.Find(name);Undo.RecordObject(t,"Seat gate clasp");
            var p=t.position;p.z=name.EndsWith("2")?-8.013f:5.237f;t.position=p;
        }
        var old=outside.Find("Fachada/Condensadora split 2");
        if(old!=null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject)!=PrefabPath("Condensadora_Split_Usada")){
            Place("Condensadora_Split_Usada",old.parent,old.position,old.forward,old.name);Undo.DestroyObjectImmediate(old.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static Bounds Box(Vector3 center,Vector3 size) => new Bounds(center,size);
    static void PatchSign(Transform parent,string oldName,string asset,Vector3 position,Vector3 normal)
    {
        var wrapper=parent.Find(oldName);
        if(wrapper==null)throw new InvalidOperationException("Missing sign "+oldName);
        // Retain the wrapper and its identity, removing only its old printed geometry.
        foreach(var t in wrapper.Cast<Transform>().ToArray())Undo.DestroyObjectImmediate(t.gameObject);
        Place(asset,wrapper,position,normal,"Placa");
    }

    static void Trim(MeshFilter filter,Bounds[] exclusions,string name)
    {
        var original=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);mesh.name=name;
        var v=mesh.vertices;int removed=0;
        for(int sub=0;sub<mesh.subMeshCount;sub++){
            var keep=new List<int>();var tris=mesh.GetTriangles(sub);
            for(int i=0;i<tris.Length;i+=3){
                var a=filter.transform.TransformPoint(v[tris[i]]);var b=filter.transform.TransformPoint(v[tris[i+1]]);var c=filter.transform.TransformPoint(v[tris[i+2]]);
                if(exclusions.Any(box=>box.Contains(a)&&box.Contains(b)&&box.Contains(c))){removed++;continue;}
                keep.Add(tris[i]);keep.Add(tris[i+1]);keep.Add(tris[i+2]);
            }
            mesh.SetTriangles(keep,sub,false);
        }
        mesh.RecalculateBounds();
        string path=Folder+"/Malhas/"+name+".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
        else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}
        Undo.RecordObject(filter,"Replace selected placeholder triangles");filter.sharedMesh=saved;
        Debug.Log("Unclaimed: "+name+" removed "+removed+" placeholder triangles; unrelated triangles preserved.");
    }

    static void Ensure(string parent,string name){if(!AssetDatabase.IsValidFolder(parent+"/"+name))AssetDatabase.CreateFolder(parent,name);}
}
