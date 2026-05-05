namespace Naiad.Diagrams.Flowchart;

internal sealed class FlowchartParser : IDiagramParser<FlowchartModel>
{
    // ── Parsed-line discriminated union ──────────────────────────────────────

    private abstract record ParsedLine
    {
        public sealed record NodeEdgeLine(
            List<Node> Nodes,
            List<(EdgeType Type, EdgeStyle Style, string? Label)> Edges) : ParsedLine;

        public sealed record SubgraphOpenLine(string Id, string? Title) : ParsedLine;
        public sealed record SubgraphCloseLine : ParsedLine;
        public sealed record SkipLine : ParsedLine;
    }

    // ── Shape helper ─────────────────────────────────────────────────────────

    // Node labels can be bare text or double-quoted strings; strip the quotes.
    private static Parser<char, string> ShapeContent(char closingChar) =>
        CommonParsers.DoubleQuotedString.Or(
            Token(_ => _ != closingChar).ManyString());

    // For shapes delimited by multi-character sequences we read until the first
    // closing character and strip the optional outer quotes afterwards.
    private static string StripOuterQuotes(string s) =>
        s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1] : s;

    // ── Node shape parsers ───────────────────────────────────────────────────

    private static readonly Parser<char, (string Label, NodeShape Shape)> DoubleCircleShape =
        String("(((")
            .Then(Token(_ => _ != ')').ManyString())
            .Before(String(")))"))
            .Select(text => (StripOuterQuotes(text), NodeShape.DoubleCircle));

    private static readonly Parser<char, (string Label, NodeShape Shape)> CircleShape =
        String("((")
            .Then(Token(_ => _ != ')').ManyString())
            .Before(String("))"))
            .Select(text => (StripOuterQuotes(text), NodeShape.Circle));

    private static readonly Parser<char, (string Label, NodeShape Shape)> StadiumShape =
        String("([")
            .Then(Token(_ => _ != ']').ManyString())
            .Before(String("])"))
            .Select(text => (StripOuterQuotes(text), NodeShape.Stadium));

    private static readonly Parser<char, (string Label, NodeShape Shape)> SubroutineShape =
        String("[[")
            .Then(Token(_ => _ != ']').ManyString())
            .Before(String("]]"))
            .Select(text => (StripOuterQuotes(text), NodeShape.Subroutine));

    private static readonly Parser<char, (string Label, NodeShape Shape)> CylinderShape =
        String("[(")
            .Then(Token(_ => _ != ')').ManyString())
            .Before(String(")]"))
            .Select(text => (StripOuterQuotes(text), NodeShape.Cylinder));

    private static readonly Parser<char, (string Label, NodeShape Shape)> HexagonShape =
        String("{{")
            .Then(Token(_ => _ != '}').ManyString())
            .Before(String("}}"))
            .Select(text => (StripOuterQuotes(text), NodeShape.Hexagon));

    private static readonly Parser<char, (string Label, NodeShape Shape)> DiamondShape =
        Char('{')
            .Then(Token(_ => _ != '}').ManyString())
            .Before(Char('}'))
            .Select(text => (StripOuterQuotes(text), NodeShape.Diamond));

    private static readonly Parser<char, (string Label, NodeShape Shape)> RoundedShape =
        Char('(')
            .Then(Token(_ => _ != ')').ManyString())
            .Before(Char(')'))
            .Select(text => (StripOuterQuotes(text), NodeShape.RoundedRectangle));

    // RectangleShape uses ShapeContent to handle both bare and quoted labels.
    private static readonly Parser<char, (string Label, NodeShape Shape)> RectangleShape =
        Char('[')
            .Then(ShapeContent(']'))
            .Before(Char(']'))
            .Select(text => (text, NodeShape.Rectangle));

    private static readonly Parser<char, (string Label, NodeShape Shape)> AsymmetricShape =
        Char('>')
            .Then(Token(_ => _ != ']').ManyString())
            .Before(Char(']'))
            .Select(text => (StripOuterQuotes(text), NodeShape.Asymmetric));

    private static readonly Parser<char, (string Label, NodeShape Shape)> NodeShapeParser =
        OneOf(
            Try(DoubleCircleShape),
            Try(CircleShape),
            Try(StadiumShape),
            Try(SubroutineShape),
            Try(CylinderShape),
            Try(HexagonShape),
            Try(DiamondShape),
            Try(RoundedShape),
            Try(AsymmetricShape),
            RectangleShape
        );

    // Node parser: identifier optionally followed by shape
    private static readonly Parser<char, Node> NodeParser =
        from id in CommonParsers.Identifier
        from shape in NodeShapeParser.Optional()
        select new Node
        {
            Id = id,
            Label = shape.HasValue ? shape.Value.Label : null,
            Shape = shape.HasValue ? shape.Value.Shape : NodeShape.Rectangle,
        };

    // ── Arrow / edge parsers ─────────────────────────────────────────────────

    private static readonly Parser<char, (EdgeType Type, EdgeStyle Style)> ArrowTypeParser =
        OneOf(
            Try(String("<-->")).ThenReturn((EdgeType.BiDirectional, EdgeStyle.Solid)),
            Try(String("o--o")).ThenReturn((EdgeType.BiDirectionalCircle, EdgeStyle.Solid)),
            Try(String("x--x")).ThenReturn((EdgeType.BiDirectionalCross, EdgeStyle.Solid)),
            Try(String("-.->")).ThenReturn((EdgeType.DottedArrow, EdgeStyle.Dotted)),
            Try(String("-.-")).ThenReturn((EdgeType.Dotted, EdgeStyle.Dotted)),
            Try(String("==>")).ThenReturn((EdgeType.ThickArrow, EdgeStyle.Thick)),
            Try(String("===")).ThenReturn((EdgeType.Thick, EdgeStyle.Thick)),
            Try(String("--o")).ThenReturn((EdgeType.CircleEnd, EdgeStyle.Solid)),
            Try(String("--x")).ThenReturn((EdgeType.CrossEnd, EdgeStyle.Solid)),
            Try(String("-->")).ThenReturn((EdgeType.Arrow, EdgeStyle.Solid)),
            String("---").ThenReturn((EdgeType.Open, EdgeStyle.Solid))
        );

    // Edge label: |text|
    private static readonly Parser<char, string> EdgeLabelParser =
        Char('|')
            .Then(Token(_ => _ != '|').ManyString())
            .Before(Char('|'));

    // ── Direction / directive parsers ─────────────────────────────────────────

    private static readonly Parser<char, Direction> FlowchartDirection =
        OneOf(
            Try(String("TB")).ThenReturn(Direction.TopToBottom),
            Try(String("TD")).ThenReturn(Direction.TopToBottom),
            Try(String("BT")).ThenReturn(Direction.BottomToTop),
            Try(String("LR")).ThenReturn(Direction.LeftToRight),
            String("RL").ThenReturn(Direction.RightToLeft)
        );

    private static readonly Parser<char, Unit> StyleDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("style")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    private static readonly Parser<char, Unit> ClassDefDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("classDef")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    private static readonly Parser<char, Unit> ClassDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("class")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    private static readonly Parser<char, Unit> ClickDirective =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("click")
        from ___ in CommonParsers.RequiredWhitespace
        from ____ in Token(_ => _ != '\r' && _ != '\n').ManyString()
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // ── Subgraph parsers ──────────────────────────────────────────────────────

    // Optional [title] or ["title"] portion of a subgraph header.
    private static readonly Parser<char, string> SubgraphTitleParser =
        from _ in CommonParsers.InlineWhitespace
        from __ in Char('[')
        from title in CommonParsers.DoubleQuotedString.Or(Token(_ => _ != ']').ManyString())
        from ___ in Char(']')
        select title;

    // subgraph <id> ["optional title"]
    private static readonly Parser<char, (string Id, string? Title)> SubgraphHeaderParser =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("subgraph")
        from ___ in CommonParsers.RequiredWhitespace
        from id in CommonParsers.Identifier
        from title in SubgraphTitleParser.Optional()
        from ____ in Token(_ => _ != '\r' && _ != '\n').SkipMany()
        from lineEnd in CommonParsers.LineEnd
        select (id, title.HasValue ? title.Value : (string?)null);

    // end (stands alone on a line, closing a subgraph block)
    private static readonly Parser<char, Unit> SubgraphEndParser =
        from _ in CommonParsers.InlineWhitespace
        from __ in String("end")
        from ___ in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        select Unit.Value;

    // ── Skip-line parser (directives / blank lines / comments) ───────────────
    // Note: subgraph open/close are handled separately in ParseLines, not here.

    private static readonly Parser<char, Unit> SkipLine =
        OneOf(
            Try(StyleDirective),
            Try(ClassDefDirective),
            Try(ClassDirective),
            Try(ClickDirective),
            CommonParsers.InlineWhitespace.Then(Try(CommonParsers.Comment).Or(CommonParsers.Newline))
        );

    // ── Statement parser (A --> B --> C) ─────────────────────────────────────

    public static Parser<char, (List<Node> Nodes, List<(EdgeType Type, EdgeStyle Style, string? Label)> Edges)> StatementParser =>
        from first in NodeParser
        from rest in (
                         from _1 in CommonParsers.InlineWhitespace
                         from label1 in EdgeLabelParser.Optional()
                         from _2 in CommonParsers.InlineWhitespace
                         from arrow in ArrowTypeParser
                         from _3 in CommonParsers.InlineWhitespace
                         from label2 in EdgeLabelParser.Optional()
                         from _4 in CommonParsers.InlineWhitespace
                         from node in NodeParser
                         select (node, arrow.Type, arrow.Style, label1.HasValue ? label1.Value : label2.HasValue ? label2.Value : null)
                     ).Many()
        select (
                   new List<Node>([first, .. rest.Select(_ => _.node)]),
                   rest.Select(_ => (_.Type, _.Style, (string?)_.Item4)).ToList()
               );

    // ── Top-level parser ──────────────────────────────────────────────────────

    public static Parser<char, FlowchartModel> Parser =>
        from _ in CommonParsers.InlineWhitespace
        from keyword in Try(String("flowchart")).Or(String("graph"))
        from __ in CommonParsers.InlineWhitespace
        from direction in FlowchartDirection.Optional()
        from ___ in CommonParsers.InlineWhitespace
        from lineEnd in CommonParsers.LineEnd
        from lines in ParseLines()
        select BuildModel(direction.GetValueOrDefault(Direction.TopToBottom), lines);

    private static Parser<char, List<ParsedLine>> ParseLines()
    {
        var statement =
            CommonParsers.InlineWhitespace
                         .Then(StatementParser)
                         .Before(CommonParsers.InlineWhitespace.Then(CommonParsers.LineEnd))
                         .Select(x => (ParsedLine)new ParsedLine.NodeEdgeLine(x.Item1, x.Item2));

        var subgraphOpen =
            SubgraphHeaderParser
                .Select(x => (ParsedLine)new ParsedLine.SubgraphOpenLine(x.Id, x.Title));

        var subgraphClose =
            SubgraphEndParser
                .ThenReturn((ParsedLine)new ParsedLine.SubgraphCloseLine());

        var skipLine =
            SkipLine.ThenReturn((ParsedLine)new ParsedLine.SkipLine());

        // Subgraph keywords are tried before statement so that bare "end" lines
        // are consumed as SubgraphCloseLine rather than parsed as a node.
        return Try(subgraphOpen)
               .Or(Try(subgraphClose))
               .Or(Try(skipLine))
               .Or(Try(statement))
               .Many()
               .Select(x => x.ToList());
    }

    public Result<char, FlowchartModel> Parse(string input) => Parser.Parse(input);

    // ── BuildModel ────────────────────────────────────────────────────────────

    private static FlowchartModel BuildModel(Direction direction, List<ParsedLine> lines)
    {
        var model = new FlowchartModel { Direction = direction };
        var nodeDict = new Dictionary<string, Node>();
        var subgraphStack = new Stack<Subgraph>();

        foreach (var line in lines)
        {
            switch (line)
            {
                case ParsedLine.SubgraphOpenLine open:
                {
                    var subgraph = new Subgraph { Id = open.Id, Title = open.Title };
                    model.Subgraphs.Add(subgraph);
                    subgraphStack.Push(subgraph);
                    break;
                }

                case ParsedLine.SubgraphCloseLine:
                    if (subgraphStack.Count > 0)
                    {
                        subgraphStack.Pop();
                    }

                    break;

                case ParsedLine.NodeEdgeLine(var nodes, var edges):
                {
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var node = nodes[i];

                        if (!nodeDict.TryGetValue(node.Id, out var existingNode))
                        {
                            nodeDict[node.Id] = node;
                            model.Nodes.Add(node);
                        }
                        else if (node.Label != null && existingNode.Label == null)
                        {
                            existingNode.Label = node.Label;
                            existingNode.Shape = node.Shape;
                        }

                        // Standalone node definitions (no edges) inside a subgraph block
                        // are registered as children of that subgraph.
                        if (subgraphStack.Count > 0 && edges.Count == 0)
                        {
                            var sg = subgraphStack.Peek();
                            if (!sg.NodeIds.Contains(node.Id))
                            {
                                sg.NodeIds.Add(node.Id);
                            }
                        }

                        if (i < edges.Count)
                        {
                            var edge = edges[i];
                            model.Edges.Add(
                                new()
                                {
                                    SourceId = nodes[i].Id,
                                    TargetId = nodes[i + 1].Id,
                                    Type = edge.Type,
                                    LineStyle = edge.Style,
                                    Label = edge.Label,
                                });
                        }
                    }

                    break;
                }
            }
        }

        return model;
    }
}
