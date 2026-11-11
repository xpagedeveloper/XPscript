"""Exercise the real Android UI through UIAutomator instrumentation."""
import uiautomator2 as u2

device = u2.connect("emulator-5554")
device.settings["wait_timeout"] = 30
try:
    field = device(description="xps-field-name")
    if not field.wait(timeout=30):
        raise RuntimeError("Android UIForm name editor did not appear")
    field.set_text("AndroidTest")
    print("UIFORM_TEXT_SET", flush=True)
    device(text="OK").click(timeout=30)
    print("UIFORM_OK_CLICKED", flush=True)
finally:
    with open("android-uiform-ui.xml", "w", encoding="utf-8") as output:
        output.write(device.dump_hierarchy())
