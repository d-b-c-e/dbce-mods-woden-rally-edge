using System.Text.Json;
using Woden.ControlsTesting;
if(args.Length!=3) throw new ArgumentException("Expected original.cfg loaded.cfg SHA256(original).");
byte[] Read(string path) { if(new FileInfo(path).Length>1024*1024) throw new InvalidDataException("Config size refused.");return File.ReadAllBytes(path); }
var added=ConfigContract.Verify(Read(args[0]),Read(args[1]),args[2]);
Console.WriteLine(JsonSerializer.Serialize(new{status="passed",originalValues="unchanged",addedDefaults=added}));
