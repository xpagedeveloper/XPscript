from http.server import BaseHTTPRequestHandler, HTTPServer
import json

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/lookup'):
            body = json.dumps([
                {"id": "c1", "name": "Customer One"},
                {"id": "c2", "name": "Customer Two"},
                {"id": "p1", "name": "Product One"}
            ]).encode('utf-8')
            self.send_response(200)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        self.send_response(404)
        self.end_headers()

    def log_message(self, format, *args):
        pass

HTTPServer(('127.0.0.1', 18083), Handler).serve_forever()
