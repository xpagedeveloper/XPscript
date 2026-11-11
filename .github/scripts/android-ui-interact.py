"""Exercise the real Android UI through UIAutomator instrumentation."""
import subprocess
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
    field.click()
    subprocess.run(["adb", "shell", "input", "text", "AndroidTest"], check=True)
    print("UIFORM_TEXT_SET", flush=True)
    device(text="OK").click(timeout=30)
    print("UIFORM_OK_CLICKED", flush=True)
finally:
    hierarchy = device.dump_hierarchy()
    print(hierarchy, flush=True)
    with open("android-uiform-ui.xml", "w", encoding="utf-8") as output:
        output.write(hierarchy)
