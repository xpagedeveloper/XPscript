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

    def _send_request_inspection(self):
        length_header = self.headers.get('Content-Length')
        content_type = self.headers.get('Content-Type')
        body = self._body()
        payload = '|'.join([
            'INSPECT',
            'LENGTH=' + ('ABSENT' if length_header is None else length_header),
            'TYPE=' + ('ABSENT' if content_type is None else content_type),
            'BODY=' + body,
        ])
        data = payload.encode('utf-8')
        self.send_response(200)
        self.send_header('Content-Type', 'text/plain; charset=utf-8')
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

    def _send_binary(self):
        data = bytes([0, 255, 1, 254, 2, 253, 10, 13, 128, 127]) + b'XPS-BINARY'
        self.send_response(200)
        self.send_header('Content-Type', 'application/octet-stream')
        self.send_header('Content-Disposition', 'attachment; filename="report.bin"')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _send_multipart_files(self):
        boundary = 'xpscript-boundary-2026'
        metadata = b'{"status":"ok","count":2}'
        first = bytes([0, 1, 2, 250, 251, 252]) + b'FIRST'
        second = bytes([255, 254, 253, 10, 13]) + b'SECOND'
        chunks = [
            f'--{boundary}\r\n'.encode('ascii'),
            b'Content-Disposition: form-data; name="metadata"\r\n',
            b'Content-Type: application/json; charset=utf-8\r\n\r\n',
            metadata,
            b'\r\n',
            f'--{boundary}\r\n'.encode('ascii'),
            b'Content-Disposition: form-data; name="first"; filename="first.bin"\r\n',
            b'Content-Type: application/octet-stream\r\n\r\n',
            first,
            b'\r\n',
            f'--{boundary}\r\n'.encode('ascii'),
            b"Content-Disposition: form-data; name=\"second\"; filename*=UTF-8''second-%C3%A5.bin\r\n",
            b'Content-Type: application/octet-stream\r\n\r\n',
            second,
            b'\r\n',
            f'--{boundary}--\r\n'.encode('ascii'),
        ]
        data = b''.join(chunks)
        self.send_response(200)
        self.send_header('Content-Type', f'multipart/form-data; boundary="{boundary}"')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

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
        if self.path == '/binary':
            self._send_binary()
            return
        if self.path == '/multipart-files':
            self._send_multipart_files()
            return
        self._send('GET')

    def do_POST(self):
        if self.path == '/inspect':
            self._send_request_inspection()
            return
        self._send('POST', self._body())

    def do_PUT(self):
        self._send('PUT', self._body())

    def do_PATCH(self):
        self._send('PATCH', self._body())

    def do_DELETE(self):
        self._send('DELETE')


if __name__ == '__main__':
    ThreadingHTTPServer((HOST, PORT), Handler).serve_forever()
