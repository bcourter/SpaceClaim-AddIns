using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using SpaceClaim.AddInLibrary;
using SpaceClaim.Svg;
using Utilities.Properties;
using Color = System.Drawing.Color;

namespace SpaceClaim.AddIn.Utilities {

	/// <summary>
	/// Exports all visible components' bodies in individual step files at their orientation in the assembly.  
	/// </summary>
	class ExplodedSTEPSaveHandler : FileSaveHandler {
		Part mainPart;

		public ExplodedSTEPSaveHandler()
			: base("Exploded STEP Files", "stp") {
		}

		public override void SaveFile(string path) {
			mainPart = Window.ActiveWindow.Scene as Part;
			if (mainPart == null)
				return;

			path = mainPart.Document.Path;
			WriteBlock.ExecuteTask("Adjust visibility", new Task(delegate {
				mainPart.Export(PartExportFormat.Step, path, true, null);
			}));

			path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
			foreach (IComponent component in mainPart.Components) 
				RecurseComponents(path, component);
		}

		private void RecurseComponents(string namePath, IComponent component) {
			if (!Directory.Exists(namePath))
				Directory.CreateDirectory(namePath);

			string newNamePath = Path.Combine(namePath, component.Master.Template.Name);
			string fileName = newNamePath + ".stp";
			if (File.Exists(fileName))
				return;

			WriteBlock.ExecuteTask("Adjust visibility", new Task(delegate {
				foreach (IDesignBody body in mainPart.GetDescendants<IDesignBody>())
					body.SetVisibility(null, false);

				foreach (IDesignBody body in component.Content.GetDescendants<IDesignBody>())
					body.SetVisibility(null, null);

				mainPart.Export(PartExportFormat.Step, fileName, true, null);

				foreach (IDesignBody body in mainPart.GetDescendants<IDesignBody>())
					body.SetVisibility(null, null);
			}));

			foreach (IComponent c in component.Content.Components) 
				RecurseComponents(newNamePath + "-", c);
		}

	}
}
