﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Common.Core.Services;
using Microsoft.Markdown.Editor.Commands;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.VisualStudio.R.Package.Publishing.Definitions;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.R.Package.Publishing.Commands {
    internal sealed class PreviewPdfCommand : PreviewCommand {
        public PreviewPdfCommand(
            ITextView textView,
            IRInteractiveWorkflowVisualProvider workflowProvider,
            IServiceContainer services)
            : base(textView, (int)MdPackageCommandId.icmdPreviewPdf, workflowProvider, services) { }

        protected override string FileExtension => "pdf";
         protected override PublishFormat Format => PublishFormat.Pdf;

        protected override async Task<bool> CheckPrerequisitesAsync() {
            if (!await base.CheckPrerequisitesAsync()) {
                return false;
            }
            if (!await CheckExecutableExistsOnPathAsync("pdflatex")) {
                var session = _workflowProvider.GetOrCreate().RSession;
                var message = session.IsRemote ? Resources.Error_PdfLatexMissingRemote : Resources.Error_PdfLatexMissingLocal;
                await Services.ShowErrorMessageAsync(message);
                Process.Start("https://miktex.org/2.9/setup");
                return false;
            }
            return true;
        }
    }
}
