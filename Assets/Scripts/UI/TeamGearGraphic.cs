using UnityEngine;
using UnityEngine.UI;

/// <summary>Small, asset-free drivetrain silhouettes. Decorative assemblies never receive pointer input.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TeamGearGraphic : MaskableGraphic
{
    public enum Shape { Assembly, RoundGate, MetalGate, Paddle, SequentialGate, ElectricMode, QuickShiftGate, DragRail, DragHandle }

    private Shape shape;
    private TeamId team;
    private int pendingGear = 1;
    private int rpmSteps;
    private bool current;
    private bool selected;
    private bool available = true;

    public Shape GraphicShape => shape;
    public int EngineLightCount => rpmSteps;

    public void Configure(Shape value, TeamId teamId)
    {
        if (shape == value && team == teamId) return;
        shape = value;
        team = teamId;
        SetVerticesDirty();
    }

    public void SetState(int pending, float engineIntensity)
    {
        int lights = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(engineIntensity) * 10f), 0, 10);
        if (pendingGear == pending && rpmSteps == lights) return;
        pendingGear = pending;
        rpmSteps = lights;
        SetVerticesDirty();
    }

    public void SetGateState(bool isCurrent, bool isSelected, bool isAvailable)
    {
        if (current == isCurrent && selected == isSelected && available == isAvailable) return;
        current = isCurrent;
        selected = isSelected;
        available = isAvailable;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f) return;
        if (shape != Shape.Assembly)
        {
            DrawGate(mesh, rect);
            return;
        }

        Color accent = TeamGearPresentationUI.GetAccent(team);
        Color dark = RacingTechTheme.Graphite;
        Color metal = RacingTechTheme.Secondary;
        // Reserve the upper band for movement and heat. Assemblies share only this lower area.
        Rect assemblyRect = new Rect(rect.x, rect.y + rect.height * 0.00689f, rect.width, rect.height * 0.79245f);
        switch (team)
        {
            case TeamId.UK:
                Quad(mesh, assemblyRect, 0.02f, 0.25f, 0.98f, 0.78f, RacingTechTheme.Raised);
                Line(mesh, assemblyRect, new Vector2(0.25f, 0.35f), new Vector2(0.25f, 0.67f), 5f, dark);
                Line(mesh, assemblyRect, new Vector2(0.75f, 0.35f), new Vector2(0.75f, 0.67f), 5f, dark);
                Line(mesh, assemblyRect, new Vector2(0.25f, 0.51f), new Vector2(0.75f, 0.51f), 5f, dark);
                Line(mesh, assemblyRect, new Vector2(0.5f, 0.51f), GatePosition(pendingGear), 4f, metal);
                break;
            case TeamId.IT:
                Quad(mesh, assemblyRect, 0.02f, 0.25f, 0.98f, 0.78f, RacingTechTheme.Raised);
                Line(mesh, assemblyRect, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.67f), 8f, dark);
                Line(mesh, assemblyRect, new Vector2(0.17f, 0.35f), new Vector2(0.17f, 0.51f), 8f, dark);
                Line(mesh, assemblyRect, new Vector2(0.83f, 0.35f), new Vector2(0.83f, 0.51f), 8f, dark);
                Line(mesh, assemblyRect, new Vector2(0.17f, 0.51f), new Vector2(0.83f, 0.51f), 7f, dark);
                Vector2 italianGate = pendingGear == 1 ? new Vector2(0.5f, 0.67f) : new Vector2(0.17f + (pendingGear - 2) * 0.33f, 0.35f);
                Line(mesh, assemblyRect, new Vector2(0.5f, 0.51f), italianGate, 3f, Color.white);
                break;
            case TeamId.DE:
                Ring(mesh, assemblyRect, new Vector2(0.5f, 0.58f), assemblyRect.height * 0.23f, 4f, metal);
                Quad(mesh, assemblyRect, 0.32f, 0.43f, 0.68f, 0.73f, dark);
                Quad(mesh, assemblyRect, 0.36f, 0.42f, 0.64f, 0.44f, accent);
                Line(mesh, assemblyRect, new Vector2(0.3f, 0.57f), new Vector2(0.7f, 0.57f), 2f, new Color(0.19f, 0.23f, 0.26f));
                break;
            case TeamId.US:
                Quad(mesh, assemblyRect, 0.02f, 0.25f, 0.98f, 0.78f, RacingTechTheme.Raised);
                Quad(mesh, assemblyRect, 0.47f, 0.26f, 0.98f, 0.77f, dark);
                for (int i = 0; i < 3; i++)
                    Quad(mesh, assemblyRect, 0.45f, 0.3f + i * 0.19f, 0.47f, 0.38f + i * 0.19f, accent);
                break;
            case TeamId.CN:
                Quad(mesh, assemblyRect, 0.02f, 0.26f, 0.98f, 0.78f, RacingTechTheme.Raised);
                Quad(mesh, assemblyRect, 0.05f, 0.29f, 0.95f, 0.75f, dark);
                Line(mesh, assemblyRect, new Vector2(0.5f, 0.33f), new Vector2(0.5f, 0.71f), 2f, accent);
                Line(mesh, assemblyRect, new Vector2(0.5f, 0.51f), new Vector2(0.94f, 0.51f), 1.5f, accent);
                Line(mesh, assemblyRect, new Vector2(0.5f, 0.43f), new Vector2(0.06f, 0.43f), 1.5f, accent);
                for (int i = 0; i < 4; i++)
                    Quad(mesh, assemblyRect, 0.1f + i * 0.2f, 0.74f, 0.13f + i * 0.2f, 0.76f, accent);
                break;
            case TeamId.JP:
                Quad(mesh, assemblyRect, 0.02f, 0.25f, 0.98f, 0.78f, RacingTechTheme.Raised);
                Ring(mesh, assemblyRect, new Vector2(0.25f, 0.6f), assemblyRect.height * 0.17f, 2f, metal);
                for (int i = 0; i < 8; i++)
                {
                    float angle = 2.6f - i * 3f / 7f;
                    Vector2 center = Point(assemblyRect, new Vector2(0.25f, 0.6f));
                    float radius = assemblyRect.height * 0.155f;
                    Vector2 from = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    Vector2 to = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius - 3f);
                    LinePixels(mesh, from, to, 2f, i < rpmSteps ? accent : metal);
                }
                float needle = Mathf.Lerp(2.6f, -0.4f, rpmSteps / 10f);
                Vector2 gaugeCenter = Point(assemblyRect, new Vector2(0.25f, 0.6f));
                LinePixels(mesh, gaugeCenter, gaugeCenter + new Vector2(Mathf.Cos(needle), Mathf.Sin(needle)) * assemblyRect.height * 0.13f, 2f, accent);
                Disc(mesh, assemblyRect, new Vector2(0.25f, 0.6f), 2f, Color.white);
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    Vector2 turbine = new Vector2(0.47f, 0.54f);
                    Line(mesh, assemblyRect, turbine,
                        turbine + new Vector2(Mathf.Cos(angle) * 0.035f, Mathf.Sin(angle) * 0.05f), 2f, metal);
                }
                Quad(mesh, assemblyRect, 0.56f, 0.44f, 0.7f, 0.46f, metal);
                Line(mesh, assemblyRect, new Vector2(0.62f, 0.45f), new Vector2(0.64f, 0.65f), 4f, metal);
                Disc(mesh, assemblyRect, new Vector2(0.64f, 0.65f), 5f, accent);
                break;
        }
    }

    private void DrawGate(VertexHelper mesh, Rect rect)
    {
        Color accent = TeamGearPresentationUI.GetAccent(team);
        Color rim = current ? RacingTechTheme.Actual : selected ? RacingTechTheme.Projected : RacingTechTheme.Separator;
        Color fill = current ? RacingTechInstrumentStyle.ActualFill : selected ? RacingTechInstrumentStyle.PendingFill : RacingTechTheme.Raised;
        if (!available && !current && !selected)
        {
            rim = RacingTechTheme.Separator;
            fill = RacingTechTheme.Raised;
        }
        rim *= color;
        fill *= color;
        Color waiting = RacingTechInstrumentStyle.PendingFill * color;
        if (shape == Shape.DragRail)
        {
            Bevel(mesh, rect, RacingTechTheme.Separator, 5f);
            Rect inset = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            Bevel(mesh, inset, RacingTechTheme.Graphite, 4f);
            Quad(mesh, rect, 0.45f, 0.06f, 0.55f, 0.94f, new Color(0.45f, 0.47f, 0.47f));
            for (int i = 0; i < 4; i++)
                Quad(mesh, rect, 0.08f, 0.865f - i * 0.25f, 0.92f, 0.885f - i * 0.25f, accent);
            return;
        }
        if (shape == Shape.Paddle && team == TeamId.DE)
        {
            bool right = transform.parent != null && transform.parent.name.Contains("Up");
            CurvedPaddle(mesh, rect, rim, right);
            Rect inset = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            CurvedPaddle(mesh, inset, fill, right);
            return;
        }
        if (shape == Shape.RoundGate)
        {
            float radius = Mathf.Min(rect.width, rect.height) * 0.49f;
            Disc(mesh, rect, Vector2.one * 0.5f, radius, rim);
            Disc(mesh, rect, Vector2.one * 0.5f, Mathf.Max(0, radius - 1f), fill);
            if (current && selected) DiscSector(mesh, rect, Vector2.one * 0.5f, Mathf.Max(0f, radius - 1f), waiting);
        }
        else
        {
            Bevel(mesh, rect, rim, shape == Shape.Paddle || shape == Shape.QuickShiftGate ? 7f : 3f);
            Rect inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            Bevel(mesh, inner, fill, shape == Shape.Paddle ? 6f : 2f);
            if (current && selected) Quad(mesh, inner, 0.5f, 0.08f, 0.96f, 0.92f, waiting);
            if (shape == Shape.ElectricMode)
            {
                Quad(mesh, rect, 0.09f, 0.07f, 0.91f, 0.11f, accent);
                Line(mesh, rect, new Vector2(0.08f, 0.78f), new Vector2(0.08f, 0.91f), 1.5f, accent);
                Line(mesh, rect, new Vector2(0.08f, 0.91f), new Vector2(0.23f, 0.91f), 1.5f, accent);
                Line(mesh, rect, new Vector2(0.92f, 0.22f), new Vector2(0.92f, 0.09f), 1.5f, accent);
            }
            else if (shape == Shape.QuickShiftGate)
            {
                Quad(mesh, rect, 0.08f, 0.06f, 0.22f, 0.13f, accent);
                Quad(mesh, rect, 0.25f, 0.06f, 0.39f, 0.13f, accent);
            }
            else if (shape == Shape.DragHandle)
                for (int i = 0; i < 3; i++) Quad(mesh, rect, 0.1f, 0.15f + i * 0.3f, 0.9f, 0.19f + i * 0.3f, rim);
        }
    }

    private static void CurvedPaddle(VertexHelper mesh, Rect rect, Color tint, bool right)
    {
        const int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float y0 = i / (float)segments;
            float y1 = (i + 1) / (float)segments;
            float x0 = 0.06f + 0.22f * Mathf.Sin(y0 * Mathf.PI);
            float x1 = 0.06f + 0.22f * Mathf.Sin(y1 * Mathf.PI);
            int start = mesh.currentVertCount;
            Add(mesh, Point(rect, new Vector2(right ? 1f - x0 : x0, y0)), tint);
            Add(mesh, Point(rect, new Vector2(right ? 1f - x1 : x1, y1)), tint);
            Add(mesh, Point(rect, new Vector2(right ? 0.47f - x1 : x1 + 0.53f, y1)), tint);
            Add(mesh, Point(rect, new Vector2(right ? 0.47f - x0 : x0 + 0.53f, y0)), tint);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }

    private static void DiscSector(VertexHelper mesh, Rect rect, Vector2 position, float radius, Color tint)
    {
        Vector2 center = Point(rect, position);
        int start = mesh.currentVertCount;
        Add(mesh, center, tint);
        const int segments = 12;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -Mathf.PI * 0.5f + i * Mathf.PI / segments;
            Add(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
        }
        for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + i + 1, start + i + 2);
    }

    private static void Screw(VertexHelper mesh, Rect rect, Vector2 position)
    {
        Disc(mesh, rect, position, 2.2f, new Color(0.06f, 0.065f, 0.07f));
        Line(mesh, rect, position - new Vector2(0.006f, 0f), position + new Vector2(0.006f, 0f), 1f, Color.gray);
    }

    private static Vector2 GatePosition(int gear) => new Vector2(gear <= 2 ? 0.25f : 0.75f,
        gear == 1 || gear == 3 ? 0.67f : 0.35f);

    private static Vector2 Point(Rect rect, Vector2 position) => rect.min + Vector2.Scale(rect.size, position);

    private static void Quad(VertexHelper mesh, Rect rect, float x0, float y0, float x1, float y1, Color tint)
    {
        Vector2 a = Point(rect, new Vector2(x0, y0));
        Vector2 b = Point(rect, new Vector2(x1, y1));
        int start = mesh.currentVertCount;
        Add(mesh, a, tint); Add(mesh, new Vector2(a.x, b.y), tint);
        Add(mesh, b, tint); Add(mesh, new Vector2(b.x, a.y), tint);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Line(VertexHelper mesh, Rect rect, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 a = Point(rect, from);
        Vector2 b = Point(rect, to);
        LinePixels(mesh, a, b, width, tint);
    }

    private static void LinePixels(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 perpendicular = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        int start = mesh.currentVertCount;
        Add(mesh, a - perpendicular, tint); Add(mesh, a + perpendicular, tint);
        Add(mesh, b + perpendicular, tint); Add(mesh, b - perpendicular, tint);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Disc(VertexHelper mesh, Rect rect, Vector2 position, float radius, Color tint)
    {
        Vector2 center = Point(rect, position);
        int start = mesh.currentVertCount;
        Add(mesh, center, tint);
        const int segments = 24;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Add(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
        }
        for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + i + 1, start + i + 2);
    }

    private static void Ring(VertexHelper mesh, Rect rect, Vector2 position, float radius, float width, Color tint)
    {
        Vector2 center = Point(rect, position);
        int start = mesh.currentVertCount;
        const int segments = 32;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Add(mesh, center + direction * radius, tint);
            Add(mesh, center + direction * Mathf.Max(0f, radius - width), tint);
        }
        for (int i = 0; i < segments; i++)
        {
            mesh.AddTriangle(start + i * 2, start + i * 2 + 1, start + i * 2 + 2);
            mesh.AddTriangle(start + i * 2 + 1, start + i * 2 + 3, start + i * 2 + 2);
        }
    }

    private static void Bevel(VertexHelper mesh, Rect rect, Color tint, float bevel)
    {
        float edge = Mathf.Min(bevel, Mathf.Min(rect.width, rect.height) * 0.25f);
        int start = mesh.currentVertCount;
        Add(mesh, rect.center, tint);
        Add(mesh, new Vector2(rect.xMin + edge, rect.yMin), tint);
        Add(mesh, new Vector2(rect.xMin, rect.yMin + edge), tint);
        Add(mesh, new Vector2(rect.xMin, rect.yMax - edge), tint);
        Add(mesh, new Vector2(rect.xMin + edge, rect.yMax), tint);
        Add(mesh, new Vector2(rect.xMax - edge, rect.yMax), tint);
        Add(mesh, new Vector2(rect.xMax, rect.yMax - edge), tint);
        Add(mesh, new Vector2(rect.xMax, rect.yMin + edge), tint);
        Add(mesh, new Vector2(rect.xMax - edge, rect.yMin), tint);
        for (int i = 0; i < 8; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
    }

    private static void Add(VertexHelper mesh, Vector2 position, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        mesh.AddVert(vertex);
    }
}
