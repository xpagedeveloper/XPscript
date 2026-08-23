#!/usr/bin/env python3
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

UNID = "0123456789ABCDEF0123456789ABCDEF"

class Handler(BaseHTTPRequestHandler):
    server_version = "XPScriptHttpDbMock/1.0"

    def log_message(self, fmt, *args):
        pass

    def _read_json(self):
        length = int(self.headers.get("Content-Length", "0") or "0")
        if length == 0:
            return None
        raw = self.rfile.read(length).decode("utf-8")
        return json.loads(raw)

    def _send(self, status, payload):
        body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _supabase_auth_ok(self):
        return self.headers.get("apikey") == "test-key" and self.headers.get("Authorization") == "Bearer user-token"

    def _domino_auth_ok(self):
        return self.headers.get("Authorization") == "Bearer login-token"

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        query = parse_qs(parsed.query)

        if path.startswith("/rest/v1/"):
            if not self._supabase_auth_ok():
                return self._send(401, {"error": "bad supabase auth"})
            if self.headers.get("Accept-Profile") != "public":
                return self._send(400, {"error": "bad schema"})
            if path == "/rest/v1/customers":
                return self._send(200, [{"id": 42, "name": "Ada", "city": "Stockholm", "internal_code": "KEEP-ME"}])

        if path == f"/api/v1/document/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino auth"})
            return self._send(200, {"Form": "Customer", "name": "Ada", "City": "Stockholm", "InternalCode": "KEEP-ME", "@meta": {"unid": UNID}})

        if path == "/api/v1/lists":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino auth"})
            return self._send(200, [{"name": "People"}])

        if path == "/api/v1/lists/People":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo" or query.get("count", [""])[0] != "10":
                return self._send(400, {"error": "bad view request"})
            return self._send(200, [{"unid": UNID, "name": "Ada", "City": "Stockholm"}])

        if path == "/api/setup-v1/design/forms":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino auth"})
            return self._send(200, [{"name": "Customer"}])

        return self._send(404, {"error": "not found", "path": self.path})

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path
        query = parse_qs(parsed.query)
        payload = self._read_json()

        if path == "/api/v1/auth":
            if not isinstance(payload, dict) or payload.get("username") != "CN=Test User/O=Example" or payload.get("password") != "secret":
                return self._send(401, {"error": "bad login"})
            return self._send(200, {"bearer": "login-token", "expires_in": 3600})

        if path == "/api/v1/auth/logout":
            if not self._domino_auth_ok():
                return self._send(401, {"error": "bad domino auth"})
            return self._send(200, {"loggedOut": True})

        if path == "/sql":
            if self.headers.get("Authorization") != "Bearer admin-token":
                return self._send(401, {"error": "bad sql token"})
            if not isinstance(payload, dict) or not payload.get("query"):
                return self._send(400, {"error": "missing query"})
            return self._send(200, {"query": payload["query"], "ok": True})

        if path.startswith("/rest/v1/"):
            if not self._supabase_auth_ok():
                return self._send(401, {"error": "bad supabase auth"})
            if self.headers.get("Content-Profile") != "public":
                return self._send(400, {"error": "bad content schema"})
            if path == "/rest/v1/customers":
                return self._send(201, [payload])
            if path == "/rest/v1/rpc/hello":
                return self._send(200, {"message": "Hello", "args": payload})

        if path == "/api/v1/document":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino auth"})
            result = dict(payload or {})
            result["@meta"] = {"unid": UNID}
            return self._send(201, result)

        if path == "/api/v1/query":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo" or query.get("action", [""])[0] != "execute":
                return self._send(400, {"error": "bad query request"})
            if not isinstance(payload, dict) or not payload.get("query"):
                return self._send(400, {"error": "bad query payload"})
            return self._send(200, [{"Form": "Customer", "name": "Ada", "City": "Stockholm"}])

        return self._send(404, {"error": "not found", "path": self.path})

    def do_PATCH(self):
        parsed = urlparse(self.path)
        path = parsed.path
        query = parse_qs(parsed.query)
        payload = self._read_json()

        if path == "/rest/v1/customers":
            if not self._supabase_auth_ok() or query.get("id", [""])[0] != "eq.42":
                return self._send(400, {"error": "bad supabase patch"})
            if isinstance(payload, dict) and "internal_code" in payload and payload.get("internal_code") != "KEEP-ME":
                return self._send(400, {"error": "hidden supabase field changed"})
            return self._send(200, [payload])

        if path == f"/api/v1/document/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino auth"})
            return self._send(200, {"patched": True, "fields": payload})

        return self._send(404, {"error": "not found", "path": self.path})

    def do_PUT(self):
        parsed = urlparse(self.path)
        path = parsed.path
        query = parse_qs(parsed.query)
        payload = self._read_json()
        if path == f"/api/v1/document/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino auth"})
            if not isinstance(payload, dict):
                return self._send(400, {"error": "bad domino update payload"})
            if any(str(key).startswith("@") for key in payload.keys()):
                return self._send(400, {"error": "domino metadata must not be written as document items"})
            if payload.get("InternalCode") != "KEEP-ME":
                return self._send(400, {"error": "hidden domino item was not preserved"})
            return self._send(200, {"updated": True, "fields": payload})
        return self._send(404, {"error": "not found", "path": self.path})

    def do_DELETE(self):
        parsed = urlparse(self.path)
        path = parsed.path
        query = parse_qs(parsed.query)

        if path == "/rest/v1/customers":
            if not self._supabase_auth_ok() or query.get("id", [""])[0] != "eq.42":
                return self._send(400, {"error": "bad supabase delete"})
            return self._send(200, [{"id": 42}])

        if path == f"/api/v1/document/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino auth"})
            return self._send(200, {"deleted": True})

        return self._send(404, {"error": "not found", "path": self.path})

if __name__ == "__main__":
    ThreadingHTTPServer(("127.0.0.1", 18082), Handler).serve_forever()
