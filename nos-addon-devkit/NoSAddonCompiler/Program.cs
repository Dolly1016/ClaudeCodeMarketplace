using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NoSAddonCompiler;

internal static class Program
{
    record AddonMeta(
        string Id = "",
        string[] Dependency = null!,
        bool Hidden = false);

    record AddonBehaviour(
        bool UseHiddenMembers = false,
        bool LoadRoles = false);

    static int Main(string[] args)
    {
        string? addonDir = null;
        string? gameDir = null;
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--addon-dir":
                    if (i + 1 < args.Length) addonDir = args[++i];
                    break;
                case "--game-dir":
                    if (i + 1 < args.Length) gameDir = args[++i];
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
            }
        }

        if (addonDir == null || gameDir == null)
        {
            PrintHelp();
            return -1;
        }

        if (!Directory.Exists(addonDir))
        {
            Console.Error.WriteLine($"Error: addon directory not found: {addonDir}");
            return -1;
        }

        string metaPath = Path.Combine(addonDir, "addon.meta");
        if (!File.Exists(metaPath))
        {
            Console.Error.WriteLine($"Error: addon.meta not found in: {addonDir}");
            Console.Error.WriteLine("--addon-dir must point to the directory that directly contains addon.meta.");
            return -1;
        }

        if (!Directory.Exists(gameDir))
        {
            Console.Error.WriteLine($"Error: game directory not found: {gameDir}");
            return -1;
        }

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Read addon.meta
        AddonMeta? meta;
        try
        {
            meta = JsonSerializer.Deserialize<AddonMeta>(File.ReadAllText(metaPath, Encoding.UTF8), jsonOpts);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: failed to parse addon.meta: {ex.Message}");
            return -1;
        }
        if (meta == null || string.IsNullOrEmpty(meta.Id))
        {
            Console.Error.WriteLine("Error: addon.meta is missing a valid Id field.");
            return -1;
        }
        string addonId = meta.Id;
        string[] dependencies = meta.Dependency ?? [];

        // Read Scripts/.behaviour (optional)
        string behaviourPath = Path.Combine(addonDir, "Scripts", ".behaviour");
        AddonBehaviour behaviour = new();
        if (File.Exists(behaviourPath))
        {
            try
            {
                behaviour = JsonSerializer.Deserialize<AddonBehaviour>(
                    File.ReadAllText(behaviourPath, Encoding.UTF8), jsonOpts) ?? new();
            }
            catch
            {
                Console.Error.WriteLine("Warning: failed to parse Scripts/.behaviour — using defaults.");
            }
        }

        Console.WriteLine($"Addon:            {addonId}");
        Console.WriteLine($"Dependencies:     {(dependencies.Length == 0 ? "(none)" : string.Join(", ", dependencies))}");
        Console.WriteLine($"UseHiddenMembers: {behaviour.UseHiddenMembers}");

        // Collect game assembly references
        var references = CollectReferences(gameDir, verbose);
        if (references.Count == 0)
        {
            Console.Error.WriteLine("Error: no assemblies found. Is NoS installed in the specified game directory?");
            return -1;
        }

        // Collect dependency DLLs from Cache/Dll/
        var depRefs = CollectDependencyReferences(gameDir, dependencies, jsonOpts, verbose);
        references.AddRange(depRefs);

        // Collect local Libraries/*.dll
        string localLibsDir = Path.Combine(addonDir, "Libraries");
        if (Directory.Exists(localLibsDir))
        {
            foreach (var dll in Directory.GetFiles(localLibsDir, "*.dll"))
            {
                references.Add(dll);
                if (verbose) Console.WriteLine($"  local lib: {dll}");
            }
        }

        string scriptsDir = Path.Combine(addonDir, "Scripts");
        if (!Directory.Exists(scriptsDir))
        {
            Console.Error.WriteLine($"Error: Scripts directory not found in: {addonDir}");
            return -1;
        }

        Console.WriteLine($"References:       {references.Count} assemblies");
        return Compile(addonId, scriptsDir, references, behaviour.UseHiddenMembers) ? 0 : 1;
    }

    static void PrintHelp()
    {
        Console.WriteLine("NoSAddonCompiler - Compile Nebula on the Ship addon scripts without launching the game");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  NoSAddonCompiler --addon-dir <path> --game-dir <path> [options]");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  --addon-dir <path>   Directory directly containing addon.meta (and Scripts/ subfolder)");
        Console.WriteLine("  --game-dir  <path>   Among Us game directory (containing Among Us.exe, with NoS installed)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --verbose            List all referenced assemblies");
        Console.WriteLine("  --help, -h           Show this help");
        Console.WriteLine();
        Console.WriteLine("UseHiddenMembers is read automatically from Scripts/.behaviour in the addon directory.");
        Console.WriteLine("Dependency DLLs are resolved from Cache/Dll/ using HandshakeHash of each dependency's ZIP.");
        Console.WriteLine();
        Console.WriteLine("Exit codes: 0 = success, 1 = compile error, -1 = invalid arguments");
    }

    static List<string> CollectReferences(string gameDir, bool verbose)
    {
        // Primary: use references.txt generated by the game at startup.
        // It contains exactly what AppDomain has loaded — all managed assemblies,
        // no native DLLs, and includes the bundled dotnet/ BCL.
        string refsFile = Path.Combine(gameDir, "Cache", "Dll", "references.txt");
        if (File.Exists(refsFile))
        {
            Console.WriteLine($"Using reference list from: {refsFile}");
            var refs = File.ReadAllLines(refsFile)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && File.Exists(l))
                .ToList();
            if (verbose) foreach (var r in refs) Console.WriteLine($"  game ref: {r}");
            return refs;
        }

        // Fallback: scan known directories (requires game to have been run once
        // for dotnet/ BCL to be extracted and references.txt to exist).
        Console.Error.WriteLine(
            $"Warning: {refsFile} not found. " +
            "Run Among Us with NoS at least once to generate it. Falling back to directory scan.");

        var fallbackRefs = new List<string>();

        void AddDir(string subDir)
        {
            string path = Path.Combine(gameDir, subDir);
            if (!Directory.Exists(path)) return;
            foreach (var dll in Directory.GetFiles(path, "*.dll"))
            {
                if (IsManagedAssembly(dll))
                {
                    fallbackRefs.Add(dll);
                    if (verbose) Console.WriteLine($"  game ref: {dll}");
                }
            }
        }

        void AddFile(string relPath)
        {
            string path = Path.Combine(gameDir, relPath);
            if (File.Exists(path)) fallbackRefs.Add(path);
            else Console.Error.WriteLine($"Warning: assembly not found: {path}");
        }

        Console.WriteLine("Scanning game directory for reference assemblies...");
        AddDir("dotnet");
        AddDir(Path.Combine("BepInEx", "core"));
        AddDir(Path.Combine("BepInEx", "interop"));
        AddDir(Path.Combine("BepInEx", "unity-libs"));
        AddFile(Path.Combine("BepInEx", "nebula", "Nebula.dll"));
        AddFile(Path.Combine("NebulaLibs", "NebulaAPI.dll"));

        return fallbackRefs;
    }

    // Checks for the CLR header in the PE optional header data directories.
    static bool IsManagedAssembly(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // MZ header
            if (br.ReadUInt16() != 0x5A4D) return false;

            // e_lfanew at offset 0x3C
            fs.Seek(0x3C, SeekOrigin.Begin);
            int peOffset = br.ReadInt32();

            fs.Seek(peOffset, SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x00004550) return false; // "PE\0\0"

            // Skip COFF header (20 bytes), then read optional header magic
            fs.Seek(20, SeekOrigin.Current);
            ushort magic = br.ReadUInt16();

            // Data directories start at offset 96 (PE32) or 112 (PE32+) from optional header start
            int ddOffset = magic == 0x20B ? 112 : 96;
            fs.Seek(peOffset + 24 + ddOffset + 14 * 8, SeekOrigin.Begin); // entry 14 = CLR runtime header
            uint clrRva = br.ReadUInt32();
            uint clrSize = br.ReadUInt32();

            return clrRva != 0 && clrSize != 0;
        }
        catch
        {
            return false;
        }
    }

    static List<string> CollectDependencyReferences(
        string gameDir,
        string[] directDepIds,
        JsonSerializerOptions jsonOpts,
        bool verbose)
    {
        var result = new List<string>();
        if (directDepIds.Length == 0) return result;

        string cacheDir = Path.Combine(gameDir, "Cache", "Dll");
        string addonsDir = Path.Combine(gameDir, "Addons");

        if (!Directory.Exists(cacheDir))
        {
            Console.Error.WriteLine($"Warning: cache directory not found: {cacheDir}");
            return result;
        }

        if (!Directory.Exists(addonsDir))
        {
            Console.Error.WriteLine($"Warning: Addons directory not found: {addonsDir}");
            return result;
        }

        // Build map: addonId → (zipPath, AddonMeta) by scanning all ZIPs in Addons/
        var zipById = new Dictionary<string, (string ZipPath, AddonMeta Meta)>(StringComparer.OrdinalIgnoreCase);
        foreach (var zipPath in Directory.GetFiles(addonsDir, "*.zip"))
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.GetEntry("addon.meta");
                if (entry == null) continue;

                using var stream = entry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var parsed = JsonSerializer.Deserialize<AddonMeta>(reader.ReadToEnd(), jsonOpts);
                if (parsed?.Id != null)
                    zipById[parsed.Id] = (zipPath, parsed);
            }
            catch { /* skip malformed ZIPs */ }
        }

        // BFS over transitive dependency graph
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(directDepIds);

        while (queue.Count > 0)
        {
            string depId = queue.Dequeue();
            if (!visited.Add(depId)) continue;

            if (!zipById.TryGetValue(depId, out var entry))
            {
                Console.Error.WriteLine($"Warning: ZIP for dependency '{depId}' not found in Addons/. Skipping.");
                continue;
            }

            string hash36;
            try
            {
                using var md5 = MD5.Create();
                using var zipStream = File.OpenRead(entry.ZipPath);
                hash36 = ToBase36(ComputeConstantHash(BitConverter.ToString(md5.ComputeHash(zipStream))));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: failed to compute hash for '{depId}': {ex.Message}");
                continue;
            }

            // Main addon DLL
            string mainDll = Path.Combine(cacheDir, $"{depId}_{hash36}.dll");
            if (File.Exists(mainDll))
            {
                result.Add(mainDll);
                if (verbose) Console.WriteLine($"  dep ref [{depId}]: {mainDll}");
            }
            else
            {
                Console.Error.WriteLine(
                    $"Warning: cached DLL for dependency '{depId}' not found ({Path.GetFileName(mainDll)}). " +
                    "Has the game loaded this addon at least once?");
            }

            // Library DLLs of this dependency
            foreach (var libDll in Directory.GetFiles(cacheDir, $"{depId}_{hash36}_*.dll"))
            {
                result.Add(libDll);
                if (verbose) Console.WriteLine($"  dep lib ref [{depId}]: {libDll}");
            }

            // Enqueue transitive dependencies
            foreach (var transitiveDep in entry.Meta.Dependency ?? [])
            {
                if (!visited.Contains(transitiveDep))
                    queue.Enqueue(transitiveDep);
            }
        }

        return result;
    }

    // Replicates Helpers.ComputeConstantHash from NebulaPluginNova
    static int ComputeConstantHash(string str)
    {
        const long MulPrime = 467;
        const long SurPrime = 2147283659;
        long val = 0;
        foreach (char c in str)
        {
            val *= MulPrime;
            val += c;
            val %= SurPrime;
        }
        return (int)(val % SurPrime);
    }

    // Replicates Helpers.ToBase36 from NebulaPluginNova
    static string ToBase36(int value)
    {
        const string Chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        uint uval = (uint)value;
        if (uval == 0) return "0";

        char[] buffer = new char[7];
        int index = 6;
        while (uval > 0)
        {
            buffer[index--] = Chars[(int)(uval % 36)];
            uval /= 36;
        }
        return new string(buffer, index + 1, 6 - index);
    }

    static bool Compile(string addonId, string scriptsDir, List<string> references, bool useHiddenMembers)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithUsings("Virial", "Virial.Compat", "System", "System.Linq", "System.Collections.Generic")
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithMetadataImportOptions(MetadataImportOptions.All);

        var trees = new List<SyntaxTree>();
        var csFiles = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories);

        if (csFiles.Length == 0)
        {
            Console.Error.WriteLine("Error: no .cs files found in Scripts directory.");
            return false;
        }

        Console.WriteLine($"Compiling {csFiles.Length} source file(s) from: {scriptsDir}");

        foreach (var file in csFiles)
        {
            string source = File.ReadAllText(file, Encoding.UTF8);
            string relativePath = Path.GetRelativePath(scriptsDir, file);
            trees.Add(CSharpSyntaxTree.ParseText(source, parseOptions, relativePath, Encoding.UTF8));
        }

        string moduleName = "Script." + char.ToUpperInvariant(addonId[0]) + addonId[1..];

        if (useHiddenMembers)
        {
            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions)
                .GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (topLevelBinderFlagsProperty != null)
                topLevelBinderFlagsProperty.SetValue(compilationOptions, (uint)1 << 22);

            trees.Add(CSharpSyntaxTree.ParseText(
                "[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"Nebula\")]\n" +
                "[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"NebulaAPI\")]",
                parseOptions, "", Encoding.UTF8));
        }

        var metadataRefs = references
            .Where(r => !string.IsNullOrWhiteSpace(r) && File.Exists(r))
            .Select(r => MetadataReference.CreateFromFile(r))
            .ToList();

        var compilation = CSharpCompilation.Create(moduleName, trees, metadataRefs, compilationOptions);

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);

        int errors = 0, warnings = 0;
        foreach (var diag in result.Diagnostics)
        {
            if (diag.Severity < DiagnosticSeverity.Warning) continue;

            var span = diag.Location.GetLineSpan();
            string file = span.Path;
            int line = span.StartLinePosition.Line + 1;
            int col = span.StartLinePosition.Character + 1;
            string severity = diag.Severity == DiagnosticSeverity.Error ? "error" : "warning";

            Console.WriteLine($"{file}({line},{col}): {severity} {diag.Id}: {diag.GetMessage()}");

            if (diag.Severity == DiagnosticSeverity.Error) errors++;
            else warnings++;
        }

        Console.WriteLine();
        if (result.Success)
        {
            Console.WriteLine($"Build succeeded. {warnings} warning(s).");
            return true;
        }
        else
        {
            Console.WriteLine($"Build FAILED. {errors} error(s), {warnings} warning(s).");
            return false;
        }
    }
}
