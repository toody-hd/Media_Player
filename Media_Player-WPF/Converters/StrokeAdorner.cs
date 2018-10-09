using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Media_Player_WPF.Converters
{
    public class StrokeAdorner : Adorner
    {
        private TextBlock _textBlock;

        private Brush _stroke;
        private double _strokeThickness;

        public Brush Stroke
        {
            get
            {
                return _stroke;
            }

            set
            {
                _stroke = value;
                _textBlock.InvalidateVisual();
                InvalidateVisual();
            }
        }

        public double StrokeThickness
        {
            get
            {
                return _strokeThickness;
            }

            set
            {
                _strokeThickness = value;
                _textBlock.InvalidateVisual();
                InvalidateVisual();
            }
        }

        public StrokeAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _textBlock = adornedElement as TextBlock;
            ensureTextBlock();
            foreach (var property in TypeDescriptor.GetProperties(_textBlock).OfType<PropertyDescriptor>())
            {
                var dp = DependencyPropertyDescriptor.FromProperty(property);
                if (dp == null) continue;
                var metadata = dp.Metadata as FrameworkPropertyMetadata;
                if (metadata == null) continue;
                if (!metadata.AffectsRender) continue;
                dp.AddValueChanged(_textBlock, (s, e) => this.InvalidateVisual());
            }
        }

        private void ensureTextBlock()
        {
            if (_textBlock == null) throw new Exception("This adorner works on TextBlocks only");
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            ensureTextBlock();
            base.OnRender(drawingContext);
#pragma warning disable CS0618 // Type or member is obsolete
            var formattedText = new FormattedText(
                _textBlock.Text,
                CultureInfo.CurrentUICulture,
                _textBlock.FlowDirection,
                new Typeface(_textBlock.FontFamily, _textBlock.FontStyle, _textBlock.FontWeight, _textBlock.FontStretch),
                _textBlock.FontSize,
                 Brushes.Black // This brush does not matter since we use the geometry of the text. 
            )
            {
                TextAlignment = _textBlock.TextAlignment,
                Trimming = _textBlock.TextTrimming,
                LineHeight = _textBlock.LineHeight,
                MaxTextWidth = _textBlock.ActualWidth - _textBlock.Padding.Left - _textBlock.Padding.Right,
                MaxTextHeight = _textBlock.ActualHeight - _textBlock.Padding.Top// - _textBlock.Padding.Bottom;
            };
#pragma warning restore CS0618 // Type or member is obsolete
            while (formattedText.Extent == double.NegativeInfinity)
            {
                formattedText.MaxTextHeight++;
            }

            // Build the geometry object that represents the text.
            var _textGeometry = formattedText.BuildGeometry(new Point(_textBlock.Padding.Left, _textBlock.Padding.Top));
            var textPen = new Pen(Stroke, StrokeThickness);
            drawingContext.DrawGeometry(Brushes.Transparent, textPen, _textGeometry);
        }

    }
}
