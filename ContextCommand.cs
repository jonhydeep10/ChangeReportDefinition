using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ChangeReportDefinition
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class ContextCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("cbd7200f-5f27-4fc2-b360-14a134827098");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private ContextCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static ContextCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in ContextCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ContextCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string message;
            string title = "Change Report Definition";

            EnvDTE.DTE dte = (EnvDTE.DTE)ServiceProvider.GetService(typeof(EnvDTE.DTE));
            EnvDTE.SelectedItems selectedItems = dte.SelectedItems;

            if (selectedItems != null)
            {
                foreach (EnvDTE.SelectedItem selectedItem in selectedItems)
                {
                    EnvDTE.ProjectItem projectItem = selectedItem.ProjectItem as EnvDTE.ProjectItem;

                    if (projectItem != null)
                    {
                        string path = projectItem.Properties.Item("FullPath").Value.ToString();

                        message = $"Executed on {projectItem.Name}";

                        ChangeDefinition(path);

                        // Show a message box to prove we were here
                        VsShellUtilities.ShowMessageBox(
                            this.package,
                            message,
                            title,
                            OLEMSGICON.OLEMSGICON_INFO,
                            OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    }
                }
            }
        }

        private void ChangeDefinition(string path)
        {
            if (path.ToLower().Contains(".rdlc"))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);

                XmlNodeList nodes = doc.GetElementsByTagName("Report");

                XmlNode reportNode = nodes[0];

                if (reportNode != null)
                {
                    var definition = reportNode.Attributes.GetNamedItem("xmlns");
                    if (definition != null && definition.Value.Contains("2016"))
                    {
                        definition.Value = definition.Value.Replace("2016", "2008");

                        var reportParameterLayoutNode = doc.GetElementsByTagName("ReportParametersLayout")[0];
                        if (reportParameterLayoutNode != null)
                        {
                            reportNode.RemoveChild(reportParameterLayoutNode);
                        }

                        var autoNode = doc.GetElementsByTagName("AutoRefresh")[0];

                        var bodyNode = doc.GetElementsByTagName("Body")[0];
                        var widthNode = doc.GetElementsByTagName("Width")[0];
                        var pageNode = doc.GetElementsByTagName("Page")[0];

                        reportNode.InsertAfter(pageNode, autoNode);
                        reportNode.InsertAfter(widthNode, autoNode);
                        reportNode.InsertAfter(bodyNode, autoNode);

                        var reportSectionsNode = doc.GetElementsByTagName("ReportSections")[0];
                        reportNode.RemoveChild(reportSectionsNode);

                        doc.Save(path);
                    }
                }
            }
        }
    }
}
