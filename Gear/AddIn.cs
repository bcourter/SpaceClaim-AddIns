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
using Gear.Properties;
using Color = System.Drawing.Color;
using Application = SpaceClaim.Api.V10.Application;

namespace SpaceClaim.AddIn.Gear {
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
			var tab = new RibbonTabCapsule("Gear", Resources.TabText, ribbonRoot);
			RibbonButtonCapsule button;

			RibbonGroupCapsule basicGroup = new GearGroupCapsule("Create", tab);
			button = new GearButtonCapsule("Spur", basicGroup, RibbonButtonCapsule.ButtonSize.large);
			basicGroup.CreateOptionsUI();

			var isHelicalCapsule = new GearIsHelicalCapsule("IsHelical", tab);
			isHelicalCapsule.CreateOptionsUI();

			var isBevelCapsule = new GearIsBevelCapsule("IsBevel", tab);
			isBevelCapsule.CreateOptionsUI();

			var isScrewCapsule = new GearIsScrewCapsule("IsScrew", tab);
			isScrewCapsule.CreateOptionsUI();

			var isHypoidCapsule = new GearIsHypoidCapsule("IsHypoid", tab);
			isHypoidCapsule.CreateOptionsUI();

			var calculationsContainerCapsule = new GearCalculationsContainerCapsule("CalculationsContainer", tab);
			labels.Add(new GearCalculationsLine1Capsule("Line1", basicGroup, calculationsContainerCapsule));
			labels.Add(new GearCalculationsLine2Capsule("Line2", basicGroup, calculationsContainerCapsule));
			labels.Add(new GearCalculationsLine3Capsule("Line3", basicGroup, calculationsContainerCapsule));

	//		RibbonGroupCapsule extraGroup = new RibbonGroupCapsule("Extras", "Extras", tab, RibbonCollectionCapsule.LayoutOrientation.horizontal);
	//		new MobiusButtonCapsule("mobiusbutton", extraGroup, RibbonButtonCapsule.ButtonSize.large);
		}

		#endregion
	}
}
