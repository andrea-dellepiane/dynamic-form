using DynamicFormLibrary.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicFormLibrary
{
    /// <summary>
    /// Entry point for the DynamicForm library.
    /// Pass a form.io JSON definition (and optional initial data JSON) to
    /// <see cref="ShowForm"/> to open the dynamic dialog.
    /// </summary>
    public static class DynamicFormBuilder
    {
        /// <summary>
        /// Parses the form.io definition JSON, shows the Windows Form dialog,
        /// and returns the collected data as a JSON string.
        /// </summary>
        /// <param name="formDefinitionJson">
        ///   The form.io JSON definition. May be either a raw
        ///   <c>{"components":[...]}</c> object or the full API response
        ///   wrapper <c>{"form":{...}}</c>.
        /// </param>
        /// <param name="initialDataJson">
        ///   Optional JSON object whose keys correspond to form field keys
        ///   and whose values pre-populate those fields.
        /// </param>
        /// <returns>
        ///   A JSON string with the raw form data when the user submits, or
        ///   <c>null</c> if the user cancels the dialog.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="formDefinitionJson"/> is null or empty.
        /// </exception>
        /// <exception cref="JsonException">
        ///   Thrown when <paramref name="formDefinitionJson"/> is not valid JSON.
        /// </exception>
        public static string? ShowForm(string formDefinitionJson, string? initialDataJson = null)
        {
            if (string.IsNullOrWhiteSpace(formDefinitionJson))
                throw new ArgumentNullException(nameof(formDefinitionJson));

            FormDefinition definition = ParseDefinition(formDefinitionJson);

            string? result = null;

            // Windows Forms must run on an STA thread.
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                result = RunDialog(definition, initialDataJson);
            }
            else
            {
                // Spin up an STA thread if we are called from an MTA context (e.g. console).
                var thread = new Thread(() =>
                {
                    result = RunDialog(definition, initialDataJson);
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }

            return result;
        }

        /// <summary>
        /// Parses a form.io JSON definition into a <see cref="FormDefinition"/>.
        /// Accepts both the bare form object and the full API-response wrapper.
        /// </summary>
        public static FormDefinition ParseDefinition(string formDefinitionJson)
        {
            if (string.IsNullOrWhiteSpace(formDefinitionJson))
                throw new ArgumentNullException(nameof(formDefinitionJson));

            var token = JToken.Parse(formDefinitionJson);

            // API response wrapper: { "form": { ... } }
            if (token is JObject obj && obj.ContainsKey("form"))
            {
                var apiResponse = token.ToObject<FormApiResponse>()
                    ?? throw new JsonException("Failed to deserialize FormApiResponse.");
                return apiResponse.Form
                    ?? throw new JsonException("The 'form' property is null.");
            }

            // Bare form definition: { "components": [...], ... }
            return token.ToObject<FormDefinition>()
                ?? throw new JsonException("Failed to deserialize FormDefinition.");
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static string? RunDialog(FormDefinition definition, string? initialDataJson)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var dialog = new DynamicFormDialog(definition, initialDataJson);
            var dialogResult = dialog.ShowDialog();

            return dialogResult == DialogResult.OK ? dialog.ResultJson : null;
        }
    }
}
