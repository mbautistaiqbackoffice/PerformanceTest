// ReSharper disable UnusedMember.Global
// ReSharper disable StringLiteralTypo
namespace JUST
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Schema;

    public class JsonValidator
    {
        private Dictionary<string, string> _schemaCollection;
        private string _schemaNoPrefix;
        private readonly string _inputJsonString;

        public JsonValidator(string inputJson) => _inputJsonString = inputJson;

        public void AddSchema(string prefix, string schemaJson)
        {
            if (prefix == string.Empty)
            {
                _schemaNoPrefix = schemaJson;
                return;
            }

            if (_schemaCollection == null)
                _schemaCollection = new Dictionary<string, string>();

            _schemaCollection.Add(prefix, schemaJson);
        }

        public void Validate()
        {
            List<string> errors = null;

            var handler = new SchemaValidationEventHandler(HandleEvent);
            if (!string.IsNullOrEmpty(_schemaNoPrefix))
            {
                var xSchemaToken = JsonConvert.DeserializeObject<JObject>(_schemaNoPrefix);
                var schema = JSchema.Parse(JsonConvert.SerializeObject(xSchemaToken));
                var json = JsonConvert.DeserializeObject<JObject>(_inputJsonString);
                try
                {
                    json.Validate(schema, handler);
                }
                catch (Exception e)
                {
                    errors = new List<string> { e.Message };
                }
            }

            if (_schemaCollection != null)
            {
                foreach (var schemaPair in _schemaCollection)
                {
                    var xSchemaToken = JsonConvert.DeserializeObject<JObject>(schemaPair.Value);
                    PrefixKey(xSchemaToken, schemaPair.Key);
                    var schema = JSchema.Parse(JsonConvert.SerializeObject(xSchemaToken));
                    var json = JsonConvert.DeserializeObject<JObject>(_inputJsonString);
                    try
                    {
                        json.Validate(schema, handler);
                    }
                    catch (Exception e)
                    {
                        if (errors == null)
                            errors = new List<string>();
                        errors.Add(e.Message);
                    }
                }
            }

            if (errors != null)
                throw new Exception(string.Join(" AND ", errors.ToArray()));
        }

        private static void PrefixKey(JObject jo, string prefix)
        {
            foreach (var jp in jo.Properties().ToList())
            {
                if (jp.Value.Type == JTokenType.Object)
                {
                    PrefixKey((JObject)jp.Value, prefix);
                }
                else if (jp.Value.Type == JTokenType.Array)
                {
                    foreach (var child in jp.Value.Where(c => c.Type == JTokenType.Object))
                    {
                        PrefixKey((JObject)child, prefix);
                    }
                }

                if (jp.Name.ToLower() == "type"
                    || jp.Name.ToLower() == "title"
                    || jp.Name.ToLower() == "description"
                    || jp.Name.ToLower() == "default"
                    || jp.Name.ToLower() == "enum"
                    || jp.Name.ToLower() == "properties"
                    || jp.Name.ToLower() == "additionalproperties")
                {
                    continue;
                }

                var name = prefix + "." + jp.Name;
                jp.Replace(new JProperty(name, jp.Value));
            }
        }

        private static void HandleEvent(object sender, SchemaValidationEventArgs args)
        {
            throw new Exception(args.Message);
        }
    }
}