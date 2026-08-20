from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from urllib.parse import urlparse

HOST = '127.0.0.1'
PORT = 18081


class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'

    def log_message(self, fmt, *args):
        pass

    def _read_body(self):
        length = int(self.headers.get('Content-Length', '0') or '0')
        return self.rfile.read(length).decode('utf-8') if length else ''

    def _json(self, status, value):
        data = json.dumps(value, separators=(',', ':'), ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == '/fail-json':
            self._json(404, {'error': 'missing'})
            return
        if parsed.path == '/array':
            self._json(200, [1, 2, 3])
            return
        self._json(200, {'name': 'Loaded customer', 'age': 42, 'enabled': True, 'query': parsed.query})

    def _save_json(self):
        body = self._read_body()
        try:
            value = json.loads(body)
        except json.JSONDecodeError:
            self._json(400, {'error': 'invalid-json'})
            return
        self._json(200, {'saved': value, 'contentType': self.headers.get('Content-Type', '')})

    def do_POST(self):
        if urlparse(self.path).path == '/form':
            self._json(200, {'body': self._read_body(), 'contentType': self.headers.get('Content-Type', '')})
            return
        self._save_json()

    def do_PUT(self):
        self._save_json()

    def do_PATCH(self):
        self._save_json()


if __name__ == '__main__':
    ThreadingHTTPServer((HOST, PORT), Handler).serve_forever()
