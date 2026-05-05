using System.Xml.Linq;
using Xunit;

namespace Naiad.Tests;

/// <summary>
/// Smoke + structural tests for all Mermaid diagram types supported by Naiad.
///
/// Each test verifies three things:
///   1. Mermaid.Render() does not throw.
///   2. The returned string is non-empty and contains &lt;svg.
///   3. Expected labels (node text, titles, subgraph names) are present in the SVG output.
///      This level of assertion catches: silent parse failures, foreignObject text loss,
///      subgraph rendering omissions, layout coordinate bugs (zero-size SVG), etc.
/// </summary>
public class MermaidRenderTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    static string Render(string input)
    {
        var svg = Mermaid.Render(input);
        Assert.False(string.IsNullOrWhiteSpace(svg), "Render returned null or empty");
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        return svg;
    }

    /// Parses the SVG as XML and returns all inner text values concatenated.
    static string AllText(string svg)
    {
        var doc = XDocument.Parse(svg);
        return string.Concat(doc.Descendants().Select(e => (string?)e.Value ?? ""));
    }

    /// Asserts that the SVG has non-trivial dimensions (width and height > 0).
    /// SvgDocument always outputs width="100%", so we read from viewBox ("0 0 W H")
    /// or the max-width style attribute.
    static void AssertNonTrivialDimensions(string svg)
    {
        var doc = XDocument.Parse(svg);
        var root = doc.Root!;

        double w = 0, h = 0;

        // Parse viewBox (format "0 0 W H")
        var vb = root.Attribute("viewBox")?.Value?.Split(' ');
        if (vb?.Length == 4)
        {
            double.TryParse(vb[2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out w);
            double.TryParse(vb[3], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out h);
        }

        Assert.True(w > 0, $"SVG viewBox width should be > 0 (got {w}; viewBox={root.Attribute("viewBox")?.Value})");
        Assert.True(h > 0, $"SVG viewBox height should be > 0 (got {h}; viewBox={root.Attribute("viewBox")?.Value})");
    }

    static void AssertContainsLabels(string svg, params string[] labels)
    {
        foreach (var label in labels)
            Assert.Contains(label, svg, StringComparison.Ordinal);
    }

    static void AssertHasElement(string svg, string localName)
    {
        var doc = XDocument.Parse(svg);
        var found = doc.Descendants().Any(e => e.Name.LocalName == localName);
        Assert.True(found, $"Expected to find <{localName}> element in SVG");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pie
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Pie_RendersCorrectly()
    {
        const string input = """
            pie title Commits by day
                "Monday" : 25
                "Tuesday" : 30
                "Friday" : 45
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Monday", "Tuesday", "Friday", "Commits by day");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Flowchart — reproduces the original bug (subgraphs, quoted labels, end node)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Flowchart_WithSubgraphsAndQuotedLabels_RendersCorrectly()
    {
        const string input = """
            graph TD
                Start([OrderInputRecord])
                subgraph G0 ["orderIndex: 0 parallel"]
                    G0_CalculateSubtotalStep["CalculateSubtotalStep"]
                    G0_EstimateDeliveryDaysStep["EstimateDeliveryDaysStep"]
                end
                subgraph G1 ["orderIndex: 1 sequential"]
                    G1_ApplyDiscountStep["ApplyDiscountStep"]
                end
                Start --> G0
                G0 --> G1
                G1 --> End([Done])
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);

        // Node labels must be present in SVG text (was broken with foreignObject)
        AssertContainsLabels(svg,
            "CalculateSubtotalStep",
            "EstimateDeliveryDaysStep",
            "ApplyDiscountStep",
            "OrderInputRecord",
            "Done");

        // Subgraph titles must be rendered
        AssertContainsLabels(svg,
            "orderIndex: 0 parallel",
            "orderIndex: 1 sequential");

        // Edges rendered as <path> elements
        AssertHasElement(svg, "path");
    }

    [Fact]
    public void Flowchart_LR_BasicNodes_RendersCorrectly()
    {
        const string input = """
            flowchart LR
                A[Start] --> B{Decision}
                B -->|Yes| C[OK]
                B -->|No| D[Fail]
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Start", "Decision", "OK", "Fail");
        AssertHasElement(svg, "text");
        AssertHasElement(svg, "path");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sequence
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sequence_RendersCorrectly()
    {
        const string input = """
            sequenceDiagram
                participant Alice
                participant Bob
                Alice->>Bob: Hello Bob!
                Bob-->>Alice: Hi Alice!
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Alice", "Bob", "Hello Bob!", "Hi Alice!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Class
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Class_RendersCorrectly()
    {
        const string input = """
            classDiagram
                class Animal {
                    +name: String
                    +makeSound(): void
                }
                class Dog {
                    +fetch(): void
                }
                Animal <|-- Dog
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Animal", "Dog", "name", "makeSound");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void State_RendersCorrectly()
    {
        const string input = """
            stateDiagram-v2
                [*] --> Idle
                Idle --> Running : start
                Running --> Idle : stop
                Running --> [*]
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Idle", "Running");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Entity Relationship
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EntityRelationship_RendersCorrectly()
    {
        const string input = """
            erDiagram
                CUSTOMER ||--o{ ORDER : places
                ORDER ||--|{ LINE-ITEM : contains
                CUSTOMER {
                    string name
                    string email
                }
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "CUSTOMER", "ORDER", "LINE-ITEM");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GitGraph
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GitGraph_RendersCorrectly()
    {
        const string input = """
            gitGraph
                commit id: "Initial"
                branch feature
                checkout feature
                commit id: "Add feature"
                checkout main
                merge feature
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "main", "feature");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gantt
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Gantt_RendersCorrectly()
    {
        const string input = """
            gantt
                title Project Timeline
                dateFormat YYYY-MM-DD
                section Planning
                    Requirements: req, 2024-01-01, 7d
                    Design: des, after req, 5d
                section Development
                    Backend: be, 2024-01-14, 10d
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Project Timeline", "Planning", "Development", "Requirements", "Backend");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mindmap
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Mindmap_RendersCorrectly()
    {
        const string input = """
            mindmap
              root((Project))
                Frontend
                  React
                  CSS
                Backend
                  API
                  Database
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Project", "Frontend", "Backend", "React", "Database");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Timeline
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Timeline_RendersCorrectly()
    {
        const string input = """
            timeline
                title History of Social Media
                2004 : Facebook launched
                2005 : YouTube founded
                2006 : Twitter launched
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "History of Social Media", "Facebook launched", "YouTube founded", "Twitter launched");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // User Journey
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UserJourney_RendersCorrectly()
    {
        const string input = """
            journey
                title My working day
                section Morning
                    Make coffee: 5: Me
                    Read emails: 3: Me
                section Afternoon
                    Write code: 4: Me, Dev
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "My working day", "Morning", "Make coffee", "Write code");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quadrant
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Quadrant_RendersCorrectly()
    {
        const string input = """
            quadrantChart
                title Reach vs Engagement
                x-axis Low Reach --> High Reach
                y-axis Low Engagement --> High Engagement
                Campaign A: [0.3, 0.6]
                Campaign B: [0.7, 0.8]
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Reach vs Engagement", "Campaign A", "Campaign B");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // XY Chart
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void XyChart_RendersCorrectly()
    {
        const string input = """
            xychart-beta
                title "Sales Revenue"
                x-axis [Jan, Feb, Mar, Apr]
                y-axis "Revenue (USD)" 0 --> 10000
                bar [5000, 6000, 7500, 9000]
                line [4500, 5500, 7000, 8500]
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Sales Revenue");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sankey
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sankey_RendersCorrectly()
    {
        const string input = """
            sankey-beta
            Energy,Heat,100
            Energy,Electricity,50
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Energy", "Heat", "Electricity");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Block
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Block_RendersCorrectly()
    {
        const string input = """
            block-beta
                columns 3
                A["Service A"] B["Service B"] C["Service C"]
                space
                D["Database"]
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Service A", "Service B", "Database");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Kanban
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Kanban_RendersCorrectly()
    {
        const string input = """
            kanban
            todo[Todo]
                task1[Write tests]
                task2[Fix bugs]
            inprogress[In Progress]
                task3[Implement feature]
            done[Done]
                task4[Deploy]
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Todo", "Write tests", "Fix bugs", "In Progress", "Implement feature", "Done", "Deploy");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Packet
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Packet_RendersCorrectly()
    {
        const string input = """
            packet-beta
                0-15: Source Port
                16-31: Destination Port
                32-63: Sequence Number
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Source Port", "Destination Port", "Sequence Number");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C4
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void C4Context_RendersCorrectly()
    {
        const string input = """
            C4Context
                title System Context
                Person(customer, "Customer", "A user of the system")
                System(webapp, "Web App", "The main application")
                Rel(customer, webapp, "Uses")
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Customer", "Web App", "System Context");
    }

    [Fact]
    public void C4Container_RendersCorrectly()
    {
        const string input = """
            C4Container
                title Container Diagram
                Person(user, "User")
                Container(spa, "SPA", "React", "Frontend")
                Container(api, "API", "ASP.NET", "Backend")
                Rel(user, spa, "Uses")
                Rel(spa, api, "Calls")
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "User", "SPA", "API");
    }

    [Fact]
    public void C4Component_RendersCorrectly()
    {
        const string input = """
            C4Component
                title Component Diagram
                Component(authController, "Auth Controller", "ASP.NET", "Handles auth")
                Component(userRepo, "User Repository", "EF Core", "Data access")
                Rel(authController, userRepo, "Reads from")
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Auth Controller", "User Repository");
    }

    [Fact]
    public void C4Deployment_RendersCorrectly()
    {
        const string input = """
            C4Deployment
                title Deployment Diagram
                Deployment_Node(azure, "Azure") {
                    Container(appSvc, "App Service", "ASP.NET")
                    ContainerDb(sql, "Azure SQL", "SQL Server")
                }
                Rel(appSvc, sql, "Reads/Writes")
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Azure", "App Service", "Azure SQL");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Requirement
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Requirement_RendersCorrectly()
    {
        const string input = """
            requirementDiagram
                requirement req1 {
                    id: 1
                    text: The system shall authenticate users
                    risk: High
                    verifyMethod: Test
                }
                element login {
                    type: component
                }
                login - satisfies -> req1
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "req1", "login");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Architecture
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Architecture_RendersCorrectly()
    {
        const string input = """
            architecture-beta
                group cloud(cloud)[Cloud]
                service db(disk)[Database] in cloud
                service server(server)[API Server] in cloud
                db:L -- R:server
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Database", "API Server", "Cloud");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Radar
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Radar_RendersCorrectly()
    {
        const string input = """
            radar-beta
                title Team Skills
                axis Frontend, Backend, Testing, DevOps
                curve alice["Alice"]{4,3,5,2}, bob["Bob"]{2,5,3,4}
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Team Skills", "Frontend", "Backend", "Alice", "Bob");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Treemap
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Treemap_RendersCorrectly()
    {
        const string input = """
            treemap-beta
                "Root"
                    "Category A" : 40
                    "Category B" : 30
                    "Category C"
                        "Sub C1" : 15
                        "Sub C2" : 15
            """;

        var svg = Render(input);
        AssertNonTrivialDimensions(svg);
        AssertContainsLabels(svg, "Category A", "Category B", "Sub C1", "Sub C2");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Init block stripping
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void InitBlock_IsStrippedBeforeDetection()
    {
        const string input = """
            %%{init: {"theme": "dark"}}%%
            pie title Slices
                "A" : 60
                "B" : 40
            """;

        // Should not throw MermaidException("Unknown diagram type")
        var svg = Render(input);
        AssertContainsLabels(svg, "Slices", "A", "B");
    }
}
