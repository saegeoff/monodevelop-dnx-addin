﻿//
// DotNetCoreOutputOperationConsole.cs
//
// Author:
//       Matt Ward <ward.matt@gmail.com>
//
// Copyright (c) 2016 Matthew Ward
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

using System.IO;
using Microsoft.Framework.Logging;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Ide.Gui.Components;

namespace MonoDevelop.Dnx
{
	public class DotNetCoreOutputOperationConsole : OperationConsole
	{
		LogView logView;
		LogTextWriter logger = new LogTextWriter ();
		LogTextWriter errorLogger = new LogTextWriter ();
		LogTextWriter outLogger = new LogTextWriter ();
		StringReader reader = new StringReader ("");
		bool disposed;

		public DotNetCoreOutputOperationConsole (LogLevel logLevel = LogLevel.Verbose)
			: this (logLevel, DnxOutputPad.LogView)
		{
		}

		public DotNetCoreOutputOperationConsole (LogLevel logLevel, LogView logView)
		{
			LogLevel = logLevel;
			this.logView = logView;

			logger.TextWritten += WriteConsoleLogText;
			errorLogger.TextWritten += WriteError;
			outLogger.TextWritten += WriteText;
		}

		public LogLevel LogLevel { get; set; }

		public override TextReader In {
			get { return reader; }
		}

		public override TextWriter Out {
			get { return outLogger; }
		}

		public override TextWriter Error {
			get { return errorLogger; }
		}
		public override TextWriter Log {
			get { return logger; }
		}

		public override void Debug (int level, string category, string message)
		{
			if (DnxLoggerService.IsEnabled (LogLevel.Debug)) {
				logView.WriteDebug (level, category, message);
			}
		}

		public override void Dispose ()
		{
			if (!disposed) {
				disposed = true;

				logger.TextWritten -= WriteConsoleLogText;
				errorLogger.TextWritten -= WriteError;
				outLogger.TextWritten -= WriteText;

				logger.Dispose ();
				outLogger.Dispose ();
				errorLogger.Dispose ();
			}

			base.Dispose ();
		}

		void WriteText (string text)
		{
			if (DnxLoggerService.IsEnabled (LogLevel)) {
				logView.WriteText (text);
			}
		}

		void WriteError (string text)
		{
			logView.WriteError (text);
		}

		void WriteConsoleLogText (string text)
		{
			if (DnxLoggerService.IsEnabled (LogLevel)) {
				logView.WriteConsoleLogText (text);
			}
		}
	}
}

