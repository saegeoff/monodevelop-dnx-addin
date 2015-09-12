// 
// SolutionDescriptor.cs
//  
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//   Viktoria Dudka  <viktoriad@remobjects.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
// Copyright (c) 2009 RemObjects Software
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.


using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide.ProgressMonitoring;
using MonoDevelop.Projects;
using MonoDevelop.Components;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components.Commands;
using System.Collections.Generic;
using System.Xml;
using Mono.Addins;

namespace MonoDevelop.Ide.Templates
{
    internal class SolutionDescriptor
	{
        string startupProject;
        string directory;
        string name;
        string type;
		RuntimeAddin addin;
		List<CustomFileDescriptionTemplate> files = new List<CustomFileDescriptionTemplate> ();
		List<SolutionFolderDescriptor> solutionFolders = new List<SolutionFolderDescriptor> ();

        private List<ISolutionItemDescriptor> entryDescriptors = new List<ISolutionItemDescriptor> ();
        public ISolutionItemDescriptor[] EntryDescriptors
        {
            get { return entryDescriptors.ToArray(); }
        }

        public static SolutionDescriptor CreateSolutionDescriptor (RuntimeAddin addin, XmlElement xmlElement,
			FilePath baseDirectory)
        {
            SolutionDescriptor solutionDescriptor = new SolutionDescriptor ();
			solutionDescriptor.addin = addin;

            if (xmlElement.Attributes["name"] != null)
                solutionDescriptor.name = xmlElement.Attributes["name"].Value;
            else
                throw new InvalidOperationException ("Attribute 'name' not found");

            if (xmlElement.Attributes["type"] != null)
                solutionDescriptor.type = xmlElement.Attributes["type"].Value;

            if (xmlElement.Attributes["directory"] != null)
                solutionDescriptor.directory = xmlElement.Attributes["directory"].Value;

            if (xmlElement["Options"] != null && xmlElement["Options"]["StartupProject"] != null)
                solutionDescriptor.startupProject = xmlElement["Options"]["StartupProject"].InnerText;

			AddFiles (xmlElement["Files"], baseDirectory, solutionDescriptor);

            foreach (XmlNode xmlNode in xmlElement.ChildNodes) {
                if (xmlNode is XmlElement) {
                    XmlElement xmlNodeElement = (XmlElement)xmlNode;
                    switch (xmlNodeElement.Name) {
                    case "Project":
                        solutionDescriptor.entryDescriptors.Add (
							ProjectDescriptor.CreateProjectDescriptor (xmlNodeElement, baseDirectory));
                        break;
                    case "CombineEntry":
                    case "SolutionItem":
                        solutionDescriptor.entryDescriptors.Add (
							SolutionItemDescriptor.CreateDescriptor (addin, xmlNodeElement));
                        break;
					case "SolutionFolder":
						solutionDescriptor.solutionFolders.Add (
								SolutionFolderDescriptor.CreateDescriptor (xmlNodeElement, baseDirectory));
						break;
                    }
                }
            }

            return solutionDescriptor;
        }

		public WorkspaceItemCreatedInformation CreateEntry (ProjectCreateInformation projectCreateInformation, string defaultLanguage)
        {
            WorkspaceItem workspaceItem = null;

            if (string.IsNullOrEmpty (type))
                workspaceItem = new Solution ();
            else {
                Type workspaceItemType = addin.GetType (type, false);
                if (workspaceItemType != null)
                    workspaceItem = Activator.CreateInstance (workspaceItemType) as WorkspaceItem;

                if (workspaceItem == null) {
                    MessageService.ShowError (GettextCatalog.GetString ("Can't create solution with type: {0}", type));
					return null;
				}
            }

			var substitution = new string[,] { { "ProjectName", projectCreateInformation.SolutionName } };
            workspaceItem.Name = StringParserService.Parse (name, substitution);
			string newStartupProjectName = startupProject;
			if (newStartupProjectName != null) {
				newStartupProjectName = StringParserService.Parse (startupProject, substitution);
			}

            workspaceItem.SetLocation (projectCreateInformation.SolutionPath, workspaceItem.Name);

            ProjectCreateInformation localProjectCI;
            if (!string.IsNullOrEmpty (directory) && directory != ".") {
                localProjectCI = new ProjectCreateInformation (projectCreateInformation);

                localProjectCI.SolutionPath = Path.Combine (localProjectCI.SolutionPath, directory);
                localProjectCI.ProjectBasePath = Path.Combine (localProjectCI.ProjectBasePath, directory);

                if (!Directory.Exists (localProjectCI.SolutionPath))
                    Directory.CreateDirectory (localProjectCI.SolutionPath);

                if (!Directory.Exists (localProjectCI.ProjectBasePath))
                    Directory.CreateDirectory (localProjectCI.ProjectBasePath);
            }
            else
                localProjectCI = projectCreateInformation;

			var workspaceItemCreatedInfo = new WorkspaceItemCreatedInformation (workspaceItem);

            Solution solution = workspaceItem as Solution;
            if (solution != null) {

				CreateSolutionFolders (solution, projectCreateInformation, defaultLanguage);

                for ( int i = 0; i < entryDescriptors.Count; i++ ) {
                    ProjectCreateInformation entryProjectCI;
                    var entry = entryDescriptors[i] as ICustomProjectCIEntry;
					if (entry != null) {
						entryProjectCI = entry.CreateProjectCI (localProjectCI);
						entryProjectCI = new ProjectTemplateCreateInformation (entryProjectCI, localProjectCI.ProjectName);
					} else
	                    entryProjectCI = localProjectCI;

					var solutionItemDesc = entryDescriptors[i];

					SolutionEntityItem info = solutionItemDesc.CreateItem (entryProjectCI, defaultLanguage);
					if (info == null)
						continue;

					solutionItemDesc.InitializeItem (solution.RootFolder, entryProjectCI, defaultLanguage, info);

                    IConfigurationTarget configurationTarget = info as IConfigurationTarget;
                    if (configurationTarget != null) {
                        foreach (ItemConfiguration configuration in configurationTarget.Configurations) {
                            bool flag = false;
                            foreach (SolutionConfiguration solutionCollection in solution.Configurations) {
                                if (solutionCollection.Id == configuration.Id)
                                    flag = true;
                            }
                            if (!flag)
                                solution.AddConfiguration (configuration.Id, true);
                        }
                    }

					if ((info is Project) && (solutionItemDesc is ProjectDescriptor)) {
						workspaceItemCreatedInfo.AddPackageReferenceForCreatedProject ((Project)info, (ProjectDescriptor)solutionItemDesc, projectCreateInformation);
					}
                    solution.RootFolder.Items.Add (info);
					if (newStartupProjectName == info.Name)
						solution.StartupItem = info;
                }
            }

			CreateFiles (workspaceItem, projectCreateInformation, defaultLanguage);

			if (!workspaceItem.FileFormat.CanWrite (workspaceItem)) {
				// The default format can't write solutions of this type. Find a compatible format.
				FileFormat f = IdeApp.Services.ProjectService.FileFormats.GetFileFormatsForObject (workspaceItem).First ();
				workspaceItem.ConvertToFormat (f, true);
			}
			
			return workspaceItemCreatedInfo;
        }

		public bool HasPackages ()
		{
			return entryDescriptors.OfType<ProjectDescriptor> ().Any (descriptor => descriptor.HasPackages ());
		}

		static void AddFiles (XmlElement files, FilePath baseDirectory, SolutionDescriptor solutionDescriptor)
		{
			if (files == null)
				return;

			foreach (XmlElement file in files.ChildNodes.OfType<XmlElement> ()) {
				solutionDescriptor.files.Add (CustomFileDescriptionTemplate.CreateTemplate (file, baseDirectory));
			}
		}

		void CreateFiles (WorkspaceItem workspaceItem, ProjectCreateInformation projectCreateInformation, string defaultLanguage)
		{
			SolutionItem policyParent = null;
			var solution = workspaceItem as Solution;
			if (solution != null)
				policyParent = solution.RootFolder;

			var project = new GenericProject ();
			project.BaseDirectory = solution.BaseDirectory;

			foreach (CustomFileDescriptionTemplate fileTemplate in files) {
				try {
					if (!projectCreateInformation.ShouldCreate (fileTemplate.CreateCondition))
						continue;
					fileTemplate.SetProjectTagModel (projectCreateInformation.Parameters);
					fileTemplate.AddToProject (policyParent, project, defaultLanguage, project.BaseDirectory, null);
				} catch (Exception ex) {
					if (!IdeApp.IsInitialized)
						throw;
					MessageService.ShowError (GettextCatalog.GetString ("File {0} could not be written.", fileTemplate.Name), ex);
				} finally {
					fileTemplate.SetProjectTagModel (null);
				}
			}
		}

		void CreateSolutionFolders (
			Solution solution,
			ProjectCreateInformation projectCreateInformation,
			string defaultLanguage)
		{
			foreach (SolutionFolderDescriptor descriptor in solutionFolders) {
				SolutionFolder folder = descriptor.CreateFolder (projectCreateInformation);
				solution.RootFolder.AddItem (folder);
				descriptor.Initialize (folder, projectCreateInformation, defaultLanguage);
			}
		}
	}
}
