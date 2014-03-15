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
using Utilities.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Utilities {
	public class AddIn : SpaceClaim.Api.V10.Extensibility.AddIn, IExtensibility, IRibbonExtensibility, ICommandExtensibility {
		#region IExtensibility Members
		List<RibbonLabelCapsule> labels = new List<RibbonLabelCapsule>();

		public bool Connect() {
			return true;
		}

		public void Disconnect() {
		}

		#endregion

		#region IRibbonExtensibility Members

		public string GetCustomUI() {
			string test = ribbonRoot.GetUI();
			return test;
		}

		#endregion

		#region ICommandExtensibility Members

		RibbonRoot ribbonRoot = new RibbonRoot();
		public void Initialize() {
            var tab = new RibbonTabCapsule("Utilities", Resources.UtilitiesText, ribbonRoot);
			RibbonButtonCapsule button;
            RibbonGroupCapsule group;

            group = new RibbonGroupCapsule("Thread", Resources.ThreadsGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
            button = new ThreadToolCapsule("Create", group, RibbonButtonCapsule.ButtonSize.large);

            group = new RibbonGroupCapsule("Shapes", Resources.ApiGrooveGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
            button = new ApiGrooveToolCapsule("ApiGroove", group, RibbonButtonCapsule.ButtonSize.large);

            group = new RibbonGroupCapsule("Spheres", Resources.DistributeGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
            button = new DistributeSpheresToolCapsule("Tool", group, RibbonButtonCapsule.ButtonSize.large);

			SpaceClaim.Api.V10.Application.AddFileHandler(new ExplodedSTEPSaveHandler());
		}

		#endregion
	}
}
