using System.Text;
using System.Text.RegularExpressions;
using DynamicFormLibrary.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicFormLibrary
{
    /// <summary>
    /// Windows Form dialog that dynamically renders a form.io form definition
    /// and collects user input. On successful submission, <see cref="ResultJson"/>
    /// contains the JSON data entered by the user.
    /// </summary>
    public class DynamicFormDialog : Form
    {
        // ── Layout constants ──────────────────────────────────────────────
        private const int ControlHeight = 24;
        private const int LabelHeight = 18;
        private const int CtrlPadding = 6;
        private const int GroupPadding = 12;
        private const int FullWidth = 700;

        // ── Internal state ────────────────────────────────────────────────
        private readonly FormDefinition _definition;
        private readonly Dictionary<string, object?> _initialData;

        /// <summary>Maps each field key to its rendered WinForms control.</summary>
        private readonly Dictionary<string, Control> _fieldControls = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Maps each field key to its conditional rule.</summary>
        private readonly Dictionary<string, FormConditional> _conditionals = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Tracks required field keys for validation.</summary>
        private readonly HashSet<string> _requiredKeys = new(StringComparer.OrdinalIgnoreCase);

        private Panel _scrollPanel = null!;
        private Button _submitButton = null!;
        private Label _errorLabel = null!;

        // ── Public output ────────────────────────────────────────────────
        /// <summary>JSON string with the raw form data; populated after the user clicks Submit.</summary>
        public string? ResultJson { get; private set; }

        // ── Constructor ──────────────────────────────────────────────────
        public DynamicFormDialog(FormDefinition definition, string? initialDataJson = null)
        {
            _definition = definition;

            _initialData = string.IsNullOrWhiteSpace(initialDataJson)
                ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                : JsonConvert.DeserializeObject<Dictionary<string, object?>>(initialDataJson)
                  ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            InitializeLayout();
            RenderComponents(_definition.Components, _scrollPanel, 0);
            FinalizeLayout();
            PopulateInitialData();
            EvaluateAllConditionals();
            WireConditionalListeners();
        }

        // ── Layout setup ─────────────────────────────────────────────────
        private void InitializeLayout()
        {
            Text = _definition.Description ?? _definition.Name ?? "Dynamic Form";
            Width = FullWidth + 40;
            Height = 600;
            MinimumSize = new Size(480, 300);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;

            _scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(CtrlPadding)
            };

            Controls.Add(_scrollPanel);
        }

        private void FinalizeLayout()
        {
            // Error label
            _errorLabel = new Label
            {
                AutoSize = false,
                Height = 20,
                Dock = DockStyle.Bottom,
                ForeColor = Color.Red,
                Text = string.Empty,
                Font = new Font(Font, FontStyle.Bold)
            };

            // Submit button bar
            var buttonPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Bottom,
                Padding = new Padding(CtrlPadding)
            };

            _submitButton = new Button
            {
                Text = "Conferma",
                Dock = DockStyle.Right,
                Width = 120,
                Height = 30
            };
            _submitButton.Click += SubmitButton_Click;

            buttonPanel.Controls.Add(_submitButton);
            Controls.Add(buttonPanel);
            Controls.Add(_errorLabel);
        }

        // ── Component rendering ───────────────────────────────────────────
        private int RenderComponents(List<FormComponent> components, Control parent, int yOffset)
        {
            foreach (var comp in components)
            {
                yOffset = RenderComponent(comp, parent, yOffset, FullWidth);
            }
            return yOffset;
        }

        private int RenderComponent(FormComponent comp, Control parent, int y, int width)
        {
            // Track conditional rule
            if (!string.IsNullOrEmpty(comp.Key) && comp.Conditional != null && !string.IsNullOrEmpty(comp.Conditional.When))
            {
                _conditionals[comp.Key] = comp.Conditional;
            }

            return comp.Type switch
            {
                "panel" => RenderPanel(comp, parent, y, width),
                "fieldset" => RenderFieldset(comp, parent, y, width),
                "columns" => RenderColumns(comp, parent, y, width),
                "textfield" or "email" or "phoneNumber" or "number" => RenderTextField(comp, parent, y, width),
                "textarea" => RenderTextArea(comp, parent, y, width),
                "datetime" or "day" => RenderDateField(comp, parent, y, width),
                "select" => RenderSelect(comp, parent, y, width),
                "radio" => RenderRadio(comp, parent, y, width),
                "checkbox" => RenderCheckbox(comp, parent, y, width),
                "button" => RenderButton(comp, parent, y, width),
                _ => y // unknown type – skip
            };
        }

        // ── Panel ──────────────────────────────────────────────────────────
        private int RenderPanel(FormComponent comp, Control parent, int y, int width)
        {
            var inner = new Panel
            {
                Location = new Point(0, y),
                Width = width,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0)
            };
            parent.Controls.Add(inner);

            int childY = 0;
            if (comp.Components != null)
                childY = RenderComponents(comp.Components, inner, childY);

            inner.Height = childY + CtrlPadding;

            if (!string.IsNullOrEmpty(comp.Key))
                RegisterContainer(comp.Key, inner);

            return y + inner.Height + CtrlPadding;
        }

        // ── Fieldset ───────────────────────────────────────────────────────
        private int RenderFieldset(FormComponent comp, Control parent, int y, int width)
        {
            string legend = comp.Legend ?? comp.Label ?? string.Empty;
            var group = new GroupBox
            {
                Text = legend,
                Location = new Point(0, y),
                Width = width,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(GroupPadding)
            };
            parent.Controls.Add(group);

            int childY = GroupPadding + 10; // leave room for legend text
            if (comp.Components != null)
            {
                foreach (var child in comp.Components)
                    childY = RenderComponent(child, group, childY, width - GroupPadding * 2);
            }

            group.Height = childY + GroupPadding;

            if (!string.IsNullOrEmpty(comp.Key))
                RegisterContainer(comp.Key, group);

            return y + group.Height + CtrlPadding;
        }

        // ── Columns ────────────────────────────────────────────────────────
        private int RenderColumns(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.Columns == null || comp.Columns.Count == 0)
                return y;

            int totalWeight = comp.Columns.Sum(c => c.Width > 0 ? c.Width : 6);
            int maxHeight = 0;
            int xOffset = 0;

            var columnsPanel = new Panel
            {
                Location = new Point(0, y),
                Width = width,
                Height = 10,
                AutoSize = false
            };
            parent.Controls.Add(columnsPanel);

            foreach (var col in comp.Columns)
            {
                int colWidth = (int)Math.Round((double)(col.Width > 0 ? col.Width : 6) / totalWeight * width);
                var colPanel = new Panel
                {
                    Location = new Point(xOffset, 0),
                    Width = colWidth,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink
                };
                columnsPanel.Controls.Add(colPanel);

                int childY = 0;
                if (col.Components != null)
                {
                    foreach (var child in col.Components)
                        childY = RenderComponent(child, colPanel, childY, colWidth - CtrlPadding);
                }

                if (childY > maxHeight) maxHeight = childY;
                xOffset += colWidth;
            }

            columnsPanel.Height = maxHeight + CtrlPadding;
            return y + columnsPanel.Height + CtrlPadding;
        }

        // ── TextField ──────────────────────────────────────────────────────
        private int RenderTextField(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.Hidden) return y;

            y = AddLabel(comp, parent, y, width);

            var tb = new TextBox
            {
                Location = new Point(0, y),
                Width = width,
                Height = ControlHeight,
                PlaceholderText = comp.Placeholder ?? string.Empty
            };

            parent.Controls.Add(tb);
            RegisterField(comp, tb);

            return y + ControlHeight + CtrlPadding;
        }

        // ── TextArea ──────────────────────────────────────────────────────
        private int RenderTextArea(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.Hidden) return y;

            y = AddLabel(comp, parent, y, width);
            int rows = Math.Max(comp.Rows, 2);
            int h = ControlHeight * rows;

            var tb = new TextBox
            {
                Location = new Point(0, y),
                Width = width,
                Height = h,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = comp.Placeholder ?? string.Empty
            };

            parent.Controls.Add(tb);
            RegisterField(comp, tb);

            return y + h + CtrlPadding;
        }

        // ── DateTimeField ─────────────────────────────────────────────────
        private int RenderDateField(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.Hidden) return y;

            y = AddLabel(comp, parent, y, width);

            var dtp = new DateTimePicker
            {
                Location = new Point(0, y),
                Width = width,
                Height = ControlHeight,
                Format = DateTimePickerFormat.Short,
                ShowCheckBox = !comp.Validate?.Required ?? true
            };

            parent.Controls.Add(dtp);
            RegisterField(comp, dtp);

            return y + ControlHeight + CtrlPadding;
        }

        // ── Select ────────────────────────────────────────────────────────
        private int RenderSelect(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.Hidden) return y;

            y = AddLabel(comp, parent, y, width);

            var cb = new ComboBox
            {
                Location = new Point(0, y),
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Populate items from embedded values
            var items = comp.Values ?? comp.Data?.Values;
            if (items != null)
            {
                foreach (var item in items)
                {
                    cb.Items.Add(new ComboItem(item.Label ?? string.Empty, item.Value ?? string.Empty));
                }
            }
            else if (comp.DataSrc == "url")
            {
                // URL-based selects: show placeholder, data loaded at runtime in a real scenario
                cb.Items.Add(new ComboItem("(values loaded from URL)", string.Empty));
            }

            if (cb.Items.Count > 0)
                cb.SelectedIndex = 0;

            parent.Controls.Add(cb);
            RegisterField(comp, cb);

            return y + ControlHeight + CtrlPadding;
        }

        // ── Radio ─────────────────────────────────────────────────────────
        private int RenderRadio(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.Hidden) return y;
            if (comp.Values == null || comp.Values.Count == 0) return y;

            y = AddLabel(comp, parent, y, width);

            // Group box to hold radio buttons and enable mutual exclusion
            var group = new GroupBox
            {
                Location = new Point(0, y),
                Width = width,
                Text = string.Empty,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(4)
            };
            parent.Controls.Add(group);

            if (comp.Inline)
            {
                // Horizontal layout
                int xPos = 4;
                int rbY = 16;
                foreach (var val in comp.Values)
                {
                    var rb = new RadioButton
                    {
                        Text = val.Label ?? string.Empty,
                        Tag = val.Value,
                        AutoSize = true,
                        Location = new Point(xPos, rbY)
                    };
                    group.Controls.Add(rb);
                    xPos += rb.Width + CtrlPadding * 2;
                }
            }
            else
            {
                // Vertical layout
                int rbY = 16;
                foreach (var val in comp.Values)
                {
                    var rb = new RadioButton
                    {
                        Text = val.Label ?? string.Empty,
                        Tag = val.Value,
                        Width = width - GroupPadding * 2,
                        Location = new Point(4, rbY)
                    };
                    group.Controls.Add(rb);
                    rbY += ControlHeight + 2;
                }
            }

            RegisterField(comp, group);

            if (comp.Validate?.Required == true && !string.IsNullOrEmpty(comp.Key))
                _requiredKeys.Add(comp.Key);

            return y + group.Height + CtrlPadding;
        }

        // ── Checkbox ──────────────────────────────────────────────────────
        private int RenderCheckbox(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.Hidden) return y;

            var cb = new CheckBox
            {
                Location = new Point(0, y),
                Width = width,
                Text = comp.HideLabel ? string.Empty : (comp.Label ?? string.Empty),
                AutoSize = false,
                Height = ControlHeight + 4
            };

            parent.Controls.Add(cb);
            RegisterField(comp, cb);

            return y + ControlHeight + 4 + CtrlPadding;
        }

        // ── Button (submit) ───────────────────────────────────────────────
        private int RenderButton(FormComponent comp, Control parent, int y, int width)
        {
            // The main submit button is always added in FinalizeLayout();
            // additional buttons defined in the form schema are rendered here.
            if (comp.Action != "submit") return y;

            // Update the dedicated submit button label if defined in the form
            if (_submitButton != null && !string.IsNullOrEmpty(comp.Label))
                _submitButton.Text = comp.Label;

            return y;
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private int AddLabel(FormComponent comp, Control parent, int y, int width)
        {
            if (comp.HideLabel || string.IsNullOrEmpty(comp.Label))
                return y;

            string labelText = comp.Label;
            if (comp.Validate?.Required == true)
                labelText += " *";

            var lbl = new Label
            {
                Location = new Point(0, y),
                Width = width,
                Height = LabelHeight,
                Text = labelText,
                Font = new Font(Font, FontStyle.Regular)
            };
            parent.Controls.Add(lbl);
            return y + LabelHeight + 2;
        }

        private void RegisterField(FormComponent comp, Control control)
        {
            if (string.IsNullOrEmpty(comp.Key)) return;

            _fieldControls[comp.Key] = control;

            if (comp.Validate?.Required == true)
                _requiredKeys.Add(comp.Key);

            if (comp.Conditional != null && !string.IsNullOrEmpty(comp.Conditional.When))
                _conditionals[comp.Key] = comp.Conditional;
        }

        private void RegisterContainer(string key, Control control)
        {
            _fieldControls[key] = control;
        }

        // ── Initial data population ───────────────────────────────────────
        private void PopulateInitialData()
        {
            foreach (var kvp in _initialData)
            {
                if (!_fieldControls.TryGetValue(kvp.Key, out var control)) continue;
                SetControlValue(control, kvp.Value);
            }

            // Also apply defaultValues from schema for fields not in initial data
            ApplyDefaultValues(_definition.Components);
        }

        private void ApplyDefaultValues(List<FormComponent> components)
        {
            foreach (var comp in components)
            {
                if (!string.IsNullOrEmpty(comp.Key)
                    && comp.DefaultValue != null
                    && comp.DefaultValue.Type != JTokenType.Null
                    && !_initialData.ContainsKey(comp.Key!)
                    && _fieldControls.TryGetValue(comp.Key!, out var ctrl))
                {
                    SetControlValue(ctrl, comp.DefaultValue.ToObject<object?>());
                }

                if (comp.Components != null) ApplyDefaultValues(comp.Components);
                if (comp.Columns != null)
                    foreach (var col in comp.Columns)
                        if (col.Components != null) ApplyDefaultValues(col.Components);
            }
        }

        private static void SetControlValue(Control control, object? value)
        {
            if (value == null) return;
            string strVal = value.ToString() ?? string.Empty;

            switch (control)
            {
                case TextBox tb:
                    tb.Text = strVal;
                    break;
                case DateTimePicker dtp:
                    if (DateTime.TryParse(strVal, out var dt))
                    {
                        dtp.Value = dt;
                        dtp.Checked = true;
                    }
                    break;
                case ComboBox cb:
                    for (int i = 0; i < cb.Items.Count; i++)
                    {
                        if (cb.Items[i] is ComboItem item && item.Value == strVal)
                        {
                            cb.SelectedIndex = i;
                            break;
                        }
                    }
                    break;
                case GroupBox grp: // radio group
                    foreach (Control child in grp.Controls)
                    {
                        if (child is RadioButton rb && rb.Tag?.ToString() == strVal)
                        {
                            rb.Checked = true;
                            break;
                        }
                    }
                    break;
                case CheckBox chk:
                    if (bool.TryParse(strVal, out bool bVal))
                        chk.Checked = bVal;
                    break;
            }
        }

        // ── Conditional visibility ────────────────────────────────────────
        private void EvaluateAllConditionals()
        {
            foreach (var kvp in _conditionals)
            {
                if (_fieldControls.TryGetValue(kvp.Key, out var ctrl))
                    ApplyConditional(ctrl, kvp.Value);
            }
        }

        private void WireConditionalListeners()
        {
            // Collect all "when" source keys
            var sources = _conditionals.Values
                .Where(c => !string.IsNullOrEmpty(c.When))
                .Select(c => c.When!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var sourceKey in sources)
            {
                if (!_fieldControls.TryGetValue(sourceKey, out var sourceCtrl)) continue;

                switch (sourceCtrl)
                {
                    case TextBox tb:
                        tb.TextChanged += (_, _) => EvaluateConditionalsForSource(sourceKey);
                        break;
                    case ComboBox cb:
                        cb.SelectedIndexChanged += (_, _) => EvaluateConditionalsForSource(sourceKey);
                        break;
                    case GroupBox grp: // radio
                        foreach (Control c in grp.Controls)
                            if (c is RadioButton rb)
                                rb.CheckedChanged += (_, _) => EvaluateConditionalsForSource(sourceKey);
                        break;
                    case CheckBox chk:
                        chk.CheckedChanged += (_, _) => EvaluateConditionalsForSource(sourceKey);
                        break;
                }
            }
        }

        private void EvaluateConditionalsForSource(string sourceKey)
        {
            foreach (var kvp in _conditionals)
            {
                if (!string.Equals(kvp.Value.When, sourceKey, StringComparison.OrdinalIgnoreCase)) continue;
                if (_fieldControls.TryGetValue(kvp.Key, out var ctrl))
                    ApplyConditional(ctrl, kvp.Value);
            }
        }

        private void ApplyConditional(Control ctrl, FormConditional cond)
        {
            if (string.IsNullOrEmpty(cond.When)) return;

            string currentValue = GetFieldStringValue(cond.When);
            string expectedEq = cond.Eq?.ToString() ?? string.Empty;
            bool shouldShow = EvaluateShowCondition(cond.Show, currentValue, expectedEq);
            ctrl.Visible = shouldShow;
        }

        private static bool EvaluateShowCondition(JToken? showToken, string currentValue, string expectedEq)
        {
            if (showToken == null || showToken.Type == JTokenType.Null) return true;

            // "show" can be a boolean or a boolean-like string
            bool showFlag = showToken.Type switch
            {
                JTokenType.Boolean => showToken.ToObject<bool>(),
                JTokenType.String => !string.Equals(showToken.ToString(), "false", StringComparison.OrdinalIgnoreCase),
                _ => true
            };

            bool valuesMatch = string.Equals(currentValue, expectedEq, StringComparison.OrdinalIgnoreCase);
            return showFlag ? valuesMatch : !valuesMatch;
        }

        private string GetFieldStringValue(string key)
        {
            if (!_fieldControls.TryGetValue(key, out var ctrl)) return string.Empty;

            return ctrl switch
            {
                TextBox tb => tb.Text,
                ComboBox cb => cb.SelectedItem is ComboItem ci ? ci.Value : string.Empty,
                GroupBox grp => grp.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Checked)?.Tag?.ToString() ?? string.Empty,
                CheckBox chk => chk.Checked.ToString().ToLowerInvariant(),
                DateTimePicker dtp => dtp.Checked ? dtp.Value.ToString("yyyy-MM-dd") : string.Empty,
                _ => string.Empty
            };
        }

        // ── Submit / Validation ───────────────────────────────────────────
        private void SubmitButton_Click(object? sender, EventArgs e)
        {
            var errors = ValidateFields();
            if (errors.Count > 0)
            {
                _errorLabel.Text = "Campi obbligatori mancanti: " + string.Join(", ", errors);
                return;
            }

            ResultJson = BuildResultJson();
            DialogResult = DialogResult.OK;
            Close();
        }

        private List<string> ValidateFields()
        {
            var errors = new List<string>();
            foreach (var key in _requiredKeys)
            {
                if (!_fieldControls.TryGetValue(key, out var ctrl)) continue;
                if (!ctrl.Visible) continue; // hidden via conditional

                string val = GetFieldStringValue(key);
                if (string.IsNullOrWhiteSpace(val) || val == "false")
                    errors.Add(key);
            }
            return errors;
        }

        private string BuildResultJson()
        {
            var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _fieldControls)
            {
                // Skip container controls (Panel / GroupBox registered as containers)
                if (kvp.Value is Panel || (kvp.Value is GroupBox gb && !gb.Controls.OfType<RadioButton>().Any()))
                    continue;

                data[kvp.Key] = GetFieldRawValue(kvp.Value);
            }

            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        private static object? GetFieldRawValue(Control ctrl)
        {
            return ctrl switch
            {
                TextBox tb => tb.Text,
                ComboBox cb => cb.SelectedItem is ComboItem ci ? ci.Value : null,
                GroupBox grp => grp.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Checked)?.Tag?.ToString(),
                CheckBox chk => chk.Checked,
                DateTimePicker dtp => dtp.Checked ? dtp.Value.ToString("yyyy-MM-dd") : null,
                _ => null
            };
        }

        // ── Nested helper class ───────────────────────────────────────────
        private sealed class ComboItem
        {
            public string Label { get; }
            public string Value { get; }

            public ComboItem(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public override string ToString() => Label;
        }
    }
}
