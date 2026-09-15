import requests,json,sys
from pathlib import Path
s=requests.Session()
url='http://127.0.0.1:8080/mcp'
s.headers.update({'Accept':'application/json, text/event-stream'})
def call(method,params=None):
    r=s.post(url,json={'jsonrpc':'2.0','id':1,'method':method,'params':params or {}},timeout=15)
    if r.headers.get('Mcp-Session-Id'): s.headers['Mcp-Session-Id']=r.headers['Mcp-Session-Id']
    text=r.text
    if text.startswith('event:'): text=next(l[6:] for l in text.splitlines() if l.startswith('data: '))
    return json.loads(text)
call('initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'lamp-debug','version':'1'}})
if __name__ == '__main__':
    r=call('tools/call',{'name':'execute_code','arguments':{'action':'execute','code':Path(sys.argv[1]).read_text(encoding='utf-8')}}) if len(sys.argv)>1 else call('resources/list')
    print(json.dumps(r,ensure_ascii=True))
