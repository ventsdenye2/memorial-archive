"""Small Unity text-serialization adapter. Unchanged objects retain their exact text."""
from pathlib import Path
import copy, hashlib, re
import yaml

ROOT = Path(__file__).resolve().parents[2]

class Dumper(yaml.SafeDumper):
    def increase_indent(self, flow=False, indentless=False):
        return super().increase_indent(flow, False)

def represent_mapping(dumper, data):
    flow = bool(data) and (set(data) <= {'fileID','guid','type'} or set(data) <= {'x','y','z','w'} or set(data) <= {'r','g','b','a'})
    return dumper.represent_mapping('tag:yaml.org,2002:map', data.items(), flow_style=flow)
Dumper.add_representer(dict, represent_mapping)

def stable_guid(path):
    return hashlib.sha256(('MemorialArchive/SceneRebuild/'+str(path)).encode('utf-8')).hexdigest()[:32]

def guid(path):
    p=ROOT/path
    meta=Path(str(p)+'.meta')
    if not meta.exists():
        meta.parent.mkdir(parents=True,exist_ok=True)
        importer='DefaultImporter' if p.suffix=='.unity' else 'NativeFormatImporter'
        meta.write_text(f'fileFormatVersion: 2\nguid: {stable_guid(path)}\n{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
    return re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)[1]

def ref(path=None, file_id=0, asset_type=3):
    return {'fileID':file_id, 'guid':guid(path), 'type':asset_type} if path else {'fileID':file_id}

class Document:
    def __init__(self,path):
        self.path=ROOT/path
        self.parts={};self.data={};self.kinds={};self.dirty=set()
        text=self.path.read_text(encoding='utf-8-sig')
        self.header=text[:text.index('--- !u!')]
        for m in re.finditer(r'^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S):
            i=int(m[2]);self.parts[i]=m[0];self.data[i]=yaml.safe_load(m[3]);self.kinds[i]=int(m[1])
        self.next_id=900000000000
    def find(self,typ):
        return [(i,d[typ]) for i,d in self.data.items() if typ in d]
    def get(self,i): return next(iter(self.data[i].values()))
    def edit(self,i): self.dirty.add(i);return self.get(i)
    def add(self,kind,typ,fields):
        while self.next_id in self.data:self.next_id+=1
        i=self.next_id;self.next_id+=1;self.data[i]={typ:fields};self.kinds[i]=kind;self.dirty.add(i);return i
    def save(self):
        chunks=[self.header]
        for i,d in self.data.items():
            if i not in self.dirty:chunks.append(self.parts[i]);continue
            chunks.append(f'--- !u!{self.kinds[i]} &{i}\n'+yaml.dump(d,Dumper=Dumper,sort_keys=False,allow_unicode=True,width=180))
        self.path.write_text(''.join(chunks),encoding='utf-8')
    def game_object(self,name,position=(0,0,0),parent=0,scale=(1,1,1)):
        go=self.add(1,'GameObject',dict(common(),serializedVersion=6,m_Component=[],m_Layer=0,m_Name=name,m_TagString='Untagged',m_Icon=ref(),m_NavMeshLayer=0,m_StaticEditorFlags=0,m_IsActive=1))
        t=self.add(4,'Transform',dict(common(),m_GameObject=ref(file_id=go),serializedVersion=2,m_LocalRotation={'x':0,'y':0,'z':0,'w':1},m_LocalPosition=dict(zip('xyz',position)),m_LocalScale=dict(zip('xyz',scale)),m_ConstrainProportionsScale=0,m_Children=[],m_Father=ref(file_id=parent),m_LocalEulerAnglesHint={'x':0,'y':0,'z':0}))
        self.component(go,t)
        if parent:self.edit(parent)['m_Children'].append(ref(file_id=t))
        return go,t
    def component(self,go,c):self.edit(go)['m_Component'].append({'component':ref(file_id=c)})
    def mono(self,go,script,**fields):
        c=self.add(114,'MonoBehaviour',dict(common(),m_GameObject=ref(file_id=go),m_Enabled=1,m_EditorHideFlags=0,m_Script=ref(script,11500000),m_Name='',m_EditorClassIdentifier='',**fields));self.component(go,c);return c
    def clone_component(self,go,kind,typ,template,**fields):
        d=copy.deepcopy(template);d.update(common());d['m_GameObject']=ref(file_id=go);d.update(fields)
        c=self.add(kind,typ,d);self.component(go,c);return c
    def transform_of(self,go):
        return next((i for i,d in self.find('Transform') if d.get('m_GameObject',{}).get('fileID')==go and 'm_LocalPosition' in d),None)
    def world(self,t):
        d=self.get(t);p=d.get('m_Father',{}).get('fileID');pos=d['m_LocalPosition'].copy()
        if p and p in self.data and 'm_LocalPosition' in self.get(p):
            pp=self.world(p);scale=self.get(p)['m_LocalScale'];pos={k:pos[k]*scale[k]+pp[k] for k in 'xyz'}
        return pos
    def move(self,go,x,y):
        t=self.transform_of(go)
        if t is None:return
        d=self.edit(t);p=d['m_Father']['fileID'];pp=self.world(p) if p else dict(x=0,y=0,z=0)
        d['m_LocalPosition']={'x':x-pp['x'],'y':y-pp['y'],'z':0}

    def remove_hierarchy(self,go):
        transform=self.transform_of(go)
        if transform is None:raise ValueError('Managed hierarchy has no transform')
        doomed=set()
        def visit(t):
            node=self.get(t);owner=node['m_GameObject']['fileID']
            for child in node.get('m_Children',[]):visit(child['fileID'])
            doomed.add(owner)
            doomed.update(x['component']['fileID'] for x in self.get(owner)['m_Component'])
        visit(transform)
        parent=self.get(transform)['m_Father']['fileID']
        if parent:self.edit(parent)['m_Children']=[x for x in self.get(parent)['m_Children'] if x['fileID']!=transform]
        for i in doomed:
            del self.data[i];self.parts.pop(i,None);self.kinds.pop(i,None);self.dirty.discard(i)

def common():
    return dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=ref(),m_PrefabInstance=ref(),m_PrefabAsset=ref())
