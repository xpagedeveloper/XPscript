from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import time

HOST = '127.0.0.1'
PORT = 18080
MAX_BODY = 64 * 1024 * 1024


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

    def _send_redirect(self):
        data = b'redirect-not-followed'
        self.send_response(302)
        self.send_header('Location', f'http://{HOST}:{PORT}/redirect-target')
        self.send_header('Content-Type', 'text/plain; charset=utf-8')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _send_declared_oversized(self):
        self.send_response(200)
        self.send_header('Content-Type', 'text/plain; charset=utf-8')
        self.send_header('Content-Length', str(MAX_BODY + 1))
        self.end_headers()
        try:
            self.wfile.write(b'x')
        except (BrokenPipeError, ConnectionResetError):
            pass
        self.close_connection = True

    def _send_streamed_oversized(self):
        self.send_response(200)
        self.send_header('Content-Type', 'text/plain; charset=utf-8')
        self.send_header('Connection', 'close')
        self.end_headers()
        chunk = b'x' * (64 * 1024)
        remaining = MAX_BODY + 1
        try:
            while remaining > 0:
                part = chunk if remaining >= len(chunk) else b'x' * remaining
                self.wfile.write(part)
                self.wfile.flush()
                remaining -= len(part)
        except (BrokenPipeError, ConnectionResetError):
            pass
        self.close_connection = True

    def _send_slow(self):
        time.sleep(2)
        data = b'slow-response'
        self.send_response(200)
        self.send_header('Content-Type', 'text/plain; charset=utf-8')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def do_GET(self):
        if self.path == '/redirect':
            self._send_redirect()
            return
        if self.path == '/redirect-target':
            data = b'redirect-followed'
            self.send_response(200)
            self.send_header('Content-Type', 'text/plain; charset=utf-8')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            return
        if self.path == '/oversized-declared':
            self._send_declared_oversized()
            return
        if self.path == '/oversized-stream':
            self._send_streamed_oversized()
            return
        if self.path == '/slow':
            self._send_slow()
            return
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
    ThreadingHTTPServer((HOST, PORT), Handler).serve_forever()
