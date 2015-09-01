﻿using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.Languages.Editor.Test.Mocks
{
    [ExcludeFromCodeCoverage]
    public sealed class TextViewModelMock : ITextViewModel
    {
        public TextViewModelMock(ITextBuffer textBuffer)
        {
            DataBuffer = textBuffer;
            EditBuffer = textBuffer;
            VisualBuffer = textBuffer;
            DataModel = new TextDataModelMock(textBuffer);
        }

        public ITextBuffer DataBuffer { get; private set; }

        public ITextDataModel DataModel { get; private set; }

        public ITextBuffer EditBuffer { get; private set; }

        public PropertyCollection Properties { get; private set; } = new PropertyCollection();

        public ITextBuffer VisualBuffer { get; private set; }

        public void Dispose()
        {
        }

        public SnapshotPoint GetNearestPointInVisualBuffer(SnapshotPoint editBufferPoint)
        {
            throw new NotImplementedException();
        }

        public SnapshotPoint GetNearestPointInVisualSnapshot(SnapshotPoint editBufferPoint, ITextSnapshot targetVisualSnapshot, PointTrackingMode trackingMode)
        {
            throw new NotImplementedException();
        }

        public bool IsPointInVisualBuffer(SnapshotPoint editBufferPoint, PositionAffinity affinity)
        {
            return true;
        }
    }
}
