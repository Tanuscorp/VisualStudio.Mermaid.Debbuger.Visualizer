namespace Naiad.Diagrams.C4;

public class C4Renderer : IDiagramRenderer<C4Model>
{
    const double elementWidth = 160;
    const double elementHeight = 100;
    const double personHeight = 120;
    const double elementSpacing = 30;
    const double titleHeight = 50;
    const double rowSpacing = 40;
    const double boundaryPadding = 15;
    const double boundaryTitleHeight = 40;
    const double boundarySpacing = 20;

    const string personColor = "#08427B";
    const string personExtColor = "#999999";
    const string systemColor = "#1168BD";
    const string systemDbColor = "#1168BD";
    const string systemExtColor = "#999999";
    const string containerColor = "#438DD5";
    const string containerDbColor = "#438DD5";
    const string componentColor = "#85BBF0";
    const string boundaryStroke = "#444444";
    const string boundaryFill = "#FFFFFF";

    // Cached dimensions during rendering
    readonly Dictionary<string, (double w, double h)> boundaryDimensions = new();
    readonly Dictionary<string, (double x, double y, double w, double h)> elementPositions = new();
    readonly Dictionary<string, (double x, double y, double w, double h)> boundaryPositions = new();

    public SvgDocument Render(C4Model model, RenderOptions options)
    {
        boundaryDimensions.Clear();
        elementPositions.Clear();
        boundaryPositions.Clear();

        if (model.Elements.Count == 0 && model.Boundaries.Count == 0)
        {
            var emptyBuilder = new SvgBuilder().Size(200, 100);
            emptyBuilder.AddText(
                100,
                50,
                "Empty C4 diagram",
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize,
                fontFamily: options.FontFamily);
            return emptyBuilder.Build();
        }

        // Step 1: Calculate all boundary dimensions (bottom-up)
        var topLevelBoundaries = model.Boundaries.Where(_ => _.ParentBoundaryId == null).ToList();
        foreach (var boundary in topLevelBoundaries)
        {
            CalculateBoundaryDimensions(model, boundary);
        }

        // Step 2: Get elements outside any boundary
        var outsideElements = model.Elements.Where(_ => _.BoundaryId == null).ToList();
        var outsidePersons = outsideElements.Where(_ => _.Type == C4ElementType.Person).ToList();
        var outsideSystems = outsideElements.Where(_ => _.Type is C4ElementType.System or C4ElementType.SystemDb).ToList();
        var outsideContainers = outsideElements.Where(_ =>
            _.Type is C4ElementType.Container or C4ElementType.ContainerDb or C4ElementType.ContainerQueue).ToList();
        var outsideComponents = outsideElements.Where(_ => _.Type == C4ElementType.Component).ToList();

        // Step 3: Calculate total diagram dimensions
        var titleOffset = string.IsNullOrEmpty(model.Title) ? 0 : titleHeight;

        // Calculate outside element rows
        var outsidePersonsHeight = outsidePersons.Count > 0 ? personHeight + rowSpacing : 0;
        var outsideSystemsHeight = outsideSystems.Count > 0 ? elementHeight + rowSpacing : 0;
        var outsideContainersHeight = outsideContainers.Count > 0 ? elementHeight + rowSpacing : 0;
        var outsideComponentsHeight = outsideComponents.Count > 0 ? elementHeight + rowSpacing : 0;

        // Calculate top-level boundary row dimensions
        var boundaryRowWidth = topLevelBoundaries.Sum(_ => boundaryDimensions[_.Id].w + boundarySpacing) - boundarySpacing;
        var boundaryRowHeight = topLevelBoundaries.Count > 0
            ? topLevelBoundaries.Max(_ => boundaryDimensions[_.Id].h) + rowSpacing
            : 0;

        // Calculate width based on elements and boundaries
        const int MaxElementsPerRow = 4;
        var outsideElementsWidth = Math.Max(
            Math.Max(outsidePersons.Count, outsideSystems.Count),
            Math.Max(outsideContainers.Count, outsideComponents.Count)
        );
        outsideElementsWidth = Math.Min(outsideElementsWidth, MaxElementsPerRow);
        var outsideWidth = outsideElementsWidth * (elementWidth + elementSpacing) - elementSpacing;

        var width = Math.Max(Math.Max(outsideWidth, boundaryRowWidth), 400) + options.Padding * 2;
        var height = titleOffset + outsidePersonsHeight + outsideSystemsHeight +
                    boundaryRowHeight + outsideContainersHeight + outsideComponentsHeight +
                    options.Padding * 2 + 50;

        var builder = new SvgBuilder().Size(width, height);

        // Add arrow marker
        builder.AddArrowMarker("c4arrow", "#666");

        // Draw title
        if (!string.IsNullOrEmpty(model.Title))
        {
            builder.AddText(
                width / 2,
                options.Padding + titleHeight / 2,
                model.Title,
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize + 6,
                fontFamily: options.FontFamily,
                fontWeight: "bold");
        }

        var currentY = options.Padding + titleOffset;

        // Draw outside persons
        currentY = DrawElementRow(builder, outsidePersons, currentY, width, options);

        // Draw outside systems
        currentY = DrawElementRow(builder, outsideSystems, currentY, width, options);

        // Draw top-level boundaries (recursively handles nested)
        if (topLevelBoundaries.Count > 0)
        {
            var boundaryStartX = (width - boundaryRowWidth) / 2;
            foreach (var boundary in topLevelBoundaries)
            {
                var (bw, bh) = boundaryDimensions[boundary.Id];
                DrawBoundaryRecursive(builder, model, boundary, boundaryStartX, currentY, bw, bh, options);
                boundaryStartX += bw + boundarySpacing;
            }
            currentY += topLevelBoundaries.Max(_ => boundaryDimensions[_.Id].h) + rowSpacing;
        }

        // Draw outside containers
        currentY = DrawElementRow(builder, outsideContainers, currentY, width, options);

        // Draw outside components
        DrawElementRow(builder, outsideComponents, currentY, width, options);

        // Draw relationships
        foreach (var rel in model.Relationships)
        {
            if (elementPositions.TryGetValue(rel.From, out var fromPos) &&
                elementPositions.TryGetValue(rel.To, out var toPos))
            {
                DrawRelationship(builder, fromPos, toPos, rel.Label, options);
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Recursively calculate boundary dimensions (bottom-up).
    /// </summary>
    (double w, double h) CalculateBoundaryDimensions(C4Model model, C4Boundary boundary)
    {
        // Get direct elements in this boundary
        var directElements = model.Elements.Where(_ => _.BoundaryId == boundary.Id).ToList();

        // Get child boundaries
        var childBoundaries = model.Boundaries.Where(_ => _.ParentBoundaryId == boundary.Id).ToList();

        // Recursively calculate child boundary dimensions first
        foreach (var child in childBoundaries)
        {
            CalculateBoundaryDimensions(model, child);
        }

        // Calculate content dimensions
        double contentWidth = 0;
        double contentHeight = 0;

        // Layout: child boundaries in a row, then direct elements below
        if (childBoundaries.Count > 0)
        {
            var childrenWidth = childBoundaries.Sum(_ => boundaryDimensions[_.Id].w + boundarySpacing) - boundarySpacing;
            var childrenHeight = childBoundaries.Max(_ => boundaryDimensions[_.Id].h);
            contentWidth = Math.Max(contentWidth, childrenWidth);
            contentHeight += childrenHeight + (directElements.Count > 0 ? rowSpacing : 0);
        }

        // Add direct elements (laid out in a row)
        if (directElements.Count > 0)
        {
            var elementsWidth = directElements.Count * (elementWidth + elementSpacing) - elementSpacing;
            var elementsHeight = directElements.Max(_ => _.Type == C4ElementType.Person ? personHeight : elementHeight);
            contentWidth = Math.Max(contentWidth, elementsWidth);
            contentHeight += elementsHeight;
        }

        // Ensure minimum dimensions
        contentWidth = Math.Max(contentWidth, elementWidth);
        contentHeight = Math.Max(contentHeight, elementHeight);

        // Add boundary padding and title
        var totalWidth = contentWidth + boundaryPadding * 2;
        var totalHeight = contentHeight + boundaryPadding * 2 + boundaryTitleHeight;

        boundaryDimensions[boundary.Id] = (totalWidth, totalHeight);
        return (totalWidth, totalHeight);
    }

    /// <summary>
    /// Recursively draw a boundary and its contents.
    /// </summary>
    void DrawBoundaryRecursive(
        SvgBuilder builder,
        C4Model model,
        C4Boundary boundary,
        double x,
        double y,
        double width,
        double height,
        RenderOptions options)
    {
        // Draw boundary box
        builder.AddRect(
            x,
            y,
            width,
            height,
            rx: 5,
            fill: boundaryFill,
            stroke: boundaryStroke,
            strokeWidth: 2,
            style: "stroke-dasharray: 8 4");

        // Draw boundary label
        builder.AddText(
            x + width / 2,
            y + boundaryTitleHeight / 2 - 5,
            boundary.Label,
            anchor: "middle",
            baseline: "middle",
            fontSize: options.FontSize,
            fontFamily: options.FontFamily,
            fontWeight: "bold",
            fill: "#333333");

        // Draw boundary type indicator
        var typeLabel = boundary.Type switch
        {
            C4BoundaryType.Container => "[Container]",
            C4BoundaryType.System => "[System]",
            C4BoundaryType.Enterprise => "[Enterprise]",
            C4BoundaryType.Deployment => "[Deployment]",
            C4BoundaryType.Node => "[Node]",
            _ => ""
        };
        if (!string.IsNullOrEmpty(typeLabel))
        {
            builder.AddText(
                x + width / 2,
                y + boundaryTitleHeight / 2 + 10,
                typeLabel,
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize - 3,
                fontFamily: options.FontFamily,
                fill: "#666666");
        }

        boundaryPositions[boundary.Id] = (x + width / 2, y + height / 2, width, height);

        // Content area starts after title
        var contentY = y + boundaryTitleHeight + boundaryPadding;

        // Get child boundaries and direct elements
        var childBoundaries = model.Boundaries.Where(_ => _.ParentBoundaryId == boundary.Id).ToList();
        var directElements = model.Elements.Where(_ => _.BoundaryId == boundary.Id).ToList();

        // Draw child boundaries first (in a row)
        if (childBoundaries.Count > 0)
        {
            var childrenTotalWidth = childBoundaries.Sum(_ => boundaryDimensions[_.Id].w + boundarySpacing) - boundarySpacing;
            var childStartX = x + (width - childrenTotalWidth) / 2;

            foreach (var child in childBoundaries)
            {
                var (cw, ch) = boundaryDimensions[child.Id];
                DrawBoundaryRecursive(builder, model, child, childStartX, contentY, cw, ch, options);
                childStartX += cw + boundarySpacing;
            }

            // Move content Y down past child boundaries
            contentY += childBoundaries.Max(_ => boundaryDimensions[_.Id].h) + rowSpacing;
        }

        // Draw direct elements in this boundary
        if (directElements.Count > 0)
        {
            var elementsWidth = directElements.Count * (elementWidth + elementSpacing) - elementSpacing;
            var startX = x + (width - elementsWidth) / 2;

            foreach (var element in directElements)
            {
                var eh = element.Type == C4ElementType.Person ? personHeight : elementHeight;
                elementPositions[element.Id] = (startX + elementWidth / 2, contentY + eh / 2, ElementWidth: elementWidth, eh);
                DrawElement(builder, element, startX, contentY, options);
                startX += elementWidth + elementSpacing;
            }
        }
    }

    double DrawElementRow(
        SvgBuilder builder,
        List<C4Element> elements,
        double startY,
        double totalWidth,
        RenderOptions options)
    {
        if (elements.Count == 0)
        {
            return startY;
        }

        var rowWidth = elements.Count * (elementWidth + elementSpacing) - elementSpacing;
        var startX = (totalWidth - rowWidth) / 2;

        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            var x = startX + i * (elementWidth + elementSpacing);
            var h = element.Type == C4ElementType.Person ? personHeight : elementHeight;

            elementPositions[element.Id] = (x + elementWidth / 2, startY + h / 2, ElementWidth: elementWidth, h);
            DrawElement(builder, element, x, startY, options);
        }

        var maxHeight = elements.Max(_ => _.Type == C4ElementType.Person ? personHeight : elementHeight);
        return startY + maxHeight + rowSpacing;
    }

    static void DrawElement(SvgBuilder builder, C4Element element, double x, double y, RenderOptions options)
    {
        var color = GetElementColor(element);
        const string TextColor = "#FFFFFF";

        if (element.Type == C4ElementType.Person)
        {
            // Draw person shape (head + body)
            const int HeadRadius = 20;
            const int BodyHeight = 60;
            const int BodyWidth = 80;

            // Head
            builder.AddCircle(
                x + elementWidth / 2,
                y + HeadRadius + 5,
                HeadRadius,
                fill: color,
                stroke: "none");

            // Body (rounded rect)
            builder.AddRect(
                x + (elementWidth - BodyWidth) / 2,
                y + HeadRadius * 2 + 10,
                BodyWidth,
                BodyHeight,
                rx: 10,
                fill: color,
                stroke: "none");

            // Label
            builder.AddText(
                x + elementWidth / 2,
                y + personHeight - 20,
                element.Label,
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize - 1,
                fontFamily: options.FontFamily,
                fill: TextColor,
                fontWeight: "bold");

            // Description
            if (!string.IsNullOrEmpty(element.Description))
            {
                builder.AddText(
                    x + elementWidth / 2,
                    y + personHeight - 5,
                    TruncateText(element.Description, 25),
                    anchor: "middle",
                    baseline: "middle",
                    fontSize: options.FontSize - 3,
                    fontFamily: options.FontFamily,
                    fill: TextColor);
            }
        }
        else if (element.Type is
                 C4ElementType.ContainerDb or
                 C4ElementType.SystemDb)
        {
            // Draw database shape (cylinder)
            const int EllipseHeight = 15;

            // Top ellipse
            builder.AddEllipse(
                x + elementWidth / 2,
                y + EllipseHeight,
                elementWidth / 2 - 5,
                EllipseHeight,
                fill: color, stroke: "none");

            // Body
            builder.AddRect(
                x + 5,
                y + EllipseHeight,
                elementWidth - 10,
                elementHeight - EllipseHeight * 2,
                fill: color,
                stroke: "none");

            // Bottom ellipse
            builder.AddEllipse(
                x + elementWidth / 2,
                y + elementHeight - EllipseHeight,
                elementWidth / 2 - 5,
                EllipseHeight,
                fill: color,
                stroke: "none");

            DrawElementText(builder, element, x, y, options, TextColor);
        }
        else
        {
            // Standard box
            builder.AddRect(
                x,
                y,
                elementWidth,
                elementHeight,
                rx: 5,
                fill: color,
                stroke: "none");

            DrawElementText(builder, element, x, y, options, TextColor);
        }
    }

    static void DrawElementText(
        SvgBuilder builder,
        C4Element element,
        double x,
        double y,
        RenderOptions options,
        string textColor)
    {
        var centerX = x + elementWidth / 2;
        var textY = y + 25;

        // Label
        builder.AddText(
            centerX,
            textY,
            element.Label,
            anchor: "middle",
            baseline: "middle",
            fontSize: options.FontSize - 1,
            fontFamily: options.FontFamily,
            fill: textColor,
            fontWeight: "bold");

        // Technology
        if (!string.IsNullOrEmpty(element.Technology))
        {
            textY += 18;
            builder.AddText(
                centerX,
                textY,
                $"[{element.Technology}]",
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize - 3,
                fontFamily: options.FontFamily,
                fill: textColor);
        }

        // Description
        if (!string.IsNullOrEmpty(element.Description))
        {
            textY += 18;
            builder.AddText(
                centerX,
                textY,
                TruncateText(element.Description, 22),
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize - 3,
                fontFamily: options.FontFamily,
                fill: textColor);
        }
    }

    static void DrawRelationship(
        SvgBuilder builder,
        (double x, double y, double w, double h) from,
        (double x, double y, double w, double h) to,
        string? label,
        RenderOptions options)
    {
        // Calculate connection points
        var dx = to.x - from.x;
        var dy = to.y - from.y;
        var angle = Math.Atan2(dy, dx);

        var fromX = from.x + Math.Cos(angle) * from.w / 2;
        var fromY = from.y + Math.Sin(angle) * from.h / 2;
        var toX = to.x - Math.Cos(angle) * to.w / 2;
        var toY = to.y - Math.Sin(angle) * to.h / 2;

        // Draw line
        builder.AddLine(
            fromX,
            fromY,
            toX,
            toY,
            stroke: "#666",
            strokeWidth: 1.5,
            strokeDasharray: "5,5");

        // Draw arrowhead manually
        const int ArrowSize = 8;
        const double ArrowAngle = Math.PI / 6;
        var ax1 = toX - ArrowSize * Math.Cos(angle - ArrowAngle);
        var ay1 = toY - ArrowSize * Math.Sin(angle - ArrowAngle);
        var ax2 = toX - ArrowSize * Math.Cos(angle + ArrowAngle);
        var ay2 = toY - ArrowSize * Math.Sin(angle + ArrowAngle);

        builder.AddPath(
            string.Create(CultureInfo.InvariantCulture, $"M {toX:0.##} {toY:0.##} L {ax1:0.##} {ay1:0.##} L {ax2:0.##} {ay2:0.##} Z"),
            fill: "#666",
            stroke: "none");

        // Draw label
        if (!string.IsNullOrEmpty(label))
        {
            var midX = (fromX + toX) / 2;
            var midY = (fromY + toY) / 2;

            builder.AddText(
                midX,
                midY - 8,
                label,
                anchor: "middle",
                baseline: "middle",
                fontSize: options.FontSize - 3,
                fontFamily: options.FontFamily,
                fill: "#666");
        }
    }

    static string GetElementColor(C4Element element)
    {
        if (element.IsExternal)
        {
            return element.Type == C4ElementType.Person ? personExtColor : systemExtColor;
        }

        return element.Type switch
        {
            C4ElementType.Person => personColor,
            C4ElementType.System => systemColor,
            C4ElementType.SystemDb => systemDbColor,
            C4ElementType.Container => containerColor,
            C4ElementType.ContainerDb => containerDbColor,
            C4ElementType.ContainerQueue => containerColor,
            C4ElementType.Component => componentColor,
            _ => systemColor
        };
    }

    static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }

}
