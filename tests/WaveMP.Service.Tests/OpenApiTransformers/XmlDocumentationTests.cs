using System.Reflection;
using System.Xml.Linq;
using AwesomeAssertions;
using WaveMP.Service.Controllers;
using WaveMP.Service.OpenApiTransformers;

namespace WaveMP.Service.Tests.OpenApiTransformers;

[TestFixture]
public class XmlDocumentationTests
{
    private const string Type = "WaveMP.Service.Tests.OpenApiTransformers.XmlDocumentationTests.Sample";

    private static readonly XmlDocumentation _documentation = new(XDocument.Parse($$"""
        <doc>
          <members>
            <member name="M:{{Type}}.Create(System.String,System.Threading.CancellationToken)">
              <param name="request">The new <c>user</c>
                with a name.</param>
              <param name="cancellationToken">Aborts the request.</param>
            </member>
            <member name="M:{{Type}}.Update(System.Guid,System.Collections.Generic.Dictionary{System.String,System.Int32},System.Threading.CancellationToken)">
              <param name="changes">The changes.</param>
            </member>
            <member name="M:{{Type}}.Update(System.Guid,System.Threading.CancellationToken)">
              <param name="changes">The changes without values.</param>
            </member>
            <member name="M:{{Type}}.Empty">
              <param name="request"> </param>
            </member>
          </members>
        </doc>
        """));

    [Test]
    public void FindParameter_DocumentedParameter_ReturnsItsTextWithCodeInBackticks()
    {
        string? text = _documentation.FindParameter(Method(nameof(Sample.Create)), "request");

        // The line break stays; XmlCommentLineBreakTransformer joins the lines later.
        text.Should().Be("The new `user`\n        with a name.");
    }

    [Test]
    public void FindParameter_Overloads_FindsTheMethodWithTheSameNumberOfParameters()
    {
        MethodInfo method = typeof(Sample).GetMethods()
            .Single(info => info.Name == nameof(Sample.Update) && info.GetParameters().Length == 3);

        string? text = _documentation.FindParameter(method, "changes");

        text.Should().Be("The changes.");
    }

    [TestCase(nameof(Sample.Create), "unknown")]
    [TestCase(nameof(Sample.Empty), "request")]
    [TestCase(nameof(Sample.Undocumented), "request")]
    public void FindParameter_MissingOrEmptyComment_ReturnsNull(string methodName, string parameterName)
    {
        string? text = _documentation.FindParameter(Method(methodName), parameterName);

        text.Should().BeNull();
    }

    [Test]
    public void Load_AssemblyWithoutDocumentationFile_IsEmpty()
    {
        var documentation = XmlDocumentation.Load(typeof(AwesomeAssertions.AssertionExtensions).Assembly);

        documentation.FindParameter(Method(nameof(Sample.Create)), "request").Should().BeNull();
    }

    [Test]
    public void Load_ServiceAssembly_ReadsItsDocumentationFile()
    {
        var documentation = XmlDocumentation.Load(typeof(XmlDocumentation).Assembly);
        MethodInfo method = typeof(UsersController).GetMethod(
            nameof(UsersController.CreateUserAsync))!;

        documentation.FindParameter(method, "request").Should().Be("The new user.");
    }

    private static MethodInfo Method(string name)
    {
        return typeof(Sample).GetMethods().First(info => info.Name == name);
    }

    public sealed class Sample
    {
        public void Create(string request, CancellationToken cancellationToken)
        {
        }

        public void Update(Guid id, Dictionary<string, int> changes, CancellationToken cancellationToken)
        {
        }

        public void Update(Guid id, CancellationToken cancellationToken)
        {
        }

        public void Empty()
        {
        }

        public void Undocumented(string request)
        {
        }
    }
}
