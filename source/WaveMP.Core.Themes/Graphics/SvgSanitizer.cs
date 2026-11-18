using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using WaveMP.Core.Themes.Stylesheets;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Graphics;

/// <summary>
/// Checks and cleans an SVG file of a theme with an allowlist. Rejected are files with a document type declaration or
/// entities, scripts, event handler attributes (<c>on...</c>), foreign content (<c>foreignObject</c>, HTML) and
/// references to anything outside the file (links, pictures, style sheets and <c>url()</c> other than <c>#id</c>;
/// embedded PNG, JPEG, WebP or GIF pictures as <c>data:</c> addresses of <c>image</c> are fine). Everything else that
/// is not on the allowlist (e.g. the metadata of drawing programs, animations, links, processing instructions and
/// comments) is removed. The result is written anew from the allowed elements and attributes only, so nothing the
/// parser read differently can survive.
/// <para>
/// The SVG is parsed with the XML reader of .NET (without DTD processing and without resolving anything), which is
/// the parser browsers use for SVG files as well: an XML parser, not an HTML parser.
/// </para>
/// </summary>
internal static partial class SvgSanitizer
{
    public const string SvgNamespace = "http://www.w3.org/2000/svg";
    public const string XlinkNamespace = "http://www.w3.org/1999/xlink";
    public const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    public const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    private const int MaxDepth = 64;
    private const int MaxElements = 50_000;

    private static readonly HashSet<string> _elements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "symbol", "use", "title", "desc", "style",
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "text", "tspan", "textPath",
        "linearGradient", "radialGradient", "stop", "pattern", "clipPath", "mask", "marker", "image",
        "filter", "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite", "feConvolveMatrix",
        "feDiffuseLighting", "feDisplacementMap", "feDistantLight", "feDropShadow", "feFlood", "feFuncA", "feFuncB",
        "feFuncG", "feFuncR", "feGaussianBlur", "feImage", "feMerge", "feMergeNode", "feMorphology", "feOffset",
        "fePointLight", "feSpecularLighting", "feSpotLight", "feTile", "feTurbulence",
    };

    // Elements whose text is kept; the text of other elements is whitespace between elements.
    private static readonly HashSet<string> _textElements = new(StringComparer.Ordinal)
    {
        "title", "desc", "style", "text", "tspan", "textPath",
    };

    private static readonly HashSet<string> _attributes = new(StringComparer.Ordinal)
    {
        "id", "class", "style", "transform", "viewBox", "preserveAspectRatio", "width", "height", "x", "y",
        "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry", "fx", "fy", "fr", "d", "points", "pathLength",
        "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin",
        "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "stroke-opacity", "opacity", "color", "display",
        "visibility", "overflow", "clip-path", "clip-rule", "mask", "filter", "marker-start", "marker-mid",
        "marker-end", "stop-color", "stop-opacity", "offset", "gradientUnits", "gradientTransform", "spreadMethod",
        "patternUnits", "patternContentUnits", "patternTransform", "clipPathUnits", "maskUnits", "maskContentUnits",
        "filterUnits", "primitiveUnits", "markerUnits", "markerWidth", "markerHeight", "refX", "refY", "orient",
        "font-family", "font-size", "font-weight", "font-style", "font-variant", "font-stretch", "text-anchor",
        "dominant-baseline", "alignment-baseline", "baseline-shift", "letter-spacing", "word-spacing",
        "text-decoration", "writing-mode", "dx", "dy", "rotate", "textLength", "lengthAdjust", "startOffset",
        "method", "spacing", "side", "href", "mix-blend-mode", "isolation", "paint-order", "vector-effect",
        "shape-rendering", "text-rendering", "image-rendering", "color-interpolation", "color-interpolation-filters",
        "flood-color", "flood-opacity", "lighting-color", "in", "in2", "result", "mode", "type", "values",
        "tableValues", "slope", "intercept", "amplitude", "exponent", "stdDeviation", "edgeMode", "operator", "k1",
        "k2", "k3", "k4", "kernelMatrix", "order", "divisor", "bias", "targetX", "targetY", "preserveAlpha",
        "surfaceScale", "diffuseConstant", "specularConstant", "specularExponent", "kernelUnitLength", "azimuth",
        "elevation", "z", "pointsAtX", "pointsAtY", "pointsAtZ", "limitingConeAngle", "scale", "xChannelSelector",
        "yChannelSelector", "radius", "baseFrequency", "numOctaves", "seed", "stitchTiles", "media", "crossorigin",
        "systemLanguage", "requiredExtensions", "font", "direction", "unicode-bidi", "transform-origin",
        "transform-box", "clip", "cursor", "pointer-events",
    };

    private static readonly XmlReaderSettings _readerSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = false,
    };

    /// <summary>
    /// The cleaned SVG in UTF-8, or <c>null</c> with the reason in <paramref name="errors"/>.
    /// </summary>
    public static byte[]? Sanitize(string path, byte[] content, List<ThemeError> errors)
    {
        string? text = ThemeStylesheet.Decode(content);
        if (text is null)
        {
            errors.Add(new ThemeError(ThemeErrorCode.SvgInvalid, path));
            return null;
        }

        if (DeclarationPattern().IsMatch(text))
        {
            errors.Add(new ThemeError(ThemeErrorCode.SvgEntity, path));
            return null;
        }

        SvgNode root;
        try
        {
            using StringReader input = new(text);
            using var reader = XmlReader.Create(input, _readerSettings);
            root = Read(reader);
        }
        catch (SvgRejectedException exception)
        {
            errors.Add(new ThemeError(exception.Code, path));
            return null;
        }
        catch (XmlException)
        {
            errors.Add(new ThemeError(ThemeErrorCode.SvgInvalid, path));
            return null;
        }

        return Write(root);
    }

    private static SvgNode Read(XmlReader reader)
    {
        reader.MoveToContent();
        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "svg" || reader.NamespaceURI != SvgNamespace)
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgInvalid);
        }

        int elements = 0;
        SvgNode root = ReadElement(reader, ref elements)!;
        // Nothing but whitespace may follow the root; the reader rejects a second root itself.
        while (reader.Read())
        {
        }

        return root;
    }

    /// <summary>
    /// Reads the element the reader stands on with everything in it; <c>null</c> if it is not on the allowlist (it
    /// is removed with its content).
    /// </summary>
    private static SvgNode? ReadElement(XmlReader reader, ref int elements)
    {
        if (++elements > MaxElements || reader.Depth > MaxDepth)
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgInvalid);
        }

        string name = reader.LocalName;
        string ns = reader.NamespaceURI;
        CheckElement(name, ns);
        bool keep = ns == SvgNamespace && _elements.Contains(name);
        SvgNode node = new(name);
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                ReadAttribute(reader, node, keep);
            }
            while (reader.MoveToNextAttribute());

            reader.MoveToElement();
        }

        if (reader.IsEmptyElement)
        {
            return keep ? node : null;
        }

        int depth = reader.Depth;
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.EndElement when reader.Depth == depth:
                    return keep ? node : null;
                case XmlNodeType.Element:
                    SvgNode? child = ReadElement(reader, ref elements);
                    if (child is not null && keep)
                    {
                        node.Children.Add(child);
                    }

                    break;
                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                    if (keep && (_textElements.Contains(name) || string.IsNullOrWhiteSpace(reader.Value)))
                    {
                        if (name == "style")
                        {
                            CheckStyle(reader.Value);
                        }

                        node.Children.Add(new SvgText(reader.Value));
                    }

                    break;
                case XmlNodeType.EntityReference or XmlNodeType.DocumentType:
                    throw new SvgRejectedException(ThemeErrorCode.SvgEntity);
            }
        }

        throw new XmlException("The element is not closed.");
    }

    /// <summary>
    /// Rejects elements that run scripts or bring in foreign content, in any namespace.
    /// </summary>
    private static void CheckElement(string name, string ns)
    {
        if (name.Equals("script", StringComparison.OrdinalIgnoreCase))
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgScript);
        }

        if (name.Equals("foreignObject", StringComparison.OrdinalIgnoreCase) || ns == XhtmlNamespace ||
            name is "iframe" or "embed" or "object" or "handler" or "listener")
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgForeignObject);
        }
    }

    private static void ReadAttribute(XmlReader reader, SvgNode node, bool keep)
    {
        string name = reader.LocalName;
        string ns = reader.NamespaceURI;
        string value = reader.Value;
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) && ns != "http://www.w3.org/2000/xmlns/")
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgEventHandler);
        }

        bool isHref = name == "href" && (ns.Length == 0 || ns == XlinkNamespace);
        if (isHref)
        {
            // Also on elements that are removed: a link to elsewhere is reported, not hidden.
            CheckHref(value, node.Name);
        }

        if (!keep)
        {
            return;
        }

        if (isHref)
        {
            // Written as href; SVG 2 reads it, and xlink:href would need its namespace.
            node.Attributes.Add(new SvgAttribute("href", string.Empty, value));
        }
        else if (ns.Length == 0 && _attributes.Contains(name))
        {
            if (name is not ("id" or "class"))
            {
                // Presentation attributes and style are read as CSS.
                CheckStyleValue(value);
            }

            node.Attributes.Add(new SvgAttribute(name, string.Empty, value));
        }
        else if (ns == XmlNamespace && name is "space" or "lang")
        {
            node.Attributes.Add(new SvgAttribute(name, XmlNamespace, value));
        }
    }

    /// <summary>
    /// A link may only point into the same file (<c>#id</c>); <c>image</c> may also embed a picture as <c>data:</c>
    /// address.
    /// </summary>
    private static void CheckHref(string value, string element)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith('#') && CssAddresses.Classify(trimmed).Kind == CssAddressKind.Fragment)
        {
            return;
        }

        if (element == "image" && EmbeddedPicturePattern().IsMatch(trimmed))
        {
            return;
        }

        throw new SvgRejectedException(ThemeErrorCode.SvgExternalReference);
    }

    /// <summary>
    /// Checks the content of a <c>style</c> element: no <c>@import</c> and no address other than <c>#id</c>.
    /// </summary>
    private static void CheckStyle(string css)
    {
        var scan = CssScan.Run(css);
        if (scan.AtRules.Any(rule => rule.Name is "import" or "namespace") || scan.IndirectAddresses.Count > 0 ||
            scan.SyntaxErrors.Count > 0)
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgExternalReference);
        }

        CheckReferences(scan);
    }

    /// <summary>
    /// Checks a value of an attribute that CSS reads (presentation attributes such as <c>fill</c> and the
    /// <c>style</c> attribute): no address other than <c>#id</c>.
    /// </summary>
    private static void CheckStyleValue(string value)
    {
        var scan = CssScan.Run(value);
        if (scan.IndirectAddresses.Count > 0)
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgExternalReference);
        }

        CheckReferences(scan);
    }

    private static void CheckReferences(CssScan scan)
    {
        if (scan.References.Any(reference => CssAddresses.Classify(reference.Value).Kind != CssAddressKind.Fragment))
        {
            throw new SvgRejectedException(ThemeErrorCode.SvgExternalReference);
        }
    }

    private static byte[] Write(SvgNode root)
    {
        using MemoryStream output = new();
        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineHandling = NewLineHandling.Entitize,
        };
        using (var writer = XmlWriter.Create(output, settings))
        {
            WriteElement(writer, root);
        }

        return output.ToArray();
    }

    private static void WriteElement(XmlWriter writer, SvgNode node)
    {
        writer.WriteStartElement(node.Name, SvgNamespace);
        foreach (SvgAttribute attribute in node.Attributes)
        {
            if (attribute.Namespace == XmlNamespace)
            {
                writer.WriteAttributeString("xml", attribute.Name, XmlNamespace, attribute.Value);
            }
            else
            {
                writer.WriteAttributeString(attribute.Name, attribute.Value);
            }
        }

        foreach (object child in node.Children)
        {
            if (child is SvgNode element)
            {
                WriteElement(writer, element);
            }
            else if (child is SvgText text)
            {
                writer.WriteString(text.Value);
            }
        }

        writer.WriteEndElement();
    }

    // A document type declaration or an entity declaration, also with odd case or whitespace.
    [GeneratedRegex(@"<!\s*(DOCTYPE|ENTITY)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeclarationPattern();

    [GeneratedRegex(
        @"\Adata:image/(png|jpeg|webp|gif);base64,[A-Za-z0-9+/=\s]*\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedPicturePattern();

    private sealed class SvgNode(string name)
    {
        public string Name { get; } = name;

        public List<SvgAttribute> Attributes { get; } = [];

        public List<object> Children { get; } = [];
    }

    private sealed record SvgAttribute(string Name, string Namespace, string Value);

    private sealed record SvgText(string Value);

    private sealed class SvgRejectedException(ThemeErrorCode code) : Exception
    {
        public ThemeErrorCode Code { get; } = code;
    }
}
