﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Languages.Editor.Controller;
using Microsoft.Markdown.Editor.ContentTypes;
using Microsoft.VisualStudio.R.Package.Commands.Markdown;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.R.Package.Commands
{
    [Export(typeof(ICommandFactory))]
    [ContentType(MdContentTypeDefinition.ContentType)]
    internal class VsMdCommandFactory : ICommandFactory
    {
        public IEnumerable<ICommand> GetCommands(ITextView textView, ITextBuffer textBuffer)
        {
            var commands = new List<ICommand>();

            commands.Add(new ShowContextMenuCommand(textView, MdGuidList.MdPackageGuid, MdGuidList.MdCmdSetGuid, (int)MdContextMenuId.MD));
            return commands;
        }
    }
}