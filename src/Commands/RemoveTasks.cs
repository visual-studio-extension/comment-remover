﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace CommentRemover
{
    internal sealed class RemoveTasksCommand : BaseCommand<RemoveTasksCommand>
    {
        private static readonly string[] _tasks = { "todo", "hack", "undone", "unresolvedmergeconflict" };

        private RemoveTasksCommand(Package package)
        : base(package, PackageGuids.guidPackageCmdSet, PackageIds.RemoveTaskComments)
        { }

        public static void Initialize(Package package)
        {
            Instance = new RemoveTasksCommand(package);
        }

        protected override void Execute(OleMenuCommand button)
        {
            var view = ProjectHelpers.GetCurentTextView();
            var mappingSpans = GetClassificationSpans(view, "comment");

            try
            {
                VSPackage.DTE.UndoContext.Open(button.Text);

                RemoveCommmentsFromBuffer(view, mappingSpans);
                AddTelemetry("Tasks removed");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                VSPackage.DTE.UndoContext.Close();
            }
        }

        private void RemoveCommmentsFromBuffer(IWpfTextView view, IEnumerable<IMappingSpan> mappingSpans)
        {
            var affectedSpans = new List<Span>();
            var affectedLines = new List<int>();

            using (var edit = view.TextBuffer.CreateEdit())
            {
                foreach (var mappingSpan in mappingSpans)
                {
                    var start = mappingSpan.Start.GetPoint(view.TextBuffer, PositionAffinity.Predecessor).Value;
                    var end = mappingSpan.End.GetPoint(view.TextBuffer, PositionAffinity.Successor).Value;

                    var span = new Span(start, end - start);
                    var line = view.TextBuffer.CurrentSnapshot.Lines.First(l => l.Extent.IntersectsWith(span));

                    if (ContainsTaskComment(line))
                    {
                        edit.Delete(span);

                        if (!affectedLines.Contains(line.LineNumber))
                            affectedLines.Add(line.LineNumber);
                    }
                }

                edit.Apply();
            }

            using (var edit = view.TextBuffer.CreateEdit())
            {
                foreach (var lineNumber in affectedLines)
                {
                    var line = view.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber);

                    if (IsLineEmpty(line))
                    {
                        edit.Delete(line.Start, line.LengthIncludingLineBreak);
                    }
                }

                edit.Apply();
            }
        }

        public static bool ContainsTaskComment(ITextSnapshotLine line)
        {
            string text = line.GetText().ToLowerInvariant();

            foreach (var task in _tasks)
            {
                if (text.Contains(task + ":"))
                    return true;
            }

            return false;
        }
    }
}
