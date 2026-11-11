"""Exercise the real Android UI through UIAutomator instrumentation."""
import subprocess
import time
import uiautomator2 as u2

device = u2.connect("emulator-5554")
device.settings["wait_timeout"] = 30
try:
    field = device.xpath(
        '//*[@package="eu.xpscript.app" and '
        '(@content-desc="xps-field-name" or @text="xps-field-name" '
        'or contains(@resource-id,"xps-field-name"))]'
    )
    if not field.wait(timeout=30):
        raise RuntimeError("Android UIForm name editor did not appear")

    # Avalonia's TextBox is a managed view, so use Android input events after
    # focusing it. Clear existing text first; this avoids clipboard-based
    # uiautomator2 set_text, which is blocked on GitHub-hosted emulators.
    field.click()
    subprocess.run(["adb", "shell", "input", "keyevent", "KEYCODE_MOVE_END"], check=True)
    subprocess.run(["adb", "shell", "input", "keyevent", "KEYCODE_DEL"], check=True)
    subprocess.run(["adb", "shell", "input", "text", "AndroidTest"], check=True)
    time.sleep(0.5)
    actual = field.get_text()
    print("UIFORM_FIELD_TEXT=" + actual, flush=True)
    if actual != "AndroidTest":
        raise RuntimeError("Android UIForm name editor did not accept input: " + repr(actual))
    print("UIFORM_TEXT_SET", flush=True)

    device(text="OK").click(timeout=30)
    print("UIFORM_OK_CLICKED", flush=True)
finally:
    hierarchy = device.dump_hierarchy()
    print(hierarchy, flush=True)
    with open("android-uiform-ui.xml", "w", encoding="utf-8") as output:
        output.write(hierarchy)
