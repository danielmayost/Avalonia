﻿using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

#nullable enable

namespace Avalonia.Automation.Peers
{
    public class TextBoxAutomationPeer : ControlAutomationPeer,
        ITextPeer,
        ITextProvider,
        IValueProvider
    {
        public TextBoxAutomationPeer(TextBox owner)
            : base(owner)
        {
            owner.GetObservable(TextBox.SelectionStartProperty).Subscribe(OnSelectionChanged);
            owner.GetObservable(TextBox.SelectionEndProperty).Subscribe(OnSelectionChanged);
        }

        public new TextBox Owner => (TextBox)base.Owner;
        public bool IsReadOnly => Owner.IsReadOnly;
        public string? Value => Owner.Text;
        public SupportedTextSelection SupportedTextSelection => SupportedTextSelection.Single;

        public ITextRangeProvider DocumentRange => new AutomationTextRange(this, 0, Owner.Text?.Length ?? 0);

        int ITextPeer.LineCount => Owner.Presenter?.FormattedText.GetLines().Count() ?? 1;
        string ITextPeer.Text => Owner.Text ?? string.Empty;
        
        public event EventHandler? SelectedRangesChanged;

        public IReadOnlyList<ITextRangeProvider> GetSelection()
        {
            return new[] { new AutomationTextRange(this, Owner.SelectionStart, Owner.SelectionEnd) };
        }

        public IReadOnlyList<ITextRangeProvider> GetVisibleRanges()
        {
            // Not sure this is necessary, QT just returns the document range too.
            return new[] { DocumentRange };
        }

        public ITextRangeProvider RangeFromChild(AutomationPeer childElement)
        {
            // We don't currently support embedding.
            throw new ArgumentException(nameof(childElement));
        }

        public ITextRangeProvider RangeFromPoint(Point p)
        {
            var i = 0;

            if (Owner.GetVisualRoot() is IVisual root &&
                root.TransformToVisual(Owner) is Matrix m)
            {
                i = Owner.Presenter.GetCaretIndex(p.Transform(m));
            }

            return new AutomationTextRange(this, i, i);
        }

        public void SetValue(string? value) => Owner.Text = value;

        IReadOnlyList<Rect> ITextPeer.GetBounds(int start, int end)
        {
            if (Owner.Presenter is TextPresenter presenter &&
                Owner.GetVisualRoot() is IVisual root &&
                presenter.TransformToVisual(root) is Matrix m)
            {
                var scroll = Owner.Scroll as Control;
                var clip = new Rect(scroll?.Bounds.Size ?? presenter.Bounds.Size);
                var source = presenter.FormattedText.HitTestTextRange(start, end - start);
                var result = new List<Rect>();

                foreach (var rect in source)
                {
                    var r = rect.Intersect(clip);
                    if (!r.IsEmpty)
                        result.Add(r.TransformToAABB(m));
                }

                return result;
            }

            return Array.Empty<Rect>();
        }

        int ITextPeer.LineFromChar(int charIndex)
        {
            if (Owner.Presenter is null)
                return 0;

            var l = 0;
            var c = 0;

            foreach (var line in Owner.Presenter.FormattedText.GetLines())
            {
                if ((c += line.Length) > charIndex)
                    return l;
                ++l;
            }

            return l;
        }

        int ITextPeer.LineIndex(int lineIndex)
        {
            var c = 0;
            var l = 0;
            var lines = Owner.Presenter.FormattedText.GetLines();            

            foreach (var line in lines)
            {
                if (l++ == lineIndex)
                    break;
                c+= line.Length;
            }

            return c;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Edit;
        }

        private void OnSelectionChanged(int obj) => SelectedRangesChanged?.Invoke(this, EventArgs.Empty);
    }
}
