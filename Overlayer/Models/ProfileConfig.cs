using Newtonsoft.Json.Linq;
using Overlayer.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlayer.Models;

public class ProfileConfig : IModel, ICopyable<ProfileConfig> {
    public bool Active = true;
    public string Name = "Profile NULL";
    public string Path = null;
    public float Opacity = 1f;
    public List<ObjectConfig> Objects = [];

    public ProfileConfig Copy() {
        return new ProfileConfig {
            Active = Active,
            Opacity = Opacity,
            Objects = Objects.Select(o => o.Copy()).ToList()
        };
    }

    public JToken Serialize() {
        var node = new JObject {
            [nameof(Active)] = Active,
            [nameof(Opacity)] = Opacity,
            [nameof(Objects)] = new JArray(
                Objects.Select(o => {
                    var objNode = (JObject)o.Serialize();
                    var typeName = o.GetType().Name.Replace("Config", "");
                    var newNode = new JObject { ["Type"] = typeName };
                    foreach(var prop in objNode.Properties()) {
                        newNode[prop.Name] = prop.Value;
                    }
                    return newNode;
                })
            )
        };
        return node;
    }

    public void Deserialize(JToken node) {
        var defaults = new ProfileConfig();

        // Pre-profile-support Overlayer versions stored a single implicit profile as a bare
        // top-level array of TextConfig objects (e.g. an old Texts.json), with no Active/Opacity
        // wrapper and no per-item "Type" - every entry was a text object.
        if(node is JArray legacyObjects) {
            Active = defaults.Active;
            Opacity = defaults.Opacity;
            Objects = [];
            foreach(var obj in legacyObjects) {
                var cfg = new TextConfig();
                cfg.Deserialize(obj);
                Objects.Add(cfg);
            }
            return;
        }

        Active = node[nameof(Active)]?.Value<bool>() ?? defaults.Active;
        Opacity = node[nameof(Opacity)]?.Value<float>() ?? defaults.Opacity;
        Objects = [];
        var objectTokens = node[nameof(Objects)] as JArray
            ?? node["Texts"] as JArray
            ?? [];
        foreach(var obj in objectTokens) {
            string typeName = obj["Type"]?.Value<string>()?.Trim() ?? "";
            ObjectConfig cfg;
            if(!string.IsNullOrEmpty(typeName)) {
                var cfgType = Type.GetType($"Overlayer.Models.{typeName}Config");
                cfg = cfgType != null && typeof(ObjectConfig).IsAssignableFrom(cfgType)
                    ? (ObjectConfig)Activator.CreateInstance(cfgType)
                    : new TextConfig();
            } else {
                cfg = new TextConfig();
            }

            cfg.Deserialize(obj);
            Objects.Add(cfg);
        }
    }
}