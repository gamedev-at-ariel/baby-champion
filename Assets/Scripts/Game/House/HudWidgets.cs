using UnityEngine;
using UnityEngine.UIElements;

namespace BabyChampion.Game
{
    /// <summary>How pressing a need is. Shown by shape as well as colour (design Section 17, accessibility).</summary>
    public enum Urgency { Calm, Rising, Urgent }

    /// <summary>Circle (calm), triangle (rising) or square (urgent), drawn with Painter2D.</summary>
    public sealed class UrgencyShape : VisualElement
    {
        public static readonly Color CalmColor = new Color(0.36f, 0.7f, 0.42f);
        public static readonly Color RisingColor = new Color(0.95f, 0.66f, 0.2f);
        public static readonly Color UrgentColor = new Color(0.88f, 0.27f, 0.25f);
        public static readonly Color AsleepColor = new Color(0.42f, 0.5f, 0.85f);

        Urgency _urgency;
        Color _color = CalmColor;

        public Urgency Urgency => _urgency;

        public UrgencyShape()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(Urgency urgency, Color color)
        {
            if (urgency == _urgency && color == _color) return;
            _urgency = urgency;
            _color = color;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 1f) return;
            var c = r.center;
            var p = context.painter2D;
            p.fillColor = _color;
            p.strokeColor = new Color(0.2f, 0.15f, 0.2f, 0.8f);
            p.lineWidth = 2.5f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            switch (_urgency)
            {
                case Urgency.Calm:
                    p.Arc(c, s * 0.45f, Angle.Degrees(0f), Angle.Degrees(360f));
                    break;
                case Urgency.Rising:
                    p.MoveTo(c + new Vector2(0, -s * 0.47f));
                    p.LineTo(c + new Vector2(s * 0.48f, s * 0.4f));
                    p.LineTo(c + new Vector2(-s * 0.48f, s * 0.4f));
                    break;
                default:
                    p.MoveTo(c + new Vector2(-s * 0.42f, -s * 0.42f));
                    p.LineTo(c + new Vector2(s * 0.42f, -s * 0.42f));
                    p.LineTo(c + new Vector2(s * 0.42f, s * 0.42f));
                    p.LineTo(c + new Vector2(-s * 0.42f, s * 0.42f));
                    break;
            }
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }
    }

    /// <summary>A filled triangle pointing right; rotated to point at an off-screen event.</summary>
    public sealed class EdgeArrow : VisualElement
    {
        readonly Color _color;

        public EdgeArrow(Color color)
        {
            _color = color;
            pickingMode = PickingMode.Ignore;
            AddToClassList("edge-arrow");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            var c = r.center;
            var p = context.painter2D;
            p.fillColor = _color;
            p.BeginPath();
            p.MoveTo(c + new Vector2(r.width * 0.5f, 0));
            p.LineTo(c + new Vector2(r.width * 0.2f, -r.height * 0.22f));
            p.LineTo(c + new Vector2(r.width * 0.2f, r.height * 0.22f));
            p.ClosePath();
            p.Fill();
        }
    }

    /// <summary>A horizontal meter with an optional threshold mark.</summary>
    public sealed class HudBar
    {
        public readonly VisualElement Root;
        readonly VisualElement _fill;
        readonly VisualElement _mark;
        readonly Label _value;

        public HudBar(VisualElement parent, string name)
        {
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.AddToClassList("bar-row");
            var label = new Label(name) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("bar-name");
            Root.Add(label);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("bar");
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList("bar-fill");
            bar.Add(_fill);
            _mark = new VisualElement { pickingMode = PickingMode.Ignore };
            _mark.AddToClassList("bar-mark");
            bar.Add(_mark);
            Root.Add(bar);
            _value = new Label { pickingMode = PickingMode.Ignore };
            _value.AddToClassList("bar-value");
            Root.Add(_value);
            parent.Add(Root);
        }

        public float Value { get; private set; }

        /// <summary>Sets the value (0-100) and colours it by the threshold: bad past it, warning near it.</summary>
        public void Set(double value, double threshold, bool highIsBad)
        {
            Value = (float)value;
            float v = Mathf.Clamp01((float)value / 100f);
            _fill.style.width = Length.Percent(v * 100f);
            _mark.style.left = Length.Percent(Mathf.Clamp01((float)threshold / 100f) * 100f);
            bool bad = highIsBad ? value >= threshold : value <= threshold;
            bool warn = highIsBad ? value >= threshold * 0.8 : value <= threshold * 1.5;
            _fill.style.backgroundColor = bad ? UrgencyShape.UrgentColor : warn ? UrgencyShape.RisingColor : UrgencyShape.CalmColor;
            _value.text = value.ToString("0");
        }
    }

    /// <summary>A five-pointed star, filled when earned (drawn, so it needs no font glyph).</summary>
    public sealed class StarIcon : VisualElement
    {
        public static readonly Color Gold = new Color(1f, 0.76f, 0.15f);
        public static readonly Color Empty = new Color(0.85f, 0.82f, 0.8f);

        bool _filled;

        public bool Filled
        {
            get => _filled;
            set
            {
                if (_filled == value) return;
                _filled = value;
                MarkDirtyRepaint();
            }
        }

        public StarIcon(bool filled = false)
        {
            _filled = filled;
            pickingMode = PickingMode.Ignore;
            AddToClassList("star");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float outer = Mathf.Min(r.width, r.height) * 0.48f;
            if (outer <= 1f) return;
            float inner = outer * 0.45f;
            var c = r.center + new Vector2(0, outer * 0.06f);
            var p = context.painter2D;
            p.fillColor = _filled ? Gold : Empty;
            p.strokeColor = _filled ? new Color(0.7f, 0.45f, 0.05f) : new Color(0.6f, 0.56f, 0.54f);
            p.lineWidth = 2f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float angle = -Mathf.PI / 2 + i * Mathf.PI / 5;
                float radius = i % 2 == 0 ? outer : inner;
                var point = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }
    }

    /// <summary>A rounded box with a tick when done (drawn, so it needs no font glyph).</summary>
    public sealed class CheckMark : VisualElement
    {
        bool _done;

        public bool Done
        {
            get => _done;
            set
            {
                if (_done == value) return;
                _done = value;
                MarkDirtyRepaint();
            }
        }

        public CheckMark()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("check");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 1f) return;
            var c = r.center;
            var p = context.painter2D;
            float h = s * 0.42f;
            p.fillColor = _done ? UrgencyShape.CalmColor : new Color(1f, 1f, 1f, 0.9f);
            p.strokeColor = _done ? new Color(0.2f, 0.45f, 0.25f) : new Color(0.55f, 0.5f, 0.5f);
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-h, -h));
            p.LineTo(c + new Vector2(h, -h));
            p.LineTo(c + new Vector2(h, h));
            p.LineTo(c + new Vector2(-h, h));
            p.ClosePath();
            p.Fill();
            p.Stroke();
            if (!_done) return;
            p.strokeColor = Color.white;
            p.lineWidth = Mathf.Max(2f, s * 0.14f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-h * 0.55f, 0));
            p.LineTo(c + new Vector2(-h * 0.1f, h * 0.45f));
            p.LineTo(c + new Vector2(h * 0.6f, -h * 0.45f));
            p.Stroke();
        }
    }
}
