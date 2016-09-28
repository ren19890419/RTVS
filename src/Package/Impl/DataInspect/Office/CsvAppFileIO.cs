﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Common.Core;
using Microsoft.Common.Core.IO;
using Microsoft.Common.Core.OS;
using Microsoft.R.Components.ContentTypes;
using Microsoft.R.Components.Extensions;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.R.DataInspection;
using Microsoft.R.Host.Client;
using Microsoft.R.Host.Client.Extensions;
using Microsoft.VisualStudio.R.Package.ProjectSystem;
using Microsoft.VisualStudio.R.Package.Shell;
using Microsoft.VisualStudio.R.Package.Utilities;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.R.Package.DataInspect.Office {
    internal static class CsvAppFileIO {
        private const string _variableNameReplacement = "variable";
        private static int _busy;

        public static async Task OpenDataCsvApp(IREvaluationResultInfo result, IApplicationShell appShell, IFileSystem fileSystem, IProcessServices processServices) {
            await appShell.SwitchToMainThreadAsync();

            if (Interlocked.Exchange(ref _busy, 1) > 0) {
                return;
            }

            var workflow = appShell.ExportProvider.GetExportedValue<IRInteractiveWorkflowProvider>().GetOrCreate();
            var session = workflow.RSession;

            var folder = GetTempCsvFilesFolder();
            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }

            var pss = appShell.ExportProvider.GetExportedValue<IProjectSystemServices>();
            var variableName = result.Name ?? _variableNameReplacement;
            var csvFileName = MakeCsvFileName(appShell, pss, variableName);

            var file = pss.GetUniqueFileName(folder, csvFileName, "csv", appendUnderscore: true);

            string currentStatusText;
            var statusBar = appShell.GetGlobalService<IVsStatusbar>(typeof(SVsStatusbar));
            statusBar.GetText(out currentStatusText);

            try {
                statusBar.SetText(Resources.Status_WritingCSV);
                await CreateCsvAndStartProcess(result, session, file, fileSystem, processServices, statusBar);
            } catch (Win32Exception ex) {
                appShell.ShowErrorMessage(string.Format(CultureInfo.InvariantCulture, Resources.Error_CannotOpenCsv, ex.Message));
            } catch (IOException ex) {
                appShell.ShowErrorMessage(string.Format(CultureInfo.InvariantCulture, Resources.Error_CannotOpenCsv, ex.Message));
            } finally {
                statusBar.SetText(currentStatusText);
            }

            Interlocked.Exchange(ref _busy, 0);
        }

        private static async Task CreateCsvAndStartProcess(IREvaluationResultInfo result, IRSession session, string fileName, IFileSystem fileSystem, IProcessServices processServices, IVsStatusbar statusBar) {
            await TaskUtilities.SwitchToBackgroundThread();

            var sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            var dec = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            using (var e = await session.BeginEvaluationAsync()) {
                var csvDataBlobId = await e.EvaluateAsync<ulong>($"rtvs:::export_to_csv({result.Expression}, sep={sep.ToRStringLiteral()}, dec={dec.ToRStringLiteral()})", REvaluationKind.Normal);
                using (DataTransferSession dts = new DataTransferSession(session, fileSystem)) {
                    uint cookie = 0;
                    statusBar.Progress(ref cookie, 1, Resources.Status_WritingCSV, 0, 0);
                    uint total = (uint)await session.GetBlobSizeAsync(csvDataBlobId, CancellationToken.None);
                    await dts.FetchFileAsync(new RBlobInfo(csvDataBlobId), fileName, true, new Progress<long>((b) => {
                        statusBar.Progress(ref cookie, 1, Resources.Status_WritingCSV, (uint)b, total);
                    }));
                    statusBar.Progress(ref cookie, 0, Resources.Status_WritingCSV, 0, 0);
                }
            }

            if (fileSystem.FileExists(fileName)) {
                processServices.Start(fileName);
            }
        }

        public static void Close(IFileSystem fileSystem) {
            var folder = GetTempCsvFilesFolder();
            if (fileSystem.DirectoryExists(folder)) {
                // Note: some files may still be locked if they are opened in Excel
                try {
                    fileSystem.DeleteDirectory(folder, recursive: true);
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        private static string GetTempCsvFilesFolder() {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(folder, @"RTVS_CSV_Exports\");
        }

        private static string MakeCsvFileName(IApplicationShell appShell, IProjectSystemServices pss, string variableName) {
            var project = pss.GetActiveProject();
            var projectName = project?.FileName;

            var contentTypeService = appShell.ExportProvider.GetExportedValue<IContentTypeRegistryService>();
            var viewTracker = appShell.ExportProvider.GetExportedValue<IActiveWpfTextViewTracker>();

            var activeView = viewTracker.GetLastActiveTextView(contentTypeService.GetContentType(RContentTypeDefinition.ContentType));
            var filePath = activeView.GetFilePath();

            var csvFileName = string.Empty;

            if (!string.IsNullOrEmpty(projectName)) {
                csvFileName += Path.GetFileNameWithoutExtension(projectName);
                csvFileName += "_";
            }

            if (!string.IsNullOrEmpty(filePath)) {
                csvFileName += Path.GetFileNameWithoutExtension(filePath);
                csvFileName += "_";
            }

            if (variableName.StartsWith("$", StringComparison.Ordinal)) {
                variableName = variableName.Substring(1);
            }

            int invalidCharIndex = variableName.IndexOfAny(Path.GetInvalidFileNameChars());

            variableName = MakeFileSystemCompatible(variableName);
            if (variableName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                variableName = _variableNameReplacement;
            }
            csvFileName += variableName;

            return csvFileName;
        }

        private static string MakeFileSystemCompatible(string s) {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char ch in s) {
                if (invalidChars.Contains(ch)) {
                    sb.Append('_');
                }
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
