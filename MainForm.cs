using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace FlowTurkViewer
{
    public partial class MainForm : Form
    {
        private FlowDocument _document;
        private readonly Dictionary<string, FlowSymbolControl> _symbolControls =
            new Dictionary<string, FlowSymbolControl>(StringComparer.Ordinal);
        private FlowSymbolControl _selectedSymbol;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1)
                return;

            try
            {
                FlowDocument document = ParseFile(File.ReadAllLines(files[0]));
                DisplayDocument(document);

                if (labelDrag != null)
                    labelDrag.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The selected file could not be loaded.\r\n\r\n" + ex.Message,
                    "Invalid FlowTurk file",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void MainForm_DragOver(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? e.Data.GetData(DataFormats.FileDrop) as string[]
                : null;

            e.Effect = files != null && files.Length == 1
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_document == null || _document.Connections.Count == 0)
                return;

            Graphics graphics = e.Graphics;
            SmoothingMode previousSmoothingMode = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen linePen = new Pen(
                _document.Header.LineColor,
                Math.Max(1, _document.Header.LineThickness)))
            using (Brush arrowBrush = new SolidBrush(_document.Header.LineColor))
            using (Font conditionFont = new Font(SystemFonts.DefaultFont.FontFamily, 8f, FontStyle.Bold))
            using (Brush conditionBackBrush = new SolidBrush(BackColor))
            {
                foreach (FlowConnection connection in _document.Connections)
                {
                    FlowSymbolControl startControl;
                    FlowSymbolControl endControl;

                    if (!_symbolControls.TryGetValue(connection.StartSymbolId, out startControl) ||
                        !_symbolControls.TryGetValue(connection.EndSymbolId, out endControl))
                    {
                        continue;
                    }

                    Rectangle startBounds = startControl.Bounds;
                    Rectangle endBounds = endControl.Bounds;
                    PointF startCenter = GetCenter(startBounds);
                    PointF endCenter = GetCenter(endBounds);
                    PointF startPoint = GetBoundaryPoint(startBounds, endCenter);
                    PointF endPoint = GetBoundaryPoint(endBounds, startCenter);

                    graphics.DrawLine(linePen, startPoint, endPoint);
                    DrawArrowHead(graphics, arrowBrush, startPoint, endPoint);

                    if (!string.IsNullOrWhiteSpace(connection.IfCondition))
                    {
                        string condition = connection.IfCondition.Trim();
                        Size textSize = TextRenderer.MeasureText(condition, conditionFont);
                        float centerX = (startPoint.X + endPoint.X) / 2f;
                        float centerY = (startPoint.Y + endPoint.Y) / 2f;
                        Rectangle textBounds = new Rectangle(
                            (int)(centerX - textSize.Width / 2f) - 2,
                            (int)(centerY - textSize.Height / 2f) - 2,
                            textSize.Width + 4,
                            textSize.Height + 4);

                        graphics.FillRectangle(conditionBackBrush, textBounds);
                        TextRenderer.DrawText(
                            graphics,
                            condition,
                            conditionFont,
                            textBounds,
                            ForeColor,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    }
                }
            }

            graphics.SmoothingMode = previousSmoothingMode;
        }

        private static PointF GetCenter(Rectangle bounds)
        {
            return new PointF(
                bounds.Left + bounds.Width / 2f,
                bounds.Top + bounds.Height / 2f);
        }

        private static PointF GetBoundaryPoint(Rectangle bounds, PointF toward)
        {
            PointF center = GetCenter(bounds);
            float dx = toward.X - center.X;
            float dy = toward.Y - center.Y;

            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                return center;

            float scaleX = Math.Abs(dx) < 0.001f
                ? float.MaxValue
                : (bounds.Width / 2f) / Math.Abs(dx);
            float scaleY = Math.Abs(dy) < 0.001f
                ? float.MaxValue
                : (bounds.Height / 2f) / Math.Abs(dy);
            float scale = Math.Min(scaleX, scaleY);

            return new PointF(center.X + dx * scale, center.Y + dy * scale);
        }

        private static void DrawArrowHead(Graphics graphics, Brush brush, PointF start, PointF end)
        {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            if (length < 1f)
                return;

            float ux = dx / length;
            float uy = dy / length;
            const float arrowLength = 9f;
            const float arrowHalfWidth = 4f;

            PointF basePoint = new PointF(
                end.X - ux * arrowLength,
                end.Y - uy * arrowLength);
            PointF perpendicular = new PointF(-uy, ux);
            PointF left = new PointF(
                basePoint.X + perpendicular.X * arrowHalfWidth,
                basePoint.Y + perpendicular.Y * arrowHalfWidth);
            PointF right = new PointF(
                basePoint.X - perpendicular.X * arrowHalfWidth,
                basePoint.Y - perpendicular.Y * arrowHalfWidth);

            graphics.FillPolygon(brush, new[] { end, left, right });
        }

        private static FlowDocument ParseFile(string[] lines)
        {
            if (lines == null)
                throw new ArgumentNullException("lines");

            FlowDocument document = new FlowDocument();
            bool readingConnections = false;
            bool headerFound = false;
            bool separatorFound = false;
            int lineNumber = 0;

            foreach (string originalLine in lines)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(originalLine))
                    continue;

                string line = originalLine.Trim();
                if (line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line == "2")
                {
                    if (!headerFound)
                        throw CreateFormatException(lineNumber, "The file header must appear before the '2' separator.");
                    if (separatorFound)
                        throw CreateFormatException(lineNumber, "The '2' separator can only appear once.");

                    readingConnections = true;
                    separatorFound = true;
                    continue;
                }

                // StringSplitOptions.None is important: the empty symbol fields are
                // part of the documented format (for example, "opcode~~text~~...").
                string[] fields = line.Split(new[] { '~' }, StringSplitOptions.None);

                if (!readingConnections && fields.Length > 0 && fields[0] == "1")
                {
                    if (headerFound)
                        throw CreateFormatException(lineNumber, "The file header can only appear once.");

                    RequireFieldCount(fields, 6, lineNumber, "header");
                    document.Header = new FlowHeader
                    {
                        RunDelay = ParseInteger(fields[1], "RunDelay", lineNumber),
                        LineColor = ParseColor(fields[2], "LineColor", lineNumber),
                        LineThickness = ParseInteger(fields[3], "LineThickness", lineNumber),
                        BackColor = ParseColor(fields[4], "BackColor", lineNumber),
                        SelectionColor = ParseColor(fields[5], "SelectionColor", lineNumber)
                    };

                    if (document.Header.RunDelay < 0)
                        throw CreateFormatException(lineNumber, "RunDelay cannot be negative.");
                    if (document.Header.LineThickness < 0)
                        throw CreateFormatException(lineNumber, "LineThickness cannot be negative.");

                    headerFound = true;
                    continue;
                }

                if (!headerFound)
                    throw CreateFormatException(lineNumber, "The file must start with a header record.");

                if (readingConnections)
                    document.Connections.Add(ParseConnection(fields, lineNumber));
                else
                    document.Symbols.Add(ParseSymbol(fields, lineNumber));
            }

            if (!headerFound)
                throw new FormatException("The file does not contain a header record.");
            if (!separatorFound)
                throw new FormatException("The file does not contain the '2' separator before its connection records.");

            Dictionary<string, FlowSymbol> symbolsById =
                new Dictionary<string, FlowSymbol>(StringComparer.Ordinal);

            foreach (FlowSymbol symbol in document.Symbols)
            {
                if (symbolsById.ContainsKey(symbol.Id))
                    throw new FormatException("Duplicate symbol ID: " + symbol.Id);

                symbolsById.Add(symbol.Id, symbol);
            }

            foreach (FlowConnection connection in document.Connections)
            {
                if (!symbolsById.ContainsKey(connection.StartSymbolId))
                    throw new FormatException("Connection '" + connection.Id + "' refers to an unknown start symbol: " + connection.StartSymbolId);
                if (!symbolsById.ContainsKey(connection.EndSymbolId))
                    throw new FormatException("Connection '" + connection.Id + "' refers to an unknown end symbol: " + connection.EndSymbolId);
            }

            return document;
        }

        private static FlowSymbol ParseSymbol(string[] fields, int lineNumber)
        {
            // SymID~Opcode~~Text~~VertPos~HorPos~Width~Height~BackColor~TextColor~OutlineColor~FontName~FontSize~FontStyle~TitleColor
            RequireFieldCount(fields, 16, lineNumber, "symbol");

            string id = fields[0].Trim();
            if (id.Length == 0)
                throw CreateFormatException(lineNumber, "A symbol ID cannot be empty.");

            int width = ParseInteger(fields[7], "Width", lineNumber);
            int height = ParseInteger(fields[8], "Height", lineNumber);
            int fontSize = ParseInteger(fields[13], "FontSize", lineNumber);
            int fontStyleValue = ParseInteger(fields[14], "FontStyle", lineNumber);

            if (width <= 0 || height <= 0)
                throw CreateFormatException(lineNumber, "Symbol width and height must be greater than zero.");
            if (fontSize <= 0)
                throw CreateFormatException(lineNumber, "FontSize must be greater than zero.");

            return new FlowSymbol
            {
                Id = id,
                Opcode = fields[1].Trim(),
                // Some records in the supplied example put the text in field 2,
                // while others put it in field 3. Keep both layouts readable.
                Text = fields[3].Length > 0 ? fields[3] : fields[2],
                VerticalPosition = ParseInteger(fields[5], "VertPos", lineNumber),
                HorizontalPosition = ParseInteger(fields[6], "HorPos", lineNumber),
                Width = width,
                Height = height,
                BackColor = ParseColor(fields[9], "BackColor", lineNumber),
                TextColor = ParseColor(fields[10], "TextColor", lineNumber),
                OutlineColor = ParseColor(fields[11], "OutlineColor", lineNumber),
                FontName = string.IsNullOrWhiteSpace(fields[12]) ? SystemFonts.DefaultFont.Name : fields[12].Trim(),
                FontSize = fontSize,
                FontStyle = ParseFontStyle(fontStyleValue, lineNumber),
                TitleColor = ParseColor(fields[15], "TitleColor", lineNumber)
            };
        }

        private static FlowConnection ParseConnection(string[] fields, int lineNumber)
        {
            // ConnID~StartSymID~EndSymID~IfCondition
            RequireFieldCount(fields, 4, lineNumber, "connection");

            string id = fields[0].Trim();
            string startId = fields[1].Trim();
            string endId = fields[2].Trim();

            if (id.Length == 0 || startId.Length == 0 || endId.Length == 0)
                throw CreateFormatException(lineNumber, "Connection ID, start symbol ID, and end symbol ID are required.");

            return new FlowConnection
            {
                Id = id,
                StartSymbolId = startId,
                EndSymbolId = endId,
                IfCondition = fields[3].Trim()
            };
        }

        private static FontStyle ParseFontStyle(int value, int lineNumber)
        {
            switch (value)
            {
                case 0: return FontStyle.Regular;
                case 1: return FontStyle.Bold;
                case 2: return FontStyle.Italic;
                case 3: return FontStyle.Bold | FontStyle.Italic;
                case 4: return FontStyle.Underline;
                case 5: return FontStyle.Bold | FontStyle.Italic | FontStyle.Underline;
                default:
                    throw CreateFormatException(lineNumber, "FontStyle must be a value from 0 to 5.");
            }
        }

        private static int ParseInteger(string value, string fieldName, int lineNumber)
        {
            int result;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                throw CreateFormatException(lineNumber, "The " + fieldName + " value is not a valid integer: '" + value + "'.");

            return result;
        }

        private static Color ParseColor(string value, string fieldName, int lineNumber)
        {
            int argb = ParseInteger(value, fieldName, lineNumber);
            try
            {
                return Color.FromArgb(argb);
            }
            catch (ArgumentException)
            {
                throw CreateFormatException(lineNumber, "The " + fieldName + " value is not a valid ARGB color: '" + value + "'.");
            }
        }

        private static void RequireFieldCount(string[] fields, int expectedCount, int lineNumber, string recordType)
        {
            if (fields.Length != expectedCount)
            {
                throw CreateFormatException(
                    lineNumber,
                    "A " + recordType + " record must contain " + expectedCount +
                    " fields, but " + fields.Length + " were found. Empty fields must be kept.");
            }
        }

        private static FormatException CreateFormatException(int lineNumber, string message)
        {
            return new FormatException("Line " + lineNumber + ": " + message);
        }

        private void DisplayDocument(FlowDocument document)
        {
            SuspendLayout();

            foreach (FlowSymbolControl oldControl in _symbolControls.Values)
            {
                Controls.Remove(oldControl);
                oldControl.Dispose();
            }
            _symbolControls.Clear();
            _selectedSymbol = null;

            _document = document;
            BackColor = document.Header.BackColor;

            foreach (FlowSymbol symbol in document.Symbols)
            {
                FlowSymbolControl symbolControl = new FlowSymbolControl(symbol, document.Header.SelectionColor)
                {
                    Name = symbol.Id,
                    Location = new Point(symbol.HorizontalPosition, symbol.VerticalPosition),
                    Size = new Size(symbol.Width, symbol.Height),
                    TabStop = false
                };

                symbolControl.Click += SymbolControl_Click;
                _symbolControls.Add(symbol.Id, symbolControl);
                Controls.Add(symbolControl);
            }

            ResumeLayout(true);
            Invalidate();
        }

        private void SymbolControl_Click(object sender, EventArgs e)
        {
            FlowSymbolControl clickedControl = sender as FlowSymbolControl;
            if (clickedControl == null || clickedControl == _selectedSymbol)
                return;

            if (_selectedSymbol != null)
                _selectedSymbol.SetSelected(false);

            _selectedSymbol = clickedControl;
            _selectedSymbol.SetSelected(true);
        }

        private sealed class FlowDocument
        {
            public FlowHeader Header { get; set; }
            public List<FlowSymbol> Symbols { get; private set; }
            public List<FlowConnection> Connections { get; private set; }

            public FlowDocument()
            {
                Symbols = new List<FlowSymbol>();
                Connections = new List<FlowConnection>();
            }
        }

        private sealed class FlowHeader
        {
            public int RunDelay { get; set; }
            public Color LineColor { get; set; }
            public int LineThickness { get; set; }
            public Color BackColor { get; set; }
            public Color SelectionColor { get; set; }
        }

        private sealed class FlowSymbol
        {
            public string Id { get; set; }
            public string Opcode { get; set; }
            public string Text { get; set; }
            public int VerticalPosition { get; set; }
            public int HorizontalPosition { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public Color BackColor { get; set; }
            public Color TextColor { get; set; }
            public Color OutlineColor { get; set; }
            public string FontName { get; set; }
            public int FontSize { get; set; }
            public FontStyle FontStyle { get; set; }
            public Color TitleColor { get; set; }
        }

        private sealed class FlowConnection
        {
            public string Id { get; set; }
            public string StartSymbolId { get; set; }
            public string EndSymbolId { get; set; }
            public string IfCondition { get; set; }
        }

        private sealed class FlowSymbolControl : Control
        {
            private readonly string _opcode;
            private readonly string _text;
            private readonly Color _outlineColor;
            private readonly Color _titleColor;
            private readonly Color _selectionColor;
            private readonly Font _symbolFont;
            private bool _selected;

            public FlowSymbolControl(FlowSymbol symbol, Color selectionColor)
            {
                _opcode = symbol.Opcode;
                _text = symbol.Text;
                _outlineColor = symbol.OutlineColor;
                _titleColor = symbol.TitleColor;
                _selectionColor = selectionColor;
                BackColor = symbol.BackColor;
                ForeColor = symbol.TextColor;

                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);

                try
                {
                    _symbolFont = new Font(symbol.FontName, symbol.FontSize, symbol.FontStyle, GraphicsUnit.Point);
                }
                catch (ArgumentException)
                {
                    _symbolFont = new Font(SystemFonts.DefaultFont.FontFamily, symbol.FontSize, symbol.FontStyle, GraphicsUnit.Point);
                }

                Font = _symbolFont;
                Cursor = Cursors.Hand;
            }

            public void SetSelected(bool selected)
            {
                _selected = selected;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                Rectangle clientBounds = ClientRectangle;
                if (clientBounds.Width <= 1 || clientBounds.Height <= 1)
                    return;

                int padding = 3;
                int titleHeight = Math.Min(clientBounds.Height, Math.Max(Font.Height + 4, 18));
                Rectangle titleBounds = new Rectangle(
                    padding,
                    1,
                    Math.Max(0, clientBounds.Width - padding * 2),
                    Math.Max(0, titleHeight - 1));
                Rectangle textBounds = new Rectangle(
                    padding,
                    titleHeight,
                    Math.Max(0, clientBounds.Width - padding * 2),
                    Math.Max(0, clientBounds.Height - titleHeight - 2));

                TextRenderer.DrawText(
                    e.Graphics,
                    _opcode,
                    Font,
                    titleBounds,
                    _titleColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding);

                TextRenderer.DrawText(
                    e.Graphics,
                    _text,
                    Font,
                    textBounds,
                    ForeColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak |
                    TextFormatFlags.EndEllipsis);

                Color borderColor = _selected ? _selectionColor : _outlineColor;
                using (Pen borderPen = new Pen(borderColor, _selected ? 2f : 1f))
                {
                    e.Graphics.DrawRectangle(
                        borderPen,
                        0,
                        0,
                        clientBounds.Width - 1,
                        clientBounds.Height - 1);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _symbolFont != null)
                    _symbolFont.Dispose();

                base.Dispose(disposing);
            }
        }
    }
}
