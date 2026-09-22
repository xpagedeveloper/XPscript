import http.client
import json
import sys

host = "127.0.0.1"
port = int(sys.argv[1])

def post(body):
    conn = http.client.HTTPConnection(host, port, timeout=15)
    conn.request("POST", "/api/json-hardening", body=body.encode("utf-8"),
                 headers={"Content-Type": "application/json"})
    response = conn.getresponse()
    data = response.read().decode("utf-8", "replace")
    conn.close()
    return response.status, data

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

status, body = post('{"value":"' + ("x" * (4 * 1024 * 1024)) + '"}')
assert status in (400, 413), (status, body[:500])
assert_no_diagnostics(body)

print("JSON-API-HARDENING=OK")
