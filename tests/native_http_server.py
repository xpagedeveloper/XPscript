from http.server import BaseHTTPRequestHandler, HTTPServer

HOST = '127.0.0.1'
PORT = 18080


class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'

    def log_message(self, fmt, *args):
        pass

    def _body(self):
        length = int(self.headers.get('Content-Length', '0') or '0')
        return self.rfile.read(length).decode('utf-8') if length else ''

    def _send(self, method, body=''):
        status = 418 if self.path == '/fail' else 200
        header = self.headers.get('X-XPScript-Test', '')
        payload = f'{method}|{header}|{body}' if body else f'{method}|{header}'
        data = payload.encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'text/plain; charset=utf-8')
        self.send_header('X-Regression', 'native-http')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        self._send('GET')

    def do_POST(self):
        self._send('POST', self._body())

    def do_PUT(self):
        self._send('PUT', self._body())

    def do_PATCH(self):
        self._send('PATCH', self._body())

    def do_DELETE(self):
        self._send('DELETE')


if __name__ == '__main__':
    HTTPServer((HOST, PORT), Handler).serve_forever()
