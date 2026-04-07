using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicFormLibrary.Models
{
    /// <summary>
    /// Top-level API response wrapper for a form.io form definition.
    /// </summary>
    public class FormApiResponse
    {
        [JsonProperty("form")]
        public FormDefinition? Form { get; set; }
    }

    /// <summary>
    /// The form.io form definition.
    /// </summary>
    public class FormDefinition
    {
        [JsonProperty("form_id")]
        public string? FormId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("components")]
        public List<FormComponent> Components { get; set; } = new();
    }

    /// <summary>
    /// A single form.io component (field, panel, columns, etc.).
    /// </summary>
    public class FormComponent
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("key")]
        public string? Key { get; set; }

        [JsonProperty("label")]
        public string? Label { get; set; }

        [JsonProperty("legend")]
        public string? Legend { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("hidden")]
        public bool Hidden { get; set; }

        [JsonProperty("hideLabel")]
        public bool HideLabel { get; set; }

        [JsonProperty("disabled")]
        public bool Disabled { get; set; }

        [JsonProperty("defaultValue")]
        public JToken? DefaultValue { get; set; }

        [JsonProperty("validate")]
        public FormValidation? Validate { get; set; }

        [JsonProperty("conditional")]
        public FormConditional? Conditional { get; set; }

        [JsonProperty("values")]
        public List<FormSelectValue>? Values { get; set; }

        [JsonProperty("data")]
        public FormSelectData? Data { get; set; }

        [JsonProperty("dataSrc")]
        public string? DataSrc { get; set; }

        [JsonProperty("valueProperty")]
        public string? ValueProperty { get; set; }

        [JsonProperty("template")]
        public string? Template { get; set; }

        [JsonProperty("multiple")]
        public bool Multiple { get; set; }

        [JsonProperty("inline")]
        public bool Inline { get; set; }

        [JsonProperty("placeholder")]
        public string? Placeholder { get; set; }

        [JsonProperty("inputType")]
        public string? InputType { get; set; }

        [JsonProperty("rows")]
        public int Rows { get; set; } = 3;

        [JsonProperty("action")]
        public string? Action { get; set; }

        [JsonProperty("theme")]
        public string? Theme { get; set; }

        // Container component children
        [JsonProperty("components")]
        public List<FormComponent>? Components { get; set; }

        // Column-specific: list of column definitions
        [JsonProperty("columns")]
        public List<FormColumn>? Columns { get; set; }

        // Panel breadcrumb
        [JsonProperty("breadcrumb")]
        public string? Breadcrumb { get; set; }
    }

    public class FormValidation
    {
        [JsonProperty("required")]
        public bool Required { get; set; }

        [JsonProperty("minLength")]
        public JToken? MinLength { get; set; }

        [JsonProperty("maxLength")]
        public JToken? MaxLength { get; set; }

        [JsonProperty("pattern")]
        public string? Pattern { get; set; }
    }

    public class FormConditional
    {
        [JsonProperty("show")]
        public JToken? Show { get; set; }

        [JsonProperty("when")]
        public string? When { get; set; }

        [JsonProperty("eq")]
        public JToken? Eq { get; set; }
    }

    public class FormSelectValue
    {
        [JsonProperty("value")]
        public string? Value { get; set; }

        [JsonProperty("label")]
        public string? Label { get; set; }
    }

    public class FormSelectData
    {
        [JsonProperty("values")]
        public List<FormSelectValue>? Values { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("json")]
        public JToken? Json { get; set; }
    }

    public class FormColumn
    {
        [JsonProperty("width")]
        public int Width { get; set; } = 6;

        [JsonProperty("components")]
        public List<FormComponent>? Components { get; set; }
    }
}
