"""Exercise the Android UIForm through ADB's built-in UIAutomator dump."""
import subprocess
import time
import xml.etree.ElementTree as ET
import re

def adb(*args, capture=True):
    result = subprocess.run(["adb", *args], check=True, text=True, capture_output=capture)
    return result.stdout if capture else ""

def hierarchy():
    adb("shell", "uiautomator", "dump", "/sdcard/xpscript-ui.xml")
    return adb("exec-out", "cat", "/sdcard/xpscript-ui.xml")

def bounds(node):
    values = [int(value) for value in re.findall(r"\d+", node.attrib.get("bounds", ""))]
    return values[0], values[1], values[2], values[3]

def find(root, predicate):
    return next((node for node in root.iter("node") if predicate(node)), None)

def wait_for(predicate, label, timeout=45):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        root = ET.fromstring(hierarchy())
        node = find(root, predicate)
        if node is not None:
            print(f"UIFORM_{label}_FOUND", flush=True)
            return node
        time.sleep(2)
    raise RuntimeError(f"Android UIForm {label} did not appear")

field = wait_for(lambda node: node.attrib.get("package") == "eu.xpscript.app" and (node.attrib.get("content-desc") == "xps-field-name" or node.attrib.get("text") == "xps-field-name" or "xps-field-name" in node.attrib.get("resource-id", "")), "FIELD")
x1, y1, x2, y2 = bounds(field)
adb("shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2), capture=False)
adb("shell", "input", "keyevent", "KEYCODE_MOVE_END", capture=False)
adb("shell", "input", "keyevent", "KEYCODE_DEL", capture=False)
adb("shell", "input", "text", "AndroidTest", capture=False)
time.sleep(1)
field = wait_for(lambda node: node.attrib.get("text") == "AndroidTest", "TEXT")
print("UIFORM_FIELD_TEXT=" + field.attrib.get("text", ""), flush=True)
print("UIFORM_TEXT_SET", flush=True)

button = wait_for(lambda node: node.attrib.get("package") == "eu.xpscript.app" and node.attrib.get("text") == "OK", "OK_BUTTON")
x1, y1, x2, y2 = bounds(button)
adb("shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2), capture=False)
print("UIFORM_OK_CLICKED", flush=True)
with open("android-uiform-ui.xml", "w", encoding="utf-8") as output:
    output.write(hierarchy())
