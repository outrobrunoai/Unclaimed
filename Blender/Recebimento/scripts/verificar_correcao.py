import bpy,json,os
from mathutils.bvhtree import BVHTree
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'../../..'))
bpy.ops.wm.open_mainfile(filepath=root+'/Blender/Recebimento/Recebimento.blend')
def tree(name):
    m=bpy.data.objects[name].data
    return BVHTree.FromPolygons([v.co for v in m.vertices],[list(f.vertices) for f in m.polygons],all_triangles=True,epsilon=0)
hits=tree('CorpoCoberto').overlap(tree('MacaRecebimento'))
result={'sheetStretcherIntersectingTrianglePairs':len(hits),'assets':{n:{'triangles':len(bpy.data.objects[n].data.polygons),'degenerate':sum(p.area<1e-12 for p in bpy.data.objects[n].data.polygons)} for n in ['CorpoCoberto','EstacaoLavagem','CarroAuxiliar','SuporteEPI']}}
print('GEOMETRY_CHECK',json.dumps(result),flush=True)
open(root+'/Blender/Recebimento/revisao/correcoes/geometria.json','w').write(json.dumps(result,indent=2))
assert not hits,'Sheet intersects stretcher'
