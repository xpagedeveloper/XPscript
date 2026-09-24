import http.client
import json
import sys

host = "127.0.0.1"
port = int(sys.argv[1])

def post(body, allow_early_close=False):
    conn = http.client.HTTPConnection(host, port, timeout=15)
    try:
        conn.request("POST", "/api/json-hardening", body=body.encode("utf-8"),
                     headers={"Content-Type": "application/json"})
        response = conn.getresponse()
        data = response.read().decode("utf-8", "replace")
        return response.status, data
    except (BrokenPipeError, ConnectionResetError):
        if allow_early_close:
            return None, ""
        raise
    finally:
        conn.close()

def assert_server_healthy():
    conn = http.client.HTTPConnection(host, port, timeout=15)
    try:
        conn.request("GET", "/")
        response = conn.getresponse()
        data = response.read().decode("utf-8", "replace")
        assert response.status == 200, (response.status, data[:500])
        assert "XPSCRIPT-HARDENING" in data, data[:500]
    finally:
        conn.close()

def assert_no_diagnostics(body):
    lowered = body.lower()
    for marker in ("stacktrace", ".cs:", "/home/", "\\users\\"):
        assert marker not in lowered, (marker, body[:1000])

status, body = post("{not-json")
assert status == 400, (status, body[:500])
assert_no_diagnostics(body)

status, body = post("[" * 65 + "0" + "]" * 65)
assert status == 400, (status, body[:500])
assert_no_diagnostics(body)

status, body = post('{"value":"first","value":"second"}')
assert status == 200, (status, body[:500])
assert json.loads(body).get("value") == "second", body[:500]

status, body = post('{"value":"' + ("x" * (4 * 1024 * 1024)) + '"}', allow_early_close=True)
if status is not None:
    assert status in (400, 413), (status, body[:500])
    assert_no_diagnostics(body)
assert_server_healthy()

print("JSON-API-HARDENING=OK")
