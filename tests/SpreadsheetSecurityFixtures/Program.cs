using System.IO.Compression;
using System.Text;
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "./out/spreadsheet-security-fixtures");
Directory.CreateDirectory(output);
File.WriteAllBytes(Path.Combine(output, "malformed.xlsx"), Encoding.UTF8.GetBytes("not-an-xlsx"));
CreateZip("missing-workbook.xlsx", a => AddText(a, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>"));
CreateZip("too-many-parts.xlsx", a => { AddText(a, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>"); AddText(a, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"/>"); for (var i=0;i<4096;i++) AddText(a,$"padding/{i:D4}.txt","x"); });
CreateZip("oversized-part.xlsx", a => { AddText(a, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>"); AddText(a, "xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"/>"); var e=a.CreateEntry("xl/oversized.bin",CompressionLevel.SmallestSize); using var s=e.Open(); var block=new byte[1024*1024]; for(var i=0;i<17;i++) s.Write(block); });
Console.WriteLine("SPREADSHEET-SECURITY-FIXTURES=OK");
void CreateZip(string name, Action<ZipArchive> populate){using var s=File.Create(Path.Combine(output,name));using var a=new ZipArchive(s,ZipArchiveMode.Create,false);populate(a);}
static void AddText(ZipArchive a,string path,string text){var e=a.CreateEntry(path,CompressionLevel.SmallestSize);using var w=new StreamWriter(e.Open(),new UTF8Encoding(false));w.Write(text);}
