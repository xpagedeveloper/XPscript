import subprocess
import sys
import threading
from http.server import ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from httpdb_mock_server import Handler

ROOT = Path(__file__).resolve().parents[2]
COMPILER = ROOT / "src" / "XPScript.Compiler" / "bin" / "Release" / "net10.0" / "xpscriptc.dll"


def main() -> int:
    server = ThreadingHTTPServer(("127.0.0.1", 18082), Handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        for fixture in ("samples/httpdb-supabase.xps", "samples/httpdb-domino.xps"):
            result = subprocess.run(
                ["dotnet", str(COMPILER), "run", str(ROOT / fixture), "--security=off"],
                cwd=ROOT,
                check=False,
            )
            if result.returncode != 0:
                print(f"HTTP_RUNTIME_FIXTURE_FAILED={fixture}", flush=True)
                return result.returncode
            print(f"HTTP_RUNTIME_FIXTURE_OK={fixture}", flush=True)
        print("HTTP_RUNTIME_FIXTURE_BATCH_OK", flush=True)
        return 0
    finally:
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    sys.exit(main())
