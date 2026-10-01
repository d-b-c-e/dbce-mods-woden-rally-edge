using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.InstructionSets;
using Cpp2IL.Core.OutputFormats;
using Cpp2IL.Core.ProcessingLayers;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using LibCpp2IL;

// Reads files; does not load GameAssembly into the process or start the game.
if (args.Length != 3) throw new ArgumentException("Usage: <game directory> <Unity version> <empty output directory>");
var game = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[2]);
if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
    throw new IOException("Output must be empty; refusing to mix game builds.");
Directory.CreateDirectory(output);
InstructionSetRegistry.RegisterInstructionSet<X86InstructionSet>(DefaultInstructionSets.X86_64);
LibCpp2IlBinaryRegistry.RegisterBuiltInBinarySupport();
Cpp2IL.Core.Logging.Logger.InfoLog += (message, source) => Console.WriteLine($"[{source}] {message.Trim()}");
Cpp2IlApi.InitializeLibCpp2Il(Path.Combine(game, "GameAssembly.dll"),
    Path.Combine(game, "Super Woden Rally Edge_Data/il2cpp_data/Metadata/global-metadata.dat"), UnityVersion.Parse(args[1]));
var layers = new List<Cpp2IlProcessingLayer> { new AttributeInjectorProcessingLayer() };
foreach (var layer in layers) layer.PreProcess(Cpp2IlApi.CurrentAppContext, layers);
foreach (var layer in layers) layer.Process(Cpp2IlApi.CurrentAppContext);
var assemblies = new AsmResolverDllOutputFormatDefault().BuildAssemblies(Cpp2IlApi.CurrentAppContext);
Il2CppInteropGenerator.Create(new GeneratorOptions {
    GameAssemblyPath = Path.Combine(game, "GameAssembly.dll"),
    Source = assemblies, OutputDir = output, UnityBaseLibsDir = null
}).AddInteropAssemblyGenerator().Run();
Console.WriteLine($"Generated {Directory.GetFiles(output, "*.dll").Length} interop assemblies.");
