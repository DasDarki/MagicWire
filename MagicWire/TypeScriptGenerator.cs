using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace MagicWire;

/// <summary>
/// The TypeScript generator is responsible for generating TypeScript code from the C# types.
/// </summary>
public sealed class TypeScriptGenerator
{
    /// <summary>
    /// Checks arguments for the presence of the "--gen-ts" flag and generates TypeScript code if found.
    /// </summary>
    internal static void CheckAndGenerate()
    {
        var args = Environment.GetCommandLineArgs();
        var generate = false;
        var outputPath = Path.Combine(Environment.CurrentDirectory, "ts-output");
        Mode? mode = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--gen-ts"))
            {
                generate = true;
                if (arg.StartsWith("--gen-ts="))
                {
                    var path = arg["--gen-ts=".Length..];
                    if (Directory.Exists(path))
                    {
                        outputPath = path;
                    }
                }
            }
            else if (arg.StartsWith("--mode"))
            {
                var modeStr = arg["--mode=".Length..];
                if (Enum.TryParse<Mode>(modeStr, true, out var parsedMode))
                {
                    mode = parsedMode;
                }
            }
            else if (arg.StartsWith("--help"))
            {
                Console.WriteLine("Usage: --gen-ts=<output-path> --mode=<mode>");
                Console.WriteLine("  --gen-ts: Specify the output path for TypeScript files.");
                Console.WriteLine("  --mode: Specify the compatibility mode (e.g., Vue).");
                Environment.Exit(0);
            }
        }
        
        if (generate)
        {
            Generate(outputPath, mode);
            
            Environment.Exit(0);
        }
    }
    
    /// <summary>
    /// Generates TypeScript code from the given assembly and writes it to the specified output path.
    /// </summary>
    public static void Generate(string? outputPath = null, Mode? mode = null, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        outputPath ??= Path.Combine(Environment.CurrentDirectory, "ts-output");
        
        var generator = new TypeScriptGenerator(outputPath, mode);
        
        foreach (var clazz in assembly.GetTypes())
        {
            if (clazz.IsDefined(typeof(WireAttribute), true))
            {
                generator.AddWireClass(clazz);
            }
        }
        
        generator.Write();
    }
    
    private readonly Mode? _mode;
    private readonly string _output;
    private readonly Dictionary<Type, string> _dtoTypes = [];
    private readonly List<string> _outside = [];
    private readonly List<string> _inside = [];
    
    private TypeScriptGenerator(string output, Mode? mode)
    {
        _output = output;
        _mode = mode;
    }

    private void AddWireClass(Type type)
    {
        var output = type.IsDefined(typeof(StandaloneAttribute), true) ? _outside : _inside;
        var name = type.Name;
        var instanceName = name;
        if (type.IsDefined(typeof(WireNameAttribute), true))
        {
            var instanceNameAttribute = type.GetCustomAttribute<WireNameAttribute>(true);
            if (instanceNameAttribute != null)
            {
                instanceName = instanceNameAttribute.Name;
            }
        }

        name = "W" + name;

        var fields = new List<Tuple<string, string, string, string>>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!field.IsDefined(typeof(WireAttribute), true))
            {
                continue;
            }
            
            var fieldName = field.Name;
            var propName = fieldName.TrimStart('_');
            propName = propName[..1].ToUpper() + propName[1..];
            var frontendName = propName;
            var attrName = field.GetCustomAttribute<WireNameAttribute>();
            if (attrName != null)
            {
                frontendName = attrName.Name;
            }
            
            fields.Add(new Tuple<string, string, string, string>(propName, fieldName, frontendName, CSharpTypeToTypeScriptType(field.FieldType)));
        }
        
        output.Add($"export class {name} {{");
        output.Add($"  private readonly _cName = \"{type.Name}\";");
        output.Add($"  private readonly _evs = new Map<string, (...data: any[]) => void|Promise<void>>();");
        foreach (var field in fields)
        {
            if (_mode == Mode.Vue)
            {
                output.Add($"  private _w{field.Item1} = ref<{field.Item4}>(null!);");
                output.Add($"  public get {field.Item3}() {{ return this._w{field.Item1}.value; }}");
                output.Add($"  public get {field.Item3}Ref() {{ return this._w{field.Item1}; }}");
            }
            else
            {
                output.Add($"  private _w{field.Item1}: {field.Item4} = null!;");
                output.Add($"  public get {field.Item3}() {{ return this._w{field.Item1}; }}");
            }
        }
        
        output.Add("");
        output.Add($"  public __setProperty(name: string, value: any) {{");
        output.Add($"    switch (name) {{");
        foreach (var field in fields)
        {
            output.Add($"      case \"{field.Item1}\":");

            if (_mode == Mode.Vue)
            {
                output.Add($"        this._w{field.Item1}.value = value;");
            }
            else
            {
                output.Add($"        this._w{field.Item1} = value;");
            }
            
            output.Add($"        break;");
        }
        
        output.Add($"      default:");
        output.Add($"        console.warn(`Unknown property: ${name}`);");
        output.Add($"        break;");
        output.Add($"    }}");
        output.Add("  }");
        output.Add("");
        output.Add($"  public __callEvent(name: string, data: any) {{");
        output.Add($"    const ev = this._evs.get(name);");
        output.Add($"    if (ev) {{");
        output.Add($"      ev(...data);");
        output.Add($"    }}");
        output.Add($"  }}");
        output.Add("");

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!method.IsDefined(typeof(WireAttribute), true))
            {
                continue;
            }
            
            var methodName = method.Name;
            var frontendName = methodName;
            var attrName = method.GetCustomAttribute<WireNameAttribute>();
            if (attrName != null)
            {
                frontendName = attrName.Name;
            }

            var paras = method.GetParameters().Where(x => !typeof(IFrontend).IsAssignableFrom(x.ParameterType)).ToList();
            var parameterList = (from para in paras
                let paraName = para.Name
                let paraType = CSharpTypeToTypeScriptType(para.ParameterType)
                select $"{paraName}: {paraType}").ToList();

            if (method.IsDefined(typeof(ToClientAttribute)))
            {
                output.Add($"  public on{frontendName}(callback: ({string.Join(", ", parameterList)}) => void|Promise<void>) {{");
                output.Add($"    this._evs.set(\"{frontendName}\", callback as any);");
                output.Add($"    return this;");
                output.Add($"  }}");
                output.Add($"  public off{frontendName}() {{");
                output.Add($"    this._evs.delete(\"{frontendName}\");");
                output.Add($"    return this;");
                output.Add($"  }}");
            }
            else
            {
                output.Add($"  public async {frontendName}({string.Join(", ", parameterList)}): Promise<{(method.ReturnType == typeof(void) ? "void" : CSharpTypeToTypeScriptType(method.ReturnType))}> {{");
                output.Add($"    const data = await post(\"/.well-known/wire/objects/{type.Name}/invoke/{methodName}\", [");
                output.AddRange(paras.Select(para => para.Name).Select(paraName => $"      {paraName},"));
                output.Add($"    ]);");
                if (method.ReturnType != typeof(void))
                {
                    output.Add($"    return data;");
                }
                output.Add($"  }}");
            }
        }
        
        output.Add("}");
        output.Add($"export const {instanceName} = new {name}();");
        output.Add($"objMap.set(\"{type.Name}\", {instanceName});");
    }

    private void Write()
    {
        var sourceBuilder = new StringBuilder();
        
        sourceBuilder.AppendLine("// This file is auto-generated. Do not edit it directly.");

        if (_mode == Mode.Vue)
        {
            sourceBuilder.AppendLine("import {ref} from \"vue\";");
        }
        
        sourceBuilder.AppendLine("let _stk = \"\";");
        sourceBuilder.AppendLine("let _baseUrl = \"\";");
        sourceBuilder.AppendLine("let _fetch: (url: string, init?: RequestInit) => Promise<Response> = fetch;");
        sourceBuilder.AppendLine("let _eventSource: EventSource;");
        sourceBuilder.AppendLine("const objMap = new Map<string, any>();");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("export function setBaseUrl(url: string) {");
        sourceBuilder.AppendLine("  _baseUrl = url.endsWith(\"/\") ? url.substring(0, url.length - 1) : url;");
        sourceBuilder.AppendLine("}");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("export function setFetch(fetch: (url: string, init?: RequestInit) => Promise<Response>) {");
        sourceBuilder.AppendLine("  _fetch = fetch;");
        sourceBuilder.AppendLine("}");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("function connectSSE() {");
        sourceBuilder.AppendLine("  _eventSource = new EventSource(_baseUrl + \"/.well-known/wire/stream?stk=\" + _stk);");
        sourceBuilder.AppendLine("  _eventSource.onmessage = (e) => {");
        sourceBuilder.AppendLine("    const ms = e.data.split(\"|\");");
        sourceBuilder.AppendLine("    const type = ms[0];");
        sourceBuilder.AppendLine("    const objName = ms[1];");
        sourceBuilder.AppendLine("    const actName = ms[2];");
        sourceBuilder.AppendLine("    const rawData = ms.slice(3).join(\"|\");");
        sourceBuilder.AppendLine("    const data = rawData === \"null\" ? null : JSON.parse(rawData);");
        sourceBuilder.AppendLine("    const obj = objMap.get(objName);");
        sourceBuilder.AppendLine("    if (obj) {");
        sourceBuilder.AppendLine("      if (type === \"PC\") {");
        sourceBuilder.AppendLine("        obj.__setProperty(actName, data);");
        sourceBuilder.AppendLine("      } else if (type === \"EV\") {");
        sourceBuilder.AppendLine("        obj.__callEvent(actName, data);");
        sourceBuilder.AppendLine("      }");
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine("  };");
        sourceBuilder.AppendLine("  _eventSource.onerror = (e) => {");
        sourceBuilder.AppendLine("    console.error(\"SSE error\", e);");
        sourceBuilder.AppendLine("    _eventSource.close();");
        sourceBuilder.AppendLine("    setTimeout(() => {");
        sourceBuilder.AppendLine("      connectSSE();");
        sourceBuilder.AppendLine("    }, 1000);");
        sourceBuilder.AppendLine("  };");
        sourceBuilder.AppendLine("}");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("async function post(url: string, data?: any, init = false) {");
        sourceBuilder.AppendLine("  const response = await _fetch(_baseUrl + url, {");
        sourceBuilder.AppendLine("    method: \"POST\",");
        sourceBuilder.AppendLine("    headers: {");
        sourceBuilder.AppendLine("      \"Content-Type\": \"application/json\",");
        sourceBuilder.AppendLine("      \"X-Session\": _stk,");
        sourceBuilder.AppendLine("    },");
        sourceBuilder.AppendLine("    body: data ? JSON.stringify(data) : undefined,");
        sourceBuilder.AppendLine("  });");
        sourceBuilder.AppendLine("  if (!response.ok) {");
        sourceBuilder.AppendLine("    throw new Error(`HTTP error! status: ${response.status}`);");
        sourceBuilder.AppendLine("  }");
        sourceBuilder.AppendLine("  const rdata = await response.json();");
        sourceBuilder.AppendLine("  if (init) {");
        sourceBuilder.AppendLine("    _stk = rdata[\"__w__Session\"];");
        sourceBuilder.AppendLine("    if (!_stk) {");
        sourceBuilder.AppendLine("      throw new Error(\"Session ID not found in response.\");");
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine("  }");
        sourceBuilder.AppendLine("  return rdata;");
        sourceBuilder.AppendLine("}");
        sourceBuilder.AppendLine();
        
        foreach (var (type, name) in _dtoTypes)
        {
            foreach (var line in type.IsEnum ? AddEnum(type, name) : AddInterface(type, name))
            {
                sourceBuilder.AppendLine(line);
            }
            sourceBuilder.AppendLine();
        }
        
        foreach (var line in _outside)
        {
            sourceBuilder.AppendLine(line);
        }
        
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace wire {");
        sourceBuilder.AppendLine("  export async function initialize() {");
        sourceBuilder.AppendLine("    const data = await post(\"/.well-known/wire/init\", undefined, true) as { [key: string]: any };");
        sourceBuilder.AppendLine("    for (const entry of Object.entries(data)) {");
        sourceBuilder.AppendLine("      const [objName, objState] = entry;");
        sourceBuilder.AppendLine("      const obj = objMap.get(objName);");
        sourceBuilder.AppendLine("      if (obj) {");
        sourceBuilder.AppendLine("        for (const [propName, propValue] of Object.entries(objState)) {");
        sourceBuilder.AppendLine("          obj.__setProperty(propName, propValue);");
        sourceBuilder.AppendLine("        }");
        sourceBuilder.AppendLine("      }");
        sourceBuilder.AppendLine("    }");
        sourceBuilder.AppendLine("    connectSSE();");
        sourceBuilder.AppendLine("  }");
        sourceBuilder.AppendLine();
        
        foreach (var line in _inside)
        {
            sourceBuilder.AppendLine("  " + line);
        }
        
        sourceBuilder.AppendLine("}");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("export default wire;");
        
        var fileName = Path.Combine(_output, "magic-wire.ts");
        if (!Directory.Exists(_output))
        {
            Directory.CreateDirectory(_output);
        }
        
        File.WriteAllText(fileName, sourceBuilder.ToString());
    }

    private string AddDtoType(Type type)
    {
        if (_dtoTypes.TryGetValue(type, out var dtoType))
        {
            return dtoType;
        }

        var appendNs = _dtoTypes.Values.Any(v => v == type.Name);
        var name = type.Name;
        if (appendNs)
        {
            var ns = type.Namespace;
            if (ns != null)
            {
                name = ns.Replace('.', '_') + "_" + name;
            }
        }
        
        _dtoTypes[type] = name;

        return name;
    }
    
    private IEnumerable<string> AddEnum(Type type, string name)
    {
        yield return "export enum " + name + " {";
        
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = field.GetRawConstantValue();
            if (value != null)
            {
                yield return $"  {field.Name} = {value},";
            }
            else
            {
                yield return $"  {field.Name},";
            }
        }
        
        yield return "}";
    }

    private IEnumerable<string> AddInterface(Type type, string name)
    {
        yield return "export interface " + name + " {";
        
        foreach (var property in GetInterfaceBody(type))
        {
            yield return "  " + property;
        }
        
        yield return "}";
    }

    private IEnumerable<string> GetInterfaceBody(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.IsDefined(typeof(JsonIgnoreAttribute), true))
            {
                continue;
            }
            
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }
            
            var propertyType = property.PropertyType;
            var realType = CSharpTypeToTypeScriptType(propertyType);
            var propertyName = property.Name;
            var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (jsonPropertyName != null)
            {
                propertyName = jsonPropertyName.Name;
            }
            
            yield return $"{propertyName}: {realType};";
        }
        
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsDefined(typeof(JsonIgnoreAttribute), true))
            {
                continue;
            }
            
            var fieldType = field.FieldType;
            var realType = CSharpTypeToTypeScriptType(fieldType);
            var fieldName = field.Name;
            var jsonPropertyName = field.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (jsonPropertyName != null)
            {
                fieldName = jsonPropertyName.Name;
            }
            
            yield return $"  {fieldName}: {realType};";
        }
    }
    
    private string CSharpTypeToTypeScriptType(Type type)
    {
        if (type.IsEnum || type.IsClass || type is { IsValueType: true, IsPrimitive: false })
        {
            return AddDtoType(type);
        }
        
        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            if (genericType == typeof(Nullable<>))
            {
                return CSharpTypeToTypeScriptType(type.GenericTypeArguments[0]) + " | null";
            }

            if (genericType == typeof(List<>))
            {
                return CSharpTypeToTypeScriptType(type.GenericTypeArguments[0]) + "[]";
            }

            if (genericType == typeof(Dictionary<,>))
            {
                return $"{{ [key: string]: {CSharpTypeToTypeScriptType(type.GenericTypeArguments[1])} }}";
            }
            
            if (genericType == typeof(IList<>))
            {
                return CSharpTypeToTypeScriptType(type.GenericTypeArguments[0]) + "[]";
            }
            
            if (genericType == typeof(ICollection<>))
            {
                return CSharpTypeToTypeScriptType(type.GenericTypeArguments[0]) + "[]";
            }
            
            if (genericType == typeof(IReadOnlyList<>))
            {
                return CSharpTypeToTypeScriptType(type.GenericTypeArguments[0]) + "[]";
            }
            
            if (genericType == typeof(IReadOnlyCollection<>))
            {
                return CSharpTypeToTypeScriptType(type.GenericTypeArguments[0]) + "[]";
            }

            return "any";
        }

        return type.Name switch
        {
            "String" => "string",
            "int" => "number",
            "Int32" => "number",
            "Int16" => "number",
            "Int64" => "number",
            "UInt32" => "number",
            "UInt16" => "number",
            "UInt64" => "number",
            "Byte" => "number",
            "SByte" => "number",
            "Double" => "number",
            "Single" => "number",
            "float" => "number",
            "Char" => "string",
            "DateTime" => "Date",
            "Boolean" => "boolean",
            "bool" => "boolean",
            _ => "any"
        };
    }

    /// <summary>
    /// Compatibility modes for TypeScript generation.
    /// </summary>
    public enum Mode
    {
        /// <summary>
        /// Compatbility mode for Vue.js 3.
        /// </summary>
        Vue
    }
}