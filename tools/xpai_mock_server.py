#!/usr/bin/env python3
"""Local OpenAI-compatible regression server for XPAi CI tests."""

from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import sys


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self):
        if self.path == "/async/http":
            self._json(200, {"message": "async-ok"})
            return
        self.send_error(404)

    def do_POST(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
            payload = json.loads(self.rfile.read(length))
        except (ValueError, json.JSONDecodeError):
            self._json(400, {"error": {"message": "invalid request"}})
            return

        if self.path.startswith("/compat/"):
            self._handle_compat(payload)
            return

        if self.path != "/custom/chat":
            self.send_error(404)
            return

        if self.headers.get("Authorization") != "Bearer XPAI_SECRET_MARKER":
            self._json(401, {"error": {"message": "missing authorization"}})
            return
        if self.headers.get("X-Provider") != "xpscript-test":
            self._json(400, {"error": {"message": "missing provider header"}})
            return
        if payload.get("model") == "error-model":
            self._json(401, {"error": {"message": "XPAI_SECRET_MARKER must stay private"}})
            return
        if payload.get("model") != "mock-model" or payload.get("temperature") != 0.2:
            self._json(400, {"error": {"message": "request schema mismatch"}})
            return
        if len(payload.get("messages", [])) != 2:
            self._json(400, {"error": {"message": "message schema mismatch"}})
            return

        if payload.get("stream"):
            events = [
                {"model": "mock-model", "choices": [{"delta": {"content": "Hello "}}]},
                {"model": "mock-model", "choices": [{"delta": {"content": "stream"}}]},
                {"model": "mock-model", "choices": [], "usage": {"total_tokens": 9}},
            ]
            body = "".join(f"data: {json.dumps(event)}\n\n" for event in events)
            body += "data: [DONE]\n\n"
            encoded = body.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Content-Length", str(len(encoded)))
            self.end_headers()
            self.wfile.write(encoded)
            return

        self._json(
            200,
            {
                "model": "mock-model",
                "choices": [{"message": {"role": "assistant", "content": "Hello response"}}],
                "usage": {"prompt_tokens": 5, "completion_tokens": 2, "total_tokens": 7},
                "provider_extra": {"preserved": True},
            },
        )

    def _handle_compat(self, payload):
        if payload.get("stream") is not False:
            self._json(400, {"error": {"message": "compat fixtures require non-streaming requests"}})
            return
        if len(payload.get("messages", [])) != 1:
            self._json(400, {"error": {"message": "compat message schema mismatch"}})
            return

        if self.path == "/compat/openai":
            if payload.get("model") != "openai-compat-model":
                self._json(400, {"error": {"message": "openai model mismatch"}})
                return
            if self.headers.get("Authorization") != "Bearer OPENAI_COMPAT_KEY":
                self._json(401, {"error": {"message": "openai authorization mismatch"}})
                return
            self._compat_response("openai-compat-model", "OPENAI-COMPAT=OK")
            return

        if self.path == "/compat/openrouter":
            if payload.get("model") != "openrouter-compat-model":
                self._json(400, {"error": {"message": "openrouter model mismatch"}})
                return
            if self.headers.get("Authorization") != "Bearer OPENROUTER_COMPAT_KEY":
                self._json(401, {"error": {"message": "openrouter authorization mismatch"}})
                return
            if self.headers.get("HTTP-Referer") != "https://xpscript.test/compat":
                self._json(400, {"error": {"message": "openrouter referer mismatch"}})
                return
            if self.headers.get("X-OpenRouter-Title") != "XPScript CI":
                self._json(400, {"error": {"message": "openrouter title mismatch"}})
                return
            self._compat_response("openrouter-compat-model", "OPENROUTER-COMPAT=OK")
            return

        if self.path == "/compat/azure":
            if payload.get("model") != "azure-compat-model":
                self._json(400, {"error": {"message": "azure model mismatch"}})
                return
            if self.headers.get("api-key") != "AZURE_COMPAT_KEY":
                self._json(401, {"error": {"message": "azure api-key mismatch"}})
                return
            if self.headers.get("Authorization") is not None:
                self._json(400, {"error": {"message": "azure fixture must not receive authorization"}})
                return
            self._compat_response("azure-compat-model", "AZURE-COMPAT=OK")
            return

        self.send_error(404)

    def _compat_response(self, model, text):
        self._json(
            200,
            {
                "model": model,
                "choices": [{"message": {"role": "assistant", "content": text}}],
                "usage": {"prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2},
            },
        )

    def _json(self, status, payload):
        encoded = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def log_message(self, _format, *_args):
        return


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 18765
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
