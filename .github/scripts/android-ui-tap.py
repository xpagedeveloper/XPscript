"""Tap a control selected from a UIAutomator hierarchy."""
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

root = ET.parse("android-uiform-ui.xml").getroot()
for node in root.iter("node"):
    if (sys.argv[1] == "field" and node.get("class", "").endswith("EditText")) or (
        sys.argv[1] == "ok" and "OK" in (node.get("text", ""), node.get("content-desc", ""))
    ):
        bounds = list(map(int, re.findall(r"\d+", node.get("bounds", ""))))
        if len(bounds) == 4:
            x1, y1, x2, y2 = bounds
            subprocess.run(["adb", "shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2)], check=True)
            break
else:
    raise SystemExit("UI control not found: " + sys.argv[1])
