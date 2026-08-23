#!/usr/bin/env python3
import json
import re
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs, unquote

UNID = "0123456789ABCDEF0123456789ABCDEF"
SUPABASE_FILES = {}
DOMINO_FILES = {}

class Handler(BaseHTTPRequestHandler):
    server_version = "XPScriptHttpDbMock/1.0"

    def log_message(self, fmt, *args):
        pass

    def _read_body(self):
        length = int(self.headers.get("Content-Length", "0") or "0")
        return self.rfile.read(length) if length else b""

    def _read_json(self):
        raw = self._read_body()
        return None if not raw else json.loads(raw.decode("utf-8"))

    def _send(self, status, payload):
        body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
        self._send_bytes(status, body, "application/json; charset=utf-8")

    def _send_bytes(self, status, body, content_type="application/octet-stream"):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _supabase_auth_ok(self):
        return self.headers.get("apikey") == "test-key" and self.headers.get("Authorization") == "Bearer user-token"

    def _domino_auth_ok(self):
        return self.headers.get("Authorization") == "Bearer login-token"

    def _storage_path(self, prefix):
        raw = urlparse(self.path).path[len(prefix):]
        return unquote(raw.lstrip("/"))

    def _multipart_file(self, raw):
        content_type = self.headers.get("Content-Type", "")
        match = re.search(r"boundary=([^;]+)", content_type)
        if not match:
            return None, None
        boundary = ("--" + match.group(1).strip().strip('"')).encode("utf-8")
        for part in raw.split(boundary):
            part = part.strip(b"\r\n-")
            if not part or b"\r\n\r\n" not in part:
                continue
            headers, data = part.split(b"\r\n\r\n", 1)
            if b'name="filename"' not in headers:
                continue
            name_match = re.search(br'filename="([^"]+)"', headers)
            if not name_match:
                continue
            return name_match.group(1).decode("utf-8"), data.rstrip(b"\r\n")
        return None, None

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        query = parse_qs(parsed.query)

        if path.startswith("/storage/v1/object/authenticated/attachments/"):
            if not self._supabase_auth_ok():
                return self._send(401, {"error": "bad supabase storage auth"})
            object_path = self._storage_path("/storage/v1/object/authenticated/attachments/")
            item = SUPABASE_FILES.get(object_path)
            if not item:
                return self._send(404, {"error": "storage object not found"})
            return self._send_bytes(200, item["data"], item["content_type"])

        if path == f"/api/v1/attachmentnames/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino attachment auth"})
            rows = [{"name": name, "size": len(value["data"]), "contentType": value["content_type"], "modified": "2026-08-23T12:00:00Z"}
                    for name, value in sorted(DOMINO_FILES.items())]
            return self._send(200, {"attachments": rows})

        attachment_prefix = f"/api/v1/attachments/{UNID}/"
        if path.startswith(attachment_prefix):
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo":
                return self._send(401, {"error": "bad domino attachment auth"})
            name = unquote(path[len(attachment_prefix):])
            item = DOMINO_FILES.get(name)
            if not item:
                return self._send(404, {"error": "domino attachment not found"})
            return self._send_bytes(200, item["data"], item["content_type"])

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

        if path == "/storage/v1/object/list/attachments":
            if not self._supabase_auth_ok():
                return self._send(401, {"error": "bad supabase storage auth"})
            payload = self._read_json()
            prefix = (payload or {}).get("prefix", "").rstrip("/") + "/"
            rows = []
            for object_path, item in sorted(SUPABASE_FILES.items()):
                if object_path.startswith(prefix):
                    rows.append({"name": object_path[len(prefix):], "updated_at": "2026-08-23T12:00:00Z", "metadata": {"size": len(item["data"]), "mimetype": item["content_type"]}})
            return self._send(200, rows)

        if path.startswith("/storage/v1/object/attachments/"):
            if not self._supabase_auth_ok() or self.headers.get("x-upsert") != "true":
                return self._send(401, {"error": "bad supabase storage upload"})
            object_path = self._storage_path("/storage/v1/object/attachments/")
            raw = self._read_body()
            SUPABASE_FILES[object_path] = {"data": raw, "content_type": self.headers.get("Content-Type", "application/octet-stream")}
            return self._send(200, {"Key": "attachments/" + object_path})

        if path == f"/api/v1/attachments/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo" or query.get("fieldName", [""])[0] != "Body":
                return self._send(400, {"error": "bad domino attachment upload"})
            raw = self._read_body()
            name, data = self._multipart_file(raw)
            if not name or data is None:
                return self._send(400, {"error": "bad domino multipart upload"})
            DOMINO_FILES[name] = {"data": data, "content_type": "application/octet-stream"}
            return self._send(200, {"status": "upload complete", "filename": [name]})

        payload = self._read_json()
        if path == "/api/v1/auth":
            if not isinstance(payload, dict) or payload.get("username") != "CN=Test User/O=Example" or payload.get("password") != "secret":
                return self._send(401, {"error": "bad login"})
            return self._send(200, {"bearer": "login-token", "expires_in": 3600})
        if path == "/api/v1/auth/logout":
            if not self._domino_auth_ok(): return self._send(401, {"error": "bad domino auth"})
            return self._send(200, {"loggedOut": True})
        if path == "/sql":
            if self.headers.get("Authorization") != "Bearer admin-token": return self._send(401, {"error": "bad sql token"})
            if not isinstance(payload, dict) or not payload.get("query"): return self._send(400, {"error": "missing query"})
            return self._send(200, {"query": payload["query"], "ok": True})
        if path.startswith("/rest/v1/"):
            if not self._supabase_auth_ok(): return self._send(401, {"error": "bad supabase auth"})
            if self.headers.get("Content-Profile") != "public": return self._send(400, {"error": "bad content schema"})
            if path == "/rest/v1/customers": return self._send(201, [payload])
            if path == "/rest/v1/rpc/hello": return self._send(200, {"message": "Hello", "args": payload})
        if path == "/api/v1/document":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo": return self._send(401, {"error": "bad domino auth"})
            result = dict(payload or {}); result["@meta"] = {"unid": UNID}; return self._send(201, result)
        if path == "/api/v1/query":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo" or query.get("action", [""])[0] != "execute": return self._send(400, {"error": "bad query request"})
            if not isinstance(payload, dict) or not payload.get("query"): return self._send(400, {"error": "bad query payload"})
            return self._send(200, [{"Form": "Customer", "name": "Ada", "City": "Stockholm"}])
        return self._send(404, {"error": "not found", "path": self.path})

    def do_PATCH(self):
        parsed = urlparse(self.path); path = parsed.path; query = parse_qs(parsed.query); payload = self._read_json()
        if path == "/rest/v1/customers":
            if not self._supabase_auth_ok() or query.get("id", [""])[0] != "eq.42": return self._send(400, {"error": "bad supabase patch"})
            if isinstance(payload, dict) and payload.get("name") == "Ada Shared" and payload.get("internal_code") != "KEEP-ME": return self._send(400, {"error": "hidden supabase field was not preserved by SaveRow"})
            return self._send(200, [payload])
        if path == f"/api/v1/document/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo": return self._send(401, {"error": "bad domino auth"})
            return self._send(200, {"patched": True, "fields": payload})
        return self._send(404, {"error": "not found", "path": self.path})

    def do_PUT(self):
        parsed = urlparse(self.path); path = parsed.path; query = parse_qs(parsed.query); payload = self._read_json()
        if path == f"/api/v1/document/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo": return self._send(401, {"error": "bad domino auth"})
            if not isinstance(payload, dict): return self._send(400, {"error": "bad domino update payload"})
            if any(str(key).startswith("@") for key in payload.keys()): return self._send(400, {"error": "domino metadata must not be written as document items"})
            if payload.get("name") == "Shared Updated" and payload.get("InternalCode") != "KEEP-ME": return self._send(400, {"error": "hidden domino item was not preserved by SaveRow"})
            return self._send(200, {"updated": True, "fields": payload})
        return self._send(404, {"error": "not found", "path": self.path})

    def do_DELETE(self):
        parsed = urlparse(self.path); path = parsed.path; query = parse_qs(parsed.query)
        if path.startswith("/storage/v1/object/attachments/"):
            if not self._supabase_auth_ok(): return self._send(401, {"error": "bad supabase storage auth"})
            object_path = self._storage_path("/storage/v1/object/attachments/")
            if SUPABASE_FILES.pop(object_path, None) is None: return self._send(404, {"error": "storage object not found"})
            return self._send(200, {"message": "Successfully deleted"})
        attachment_prefix = f"/api/v1/attachments/{UNID}/"
        if path.startswith(attachment_prefix):
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo" or query.get("fieldName", [""])[0] != "Body": return self._send(400, {"error": "bad domino attachment delete"})
            name = unquote(path[len(attachment_prefix):])
            if DOMINO_FILES.pop(name, None) is None: return self._send(404, {"error": "domino attachment not found"})
            return self._send(200, {"deleted": True})
        if path == "/rest/v1/customers":
            if not self._supabase_auth_ok() or query.get("id", [""])[0] != "eq.42": return self._send(400, {"error": "bad supabase delete"})
            return self._send(200, [{"id": 42}])
        if path == f"/api/v1/document/{UNID}":
            if not self._domino_auth_ok() or query.get("dataSource", [""])[0] != "demo": return self._send(401, {"error": "bad domino auth"})
            return self._send(200, {"deleted": True})
        return self._send(404, {"error": "not found", "path": self.path})

if __name__ == "__main__":
    ThreadingHTTPServer(("127.0.0.1", 18082), Handler).serve_forever()
