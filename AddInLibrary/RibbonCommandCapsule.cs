using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Data;
using System.Xml;
using System.Drawing;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;

/// <summary>
/// Automatically generate Ribbon buttons for add-ins, copied from AECollection AddinLibrary
/// </summary>

namespace SpaceClaim.AddInLibrary {
    public interface IRibbonObject {
        IRibbonObject Parent { get; }
        IList<IRibbonObject> Children { get; }
        string Id { get; }

        void WriteStartElement(XmlWriter xmlWriter);
        void WriteEndElement(XmlWriter xmlWriter);
    }

    public class RibbonRoot : IRibbonObject {
        List<IRibbonObject> children = new List<IRibbonObject>();
        public RibbonRoot() { }

        public string GetUI() {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            StringBuilder stringBuilder = new StringBuilder();
            XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, settings);

            RecurseGetXml(this, xmlWriter);
            return stringBuilder.ToString();
        }

        public void RecurseGetXml(IRibbonObject iRibbonObject, XmlWriter xmlWriter) {
            iRibbonObject.WriteStartElement(xmlWriter);

            foreach (IRibbonObject child in iRibbonObject.Children)
                RecurseGetXml(child, xmlWriter);

            iRibbonObject.WriteEndElement(xmlWriter);
        }

        public IEnumerable<RibbonCommandCapsule> GetCapsules() {
            return RecurseGetCapsules(this);
        }

        public IEnumerable<RibbonCommandCapsule> RecurseGetCapsules(IRibbonObject iRibbonObject) {
            foreach (RibbonCommandCapsule child in iRibbonObject.Children) {
                yield return child;

                foreach (RibbonCommandCapsule capsule in RecurseGetCapsules(child))
                    yield return capsule;
            }
        }

        #region IRibbonObject Members

        public IRibbonObject Parent {
            get { return null; }
        }

        public IList<IRibbonObject> Children {
            get { return children; }
        }

        public string Id {
            get { return string.Empty; }
        }

        public void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("customUI");
            xmlWriter.WriteStartElement("ribbon");
            xmlWriter.WriteStartElement("tabs");
        }

        public void WriteEndElement(XmlWriter xmlWriter) {
            xmlWriter.WriteEndElement(); // <tabs>
            xmlWriter.WriteEndElement(); // <ribbon>
            xmlWriter.WriteEndElement(); // <customUI>
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
        }

        #endregion
    }

    public abstract class RibbonCommandCapsule : CommandCapsule, IRibbonObject {
        IRibbonObject parent;
        List<IRibbonObject> children = new List<IRibbonObject>();

        public RibbonCommandCapsule(string name, string text, System.Drawing.Image image, string hint, IRibbonObject parent)
            : base(parent as RibbonCommandCapsule != null ? String.Format("{0}.{1}", (parent as RibbonCommandCapsule).Command.Name, name) : name, text, image, hint) {  //TBD figure out how to interrupt this

            this.parent = parent;
            parent.Children.Add(this);
            this.Initialize();
        }

        protected override void OnUpdate(Command command) {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
        }

        public void Update() {
            this.OnUpdate(this.Command);
        }

        public virtual Dictionary<string, RibbonCommandValue> Values {
            get { return ((RibbonCommandCapsule)Parent).Values; }
            set { ((RibbonCommandCapsule)Parent).Values = value; }
        }

        public virtual Dictionary<string, RibbonCommandBoolean> Booleans {
            get { return ((RibbonCommandCapsule)Parent).Booleans; }
            set { ((RibbonCommandCapsule)Parent).Booleans = value; }
        }

        #region Convenience properties
        protected static Window ActiveWindow {
            get { return Window.ActiveWindow; }
        }

        protected static Document ActiveDocument {
            get { return ActiveWindow.Document; }
        }

        protected static Part ScenePart {
            get { return ActiveWindow.Scene as Part; }
        }

        protected static Part MainPart {
            get { return ActiveDocument.MainPart; }
        }

        protected IPart ActiveIPart {
            get { return ActiveWindow.ActiveContext.ActivePart; }
        }

        #endregion

        #region IRibbonObject Members

        public IRibbonObject Parent {
            get { return parent; }
        }

        public IList<IRibbonObject> Children {
            get { return children; }
        }

        public string Id {
            get { return Command.Name; }
        }

        public virtual void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteAttributeString("id", Command.Name);
            xmlWriter.WriteAttributeString("command", Command.Name);
        }

        public virtual void WriteEndElement(XmlWriter xmlWriter) {
            xmlWriter.WriteEndElement();
        }

        #endregion
    }

    public class RibbonTabCapsule : RibbonCommandCapsule {
        Dictionary<string, RibbonCommandValue> values = new Dictionary<string, RibbonCommandValue>();
        Dictionary<string, RibbonCommandBoolean> booleans = new Dictionary<string, RibbonCommandBoolean>();

        public RibbonTabCapsule(string name, string text, IRibbonObject parent)
            : base(name, text, null, null, parent) {
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("tab");
            xmlWriter.WriteAttributeString("label", Command.Text); // TBD shouldn't be necessary

            base.WriteStartElement(xmlWriter); // ID and Command strings
        }

        public override Dictionary<string, RibbonCommandValue> Values {
            get { return values; }
            set { values = value; }
        }

        public override Dictionary<string, RibbonCommandBoolean> Booleans {
            get { return booleans; }
            set { booleans = value; }
        }
    }

    public abstract class RibbonCollectionCapsule : RibbonCommandCapsule {
        public int? Spacing { get; set; }

        protected LayoutOrientation layoutOrientation;

        public RibbonCollectionCapsule(string name, string text, IRibbonObject parent, LayoutOrientation layoutOrientation)
            : base(name, text, null, null, parent) {

            this.layoutOrientation = layoutOrientation;
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteAttributeString("layoutOrientation", layoutOrientation.ToString());
            xmlWriter.WriteAttributeString("itemSpacing", Spacing.ToString() ?? "1");

            base.WriteStartElement(xmlWriter); // ID and Command strings
        }

        public enum LayoutOrientation {
            horizontal = 0,
            vertical = 1
        }

    }

    public class RibbonGroupCapsule : RibbonCollectionCapsule {
        public RibbonGroupCapsule(string name, string text, RibbonTabCapsule parent, LayoutOrientation layoutOrientation)
            : base(name, text, parent, layoutOrientation) {
        }

        public virtual void CreateOptionsUI() {
            Dictionary<string, RibbonCommandValue> newValues = Values.Where(v => v.Value.isDrawn == false).ToDictionary(k => k.Key, v => v.Value);

            RibbonContainerCapsule container, valueContainer = new RibbonContainerCapsule("Values", this, RibbonCollectionCapsule.LayoutOrientation.vertical, true);

            var valueColumns = new List<Dictionary<string, RibbonCommandValue>>();
            var valueColumn = new Dictionary<string, RibbonCommandValue>();
            for (int i = 0; i < newValues.Count; i++) {
                newValues.ElementAt(i).Value.isDrawn = true;
                if (i % 3 == 0) {
                    valueColumn = new Dictionary<string, RibbonCommandValue>();
                    valueColumns.Add(valueColumn);
                }

                valueColumn.Add(newValues.Keys.ToArray()[i], newValues.Values.ToArray()[i]);
            }

            Graphics graphics = Graphics.FromImage(new System.Drawing.Bitmap(400, 50));
            Font font = new Font("Sans Serif", 8.25f, FontStyle.Regular);

            if (newValues.Count != 0) {
                int i = 0;
                foreach (Dictionary<string, RibbonCommandValue> column in valueColumns) {
                    int width = 0;
                    foreach (string name in column.Keys) {
                        width = Math.Max(width, (int)graphics.MeasureString(name, font).Width);
                    }

                    valueContainer = new RibbonContainerCapsule(String.Format("Values{0}", i++), this, RibbonCollectionCapsule.LayoutOrientation.vertical, true);
                    foreach (string name in column.Keys) {
                        container = new RibbonContainerCapsule(name + "Container", valueContainer, RibbonCollectionCapsule.LayoutOrientation.horizontal, false);
                        new RibbonLabelCapsule(name + "Label", name, name, container, width);
                        newValues[name].Command = new RibbonTextBoxCapsule(name + "TextBox", newValues[name].Value.ToString(), name, container, 42).Command;
                    }
                }
            }

            if (Booleans.Count != 0) {
                container = new RibbonContainerCapsule("Checkboxes", this, RibbonCollectionCapsule.LayoutOrientation.vertical, true);
                foreach (string name in Booleans.Keys)
                    Booleans[name].Command = new RibbonCheckBoxCapsule(name + "Checkbox", name, null, name, container, Booleans[name].Value).Command;
            }
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("group");
            xmlWriter.WriteAttributeString("label", Command.Name); // TBD shouldn't be necessary

            base.WriteStartElement(xmlWriter);
        }
    }

    public class RibbonBooleanGroupCapsule : RibbonCollectionCapsule {
        static Dictionary<string, RibbonBooleanGroupCapsule> booleanGroupCapsules = new Dictionary<string, RibbonBooleanGroupCapsule>();
        string text, booleanText;

        public RibbonBooleanGroupCapsule(string name, string text, string booleanText, RibbonTabCapsule parent)
            : base(name, text, parent, LayoutOrientation.vertical) {

            booleanGroupCapsules[text] = this;
            Values = new Dictionary<string, RibbonCommandValue>();
            Booleans = new Dictionary<string, RibbonCommandBoolean>();

            this.text = text;
            this.booleanText = text;
            IsEnabledCommandBoolean = new RibbonCommandBoolean(false);
        }

        public void CreateOptionsUI() {
            IsEnabledCommandBoolean.Command = new RibbonCheckBoxCapsule(booleanText, booleanText, null, booleanText, this, false).Command;
            RibbonContainerCapsule topContainer = new RibbonContainerCapsule("BooleanGroup", this, RibbonCollectionCapsule.LayoutOrientation.horizontal, false);
            RibbonContainerCapsule container, valueContainer;

            Debug.Assert(Values.Count > 0);

            var valueColumns = new List<Dictionary<string, RibbonInput>>();
            var valueColumn = new Dictionary<string, RibbonInput>();
            for (int i = 0; i < Values.Count; i++) {
                if (i % 2 == 0) {
                    valueColumn = new Dictionary<string, RibbonInput>();
                    valueColumns.Add(valueColumn);
                }

                valueColumn.Add(Values.Keys.ToArray()[i], Values.Values.ToArray()[i]);
            }

            Graphics graphics = Graphics.FromImage(new System.Drawing.Bitmap(400, 50));
            Font font = new Font("Sans Serif", 8.25f, FontStyle.Regular);

            if (Values.Count != 0) {
                int i = 0;
                foreach (Dictionary<string, RibbonInput> column in valueColumns) {
                    int width = 0;
                    foreach (string name in column.Keys) {
                        width = Math.Max(width, (int)graphics.MeasureString(name, font).Width);
                    }

                    if (valueColumn.Count == 1)
                        valueContainer = topContainer;
                    else
                        valueContainer = new RibbonContainerCapsule(String.Format("Values{0}", i++), topContainer, LayoutOrientation.vertical, false);

                    foreach (string name in column.Keys) {
                        container = new RibbonContainerCapsule(name + "Container", valueContainer, LayoutOrientation.horizontal, false);
                        new RibbonLabelCapsule(name + "Label", name, name, container, width).Command.Updating += delegate(object sender, EventArgs e) {
                            ((Command)sender).IsEnabled = IsEnabledCommandBoolean.Command.IsChecked;
                        };

                        Values[name].Command = new RibbonTextBoxCapsule(name + "TextBox", Values[name].Value.ToString(), name, container, 42).Command;
                        Values[name].Command.Updating += delegate(object sender, EventArgs e) {
                            ((Command)sender).IsEnabled = IsEnabledCommandBoolean.Command.IsChecked;
                        };
                    }
                }
            }

            Debug.Assert(Booleans.Count == 0);
            //if (Booleans.Count != 0) {
            //    container = new RibbonContainerCapsule("Checkboxes", this, RibbonCollectionCapsule.LayoutOrientation.vertical, true);
            //    foreach (string name in Booleans.Keys)
            //        Booleans[name].Command = new RibbonCheckBoxCapsule(name + "Checkbox", name, null, name, container, Booleans[name].Value).Command;
            //}
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("group");
            xmlWriter.WriteAttributeString("label", Command.Name); // TBD shouldn't be necessary

            base.WriteStartElement(xmlWriter);
        }

        public static Dictionary<string, RibbonBooleanGroupCapsule> BooleanGroupCapsules {
            get { return booleanGroupCapsules; }
            set { booleanGroupCapsules = value; }
        }

        public RibbonCommandBoolean IsEnabledCommandBoolean { get; set; }
        public override Dictionary<string, RibbonCommandValue> Values { get; set; }
        public override Dictionary<string, RibbonCommandBoolean> Booleans { get; set; }
    }

    public class RibbonContainerCapsule : RibbonCollectionCapsule {
        bool isGroup;
        public RibbonContainerCapsule(string name, RibbonCollectionCapsule parent, LayoutOrientation layoutOrientation, bool isGroup)
            : base(name, null, parent, layoutOrientation) {

            this.isGroup = isGroup;
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("container");
            xmlWriter.WriteAttributeString("isGroup", isGroup.ToString());

            if (layoutOrientation == LayoutOrientation.vertical)
                xmlWriter.WriteAttributeString("verticalAlign", "middle");

            base.WriteStartElement(xmlWriter);
        }
    }

    public class RibbonButtonCapsule : RibbonCommandCapsule {
        ButtonSize size;

        public RibbonButtonCapsule(string name, string text, System.Drawing.Image image, string hint, RibbonCollectionCapsule parent, ButtonSize size)
            : base(name, text, image, hint, parent) {

            this.size = size;
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("button");
            xmlWriter.WriteAttributeString("size", size.ToString());

            base.WriteStartElement(xmlWriter); // ID and Command strings
        }

        public enum ButtonSize {
            small = 0,
            large = 1
        }
    }

    public class RibbonCheckBoxCapsule : RibbonCommandCapsule {
        public RibbonCheckBoxCapsule(string name, string text, System.Drawing.Image image, string hint, IRibbonObject parent, bool isChecked)
            : base(name, text, image, hint, parent) {

            Command.IsChecked = isChecked;
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("checkBox");
            base.WriteStartElement(xmlWriter); // ID and Command strings
        }

        protected override void OnExecute(Command command, ExecutionContext executionContext, System.Drawing.Rectangle buttonRect) {
            base.OnExecute(command, executionContext, buttonRect);
            command.IsChecked = !command.IsChecked;
        }
    }

    public abstract class RibbonTextCapsule : RibbonCommandCapsule {
        int width;

        public RibbonTextCapsule(string name, string text, string hint, IRibbonObject parent, int width)
            : base(name, text, null, hint, parent) {

            this.width = width;
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteAttributeString("text", Command.Text);
            xmlWriter.WriteAttributeString("width", width.ToString());

            base.WriteStartElement(xmlWriter); // ID and Command strings
        }
    }

    public class RibbonTextBoxCapsule : RibbonTextCapsule {
        public RibbonTextBoxCapsule(string name, string text, string hint, IRibbonObject parent, int width)
            : base(name, text, hint, parent, width) {

            RibbonRoot root;
            IRibbonObject test = this;

            while (true) {
                test = test.Parent;
                if (test is RibbonRoot) {
                    root = test as RibbonRoot;
                    break;
                }
            }

            Command.TextChanged += delegate {
                foreach (RibbonCommandCapsule capsule in root.GetCapsules()) {
                    capsule.Update();
                }
            };
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("textBox");
            xmlWriter.WriteAttributeString("multiline", "false");

            base.WriteStartElement(xmlWriter);
        }
    }

    public class RibbonLabelCapsule : RibbonTextCapsule {
        public LabelJustification Justification { get; set; }

        public RibbonLabelCapsule(string name, string text, string hint, IRibbonObject parent, int width)
            : base(name, text, hint, parent, width) {

            this.Justification = LabelJustification.far;
        }

        public override void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement("label");

            string justificationText = Justification == LabelJustification.near ? "near" : "far";
            xmlWriter.WriteAttributeString("align", justificationText);

            base.WriteStartElement(xmlWriter);
        }

        public enum LabelJustification {
            near = 0,
            far = 1
        }
    }

    public abstract class RibbonInput {
        public Command Command { get; set; }
        public bool isDrawn { get; set; }

        public RibbonInput(Command command) {
            Command = command;
            isDrawn = false;
        }
    }

    public class RibbonCommandValue : RibbonInput {
        static Dictionary<string, RibbonCommandValue> allRibbonCommandValues = new Dictionary<string, RibbonCommandValue>();

        double value = 0;

        public RibbonCommandValue(double value)
            : base(null) {
            Value = value;
        }

        public RibbonCommandValue CreateUnique(string key, double value) { // TBD wire this in
            if (allRibbonCommandValues.ContainsKey(key))
                return allRibbonCommandValues[key];

            return new RibbonCommandValue(value);
        }

        public double Value {
            get {
                double parsedDouble;
                if (Command != null && Double.TryParse(Command.Text, out parsedDouble))
                    value = parsedDouble;

                return value;
            }

            set {
                this.value = value;
                if (Command != null)
                    Command.Text = value.ToString();
            }

        }
    }

    public class RibbonCommandBoolean : RibbonInput {
        bool value = false;

        public RibbonCommandBoolean(bool value)
            : base(null) {
            Value = value;
        }

        public bool Value {
            get {
                if (Command != null)
                    value = Command.IsChecked;

                return value;
            }

            set {
                this.value = value;
                if (Command != null)
                    Command.IsChecked = value;
            }

        }
    }

    public class RibbonCommandRadio : RibbonInput {
        bool value = false;

        public RibbonCommandRadio(bool value)
            : base(null) {
            Value = value;
        }

        public bool Value {
            get {
                if (Command != null)
                    value = Command.IsChecked;

                return value;
            }

            set {
                this.value = value;
                if (Command != null)
                    Command.IsChecked = value;
            }

        }
    }

    public class NamedRibbonObject : IRibbonObject {
        string id;
        List<IRibbonObject> children = new List<IRibbonObject>();

        public NamedRibbonObject(string type, string id, IRibbonObject parent) {
            Type = type;
            this.id = id;
            parent.Children.Add(this);
        }

        public string Type { get; set; }

        #region IRibbonObject Members

        public IRibbonObject Parent { get; set; }

        public IList<IRibbonObject> Children {
            get { return children; }
        }

        public string Id {
            get { return id; }
        }

        public void WriteStartElement(XmlWriter xmlWriter) {
            xmlWriter.WriteStartElement(Type);
            xmlWriter.WriteAttributeString("id", Id);
        }

        public void WriteEndElement(XmlWriter xmlWriter) {
            xmlWriter.WriteEndElement();
        }

        #endregion
    }


}
