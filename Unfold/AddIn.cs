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
using Unfold.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Unfold {
	public class AddIn : SpaceClaim.Api.V10.Extensibility.AddIn, IExtensibility, IRibbonExtensibility, ICommandExtensibility {

		#region IExtensibility Members

		public bool Connect() {
			return true;
		}

		public void Disconnect() {
		}

		#endregion

		#region IRibbonExtensibility Members

		public string GetCustomUI() {
			return ribbonRoot.GetUI();
		}

		#endregion

		#region ICommandExtensibility Members

		RibbonRoot ribbonRoot = new RibbonRoot();
		public void Initialize() {
			var tab = new RibbonTabCapsule("Unfold", Resources.TabText, ribbonRoot);
			RibbonGroupCapsule group;
			RibbonButtonCapsule button;

			group = new RibbonGroupCapsule("Unfold", Resources.UnfoldGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			new UnfoldButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);

			var isBreakCapsule = new UnfoldBreakOptionsCapsule("IsBreak", tab);
			isBreakCapsule.CreateOptionsUI();

			group = new RibbonGroupCapsule("Tessellate", Resources.TessellateGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			button = new TessellateButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);
			group.CreateOptionsUI();


	//		container = new RibbonContainerCapsule("Settings", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
	//		new UnfoldWithCurvesButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
	//		new UnfoldVerifyPlanarButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
	//		container = new RibbonContainerCapsule("More", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
	//		new MergeComponentBodiesButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
	//		new ConvexHullButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
	//		group.CreateOptionsUI();


			group = new RibbonGroupCapsule("Create", Resources.CreateGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			new TessellateLoftButtonCapsule(group, RibbonButtonCapsule.ButtonSize.large);
			group.CreateOptionsUI();

			//	new TessellateFoldCornerButtonCapsule(group, RibbonButtonCapsule.ButtonSize.small);

			//group = new RibbonGroupCapsule("Dashes", Resources.DashesGroupText, tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
			//container = new RibbonContainerCapsule("Buttons", group, RibbonCollectionCapsule.LayoutOrientation.vertical, false);
			//button = new DashesButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			//new DashChainButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			//new CopyCurvesButtonCapsule(container, RibbonButtonCapsule.ButtonSize.small);
			//group.CreateOptionsUI();
		}

		#endregion
	}
}
