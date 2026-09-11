from pathlib import Path
p=Path('src/XPScript.UI.Desktop/DesktopImageHost.cs')
s=p.read_text()
s=s.replace('var bytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();\n                ValidateSize(bytes.LongLength);\n                var mime = Path.GetExtension(uri.AbsolutePath)', 'var remoteBytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();\n                ValidateSize(remoteBytes.LongLength);\n                var remoteMime = Path.GetExtension(uri.AbsolutePath)', 1)
s=s.replace('return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);', 'return "data:" + remoteMime + ";base64," + Convert.ToBase64String(remoteBytes);', 1)
p.write_text(s)
print('desktop image scope fix applied')
