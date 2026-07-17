namespace Naiad;

public static class Mermaid
{
    public static string Render(string input, RenderOptions? options = null)
    {
        input = input.Trim();
        options ??= RenderOptions.Default;
        input = StripInitBlock(input);
        var diagramType = DetectDiagramType(input);

        return diagramType switch
        {
            DiagramType.Pie => RenderPie(input, options),
            DiagramType.Flowchart => RenderFlowchart(input, options),
            DiagramType.Sequence => RenderSequence(input, options),
            DiagramType.Class => RenderClass(input, options),
            DiagramType.State => RenderState(input, options),
            DiagramType.EntityRelationship => RenderEntityRelationship(input, options),
            DiagramType.GitGraph => RenderGitGraph(input, options),
            DiagramType.Gantt => RenderGantt(input, options),
            DiagramType.Mindmap => RenderMindmap(input, options),
            DiagramType.Timeline => RenderTimeline(input, options),
            DiagramType.UserJourney => RenderUserJourney(input, options),
            DiagramType.Quadrant => RenderQuadrant(input, options),
            DiagramType.XyChart => RenderXyChart(input, options),
            DiagramType.Sankey => RenderSankey(input, options),
            DiagramType.Block => RenderBlock(input, options),
            DiagramType.Kanban => RenderKanban(input, options),
            DiagramType.Packet => RenderPacket(input, options),
            DiagramType.C4Context => RenderC4(input, options),
            DiagramType.C4Container => RenderC4(input, options),
            DiagramType.C4Component => RenderC4(input, options),
            DiagramType.C4Deployment => RenderC4(input, options),
            DiagramType.Requirement => RenderRequirement(input, options),
            DiagramType.Architecture => RenderArchitecture(input, options),
            DiagramType.Radar => RenderRadar(input, options),
            DiagramType.Treemap => RenderTreemap(input, options),
            _ => throw new MermaidException($"Unsupported diagram type: {diagramType}"),
        };
    }

    /// <summary>
    ///     Attempts to detect the Mermaid diagram type from the first meaningful line of
    ///     <paramref name="input" />, skipping any leading <c>%%{init:...}%%</c> blocks.
    ///     Returns <see langword="false" /> (instead of throwing) when the type is not recognized.
    ///     This is the single source of truth for "is this string a renderable Mermaid diagram?"
    ///     and is consumed both by rendering and by the debugger visualizer's content detection.
    /// </summary>
    public static bool TryDetectDiagramType(string input, out DiagramType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var detected = DetectFromFirstLine(SkipInitBlocks(input));
        if (detected is null)
            return false;

        type = detected.Value;
        return true;
    }

    private static DiagramType DetectDiagramType(string input)
    {
        if (TryDetectDiagramType(input, out var type))
            return type;

        var firstLine = SkipInitBlocks(input);
        throw new MermaidException($"Unknown diagram type in: {firstLine.Split('\n')[0]}");
    }

    /// <summary>Skips leading <c>%%{init:...}%%</c> configuration blocks and returns the remainder, left-trimmed.</summary>
    private static string SkipInitBlocks(string input)
    {
        var remainder = input.TrimStart();

        while (remainder.StartsWith("%%{", StringComparison.Ordinal))
        {
            var endIndex = remainder.IndexOf("}%%", StringComparison.Ordinal);
            if (endIndex < 0)
                break;

            // TrimStart() already consumes the newline(s) between the init block and the
            // diagram keyword, so land directly on the first meaningful content.
            remainder = remainder[(endIndex + 3)..].TrimStart();
        }

        return remainder;
    }

    private static DiagramType? DetectFromFirstLine(string firstLine)
    {
        if (firstLine.StartsWith("pie", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Pie;

        if (firstLine.StartsWith("flowchart", StringComparison.OrdinalIgnoreCase) ||
            firstLine.StartsWith("graph", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Flowchart;

        if (firstLine.StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Sequence;

        if (firstLine.StartsWith("classDiagram", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Class;

        if (firstLine.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase))
            return DiagramType.State;

        if (firstLine.StartsWith("erDiagram", StringComparison.OrdinalIgnoreCase))
            return DiagramType.EntityRelationship;

        if (firstLine.StartsWith("gantt", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Gantt;

        if (firstLine.StartsWith("gitGraph", StringComparison.OrdinalIgnoreCase))
            return DiagramType.GitGraph;

        if (firstLine.StartsWith("mindmap", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Mindmap;

        if (firstLine.StartsWith("timeline", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Timeline;

        if (firstLine.StartsWith("journey", StringComparison.OrdinalIgnoreCase))
            return DiagramType.UserJourney;

        if (firstLine.StartsWith("quadrantChart", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Quadrant;

        if (firstLine.StartsWith("xychart", StringComparison.OrdinalIgnoreCase))
            return DiagramType.XyChart;

        if (firstLine.StartsWith("sankey", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Sankey;

        if (firstLine.StartsWith("block", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Block;

        if (firstLine.StartsWith("packet", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Packet;

        if (firstLine.StartsWith("kanban", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Kanban;

        if (firstLine.StartsWith("architecture-beta", StringComparison.OrdinalIgnoreCase) ||
            firstLine.StartsWith("architecture", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Architecture;

        if (firstLine.StartsWith("C4Context", StringComparison.OrdinalIgnoreCase))
            return DiagramType.C4Context;

        if (firstLine.StartsWith("C4Container", StringComparison.OrdinalIgnoreCase))
            return DiagramType.C4Container;

        if (firstLine.StartsWith("C4Component", StringComparison.OrdinalIgnoreCase))
            return DiagramType.C4Component;

        if (firstLine.StartsWith("C4Deployment", StringComparison.OrdinalIgnoreCase))
            return DiagramType.C4Deployment;

        if (firstLine.StartsWith("requirementDiagram", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Requirement;

        if (firstLine.StartsWith("radar-beta", StringComparison.OrdinalIgnoreCase) ||
            firstLine.StartsWith("radar", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Radar;

        if (firstLine.StartsWith("treemap-beta", StringComparison.OrdinalIgnoreCase) ||
            firstLine.StartsWith("treemap", StringComparison.OrdinalIgnoreCase))
            return DiagramType.Treemap;

        return null;
    }

    /// <summary>
    ///     Strips %%{init:...}%% configuration blocks from the beginning of input.
    /// </summary>
    private static string StripInitBlock(string input)
    {
        var result = input.TrimStart();

        while (result.StartsWith("%%{", StringComparison.Ordinal))
        {
            var endIndex = result.IndexOf("}%%", StringComparison.Ordinal);
            if (endIndex < 0)
                break;

            result = result[(endIndex + 3)..].TrimStart();
        }

        return result;
    }

    private static string ToXml(SvgDocument svg)
    {
        var builder = new StringBuilder();
        svg.ToXml(builder);
        return builder.ToString();
    }

    private static string RenderPie(string input, RenderOptions options)
    {
        var parser = new PieParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse pie chart: {result.Error}");
        }

        var renderer = new PieRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderFlowchart(string input, RenderOptions options)
    {
        var parser = new FlowchartParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse flowchart: {result.Error}");
        }

        var renderer = new FlowchartRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderSequence(string input, RenderOptions options)
    {
        var parser = new SequenceParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse sequence diagram: {result.Error}");
        }

        var renderer = new SequenceRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderClass(string input, RenderOptions options)
    {
        var parser = new ClassParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse class diagram: {result.Error}");
        }

        var renderer = new ClassRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderState(string input, RenderOptions options)
    {
        var parser = new StateParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse state diagram: {result.Error}");
        }

        var renderer = new StateRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderEntityRelationship(string input, RenderOptions options)
    {
        var parser = new ErParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse ER diagram: {result.Error}");
        }

        var renderer = new ErRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderGitGraph(string input, RenderOptions options)
    {
        var parser = new GitGraphParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse git graph: {result.Error}");
        }

        var renderer = new GitGraphRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderGantt(string input, RenderOptions options)
    {
        var parser = new GanttParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse gantt chart: {result.Error}");
        }

        var renderer = new GanttRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderMindmap(string input, RenderOptions options)
    {
        var parser = new MindmapParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse mindmap: {result.Error}");
        }

        var renderer = new MindmapRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderTimeline(string input, RenderOptions options)
    {
        var parser = new TimelineParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse timeline: {result.Error}");
        }

        var renderer = new TimelineRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderUserJourney(string input, RenderOptions options)
    {
        var parser = new UserJourneyParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse user journey: {result.Error}");
        }

        var renderer = new UserJourneyRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderQuadrant(string input, RenderOptions options)
    {
        var parser = new QuadrantParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse quadrant chart: {result.Error}");
        }

        var renderer = new QuadrantRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderXyChart(string input, RenderOptions options)
    {
        var parser = new XyChartParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse XY chart: {result.Error}");
        }

        var renderer = new XyChartRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderSankey(string input, RenderOptions options)
    {
        var parser = new SankeyParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse Sankey diagram: {result.Error}");
        }

        var renderer = new SankeyRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderBlock(string input, RenderOptions options)
    {
        var parser = new BlockParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse block diagram: {result.Error}");
        }

        var renderer = new BlockRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderKanban(string input, RenderOptions options)
    {
        var parser = new KanbanParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse kanban board: {result.Error}");
        }

        var renderer = new KanbanRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderPacket(string input, RenderOptions options)
    {
        var parser = new PacketParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse packet diagram: {result.Error}");
        }

        var renderer = new PacketRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderC4(string input, RenderOptions options)
    {
        var parser = new C4Parser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse C4 diagram: {result.Error}");
        }

        var renderer = new C4Renderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderRequirement(string input, RenderOptions options)
    {
        var parser = new RequirementParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse requirement diagram: {result.Error}");
        }

        var renderer = new RequirementRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderArchitecture(string input, RenderOptions options)
    {
        var parser = new ArchitectureParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse architecture diagram: {result.Error}");
        }

        var renderer = new ArchitectureRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderRadar(string input, RenderOptions options)
    {
        var parser = new RadarParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse radar diagram: {result.Error}");
        }

        var renderer = new RadarRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }

    private static string RenderTreemap(string input, RenderOptions options)
    {
        var parser = new TreemapParser();
        var result = parser.Parse(input);

        if (!result.Success)
        {
            throw new MermaidParseException($"Failed to parse treemap diagram: {result.Error}");
        }

        var renderer = new TreemapRenderer();
        var svg = renderer.Render(result.Value, options);
        return ToXml(svg);
    }
}
