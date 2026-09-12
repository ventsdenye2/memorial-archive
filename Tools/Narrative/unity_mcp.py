"""Local Unity MCP client for repeatable authoring/verification commands."""
import json
import sys
import urllib.request

headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
# This bridge is strictly loopback; Windows system proxies must not intercept it.
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

def request(method, params=None, identifier=1):
    body = {'jsonrpc': '2.0', 'method': method}
    if identifier is not None:
        body['id'] = identifier
    if params is not None:
        body['params'] = params
    req = urllib.request.Request('http://127.0.0.1:8080/mcp', json.dumps(body).encode(), headers)
    with opener.open(req, timeout=120) as response:
        session = response.headers.get('Mcp-Session-Id')
        if session:
            headers['Mcp-Session-Id'] = session
        data = response.read().decode()
        if not data:
            return None
        if any(line.startswith('data: ') for line in data.splitlines()):
            data = '\n'.join(line[6:] for line in data.splitlines() if line.startswith('data: '))
        return json.loads(data)

request('initialize', {'protocolVersion': '2024-11-05', 'capabilities': {}, 'clientInfo': {'name': 'narrative-validation', 'version': '1.0'}})
request('notifications/initialized', identifier=None)
result = request(sys.argv[1], json.loads(sys.argv[2]) if len(sys.argv) > 2 else {})
if sys.argv[1] == 'tools/list':
    names = sys.argv[3].split(',') if len(sys.argv) > 3 else []
    result = [{k: t[k] for k in ('name', 'description', 'inputSchema')} for t in result['result']['tools'] if t['name'] in names] if names else [t['name'] for t in result['result']['tools']]
print(json.dumps(result, ensure_ascii=False, indent=2))
