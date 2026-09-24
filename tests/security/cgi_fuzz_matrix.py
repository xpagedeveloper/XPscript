import argparse
import json
import os
import subprocess
from pathlib import Path
from urllib.parse import parse_qsl, urlencode


PAYLOADS = [
    "../../../etc/passwd",
    "%252e%252e%252fetc%252fpasswd",
    "..%2f..%2f..%2fwindows/win.ini",
    "..%5c..%5c..%5cwindows%5cwin.ini",
    "%00",
    "%c0%ae%c0%ae/%c0%ae%c0%ae/etc/passwd",
]

HEADER_CASES = [
    {},
    {"HTTP_X_FORWARDED_FOR": "127.0.0.1, 10.0.0.1"},
    {"HTTP_X_ORIGINAL_URL": "/_xps/metrics"},
]


def run_case(cgi_dll, root, script, path, query, headers):
    env = os.environ.copy()
    env.update({
        "XPSCRIPT_WEB_ROOT": str(root),
        "XPSCRIPT_SITE_ID": "cgi-fuzz-matrix",
        "REQUEST_METHOD": "GET",
        "SCRIPT_NAME": path,
        "SCRIPT_FILENAME": str(script),
        "QUERY_STRING": query,
        "SERVER_NAME": "localhost",
        "SERVER_PROTOCOL": "HTTP/1.1",
        "REMOTE_ADDR": "127.0.0.1",
        "HTTPS": "off",
    })
    env.update(headers)
    proc = subprocess.run(
        ["dotnet", str(cgi_dll)],
        input=b"",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env,
        timeout=15,
        check=False,
    )
    text = proc.stdout.decode("utf-8", "replace")
    lowered = text.lower()
    leaked = any(marker in lowered for marker in (
        "root:x:",
        "[fonts]",
        "xpscript_secret_sentinel",
        "xpscript_protected_cache_sentinel",
        "stacktrace",
        "/home/runner/",
    ))
    return {
        "path": path,
        "query": query,
        "headers": headers,
        "exit_code": proc.returncode,
        "status": text.split("\r\n", 1)[0] if text else "",
        "passed": not leaked,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cgi-dll", required=True)
    parser.add_argument("--web-root", required=True)
    parser.add_argument("--rest-root", required=True)
    parser.add_argument("--json", required=True)
    args = parser.parse_args()

    cgi_dll = Path(args.cgi_dll).resolve()
    targets = [
        ("web", Path(args.web_root).resolve(), "/", "q"),
        ("rest", Path(args.rest_root).resolve(), "/api/fuzz", "q"),
    ]
    results = []
    failed = False
    for kind, root, path, parameter in targets:
        script = root / "index.xps"
        for payload in PAYLOADS:
            result = run_case(cgi_dll, root, script, path, urlencode([(parameter, payload)]), {})
            result["target"] = kind
            results.append(result)
            failed |= not result["passed"]
        for headers in HEADER_CASES:
            result = run_case(cgi_dll, root, script, path, "q=baseline", headers)
            result["target"] = kind
            results.append(result)
            failed |= not result["passed"]

    Path(args.json).write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    if failed:
        raise SystemExit("CGI fuzz matrix exposed protected or diagnostic content.")


if __name__ == "__main__":
    main()
