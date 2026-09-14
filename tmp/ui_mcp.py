import urllib.request,json,sys
from pathlib import Path
def call(m,p):
 h=json.loads(Path('tmp/ui-mcp-session.json').read_text())
 r=urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:8080/mcp',data=json.dumps({'jsonrpc':'2.0','id':3,'method':m,'params':p}).encode(),headers=h),timeout=60)
 s=r.read().decode();return json.loads(next(x[6:] for x in s.splitlines() if x.startswith('data: '))) if s.startswith('event:') else json.loads(s)
if __name__=='__main__':print(json.dumps(call(sys.argv[1],json.loads(sys.argv[2])),ensure_ascii=False))
