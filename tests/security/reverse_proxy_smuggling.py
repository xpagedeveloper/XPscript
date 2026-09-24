import argparse
import json
import socket


def exchange(host, port, payload):
    with socket.create_connection((host, port), timeout=5) as sock:
        sock.settimeout(3)
        sock.sendall(payload)
        data = bytearray()
        try:
            while True:
                chunk = sock.recv(65536)
                if not chunk:
                    break
                data.extend(chunk)
        except socket.timeout:
            pass
        return bytes(data)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--json", required=True)
    args = parser.parse_args()

    probes = {
        "cl-te": (
            b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 4\r\n"
            b"Transfer-Encoding: chunked\r\nConnection: keep-alive\r\n\r\n"
            b"0\r\n\r\nGET /secret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n"
        ),
        "te-cl": (
            b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\n"
            b"Content-Length: 44\r\nConnection: keep-alive\r\n\r\n"
            b"0\r\n\r\nGET /secret.txt HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n"
        ),
    }

    results = []
    failed = False
    for name, payload in probes.items():
        response = exchange(args.host, args.port, payload)
        protected = b"XPSCRIPT-HARDENING" in response or b"secret" in response.lower()
        result = {
            "name": name,
            "passed": not protected,
            "response_status_lines": [
                line.decode("latin-1", "replace")
                for line in response.split(b"\r\n")
                if line.startswith(b"HTTP/")
            ],
        }
        results.append(result)
        if protected:
            failed = True

    with open(args.json, "w", encoding="utf-8") as handle:
        json.dump(results, handle, indent=2)
        handle.write("\n")

    if failed:
        raise SystemExit("Reverse-proxy request-smuggling probe reached protected/application content.")


if __name__ == "__main__":
    main()
