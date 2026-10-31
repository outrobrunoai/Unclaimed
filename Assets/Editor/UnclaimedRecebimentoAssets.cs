// Original Blender assets, isolated installation into the existing receiving room.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class UnclaimedRecebimentoAssets
{
    public const string Folder = "Assets/Modelos/Recebimento";
    const string Review = "Blender/Recebimento/revisao";
    const string Group = "Recebimento - equipamentos";
    static readonly string[] Models = { "MacaRecebimento", "EstacaoLavagem", "BancadaRecebimento", "CarroAuxiliar", "CorpoCoberto", "SuporteEPI", "LixeiraClinica", "KitBancada", "InstrumentoRecebimento" };
    static readonly string[] OldNames = { "Maca de recebimento", "Pia de lavagem de corpos", "Bancada com pia - recebimento", "Mesa auxiliar", "Corpo provisório - recebimento" };
    static readonly Vector3[] Positions = {
        new Vector3(8f,0,8.375f), new Vector3(3.90f,0,8.40f), new Vector3(6f,0,11.04f),
        new Vector3(12.18f,0,10.95f), new Vector3(8f,0,8.375f),
        new Vector3(7.85f,1.22f,11.365f), new Vector3(7.37f,0,11.10f), new Vector3(6.45f,.903f,11.04f)
    };
    static readonly float[] Yaws = {0,-90,0,-7,0,0,0,0};
    public static string PrefabPath(string name) => Folder + "/Prefabs/" + name + ".prefab";
    static void RequireScene()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/PrototipoNecroterio.unity")
            throw new InvalidOperationException("Open PrototipoNecroterio in Edit Mode.");
    }
    static Texture2D Tex(string file, bool normal=false, bool srgb=false)
    {
        if (!(AssetImporter.GetAtPath(file) is TextureImporter ti)) throw new FileNotFoundException(file);
        ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        ti.sRGBTexture=srgb;ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.anisoLevel=4;
        ti.textureCompression=TextureImporterCompression.CompressedHQ;
        ti.wrapMode=TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
    }
    [MenuItem("Tools/Unclaimed/Recebimento/Build Prefabs")]
    public static void Build()
        => BuildSelected(Models);
    public static void BuildSelected(params string[] names)
    {
        Directory.CreateDirectory(Folder+"/Materiais");Directory.CreateDirectory(Folder+"/Prefabs");
        AssetDatabase.Refresh();
        foreach(string name in names)
        {
            string path=Folder+"/"+name+".fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi))throw new FileNotFoundException(path);
            mi.materialImportMode=ModelImporterMaterialImportMode.None;
            mi.importCameras=false;mi.importLights=false;mi.importAnimation=false;
            mi.isReadable=false;mi.generateSecondaryUV=true;mi.SaveAndReimport();
            string mp=Folder+"/Materiais/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,mp);}
            string t=Folder+"/Texturas/"+name;
            m.SetColor("_BaseColor",Color.white);
            m.SetTexture("_BaseMap",Tex(t+"_BaseMap.png",srgb:true));
            m.SetTexture("_MetallicGlossMap",Tex(t+"_MetallicSmoothness.png"));
            m.SetTexture("_BumpMap",Tex(t+"_Normal.png",normal:true));
            m.SetTexture("_OcclusionMap",Tex(t+"_Occlusion.png"));
            m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",1);m.SetFloat("_BumpScale",.75f);
            m.SetFloat("_OcclusionStrength",.65f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(m);
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go=UnityEngine.Object.Instantiate(source);go.name=name;
            foreach(var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials=Enumerable.Repeat(m,r.sharedMaterials.Length).ToArray();
                r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            }
            if(name!="CorpoCoberto" && name!="SuporteEPI" && name!="KitBancada" && name!="InstrumentoRecebimento")
            {
                var b=new Bounds();bool first=true;
                foreach(var f in go.GetComponentsInChildren<MeshFilter>())
                {
                    var mb=f.sharedMesh.bounds;
                    foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
                    {var p=go.transform.InverseTransformPoint(f.transform.TransformPoint(mb.center+Vector3.Scale(mb.extents,new Vector3(x,y,z))));
                     if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
                }
                var c=go.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;
                if(name=="BancadaRecebimento"){c.center=new Vector3(0,.45f,0);c.size=new Vector3(2,.90f,.60f);}
            }
            PrefabUtility.SaveAsPrefabAsset(go,PrefabPath(name));UnityEngine.Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Unclaimed/Recebimento/Install Room Assets")]
    public static void Install()
    {
        RequireScene();
        InstallRoom(GameObject.Find("Unclaimed - Necroterio (planta)/Ambientes/Recebimento de corpos"));
    }
    public static void InstallGenerated(Transform room)
    {
        if(room!=null && Models.All(n=>AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(n))!=null))
            InstallRoom(room.gameObject);
    }
    static void InstallRoom(GameObject room)
    {
        foreach(string n in Models)if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(n))==null)throw new FileNotFoundException(PrefabPath(n));
        if(room==null)throw new InvalidOperationException("Receiving room not found.");
        var existing=room.transform.Find(Group);
        if(existing!=null)throw new InvalidOperationException("Already installed. Update prefabs instead.");
        int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Furnish receiving room");
        var root=new GameObject(Group);Undo.RegisterCreatedObjectUndo(root,"Receiving room");
        root.transform.SetParent(room.transform,false);
        for(int i=0;i<Positions.Length;i++)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Models[i])),root.transform);
            Undo.RegisterCreatedObjectUndo(go,"Place "+Models[i]);
            go.transform.SetPositionAndRotation(Positions[i],Quaternion.Euler(0,Yaws[i],0));
            if(i<OldNames.Length)go.name=OldNames[i];
        }
        DressRoom(root.transform);
        DressClue();
        // Hide only verified placeholder renderers/colliders; keep objects/references and components.
        foreach(var t in room.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
        {
            if(t.IsChildOf(root.transform)||!OldNames.Contains(t.name))continue;
            if(t.position.x<3.3f||t.position.x>12.7f||t.position.z<7f||t.position.z>11.5f)continue;
            foreach(var r in t.GetComponents<Renderer>()){Undo.RecordObject(r,"Hide placeholder");r.enabled=false;}
            foreach(var c in t.GetComponents<Collider>()){Undo.RecordObject(c,"Replace placeholder collision");c.enabled=false;}
        }
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(room.scene);
    }
    static void DressRoom(Transform parent)
    {
        string a="Assets/UnclaimedPrototype/Autopsia/";
        var tile=AssetDatabase.LoadAssetAtPath<Material>(a+"Azulejo.mat");
        var paint=AssetDatabase.LoadAssetAtPath<Material>(a+"Pintura.mat");
        if(tile==null||paint==null)throw new FileNotFoundException("Existing clinical finishes are missing.");
        AddHorizontalFinishes(parent);
        // Same ceramic/paint palette as autopsy; new faces point strictly into this room.
        void Wall(string id,Vector3 origin,Vector3 along,Vector3 normal,float width,float from=0)
        {
            if(from<2.1f)Panel(parent,id+" - azulejo",origin+Vector3.up*from,along,Vector3.up,width,2.1f-from,normal,tile);
            float low=Mathf.Max(2.1f,from);
            Panel(parent,id+" - pintura",origin+Vector3.up*low,along,Vector3.up,width,3.2f-low,normal,paint);
        }
        Wall("Oeste",new Vector3(3.352f,0,5.352f),Vector3.forward,Vector3.right,6.046f);
        Wall("Norte",new Vector3(3.352f,0,11.398f),Vector3.right,Vector3.back,9.296f);
        Wall("Leste sul",new Vector3(12.648f,0,5.352f),Vector3.forward,Vector3.left,2.023f);
        Wall("Leste norte",new Vector3(12.648f,0,9.375f),Vector3.forward,Vector3.left,2.023f);
        Wall("Sobre porta patio",new Vector3(12.648f,0,7.375f),Vector3.forward,Vector3.left,2,2.2f);
        foreach(var s in new[]{(3.352f,4.625f),(6.125f,9f),(10.5f,12.648f)})
            Wall("Sul "+s.Item1,new Vector3(s.Item1,0,5.352f),Vector3.right,Vector3.forward,s.Item2-s.Item1);
        foreach(float x in new[]{4.625f,9f})
            Wall("Sobre porta sul "+x,new Vector3(x,0,5.352f),Vector3.right,Vector3.forward,1.5f,2.2f);
        foreach(var t in EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
        {
            string materialName=t.name=="Piso - Recebimento de corpos"?"Piso porcelanato":
                t.name=="Laje - Recebimento de corpos"?"Forro mineral":null;
            if(materialName==null)continue;
            var r=t.GetComponent<Renderer>();if(r==null)continue;
            var material=AssetDatabase.LoadAssetAtPath<Material>(a+materialName+".mat");
            if(material!=null){Undo.RecordObject(r,"Clinical receiving finish");r.sharedMaterial=material;}
        }
        var trim=AssetDatabase.LoadAssetAtPath<Material>(a+"Metal pintado.mat");
        void Rail(string id,Vector3 p,Vector3 size)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=id;g.transform.SetParent(parent,false);
            g.transform.position=p;g.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial=trim;Undo.RegisterCreatedObjectUndo(g,"Receiving protection");
        }
        Rail("Protetor norte",new Vector3(9.47f,.79f,11.37f),new Vector3(2.8f,.13f,.035f));
        Rail("Protetor sul",new Vector3(7.54f,.79f,5.38f),new Vector3(2.55f,.13f,.035f));
        // Existing bathroom accessory family keeps institutional fittings consistent.
        foreach(var v in new[]{("Saboneteira",new Vector3(5.00f,1.32f,11.37f)),("Toalheiro de papel",new Vector3(6.46f,1.54f,11.37f))})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Modelos/WC/"+v.Item1+".prefab");
            if(prefab==null)continue;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.position=v.Item2;
            Undo.RegisterCreatedObjectUndo(go,"Receiving wall fitting");
        }
    }
    static void AddHorizontalFinishes(Transform parent)
    {
        string a="Assets/UnclaimedPrototype/Autopsia/";
        if(parent.Find("Piso acabado")==null)
            Panel(parent,"Piso acabado",new Vector3(3.35f,.003f,5.35f),Vector3.right,Vector3.forward,9.30f,6.05f,Vector3.up,
                AssetDatabase.LoadAssetAtPath<Material>(a+"Piso porcelanato.mat"));
        if(parent.Find("Forro acabado")==null)
            Panel(parent,"Forro acabado",new Vector3(3.35f,3.197f,5.35f),Vector3.right,Vector3.forward,9.30f,6.05f,Vector3.down,
                AssetDatabase.LoadAssetAtPath<Material>(a+"Forro mineral.mat"));
    }
    static void DressClue()
    {
        var clue=GameObject.Find("Pista - instrumento deslocado");
        if(clue==null||clue.transform.Find("Instrumental modelado")!=null)return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("InstrumentoRecebimento"));
        if(prefab==null)throw new FileNotFoundException("Build InstrumentoRecebimento first.");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,clue.transform);go.name="Instrumental modelado";
        go.transform.SetPositionAndRotation(new Vector3(6,.903f,11.05f),Quaternion.Euler(0,13,0));
        Vector3 s=clue.transform.lossyScale;go.transform.localScale=new Vector3(1/s.x,1/s.y,1/s.z);
        var r=clue.GetComponent<Renderer>();Undo.RecordObject(r,"Dress receiving clue");r.enabled=false;
        Undo.RegisterCreatedObjectUndo(go,"Receiving instrument visual");
    }
    public static void RefineInstalled()
    {
        RequireScene();
        var root=GameObject.Find("Unclaimed - Necroterio (planta)/Ambientes/Recebimento de corpos/"+Group).transform;
        var wash=root.Find("Pia de lavagem de corpos");Undo.RecordObject(wash,"Orient wash station");wash.rotation=Quaternion.Euler(0,-90,0);
        AddHorizontalFinishes(root);DressClue();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }
    static void Panel(Transform parent,string id,Vector3 o,Vector3 u,Vector3 v,float w,float h,Vector3 normal,Material mat)
    {
        Vector3[] vs={o,o+u*w,o+u*w+v*h,o+v*h};
        int[] tri=Vector3.Dot(Vector3.Cross(u,v),normal)>0?new[]{0,1,2,0,2,3}:new[]{0,2,1,0,3,2};
        var mesh=new Mesh{name=id,vertices=vs,triangles=tri,
            normals=new[]{normal,normal,normal,normal},
            uv=vs.Select(p=>new Vector2(Vector3.Dot(p,u),Vector3.Dot(p,v))).ToArray()};
        mesh.RecalculateTangents();mesh.RecalculateBounds();
        Directory.CreateDirectory(Folder+"/Revestimentos");
        string path=Folder+"/Revestimentos/"+id+".asset";
        var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(stored==null){AssetDatabase.CreateAsset(mesh,path);stored=mesh;}
        else{EditorUtility.CopySerialized(mesh,stored);UnityEngine.Object.DestroyImmediate(mesh);}
        var go=new GameObject(id);go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=stored;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
        Undo.RegisterCreatedObjectUndo(go,"Receiving finish");
    }
    static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
    public static object Audit()
    {
        RequireScene();
        var ts=EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var data=new {
            lights=ts.SelectMany(t=>t.GetComponents<Light>()).Select(l=>new {l.name,pos=V(l.transform.position),l.intensity,l.range}).ToArray(),
            lightmaps=LightmapSettings.lightmaps.Length,
            objects=ts.Where(t=>OldNames.Contains(t.name)||t.name==Group).Select(t=>new {
                t.name, parent=t.parent==null?"":t.parent.name,pos=V(t.position),scale=V(t.localScale),
                components=t.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name).ToArray(),
                renderers=t.GetComponentsInChildren<Renderer>().Select(r=>new {r.name,r.enabled,center=V(r.bounds.center),size=V(r.bounds.size)}).ToArray()
            }).ToArray()
        };
        Directory.CreateDirectory(Review);
        File.WriteAllText(Review+"/audit-"+(GameObject.Find("Unclaimed - Necroterio (planta)/Ambientes/Recebimento de corpos/"+Group)==null?"antes":"depois")+".json",
            Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));
        return data;
    }
    public static object Before()=>Capture("antes");
    public static object After()=>Capture("depois");
    static object Capture(string phase)
    {
        RequireScene();Directory.CreateDirectory(Review+"/"+phase);
        var source=GameObject.Find("Player/Camera").GetComponent<Camera>();
        var go=new GameObject("Temporary receiving review camera");var c=go.AddComponent<Camera>();
        c.CopyFrom(source);c.enabled=false;c.useOcclusionCulling=false;
        var ex=c.GetUniversalAdditionalCameraData();var se=source.GetUniversalAdditionalCameraData();
        ex.renderPostProcessing=se.renderPostProcessing;ex.antialiasing=se.antialiasing;ex.renderShadows=se.renderShadows;
        var views=new[]{
            ("entrada",new Vector3(10.9f,1.65f,6.1f),new Vector3(6.3f,1.0f,9.3f)),
            ("patio",new Vector3(12.1f,1.65f,8.35f),new Vector3(6.5f,1.0f,8.5f)),
            ("lavagem",new Vector3(6.0f,1.65f,7.0f),new Vector3(3.90f,.9f,8.45f)),
            ("bancada",new Vector3(7.4f,1.65f,9.3f),new Vector3(6.3f,1.0f,11.1f)),
            ("maca",new Vector3(9.7f,1.55f,6.9f),new Vector3(8,.65f,8.375f))
        };
        var rt=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32);var prev=RenderTexture.active;
        try{
            c.targetTexture=rt;c.aspect=4f/3;c.fieldOfView=60;
            foreach(var v in views){
                c.transform.SetPositionAndRotation(v.Item2,Quaternion.LookRotation(v.Item3-v.Item2));
                c.Render();c.Render();RenderTexture.active=rt;
                var im=new Texture2D(1440,1080,TextureFormat.RGB24,false);
                im.ReadPixels(new Rect(0,0,1440,1080),0,0);im.Apply();
                File.WriteAllBytes(Review+"/"+phase+"/"+v.Item1+".png",im.EncodeToPNG());UnityEngine.Object.DestroyImmediate(im);
            }
        }finally{RenderTexture.active=prev;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        return new{phase,images=views.Length};
    }
}
