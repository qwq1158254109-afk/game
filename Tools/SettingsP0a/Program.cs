// Isolated host probe, not Unity runtime implementation or Unity Test Runner tests.
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Globalization;

var settings = new JsonSerializerSettings
{
    ContractResolver = new DefaultContractResolver(),
    MissingMemberHandling = MissingMemberHandling.Ignore,
    NullValueHandling = NullValueHandling.Include,
    ObjectCreationHandling = ObjectCreationHandling.Replace,
    TypeNameHandling = TypeNameHandling.None,
    Culture = CultureInfo.InvariantCulture,
    Converters = { new StringEnumConverter { AllowIntegerValues = false } }
};
var failures = 0;
var passed = 0;
void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Require(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void MustReject(string json)
{
    try { JsonConvert.DeserializeObject<ProbeDto>(json, settings); }
    catch (JsonSerializationException) { return; }
    throw new Exception("Expected JsonSerializationException");
}
Console.WriteLine("Host=" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
Console.WriteLine("JsonAssembly=" + typeof(JsonConvert).Assembly.Location);
Console.WriteLine("JsonProductVersion=" + System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(JsonConvert).Assembly.Location).ProductVersion);
Check("JSON-01 explicit properties roundtrip", () =>
{
    var json = JsonConvert.SerializeObject(new ProbeDto(), settings);
    var roundtrip = JsonConvert.DeserializeObject<ProbeDto>(json, settings)!;
    Require(roundtrip.SchemaVersion == 1 && roundtrip.Audio.Music == 1f && roundtrip.Mode == ProbeMode.Windowed);
    Require(json.Contains("\"Windowed\""));
});
Check("JSON-02 optional member default and unknown member", () =>
{
    var dto = JsonConvert.DeserializeObject<ProbeDto>("{\"schemaVersion\":1,\"audio\":{},\"extra\":true}", settings)!;
    Require(dto.Audio.Music == 1f);
});
Check("JSON-03 mandatory object missing rejects", () => MustReject("{\"schemaVersion\":1}"));
Check("JSON-04 mandatory object null rejects", () => MustReject("{\"schemaVersion\":1,\"audio\":null}"));
Check("JSON-05 integer enum rejects", () => MustReject("{\"schemaVersion\":1,\"audio\":{},\"mode\":0}"));
Check("JSON-06 unknown enum rejects", () => MustReject("{\"schemaVersion\":1,\"audio\":{},\"mode\":\"Bogus\"}"));
Check("JSON-07 literal null needs explicit root validation", () =>
    Require(JsonConvert.DeserializeObject<ProbeDto>("null", settings) == null));
Check("JSON-08 nonfinite values need explicit validator", () =>
{
    foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        Require(!float.IsFinite(value));
    var dto = JsonConvert.DeserializeObject<ProbeDto>("{\"schemaVersion\":1,\"audio\":{\"music\":\"NaN\"}}", settings)!;
    Require(!float.IsFinite(dto.Audio.Music));
});
Check("JSON-09 writable filter also retains public fields", () =>
{
    // Reconstructs only the statically observed predicate, not the game assembly.
    var json = JsonConvert.SerializeObject(new WritableMemberProbe(), new JsonSerializerSettings
    { ContractResolver = new WritableOnlyProbeResolver() });
    Require(json.Contains("PublicField") && json.Contains("Property") && !json.Contains("ReadOnly"));
});

if (args.Length != 1) throw new ArgumentException("Pass a workspace-contained artifacts directory.");
var workspace = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"))
    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
var artifacts = Path.GetFullPath(args[0]);
if (!artifacts.StartsWith(workspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Artifacts must stay below the project workspace.");
var run = Path.Combine(artifacts, "run-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(run);
var main = Path.Combine(run, "settings.json");
var temp = Path.Combine(run, "settings.tmp");
var backup = Path.Combine(run, "settings.bak");
Check("IO-01 first creation via same-directory move", () =>
{
    File.WriteAllText(temp, "{\"generation\":1}");
    File.Move(temp, main);
    Require(File.ReadAllText(main) == "{\"generation\":1}");
});
Check("IO-02 replacement with backup on workspace filesystem", () =>
{
    File.WriteAllText(temp, "{\"generation\":2}");
    using (var stream = new FileStream(temp, FileMode.Open, FileAccess.Write, FileShare.None)) stream.Flush(true);
    File.Replace(temp, main, backup);
    Require(File.ReadAllText(main) == "{\"generation\":2}");
    Require(File.ReadAllText(backup) == "{\"generation\":1}");
});
Check("IO-03 known replace failure leaves old main intact", () =>
{
    try { File.Replace(Path.Combine(run, "missing.tmp"), main, backup); }
    catch (IOException) { Require(File.ReadAllText(main) == "{\"generation\":2}"); return; }
    throw new Exception("Expected missing-source failure");
});
Console.WriteLine($"RESULT passed={passed} failed={failures}");
Console.WriteLine("Retained disposable probe data=" + run);
Console.WriteLine("Not tested: Unity/Mono, player persistentDataPath, power loss, disk full, display switching, gameplay.");
return failures == 0 ? 0 : 1;

public enum ProbeMode { Windowed, Borderless }
[JsonObject(MemberSerialization.OptIn)]
public sealed class ProbeDto
{
    [JsonProperty("schemaVersion", Required = Required.Always)] public int SchemaVersion { get; set; } = 1;
    [JsonProperty("audio", Required = Required.Always)] public ProbeAudio Audio { get; set; } = new();
    [JsonProperty("mode")] public ProbeMode Mode { get; set; }
}
[JsonObject(MemberSerialization.OptIn)]
public sealed class ProbeAudio
{
    [JsonProperty("music")] public float Music { get; set; } = 1f;
}
public sealed class WritableMemberProbe
{
    public int PublicField = 1;
    public int Property { get; set; } = 2;
    public int ReadOnly => 3;
}
public sealed class WritableOnlyProbeResolver : DefaultContractResolver
{
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
        => base.CreateProperties(type, serialization).Where(p => p.Writable).ToList();
}
