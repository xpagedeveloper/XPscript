using System;
using System.IO;

var root = Directory.GetParent(AppContext.BaseDirectory);
while (root is not null && !Directory.Exists(Path.Combine(root.FullName, ".git")))
    root = root.Parent;

if (root is null)
    throw new InvalidOperationException("Could not locate the XPscript repository root.");

var fixtures = new[]
{
    "samples/android-uiform-manual-test.xps",
    "samples/android-uiform-media-manual-test.xps",
    "samples/android-uiform-controls-api.xps",
    "samples/browser-wasm-uiform.xps",
    "samples/ui-showcase-web.xps",
    "samples/ui-form-core.xps"
};

foreach (var fixture in fixtures)
    if (!File.Exists(Path.Combine(root.FullName, fixture)))
        throw new FileNotFoundException("UIForm manual fixture is missing.", fixture);

Console.WriteLine("UIFORM_MANUAL_PROJECT_OK");
Console.WriteLine("Fixtures=" + fixtures.Length);
