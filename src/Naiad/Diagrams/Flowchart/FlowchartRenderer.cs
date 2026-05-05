namespace Naiad.Diagrams.Flowchart;

public partial class FlowchartRenderer(ILayoutEngine? layoutEngine = null) : IDiagramRenderer<FlowchartModel>
{
    // Mermaid.ink default colors
    private const string nodeFill = "#ECECFF";
    private const string nodeStroke = "#9370DB";
    private const string edgeStroke = "#333333";
    private const string labelBackground = "rgba(232,232,232,0.8)";

    // FontAwesome icon pattern: fa:fa-icon-name or fab:fa-icon-name
    private static readonly Regex iconPattern = IconPatternMyRegex();
    private readonly ILayoutEngine layoutEngine = layoutEngine ?? new DagreLayoutEngine();

    public SvgDocument Render(FlowchartModel model, RenderOptions options)
    {
        // Calculate node sizes based on text
        foreach (var node in model.Nodes)
        {
            var label = node.Label ?? node.Id;
            var hasIcon = iconPattern.IsMatch(label);
            var textForMeasure = hasIcon ? iconPattern.Replace(label, "") : label;
            var textSize = MeasureText(textForMeasure, options.FontSize);
            node.Width = textSize.Width + 30 + (hasIcon ? 20 : 0);
            node.Height = textSize.Height + 27;

            // Adjust size for different shapes
            if (node.Shape is NodeShape.Circle or NodeShape.DoubleCircle)
            {
                var diameter = Math.Max(node.Width, node.Height);
                node.Width = diameter;
                node.Height = diameter;
            }
            else if (node.Shape == NodeShape.Diamond)
            {
                node.Width *= 1.4;
                node.Height *= 1.4;
            }
        }

        // Size subgraph proxy nodes to contain all of their children.
        const double SubgraphPaddingX = 20.0;
        const double SubgraphPaddingY = 15.0;
        const double SubgraphTitleHeight = 26.0;
        const double SubgraphChildGap = 8.0;

        var subgraphProxyIds = model.Subgraphs.Select(sg => sg.Id).ToHashSet();

        foreach (var subgraph in model.Subgraphs)
        {
            var proxy = model.GetNode(subgraph.Id);
            if (proxy is null)
            {
                continue;
            }

            var children = subgraph.NodeIds
                                   .Select(id => model.GetNode(id))
                                   .Where(n => n is not null)
                                   .ToList();

            if (children.Count == 0)
            {
                continue;
            }

            var containerWidth = children.Max(c => c!.Width) + 2 * SubgraphPaddingX;
            var containerHeight = SubgraphTitleHeight
                                  + children.Sum(c => c!.Height)
                                  + (children.Count - 1) * SubgraphChildGap
                                  + 2 * SubgraphPaddingY;

            // Ensure the title fits
            if (subgraph.Title is not null)
            {
                var titleSize = MeasureText(subgraph.Title, options.FontSize);
                containerWidth = Math.Max(containerWidth, titleSize.Width + 2 * SubgraphPaddingX);
            }

            proxy.Width = containerWidth;
            proxy.Height = containerHeight;
        }

        // Run layout
        var layoutOptions = new LayoutOptions
        {
            Direction = model.Direction,
            NodeSeparation = 50,
            RankSeparation = 70,
        };

        var layoutResult = this.layoutEngine.Layout(model, layoutOptions);

        // Build SVG
        var builder = new SvgBuilder()
                     .Size(layoutResult.Width, layoutResult.Height)
                     .Padding(options.Padding)
                     .AddMermaidArrowMarker()
                     .AddMermaidCircleMarker()
                     .AddMermaidCrossMarker();

        // Add mermaid.ink CSS styles
        builder.AddStyles(MermaidStyles.FlowchartStyles);

        // Render subgraph containers first (background layer)
        foreach (var subgraph in model.Subgraphs)
        {
            var proxy = model.GetNode(subgraph.Id);
            if (proxy is not null)
            {
                RenderSubgraphContainer(builder, subgraph, proxy, options);
            }
        }

        // Render edges (behind nodes)
        foreach (var edge in model.Edges)
        {
            RenderEdge(builder, edge, options);
        }

        // Render nodes; skip subgraph proxy nodes — they are drawn as containers above.
        foreach (var node in model.Nodes)
        {
            if (subgraphProxyIds.Contains(node.Id))
            {
                continue;
            }

            RenderNode(builder, node, options);
        }

        return builder.Build();
    }

    [GeneratedRegex("(fa[bsr]?):fa-([a-z0-9-]+)", RegexOptions.Compiled)]
    private static partial Regex IconPatternMyRegex();

    private static void RenderNode(SvgBuilder builder, Node node, RenderOptions options)
    {
        var x = node.Position.X - node.Width / 2;
        var y = node.Position.Y - node.Height / 2;

        var shapePath = ShapePathGenerator.GetPath(node.Shape, x, y, node.Width, node.Height);

        builder.AddPath(
            shapePath,
            nodeFill,
            nodeStroke,
            1);

        // Render label as native SVG text (works in both browser and PNG via Svg.Skia)
        var label = node.Label ?? node.Id;
        var textLabel = iconPattern.Replace(label, "").Trim();
        if (string.IsNullOrEmpty(textLabel))
            textLabel = label;

        builder.AddText(
            node.Position.X,
            node.Position.Y,
            textLabel,
            anchor: "middle",
            baseline: "middle",
            fontSize: options.FontSize,
            fontFamily: options.FontFamily);
    }

    private const string subgraphFill = "#f9f9ff";
    private const string subgraphStroke = "#9370DB";
    private const string subgraphTitleColor = "#333333";

    private static void RenderSubgraphContainer(SvgBuilder builder, Subgraph subgraph, Node proxy, RenderOptions options)
    {
        var x = proxy.Position.X - proxy.Width / 2;
        var y = proxy.Position.Y - proxy.Height / 2;

        builder.AddRect(
            x,
            y,
            proxy.Width,
            proxy.Height,
            rx: 5,
            fill: subgraphFill,
            stroke: subgraphStroke,
            strokeWidth: 1,
            cssClass: "subgraph");

        if (subgraph.Title is not null)
        {
            const double TitleHeight = 26.0;
            builder.AddText(
                x + 8,
                y + TitleHeight / 2,
                subgraph.Title,
                anchor: "start",
                baseline: "middle",
                fontSize: options.FontSize * 0.85,
                fontFamily: options.FontFamily,
                fill: subgraphTitleColor,
                cssClass: "subgraphLabel");
        }
    }

    private static void RenderEdge(SvgBuilder builder, Edge edge, RenderOptions options)
    {
        if (edge.Points.Count < 2)
        {
            return;
        }

        // Build path from points
        var points = edge.Points;
        var pathBuilder = new StringBuilder();
        pathBuilder.Append(CultureInfo.InvariantCulture, $"M{points[0].X:0.##},{points[0].Y:0.##}");

        for (var i = 1; i < points.Count; i++)
        {
            pathBuilder.Append(CultureInfo.InvariantCulture, $" L{points[i].X:0.##},{points[i].Y:0.##}");
        }

        var pathData = pathBuilder.ToString();

        var strokeDasharray = edge.LineStyle switch
        {
            EdgeStyle.Dotted => "2",
            _ => null,
        };

        var strokeWidth = edge.LineStyle switch
        {
            EdgeStyle.Thick => 3.5,
            _ => 2.0,
        };

        var markerEnd = edge.HasArrowHead ? "url(#mermaid-svg_flowchart-v2-pointEnd)" :
                        edge.HasCircleEnd ? "url(#mermaid-svg_flowchart-v2-circleEnd)" :
                        edge.HasCrossEnd ? "url(#mermaid-svg_flowchart-v2-crossEnd)" : null;

        var markerStart = edge.HasArrowTail ? "url(#mermaid-svg_flowchart-v2-pointStart)" : null;

        builder.AddPath(
            pathData,
            "none",
            edgeStroke,
            strokeWidth,
            strokeDasharray,
            markerEnd: markerEnd,
            markerStart: markerStart,
            cssClass: "flowchart-link");

        // Render edge label if present
        if (!string.IsNullOrEmpty(edge.Label))
        {
            var labelX = edge.LabelPosition.X;
            var labelY = edge.LabelPosition.Y;
            var labelWidth = edge.Label.Length * 8 + 16;
            const int LabelHeight = 24;

            builder.AddRect(
                labelX - labelWidth / 2,
                labelY - LabelHeight / 2,
                labelWidth,
                LabelHeight,
                fill: labelBackground,
                stroke: "none",
                cssClass: "edgeLabel");

            builder.AddText(
                labelX,
                labelY,
                edge.Label,
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize,
                fontFamily: options.FontFamily,
                cssClass: "edgeLabel");
        }
    }

    private static Size MeasureText(ReadOnlySpan<char> text, double fontSize)
    {
        var width = text.Trim().Length * fontSize * 0.55;
        var height = fontSize * 1.5;
        return new(width, height);
    }
}
