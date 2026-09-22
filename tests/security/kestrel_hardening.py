#!/usr/bin/env python3
import argparse
import json
import socket
from pathlib import Path

def send_raw(host, port, request):
    with socket.create_connection((host, port), timeout=5) as sock:
        sock.settimeout(5)
        sock.sendall(request)
        chunks = []
        try:
            while True:
                data = sock.recv(65536)
                if not data:
                    break
                chunks.append(data)
        except socket.timeout:
            pass
    return b"".join(chunks)

def status(raw):
    try:
        line = raw.split(b"\r\n", 1)[0].decode("latin1")
        return int(line.split()[1])
    except Exception:
        return None

def headers(raw):
    head = raw.split(b"\r\n\r\n", 1)[0].decode("latin1", "replace")
    result = {}
    for line in head.split("\r\n")[1:]:
        if ":" in line:
            k, v = line.split(":", 1)
            result.setdefault(k.lower().strip(), []).append(v.strip())
    return result

def run(host, port):
    tests = []

    def add(name, request, allowed_statuses=None, predicate=None):
        raw = send_raw(host, port, request)
        code = status(raw)
        hdrs = headers(raw)
        ok = True
        detail = ""
        if allowed_statuses is not None and code not in allowed_statuses:
            ok = False
            detail = f"unexpected status {code}"
        if predicate is not None:
            pred_ok, pred_detail = predicate(code, hdrs, raw)
            ok = ok and pred_ok
            if not pred_ok:
                detail = pred_detail
        tests.append({"name": name, "passed": ok, "status": code, "detail": detail})

    add("malformed-utf8-path-rejected",
        b"GET /bad\xffpath HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (
            b"XPSCRIPT-HARDENING" not in r and
            b"stacktrace" not in r.lower() and
            b".cs:" not in r.lower() and
            b"/home/" not in r.lower() and
            b"\\users\\" not in r.lower(),
            "malformed UTF-8 path reached the application or leaked diagnostics"))

    add("malformed-percent-utf8-path-rejected",
        b"GET /bad%C3%28path HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (
            b"XPSCRIPT-HARDENING" not in r and
            b"stacktrace" not in r.lower() and
            b".cs:" not in r.lower() and
            b"/home/" not in r.lower() and
            b"\\users\\" not in r.lower(),
            "invalid percent-encoded UTF-8 path reached the application or leaked diagnostics"))

    add("non-ascii-percent-encoded-path-safe",
        b"GET /%C3%A5%C3%A4%C3%B6 HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200,404},
        lambda c,h,r: (
            b"XPSCRIPT_SECRET_SENTINEL" not in r and
            b"stacktrace" not in r.lower() and
            b".cs:" not in r.lower(),
            "valid non-ASCII path disclosed protected content or diagnostics"))

    add("non-ascii-percent-encoded-query-safe",
        b"GET /?name=Fr%C3%A9d%C3%A9ric&city=G%C3%A4vle HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200},
        lambda c,h,r: (
            b"XPSCRIPT-HARDENING" in r and
            b"stacktrace" not in r.lower() and
            b".cs:" not in r.lower(),
            "valid non-ASCII query was not handled safely"))

    add("server-header-hidden",
        b"GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200},
        lambda c,h,r: ("server" not in h, "Server header exposed"))

    add("invalid-host-rejected",
        b"GET / HTTP/1.1\r\nHost: attacker.invalid\r\nConnection: close\r\n\r\n",
        {400})

    add("duplicate-host-rejected",
        b"GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nHost: attacker.invalid\r\nConnection: close\r\n\r\n",
        {400})

    add("trace-not-successful",
        b"TRACE / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404,405,501})

    add("oversized-request-line-rejected",
        ("GET /" + ("A" * 9000) + " HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n").encode(),
        {400,414})

    add("oversized-header-rejected",
        ("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nX-Large: " + ("B" * 40000) + "\r\nConnection: close\r\n\r\n").encode(),
        {400,431})

    add("content-length-transfer-encoding-framing-safe",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 4\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n0\r\n\r\n",
        {200,400},
        lambda c,h,r: (
            r.count(b"HTTP/1.1 ") == 1 and b"XPSCRIPT_SECRET_SENTINEL" not in r,
            "ambiguous framing produced multiple responses or disclosed protected content"))

    add("conflicting-content-length-rejected",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 4\r\nContent-Length: 5\r\nConnection: close\r\n\r\nABCDE",
        {400})

    add("cl-te-desync-no-second-request",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 4\r\nTransfer-Encoding: chunked\r\nConnection: keep-alive\r\n\r\n0\r\n\r\nGET /secret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200,400},
        lambda c,h,r: (
            r.count(b"HTTP/1.1 ") == 1 and b"XPSCRIPT_SECRET_SENTINEL" not in r,
            "CL.TE payload executed or exposed a second request"))

    add("duplicate-transfer-encoding-safe",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n0\r\n\r\n",
        {200,400,501},
        lambda c,h,r: (
            r.count(b"HTTP/1.1 ") <= 1 and b"XPSCRIPT_SECRET_SENTINEL" not in r,
            "duplicate Transfer-Encoding caused ambiguous framing"))

    add("invalid-chunk-size-rejected",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\nZ\r\nboom\r\n0\r\n\r\n",
        {400})

    add("malformed-chunk-terminator-rejected",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n4\r\ntestX\r\n0\r\n\r\n",
        {400})

    add("te-cl-desync-no-second-request",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\nContent-Length: 44\r\nConnection: keep-alive\r\n\r\n0\r\n\r\nGET /secret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200,400},
        lambda c,h,r: (
            r.count(b"HTTP/1.1 ") == 1 and b"XPSCRIPT_SECRET_SENTINEL" not in r,
            "TE.CL payload executed or exposed a second request"))

    add("obfuscated-transfer-encoding-rejected",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding : chunked\r\nConnection: close\r\n\r\n0\r\n\r\n",
        {400})

    add("track-not-successful",
        b"TRACK / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404,405,501})

    add("uncommon-method-not-routed",
        b"BREW / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404,405,501},
        lambda c,h,r: (b"XPSCRIPT-HARDENING" not in r, "uncommon method reached application handler"))

    add("absolute-form-invalid-host-rejected",
        b"GET http://attacker.invalid/ HTTP/1.1\r\nHost: attacker.invalid\r\nConnection: close\r\n\r\n",
        {400})

    add("http-1-0-safe",
        b"GET / HTTP/1.0\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200,400,505},
        lambda c,h,r: (b"XPSCRIPT_SECRET_SENTINEL" not in r, "HTTP/1.0 disclosed protected content"))

    add("bare-lf-framing-canonicalized-safe",
        b"GET / HTTP/1.1\nHost: 127.0.0.1\nConnection: close\n\n",
        {200,400},
        lambda c,h,r: (
            r.count(b"HTTP/1.1 ") == 1 and b"XPSCRIPT_SECRET_SENTINEL" not in r,
            "bare LF framing produced ambiguous responses or disclosed protected content"))

    add("chunk-extension-safe",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n4;foo=bar\r\ntest\r\n0\r\n\r\n",
        {200,400},
        lambda c,h,r: (r.count(b"HTTP/1.1 ") <= 1 and b"XPSCRIPT_SECRET_SENTINEL" not in r,
                       "chunk extension caused ambiguous framing or protected-content disclosure"))

    add("malformed-http-version-rejected",
        b"GET / HTTP/1.X\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,505})

    add("malformed-header-name-rejected",
        b"GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nBad Header: value\r\nConnection: close\r\n\r\n",
        {400})

    add("header-control-character-rejected",
        b"GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nX-Test: good\x00bad\r\nConnection: close\r\n\r\n",
        {400})

    add("excessive-header-count-rejected",
        ("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n" +
         "".join(f"X-H-{i}: v\r\n" for i in range(150)) +
         "Connection: close\r\n\r\n").encode(),
        {400,431})

    add("oversized-cookie-rejected",
        ("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nCookie: session=" + ("C" * 40000) +
         "\r\nConnection: close\r\n\r\n").encode(),
        {400,431})

    add("oversized-content-length-rejected",
        b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 1048577\r\nConnection: close\r\n\r\n",
        {400,413})

    oversized_chunk = b"A" * 1048577
    add("chunked-body-over-limit-rejected",
        (b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n" +
         f"{len(oversized_chunk):X}\r\n".encode("ascii") + oversized_chunk + b"\r\n0\r\n\r\n"),
        {400,413},
        lambda c,h,r: (b"XPSCRIPT-HARDENING" not in r, "oversized chunked body reached application handler"))

    add("encoded-path-control-character-rejected",
        b"GET /hello%00world HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT-HARDENING" not in r, "encoded NUL path reached application handler"))

    add("extreme-query-string-rejected",
        ("GET /?" + ("q=" + ("Q" * 9000)) + " HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n").encode(),
        {400,414})

    add("encoded-traversal-does-not-disclose-secret",
        b"GET /assets/%2e%2e/secret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_SECRET_SENTINEL" not in r, "secret file disclosed"))

    add("double-encoded-traversal-does-not-disclose-secret",
        b"GET /assets/%252e%252e/secret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_SECRET_SENTINEL" not in r, "secret file disclosed"))

    add("mixed-slash-traversal-does-not-disclose-secret",
        b"GET /assets/..%5csecret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_SECRET_SENTINEL" not in r, "secret file disclosed"))

    add("encoded-slash-traversal-does-not-disclose-secret",
        b"GET /assets/%2e%2e%2fsecret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_SECRET_SENTINEL" not in r, "secret file disclosed"))

    add("double-encoded-slash-traversal-does-not-disclose-secret",
        b"GET /assets/%252e%252e%252fsecret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_SECRET_SENTINEL" not in r, "secret file disclosed"))

    traversal_cases = [
        ("triple-encoded-traversal", "/assets/%25252e%25252e/secret.txt"),
        ("nested-dot-traversal", "/assets/....//secret.txt"),
        ("nested-dot-backslash-traversal", "/assets/....%5c%5csecret.txt"),
        ("mixed-encoded-dot-traversal", "/assets/.%2e/secret.txt"),
        ("mixed-case-encoded-traversal", "/assets/%2E%2e/secret.txt"),
        ("double-encoded-backslash-traversal", "/assets/%252e%252e%255csecret.txt"),
        ("overlong-utf8-slash-traversal", "/assets/..%c0%afsecret.txt"),
        ("overlong-utf8-backslash-traversal", "/assets/..%c1%9csecret.txt"),
        ("encoded-null-traversal", "/assets/%2e%2e/secret.txt%00.css"),
        ("double-encoded-null-traversal", "/assets/%252e%252e/secret.txt%2500.css"),
        ("windows-drive-path", "/assets/C:%5cWindows%5cwin.ini"),
        ("unc-style-path", "/assets/%5c%5clocalhost%5cshare%5csecret.txt"),
        ("encoded-unicode-fullwidth-dot", "/assets/%EF%BC%8E%EF%BC%8E/secret.txt"),
        ("encoded-unicode-division-slash", "/assets/..%E2%88%95secret.txt"),
    ]
    for name, path in traversal_cases:
        add(name + "-does-not-disclose-secret",
            f"GET {path} HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n".encode("ascii"),
            {400,404},
            lambda c,h,r: (b"XPSCRIPT_SECRET_SENTINEL" not in r, "secret file disclosed"))

    add("protected-cache-direct-not-disclosed",
        b"GET /.xpscript-cache/protected.css HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_PROTECTED_CACHE_SENTINEL" not in r, "protected cache disclosed"))

    add("protected-cache-traversal-not-disclosed",
        b"GET /assets/%2e%2e/.xpscript-cache/protected.css HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_PROTECTED_CACHE_SENTINEL" not in r, "protected cache disclosed"))

    add("protected-cache-double-encoded-traversal-not-disclosed",
        b"GET /assets/%252e%252e/.xpscript-cache/protected.css HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"XPSCRIPT_PROTECTED_CACHE_SENTINEL" not in r, "protected cache disclosed"))

    add("operational-route-traversal-not-reachable",
        b"GET /assets/%2e%2e/_xps/metrics HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {400,404},
        lambda c,h,r: (b"xpscript_web_requests_total" not in r, "metrics endpoint reached through traversal"))

    add("source-file-not-disclosed",
        b"GET /index.xps HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200,404},
        lambda c,h,r: (
            b"Sub Index" not in r and
            b"[Anonymous]" not in r and
            b"Response.Write" not in r,
            "XPScript source disclosed"))

    add("security-headers-present",
        b"GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n",
        {200},
        lambda c,h,r: (
            "x-content-type-options" in h and
            "x-frame-options" in h and
            "referrer-policy" in h,
            "one or more default security headers missing"))

    return tests

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", required=True)
    parser.add_argument("--port", required=True, type=int)
    parser.add_argument("--json", required=True)
    args = parser.parse_args()

    results = run(args.host, args.port)
    Path(args.json).write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    failed = [x for x in results if not x["passed"]]
    for item in results:
        print(("PASS" if item["passed"] else "FAIL"), item["name"], item["status"], item["detail"])
    raise SystemExit(1 if failed else 0)

if __name__ == "__main__":
    main()
