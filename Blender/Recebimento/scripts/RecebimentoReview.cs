using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RecebimentoReview
{
    const string Folder="Blender/Recebimento/revisao";
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    public static object Verify()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!="Assets/Scenes/PrototipoNecroterio.unity")throw new InvalidOperationException("Edit Mode only.");
        var room=GameObject.Find("Unclaimed - Necroterio (planta)/Ambientes/Recebimento de corpos/Recebimento - equipamentos");
        if(room==null)throw new InvalidOperationException("Receiving equipment missing.");
        Physics.SyncTransforms();
        var routes=new[]{
            ("vestibulo",new Vector3(5.375f,0,5.7f),new Vector3(5.375f,0,10f)),
            ("camara-fria",new Vector3(9.75f,0,5.7f),new Vector3(9.75f,0,9.65f)),
            ("patio",new Vector3(12.2f,0,8.375f),new Vector3(10f,0,8.375f)),
            ("sul-da-maca",new Vector3(5.375f,0,6.7f),new Vector3(11.7f,0,6.7f)),
            ("norte-da-maca",new Vector3(5.375f,0,9.65f),new Vector3(11.7f,0,9.65f)),
            ("acesso-bancada",new Vector3(6f,0,9.65f),new Vector3(6f,0,10.30f))
        };
        var checks=routes.Select(route=>{
            var blocked=new List<string>();int n=Mathf.CeilToInt(Vector3.Distance(route.Item2,route.Item3)/.20f);
            for(int i=0;i<=n;i++){
                Vector3 p=Vector3.Lerp(route.Item2,route.Item3,i/(float)n);
                foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.42f,p+Vector3.up*1.42f,.35f,~0,QueryTriggerInteraction.Ignore))
                    if(c.transform.root.name!="Player")blocked.Add(PathOf(c.transform));
            }
            return new {route=route.Item1,samples=n+1,blockedBy=blocked.Distinct().ToArray()};
        }).ToArray();
        var missingMeshes=room.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh==null).Select(f=>PathOf(f.transform)).ToArray();
        var badMaterials=room.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name.Contains("InternalError"))).Select(r=>PathOf(r.transform)).ToArray();
        var clue=GameObject.Find("Pista - instrumento deslocado");
        var eye=new Vector3(6,1.65f,10.30f);var target=clue.GetComponent<Collider>().bounds.center;
        var hits=Physics.RaycastAll(eye,(target-eye).normalized,1.2f,~0,QueryTriggerInteraction.Collide)
            .Where(h=>h.collider.transform.root.name!="Player").OrderBy(h=>h.distance).ToArray();
        bool clueReachable=hits.Length>0&&hits[0].collider.gameObject==clue&&hits[0].distance<=1f;
        var instances=room.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).Select(t=>new{
            t.name,prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),position=t.position.ToString()
        }).ToArray();
        var baseline=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Folder+"/audit-antes.json"));
        var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).ToArray();
        var current=lights.Select(l=>new {l.name,pos=new[]{l.transform.position.x,l.transform.position.y,l.transform.position.z},l.intensity,l.range}).ToArray();
        bool lightsUnchanged=Newtonsoft.Json.Linq.JToken.DeepEquals(baseline["lights"],Newtonsoft.Json.Linq.JToken.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(current)));
        var result=new{routes=checks,clueReachable,clueFirstHit=hits.Length>0?hits[0].collider.name:"none",clueDistance=hits.Length>0?hits[0].distance:0,
            missingMeshes,badMaterials,instances,lights=lights.Length,lightsUnchanged,lightmaps=LightmapSettings.lightmaps.Length,
            temporaryCameras=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Where(c=>c.name.Contains("Temporary")).Select(c=>c.name).ToArray()};
        File.WriteAllText(Folder+"/verificacao.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
