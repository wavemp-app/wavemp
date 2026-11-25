using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WaveMP.Service.Extensions;
using WaveMP.Service.Hubs;
using WaveMP.Service.Tokens;
using YamlDotNet.Serialization;

namespace WaveMP.Service.Tests.Hubs;

/// <summary>
/// Checks asyncapi.yaml, the contract of the real-time connection of the devices, against the code: every method of the
/// hub and of its clients has an operation and every operation a method, the messages carry the argument and the result
/// of the methods, every schema matches its DTO as the connection serializes it (members, required members, null, types
/// and enum values), the schemas of the REST API come from openapi.json, and every reference resolves.
/// </summary>
[TestFixture]
public class AsyncApiContractTests
{
    private const string LocalSchemaPrefix = "#/components/schemas/";
    private const string OpenApiSchemaPrefix = "./source/WaveMP.Service/openapi.json#/components/schemas/";
    private const string DtoNamespace = "WaveMP.Service.Dtos.";
    private const string DtoSuffix = "Dto";

    private static readonly Assembly _service = typeof(DeviceHub).Assembly;

    private readonly Dictionary<string, JsonNode> _files = new(StringComparer.Ordinal);
    private string _root = null!;
    private JsonObject _document = null!;
    private JsonSerializerOptions _options = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _root = FindRepositoryRoot();
        object yaml = new DeserializerBuilder().Build()
            .Deserialize<object>(File.ReadAllText(Path.Combine(_root, "asyncapi.yaml")))!;
        string json = new SerializerBuilder().JsonCompatible().Build().Serialize(yaml);
        _document = JsonNode.Parse(json)!.AsObject();

        // The options of the connection itself, as AddWaveMpDeviceHub configures them.
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWaveMpDeviceHub();
        using ServiceProvider provider = services.BuildServiceProvider();
        _options = new JsonSerializerOptions(
            provider.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        _options.MakeReadOnly();
    }

    [Test]
    public void Document_Always_DescribesTheConnection()
    {
        _document["asyncapi"]!.GetValue<string>().Should().Be("3.0.0");
        _document["info"]!["title"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        _document["info"]!["version"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        _document["defaultContentType"]!.GetValue<string>().Should().Be("application/json");
        _document["channels"]!["devices"]!["address"]!.GetValue<string>().Should().Be(DeviceHubDefaults.Path);
        _document["servers"]!.AsObject().Select(server => server.Value!["pathname"]!.GetValue<string>())
            .Should().NotBeEmpty().And.AllBe(DeviceHubDefaults.Path);
        _document["components"]!["securitySchemes"]!["hubTicket"]!["name"]!.GetValue<string>()
            .Should().Be(DeviceHubDefaults.AccessTokenQueryParameter);
        _document["components"]!["securitySchemes"]!["hubTicket"]!["in"]!.GetValue<string>().Should().Be("query");
        _document["components"]!["securitySchemes"]!["sessionToken"]!["scheme"]!.GetValue<string>()
            .Should().Be("bearer");
    }

    [Test]
    public void Hub_Always_DeclaresItsPolicy()
    {
        typeof(DeviceHub).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()!.Policy
            .Should().Be(AuthorizationPolicies.DeviceHub);
    }

    [Test]
    public void ReceiveOperations_Always_AreTheMethodsOfTheHub()
    {
        OperationNames("receive").Should().BeEquivalentTo(HubMethods().Keys);
    }

    [Test]
    public void SendOperations_Always_AreTheMethodsOfTheClients()
    {
        OperationNames("send").Should().BeEquivalentTo(ClientMethods().Keys);
    }

    [Test]
    public void ReceiveOperations_Always_CarryTheArgumentAndTheResultOfTheirMethod()
    {
        foreach ((string name, MethodInfo method) in HubMethods())
        {
            JsonObject operation = Operation(name);
            ParameterInfo[] parameters = method.GetParameters();
            parameters.Length.Should().BeLessThanOrEqualTo(1, "hub method {0} takes one payload at most", name);
            PayloadOf(SingleMessage(operation["messages"]!)).Should().Be(
                parameters.Length == 0 ? null : SchemaName(parameters[0].ParameterType),
                "the message of {0} carries its argument",
                name);

            method.ReturnType.IsGenericType.Should().BeTrue("hub method {0} answers a result", name);
            method.ReturnType.GetGenericTypeDefinition().Should().Be(typeof(Task<>));
            PayloadOf(SingleMessage(operation["reply"]!["messages"]!)).Should().Be(
                SchemaName(method.ReturnType.GetGenericArguments()[0]),
                "the reply of {0} carries its result",
                name);
        }
    }

    [Test]
    public void SendOperations_Always_CarryTheArgumentOfTheirMethod()
    {
        foreach ((string name, MethodInfo method) in ClientMethods())
        {
            JsonObject operation = Operation(name);
            method.ReturnType.Should().Be(typeof(Task));
            ParameterInfo parameter = method.GetParameters().Should().ContainSingle().Subject;
            PayloadOf(SingleMessage(operation["messages"]!)).Should().Be(
                SchemaName(parameter.ParameterType),
                "the message of {0} carries its argument",
                name);
            operation["reply"].Should().BeNull("the server expects no answer to {0}", name);
        }
    }

    [Test]
    public void Messages_Always_BelongToTheChannelAndAnOperation()
    {
        JsonObject channelMessages = _document["channels"]!["devices"]!["messages"]!.AsObject();
        HashSet<string> used = [.. _document["operations"]!.AsObject()
            .SelectMany(operation => new[] { operation.Value!["messages"], operation.Value!["reply"]?["messages"] })
            .OfType<JsonArray>()
            .SelectMany(messages => messages)
            .Select(message => message!["$ref"]!.GetValue<string>())];

        used.Should().AllSatisfy(reference => reference.Should().StartWith("#/channels/devices/messages/"));
        used.Select(reference => reference["#/channels/devices/messages/".Length..])
            .Should().BeEquivalentTo(channelMessages.Select(message => message.Key));
        _document["components"]!["messages"]!.AsObject().Select(message => message.Key)
            .Should().BeEquivalentTo(channelMessages.Select(message => message.Key));
    }

    [Test]
    public void Schemas_Always_MatchTheirDtos()
    {
        JsonObject schemas = _document["components"]!["schemas"]!.AsObject();
        foreach ((string name, JsonNode? schema) in schemas)
        {
            if (schema!["$ref"] is not null)
            {
                continue;
            }

            Type dto = DtoOf(name);
            if (dto.IsEnum)
            {
                CompareEnum(name, schema, dto);
            }
            else
            {
                CompareObject(name, schema, dto);
            }
        }
    }

    [Test]
    public void SchemasOfTheApi_Always_ComeFromTheOpenApiDocument()
    {
        JsonObject schemas = _document["components"]!["schemas"]!.AsObject();
        List<string> aliases = [];
        foreach ((string name, JsonNode? schema) in schemas)
        {
            if (schema!["$ref"] is not { } reference)
            {
                continue;
            }

            aliases.Add(name);
            schema.AsObject().Count.Should().Be(1, "{0} only refers to openapi.json", name);
            reference.GetValue<string>().Should().Be(OpenApiSchemaPrefix + name);
            DtoOf(name).Should().NotBeNull();
        }

        aliases.Should().Contain(["Track", "Device", "ValidationError"]);
    }

    [Test]
    public void References_Always_Resolve()
    {
        List<string> references = [];
        CollectReferences(_document, references);

        references.Should().NotBeEmpty();
        foreach (string reference in references)
        {
            Resolve(reference).Should().NotBeNull("the reference {0} must resolve", reference);
        }
    }

    [Test]
    public void Examples_Always_FitTheirSchema()
    {
        foreach ((string name, JsonNode? message) in _document["components"]!["messages"]!.AsObject())
        {
            if (message!["examples"] is not JsonArray examples)
            {
                continue;
            }

            JsonNode schema = Resolve(message["payload"]!["$ref"]!.GetValue<string>())!;
            HashSet<string> properties = [.. schema["properties"]!.AsObject().Select(property => property.Key)];
            foreach (JsonNode? example in examples)
            {
                HashSet<string> members = [.. example!["payload"]!.AsObject().Select(member => member.Key)];
                members.Should().BeSubsetOf(properties, "the example of {0} uses members of its schema", name);
                Strings(schema["required"]).Should()
                    .BeSubsetOf(members, "the example of {0} has every required member", name);
            }
        }
    }

    private void CompareEnum(string name, JsonNode schema, Type dto)
    {
        JsonNode exported = _options.GetJsonSchemaAsNode(dto);
        Strings(schema["enum"]).Should().Equal(Strings(exported["enum"]), "the values of {0}", name);
        Types(schema).Should().BeEquivalentTo(["string"], "{0} is written as a string", name);
    }

    private void CompareObject(string name, JsonNode schema, Type dto)
    {
        JsonNode exported = _options.GetJsonSchemaAsNode(
            dto,
            new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
        Types(schema).Should().BeEquivalentTo(["object"], "{0} is an object", name);
        Strings(schema["required"]).Should()
            .BeEquivalentTo(Strings(exported["required"]), "the required members of {0}", name);

        JsonObject properties = schema["properties"]!.AsObject();
        JsonTypeInfo typeInfo = _options.GetTypeInfo(dto);
        properties.Select(property => property.Key)
            .Should().BeEquivalentTo(typeInfo.Properties.Select(property => property.Name), "the members of {0}", name);

        NullabilityInfoContext nullability = new();
        foreach (JsonPropertyInfo member in typeInfo.Properties)
        {
            string path = $"{name}.{member.Name}";
            JsonNode property = properties[member.Name]!;
            var clr = (PropertyInfo)member.AttributeProvider!;
            bool nullable = Nullable.GetUnderlyingType(clr.PropertyType) is not null
                || (!clr.PropertyType.IsValueType && nullability.Create(clr).ReadState == NullabilityState.Nullable);
            AllowsNull(property).Should().Be(nullable, "{0} allows null exactly when the DTO does", path);
            CompareType(path, property, Nullable.GetUnderlyingType(clr.PropertyType) ?? clr.PropertyType);
        }
    }

    private void CompareType(string path, JsonNode property, Type type)
    {
        if (IsDto(type))
        {
            References(property).Should()
                .Contain(LocalSchemaPrefix + SchemaName(type), "{0} refers to its schema", path);
            return;
        }

        Type? element = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? type.GetGenericArguments()[0]
            : null;
        if (element is not null)
        {
            Types(property).Should().Contain("array", "{0} is a list", path);
            CompareType($"{path}[]", property["items"]!, element);
            return;
        }

        (string jsonType, string? format) = type switch
        {
            _ when type == typeof(string) => ("string", null),
            _ when type == typeof(bool) => ("boolean", null),
            _ when type == typeof(int) => ("integer", "int32"),
            _ when type == typeof(long) => ("integer", "int64"),
            _ when type == typeof(double) => ("number", "double"),
            _ when type == typeof(Guid) => ("string", "uuid"),
            _ => throw new InvalidOperationException($"{path} has the unexpected type {type}."),
        };
        Types(property).Should().Contain(jsonType, "{0} is of the JSON type {1}", path, jsonType);
        property["format"]?.GetValue<string>().Should().Be(format, "{0} has the format {1}", path, format);
    }

    private Dictionary<string, MethodInfo> HubMethods()
    {
        return typeof(DeviceHub)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetBaseDefinition().DeclaringType == typeof(DeviceHub))
            .ToDictionary(method => method.GetCustomAttribute<HubMethodNameAttribute>()?.Name ?? method.Name);
    }

    private static Dictionary<string, MethodInfo> ClientMethods()
    {
        return typeof(IDeviceHubClient).GetMethods().ToDictionary(method => method.Name);
    }

    private IEnumerable<string> OperationNames(string action)
    {
        return _document["operations"]!.AsObject()
            .Where(operation => operation.Value!["action"]!.GetValue<string>() == action)
            .Select(operation => operation.Key);
    }

    private JsonObject Operation(string name)
    {
        JsonObject operation = _document["operations"]![name]!.AsObject();
        operation["channel"]!["$ref"]!.GetValue<string>().Should().Be("#/channels/devices");
        return operation;
    }

    private JsonNode SingleMessage(JsonNode messages)
    {
        JsonNode message = messages.AsArray().Should().ContainSingle().Subject!;
        // Operations refer to the messages of the channel, which refer to the components.
        while (message["$ref"] is { } reference)
        {
            message = Resolve(reference.GetValue<string>())!;
        }

        return message;
    }

    private static string? PayloadOf(JsonNode message)
    {
        if (message["payload"]?["$ref"] is not { } reference)
        {
            return null;
        }

        string value = reference.GetValue<string>();
        value.Should().StartWith(LocalSchemaPrefix);
        return value[LocalSchemaPrefix.Length..];
    }

    private static string SchemaName(Type type)
    {
        IsDto(type).Should().BeTrue("{0} is a DTO", type);
        return type.Name[..^DtoSuffix.Length];
    }

    private static bool IsDto(Type type)
    {
        return type.Namespace == "WaveMP.Service.Dtos" && type.Name.EndsWith(DtoSuffix, StringComparison.Ordinal);
    }

    private static Type DtoOf(string schemaName)
    {
        return _service.GetType(DtoNamespace + schemaName + DtoSuffix)
            ?? throw new InvalidOperationException($"The schema {schemaName} has no DTO {schemaName}{DtoSuffix}.");
    }

    private static List<string> Strings(JsonNode? node)
    {
        return node is JsonArray array ? [.. array.Select(item => item!.GetValue<string>())] : [];
    }

    private static List<string> Types(JsonNode node)
    {
        return node["type"] switch
        {
            JsonArray types => Strings(types),
            JsonValue type => [type.GetValue<string>()],
            _ => [],
        };
    }

    private static bool AllowsNull(JsonNode node)
    {
        return Types(node).Contains("null")
            || (node["oneOf"] is JsonArray alternatives && alternatives.Any(alternative => AllowsNull(alternative!)));
    }

    private static List<string> References(JsonNode node)
    {
        List<string> references = [];
        if (node["$ref"] is { } reference)
        {
            references.Add(reference.GetValue<string>());
        }

        if (node["oneOf"] is JsonArray alternatives)
        {
            references.AddRange(alternatives.SelectMany(alternative => References(alternative!)));
        }

        return references;
    }

    private static void CollectReferences(JsonNode? node, List<string> references)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string key, JsonNode? value) in obj)
                {
                    if (key == "$ref")
                    {
                        references.Add(value!.GetValue<string>());
                    }
                    else
                    {
                        CollectReferences(value, references);
                    }
                }

                break;
            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    CollectReferences(item, references);
                }

                break;
        }
    }

    /// <summary>
    /// The node a reference points to: <c>#/a/b</c> in this document, <c>./file.json#/a/b</c> in a JSON file of the
    /// repository; <c>null</c> if it does not exist.
    /// </summary>
    private JsonNode? Resolve(string reference)
    {
        int hash = reference.IndexOf('#', StringComparison.Ordinal);
        string file = hash < 0 ? reference : reference[..hash];
        string pointer = hash < 0 ? string.Empty : reference[(hash + 1)..];
        JsonNode? node = file.Length == 0 ? _document : LoadFile(file);
        foreach (string segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string name = segment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            node = node switch
            {
                JsonObject obj => obj[name],
                JsonArray array when int.TryParse(name, out int index) && index < array.Count => array[index],
                _ => null,
            };
        }

        return node;
    }

    private JsonNode? LoadFile(string file)
    {
        string path = Path.GetFullPath(Path.Combine(_root, file));
        if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return null;
        }

        if (!_files.TryGetValue(path, out JsonNode? node))
        {
            node = JsonNode.Parse(File.ReadAllText(path))!;
            _files[path] = node;
        }

        return node;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WaveMP.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root (WaveMP.slnx) was not found.");
    }
}
