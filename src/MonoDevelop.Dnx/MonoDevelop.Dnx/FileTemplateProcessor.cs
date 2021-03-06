﻿//
// FileTemplateProcessor.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc. (http://xamarin.com)
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
//

using System;
using System.IO;
using System.Linq;
using System.Xml;
using MonoDevelop.Core;
using MonoDevelop.Ide.Templates;
using MonoDevelop.Projects;

namespace MonoDevelop.Dnx
{
	public static class FileTemplateProcessor
	{
		static readonly FilePath templateSourceRootDirectory;

		static FileTemplateProcessor ()
		{
			templateSourceRootDirectory = GetTemplateSourceRootDirectory ();
		}

		public static void CreateFilesFromTemplate (DnxProject project, string projectTemplateName, params string[] files)
		{
			CreateFileFromTemplate (project.Project, projectTemplateName, "project.json");

			foreach (string templateFileName in files) {
				CreateFileFromTemplate (project.Project, projectTemplateName, templateFileName);
			}
		}

		public static void CreateFileFromCommonTemplate (Solution solution, string fileTemplateName)
		{
			FilePath templateSourceDirectory = templateSourceRootDirectory.Combine ("Common");
			CreateFileFromTemplate (solution, templateSourceDirectory, "global.json");
		}

		public static void CreateFileFromTemplate (Project project, string projectTemplateName, string fileTemplateName)
		{
			FilePath templateSourceDirectory = templateSourceRootDirectory;

			if (!String.IsNullOrEmpty (projectTemplateName)) {
				fileTemplateName = projectTemplateName + "." + fileTemplateName;
				templateSourceDirectory = templateSourceRootDirectory.Combine (projectTemplateName.Replace ("Project", ""));
			}

			CreateFileFromTemplate (project, templateSourceDirectory, fileTemplateName);
		}

		public static void CreateFileFromTemplate (Project project, FilePath templateSourceDirectory, string fileTemplateName)
		{
			CreateFileFromTemplate (project, project.ParentSolution.RootFolder, templateSourceDirectory, fileTemplateName);
		}

		static void CreateFileFromTemplate (Project project, SolutionFolderItem policyItem, FilePath templateSourceDirectory, string fileTemplateName)
		{
			string templateFileName = templateSourceDirectory.Combine (fileTemplateName + ".xft.xml");
			using (Stream stream = File.OpenRead (templateFileName)) {
				var document = new XmlDocument ();
				document.Load (stream);

				foreach (XmlElement templateElement in document.DocumentElement["TemplateFiles"].ChildNodes.OfType<XmlElement> ()) {
					var template = FileDescriptionTemplate.CreateTemplate (templateElement, templateSourceDirectory);
					template.AddToProject (policyItem, project, "C#", project.BaseDirectory, null);
				}
			}
		}

		public static void CreateFileFromTemplate (Solution solution, FilePath templateSourceDirectory, string templateName)
		{
			var project = new XProject ();
			project.BaseDirectory = solution.BaseDirectory;
			CreateFileFromTemplate (project, solution.RootFolder, templateSourceDirectory, templateName);
		}

		public static FilePath GetTemplateSourceRootDirectory ()
		{
			var assemblyPath = new FilePath (typeof(FileTemplateProcessor).Assembly.Location);
			return assemblyPath.ParentDirectory.Combine ("Templates", "Projects");
		}
	}
}

