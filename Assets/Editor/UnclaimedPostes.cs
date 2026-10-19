// Blender-authored poles and fittings, with manifest anchors in Unity local metres.
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class UnclaimedPostes
{
    public const string Folder = "Assets/Modelos/Entorno/Postes";
    static JObject anchors;
    public static string PrefabPath(string name) => Folder+"/Prefabs/"+name+".prefab";
    public static bool Available(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name))!=null;
    static JArray Points(string asset,string key)
    {
        if(anchors==null){
            string path=Folder+"/assets.json";
            anchors=File.Exists(path)?JObject.Parse(File.ReadAllText(path))["anchors"] as JObject:new JObject();
            if(anchors==null)anchors=new JObject();
        }
        return anchors[asset]?[key] as JArray;
    }
    public static int AnchorCount(string asset,string key) => Points(asset,key)?.Count??0;
    public static Vector3 Anchor(string asset,string key,int index=0)
    {
        var points=Points(asset,key);
        if(points==null || index<0 || index>=points.Count)throw new InvalidOperationException("Missing pole anchor: "+asset+"/"+key+"["+index+"]");
        var p=(JArray)points[index];return new Vector3((float)p[0],(float)p[1],(float)p[2]);
    }

    [MenuItem("Tools/Unclaimed/Build Pole Prefabs")]
    public static void Build()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before building assets.");
        anchors=null;
        Ensure("Materiais");Ensure("Prefabs");
        var manifest=JObject.Parse(File.ReadAllText(Folder+"/assets.json"));
        var materials=new Dictionary<string,Material>();
        foreach(var entry in ((JObject)manifest["assets"]).Properties()){
            string name=entry.Name,path=Folder+"/"+name+".fbx";
            AssetDatabase.ImportAsset(path);
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null)throw new InvalidOperationException("FBX not imported: "+path);
            bool changed=importer.importAnimation || importer.importCameras || importer.importLights || !importer.bakeAxisConversion ||
                importer.globalScale!=1 || importer.generateSecondaryUV || importer.isReadable || importer.meshCompression!=ModelImporterMeshCompression.Off ||
                importer.materialImportMode!=ModelImporterMaterialImportMode.ImportStandard || importer.materialLocation!=ModelImporterMaterialLocation.InPrefab ||
                importer.importNormals!=ModelImporterNormals.Import;
            importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.bakeAxisConversion=true;
            importer.globalScale=1;importer.generateSecondaryUV=false;importer.isReadable=false;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
            importer.importNormals=ModelImporterNormals.Import;
            if(changed)importer.SaveAndReimport();
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));root.name=name;
            try{
                foreach(var t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
                foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
                    var mats=r.sharedMaterials;
                    for(int i=0;i<mats.Length;i++){
                        string slot=mats[i].name;
                        if(!materials.ContainsKey(slot))materials[slot]=MaterialFor(slot,(JObject)manifest["materials"]);
                        mats[i]=materials[slot];
                    }
                    r.sharedMaterials=mats;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                    r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
                    if(r is MeshRenderer mr)mr.receiveGI=ReceiveGI.LightProbes;
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath(name));
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Unclaimed: pole prefabs built; existing asset GUIDs retained.");
    }

    static Material MaterialFor(string name,JObject colors)
    {
        string path=Folder+"/Materiais/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(m==null){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
        var spec=(JArray)colors[name];var c=(JArray)spec[0];
        m.SetColor("_BaseColor",new Color((float)c[0],(float)c[1],(float)c[2]));
        m.SetFloat("_Metallic",(float)spec[1]);m.SetFloat("_Smoothness",(float)spec[2]);
        m.SetFloat("_Surface",0);m.SetFloat("_AlphaClip",0);m.SetFloat("_Cull",2);
        m.DisableKeyword("_ALPHATEST_ON");m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if(name=="Concreto_Poste" || name=="Concreto_Cruzeta"){
            const string texture="Assets/UnclaimedPrototype/Entorno/Texturas/granular_concrete";
            string normal=texture+"_nor.jpg";
            var ti=AssetImporter.GetAtPath(normal) as TextureImporter;
            if(ti!=null && ti.textureType!=TextureImporterType.NormalMap){ti.textureType=TextureImporterType.NormalMap;ti.SaveAndReimport();}
            // Light grey re-grade of the Poly Haven grain (Blender/Entorno/Postes/scripts/textura_concreto.py).
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Texturas/Concreto_Poste.png"));
            m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));m.SetFloat("_BumpScale",.6f);m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BaseMap",new Vector2(.5f,.5f));m.SetTextureScale("_BumpMap",new Vector2(.5f,.5f));
        }
        EditorUtility.SetDirty(m);return m;
    }
    static void Ensure(string name){if(!AssetDatabase.IsValidFolder(Folder+"/"+name))AssetDatabase.CreateFolder(Folder,name);}
}
