using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class UnclaimedPatioRecebimentoAssets
{
    public const string Folder="Assets/Modelos/PatioRecebimento";
    public const string Group="Patio do recebimento";
    static readonly string[] Models={"ContentorMetalico","PaletesMadeira","ArmarioServico","PaleteiraManual","CoberturaRecebimento","RaloPatio"};
    static readonly Vector3[] Positions={new Vector3(19.975f,0,14.40f),new Vector3(17.7f,0,14.8f),new Vector3(20.55f,0,10.6f),new Vector3(16.1f,0,14.30f),new Vector3(12.85f,0,8.375f),new Vector3(13.1f,.001f,6.835f)};
    static readonly float[] Yaws={0,0,90,35,-90,0};
    // Service door between the reception area and the yard: the leaf is modelled once, DoorPrefab pairs it up.
    public const string LeafModel="PortaServicoFolha",DoorPrefab="PortaServicoDupla",DoorGroup="Porta dupla recebimento - patio";
    const float LeafWidth=.958f,LeafHinge=.962f,LeafBottom=.02f,LeafTop=2.165f,DoorOpenAngle=95f;
    static readonly Vector3 DoorPosition=new Vector3(12.85f,0,8.375f);
    static readonly string[] Old={"Cacamba","Paletes","Armarios de servico"};
    public static string PrefabPath(string name)=>Folder+"/Prefabs/"+name+".prefab";
    static Texture2D Tex(string path,bool normal=false,bool srgb=false)
    {
        if(!(AssetImporter.GetAtPath(path) is TextureImporter ti))throw new FileNotFoundException(path);
        ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=srgb;ti.mipmapEnabled=true;
        ti.maxTextureSize=2048;ti.anisoLevel=4;ti.wrapMode=TextureWrapMode.Clamp;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    [MenuItem("Tools/Unclaimed/Patio do recebimento/Build Prefabs")]
    public static void Build(){BuildSelected(Models);BuildSelected(LeafModel);}
    public static void BuildSelected(params string[] names)
    {
        Directory.CreateDirectory(Folder+"/Materiais");Directory.CreateDirectory(Folder+"/Prefabs");AssetDatabase.Refresh();
        foreach(string name in names){
            string fbx=Folder+"/"+name+".fbx";
            if(!(AssetImporter.GetAtPath(fbx) is ModelImporter mi))throw new FileNotFoundException(fbx);
            mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.importCameras=false;mi.importLights=false;mi.importAnimation=false;mi.importBlendShapes=false;
            mi.importNormals=ModelImporterNormals.Import;mi.importTangents=ModelImporterTangents.CalculateMikk;mi.generateSecondaryUV=true;mi.SaveAndReimport();
            string mp=Folder+"/Materiais/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,mp);}
            string t=Folder+"/Texturas/"+name;m.SetColor("_BaseColor",Color.white);
            m.SetTexture("_BaseMap",Tex(t+"_BaseMap.png",srgb:true));m.SetTexture("_MetallicGlossMap",Tex(t+"_MetallicSmoothness.png"));
            m.SetTexture("_BumpMap",Tex(t+"_Normal.png",normal:true));m.SetTexture("_OcclusionMap",Tex(t+"_Occlusion.png"));
            m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",1);m.SetFloat("_BumpScale",.8f);m.SetFloat("_OcclusionStrength",.7f);
            m.EnableKeyword("_NORMALMAP");m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");EditorUtility.SetDirty(m);
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));go.name=name;
            foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterials=Enumerable.Repeat(m,r.sharedMaterials.Length).ToArray();r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;}
            if(name==LeafModel){BuildDoor(go,m);UnityEngine.Object.DestroyImmediate(go);continue;}
            if(name!="CoberturaRecebimento"&&name!="RaloPatio"){
                var b=new Bounds();bool first=true;
                foreach(var f in go.GetComponentsInChildren<MeshFilter>()){
                    if(name=="PaleteiraManual"){
                        // Only the forks and the pump housing block; the drawbar is too thin to be a wall.
                        foreach(var v in f.sharedMesh.vertices){Vector3 q=go.transform.InverseTransformPoint(f.transform.TransformPoint(v));if(q.y>.30f)continue;if(first){b=new Bounds(q,Vector3.zero);first=false;}else b.Encapsulate(q);}
                        continue;
                    }
                    var mb=f.sharedMesh.bounds;foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1}){
                        Vector3 p=go.transform.InverseTransformPoint(f.transform.TransformPoint(mb.center+Vector3.Scale(mb.extents,new Vector3(x,y,z))));
                        if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
                    }
                }var c=go.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;
            }
            PrefabUtility.SaveAsPrefabAsset(go,PrefabPath(name));UnityEngine.Object.DestroyImmediate(go);
        }AssetDatabase.SaveAssets();
    }
    // Two leaves on pivots at the jambs (the right one mirrored in a child so its collider never has a negative
    // scale), opening towards the yard (-Z) like the entrance door; FolhaDePorta holds the mechanic.
    static void BuildDoor(GameObject leafModel,Material material)
    {
        var root=new GameObject(DoorPrefab);
        try{
            foreach(var (label,side) in new[]{("Folha esquerda",1f),("Folha direita",-1f)}){
                var leaf=new GameObject(label).transform;leaf.SetParent(root.transform,false);leaf.localPosition=new Vector3(-side*LeafHinge,0,0);
                var box=leaf.gameObject.AddComponent<BoxCollider>();
                box.center=new Vector3(side*LeafWidth/2,(LeafBottom+LeafTop)/2,0);box.size=new Vector3(LeafWidth,LeafTop-LeafBottom,.05f);
                var body=leaf.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                var audio=leaf.gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1f;audio.minDistance=1f;audio.maxDistance=15f;
                var door=leaf.gameObject.AddComponent<FolhaDePorta>();
                door.sentido=side>0?1:-1;door.anguloMaximo=DoorOpenAngle;door.duracao=1.1f;door.alcance=2f;
                door.iniciarAberta=true;door.aberturaInicial=1f;door.fonte=audio;door.textoAbrir="Abrir porta";door.textoFechar="Fechar porta";
                Transform geometry=leaf;
                if(side<0){geometry=new GameObject("Espelhada").transform;geometry.SetParent(leaf,false);geometry.localScale=new Vector3(-1,1,1);}
                var visual=UnityEngine.Object.Instantiate(leafModel,geometry);visual.name="Folha";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
            }
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath(DoorPrefab));
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    [MenuItem("Tools/Unclaimed/Patio do recebimento/Place Service Door")]
    public static void PlaceDoor()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(DoorPrefab));
        if(prefab==null)throw new FileNotFoundException(DoorPrefab);
        Transform group=null;
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==DoorGroup&&t.gameObject.scene.IsValid()){group=t;break;}
        if(group==null)throw new InvalidOperationException("Door group not found: "+DoorGroup);
        PlaceDoorIn(group);
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name=="PortasServicoAbertas"&&t.gameObject.scene.IsValid())Undo.DestroyObjectImmediate(t.gameObject);
    }
    // Used by the generator and by the menu: swaps any previous leaves for the current prefab.
    public static void PlaceDoorIn(Transform group)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(DoorPrefab));
        if(group==null||prefab==null)return;
        for(int i=group.childCount-1;i>=0;i--){var c=group.GetChild(i).gameObject;var src=PrefabUtility.GetCorrespondingObjectFromSource(c);if(c.name==DoorPrefab||(src!=null&&src.name==DoorPrefab))Undo.DestroyObjectImmediate(c);}
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group);
        go.transform.SetPositionAndRotation(DoorPosition,Quaternion.Euler(0,-90,0));Undo.RegisterCreatedObjectUndo(go,"Place service door");
        foreach(var l in go.GetComponentsInChildren<FolhaDePorta>())l.AplicarEstadoInicial();
        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
    }
    [MenuItem("Tools/Unclaimed/Patio do recebimento/Install Assets")]
    public static void Install()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!="Assets/Scenes/PrototipoNecroterio.unity")throw new InvalidOperationException("Open PrototipoNecroterio in Edit Mode.");
        InstallAt(GameObject.Find("Unclaimed - Necroterio (planta)/Area externa").transform);
    }
    public static void InstallGenerated(Transform outside)
    {
        if(outside!=null&&Models.All(n=>AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(n))!=null))InstallAt(outside);
    }
    static void InstallAt(Transform outside)
    {
        if(outside.Find(Group)!=null)throw new InvalidOperationException("Patio already installed; update prefabs instead.");
        foreach(string n in Models)if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(n))==null)throw new FileNotFoundException(n);
        int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Furnish receiving service yard");
        var root=new GameObject(Group);root.transform.SetParent(outside,false);Undo.RegisterCreatedObjectUndo(root,"Service yard");
        for(int i=0;i<Models.Length;i++){
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Models[i])),root.transform);
            go.transform.SetPositionAndRotation(Positions[i],Quaternion.Euler(0,Yaws[i],0));Undo.RegisterCreatedObjectUndo(go,"Place "+Models[i]);
        }
        foreach(var t in outside.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))){
            if(t.IsChildOf(root.transform)||!Old.Contains(t.name)||t.position.x<16||t.position.z<9)continue;
            foreach(var r in t.GetComponents<Renderer>()){Undo.RecordObject(r,"Replace patio placeholder");r.enabled=false;}
            foreach(var c in t.GetComponents<Collider>()){Undo.RecordObject(c,"Replace patio collision");c.enabled=false;}
        }
        FinishSurfaces(root.transform);Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(outside.gameObject.scene);AssetDatabase.SaveAssets();
    }
    static void FinishSurfaces(Transform parent)
    {
        Directory.CreateDirectory(Folder+"/Superficies");
        string f="Assets/UnclaimedPrototype/Entorno/Materiais/";
        var floor=AssetDatabase.LoadAssetAtPath<Material>(f+"Cimentado.mat");var wall=AssetDatabase.LoadAssetAtPath<Material>(f+"Reboco do muro.mat");
        if(floor==null||wall==null)throw new FileNotFoundException("Existing exterior surface material missing.");
        Floor(parent,"Piso principal",12.75f,5.25f,21f,15.5f,floor);
        Floor(parent,"Piso do recuo",9.25f,11.5f,12.75f,13.25f,floor);
        int index=0;
        foreach(var filter in EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true))){
            bool isWall=filter.name.StartsWith("Muro do patio");
            if(!isWall)continue;
            var r=filter.GetComponent<Renderer>();if(r==null)continue;
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=filter.name+" UV em metros";
            var uv=new Vector2[mesh.vertexCount];var vs=mesh.vertices;var ns=mesh.normals;
            for(int i=0;i<vs.Length;i++){
                Vector3 p=filter.transform.TransformPoint(vs[i]);Vector3 n=filter.transform.TransformDirection(ns[i]);
                uv[i]=Mathf.Abs(n.y)>.8f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.8f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
            }mesh.uv=uv;mesh.RecalculateTangents();
            string path=Folder+"/Superficies/Muro "+(index++)+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);old=mesh;}else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);}
            Undo.RecordObject(filter,"Patio UV scale");filter.sharedMesh=old;Undo.RecordObject(r,"Patio surface material");r.sharedMaterial=wall;
        }
    }
    static void Floor(Transform parent,string name,float x0,float z0,float x1,float z1,Material material)
    {
        var vs=new[]{new Vector3(x0,.002f,z0),new Vector3(x1,.002f,z0),new Vector3(x1,.002f,z1),new Vector3(x0,.002f,z1)};
        var me=new Mesh{name=name,vertices=vs,triangles=new[]{0,2,1,0,3,2},uv=vs.Select(v=>new Vector2(v.x,v.z)).ToArray(),normals=Enumerable.Repeat(Vector3.up,4).ToArray()};
        me.RecalculateTangents();me.RecalculateBounds();string path=Folder+"/Superficies/"+name+".asset";
        var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(stored==null){AssetDatabase.CreateAsset(me,path);stored=me;}else{EditorUtility.CopySerialized(me,stored);UnityEngine.Object.DestroyImmediate(me);}
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=stored;
        var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;Undo.RegisterCreatedObjectUndo(go,"Patio concrete finish");
    }
}
