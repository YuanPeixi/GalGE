using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace GalEngineSample
{
    public static class StoryLoader
    {
        // 加载并解析 story.xml（并实现继承规则：除文字外继承上一节点）
        public static Story LoadFromFile(string path)
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) throw new Exception("Invalid story XML");

            var story = new Story();
            StoryNode? lastResolved = null;

            foreach (var n in root.Elements("node"))
            {
                var node = new StoryNode
                {
                    Id = (string?)n.Attribute("id") ?? Guid.NewGuid().ToString(),
                    Background = n.Attribute("bg") != null ? (string?)n.Attribute("bg") : null,
                    BackgroundExplicitlySet = n.Attribute("bg") != null,
                    Music = n.Attribute("music") != null ? (string?)n.Attribute("music") : null,
                    MusicExplicitlySet = n.Attribute("music") != null,
                    Speaker = (string?)n.Attribute("speaker"),
                    Text = n.Element("text")?.Value.Trim()
                };

                // characters
                foreach (var c in n.Elements("character"))
                {
                    var cs = new CharacterState
                    {
                        Name = (string?)c.Attribute("name") ?? "",
                        Image = (string?)c.Attribute("image"),
                        Side = (string?)c.Attribute("side") ?? "left",
                        Expression = (string?)c.Attribute("expression")
                    };
                    node.Characters.Add(cs);
                }

                // options
                foreach (var o in n.Elements("option"))
                {
                    var opt = new StoryOption
                    {
                        Text = (string?)o.Attribute("text") ?? "(选项)",
                        Goto = (string?)o.Attribute("goto")
                    };

                    // 兼容旧的 affect 属性（格式: name:+1,other:-2）
                    var affect = (string?)o.Attribute("affect");
                    if (!string.IsNullOrEmpty(affect))
                    {
                        var parts = affect.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                        {
                            var kv = p.Split(':', StringSplitOptions.RemoveEmptyEntries);
                            if (kv.Length == 2 && int.TryParse(kv[1], out var delta))
                            {
                                opt.Changes.Add(new CounterChange { Counter = kv[0].Trim(), Delta = delta });
                            }
                        }
                    }

                    // 支持 <option><change counter="like" delta="1" /></option>
                    foreach (var co in o.Elements("change"))
                    {
                        var counter = (string?)co.Attribute("counter");
                        var deltaStr = (string?)co.Attribute("delta");
                        if (!string.IsNullOrEmpty(counter) && int.TryParse(deltaStr, out var delta))
                        {
                            opt.Changes.Add(new CounterChange { Counter = counter, Delta = delta });
                        }
                    }

                    node.Options.Add(opt);
                }

                // changes
                foreach (var c in n.Elements("change"))
                {
                    var counter = (string?)c.Attribute("counter");
                    var deltaStr = (string?)c.Attribute("delta");
                    if (!string.IsNullOrEmpty(counter) && int.TryParse(deltaStr, out var delta))
                    {
                        node.Changes.Add(new CounterChange { Counter = counter, Delta = delta });
                    }
                }

                // counter triggers
                foreach (var t in n.Elements("counterTrigger"))
                {
                    var counter = (string?)t.Attribute("counter") ?? "";
                    var v = (string?)t.Attribute("value") ?? "0";
                    var gotoId = (string?)t.Attribute("goto") ?? "";
                    var op = (string?)t.Attribute("operator") ?? ">=";
                    if (int.TryParse(v, out var val))
                    {
                        node.CounterTriggers.Add(new CounterTrigger { Counter = counter, Operator = op, Value = val, Goto = gotoId });
                    }
                }

                // 继承规则：除 text 外从 lastResolved 继承（当当前的属性没有显式设置）
                if (lastResolved != null)
                {
                    if (!node.BackgroundExplicitlySet) node.Background = lastResolved.Background;
                    if (!node.MusicExplicitlySet) node.Music = lastResolved.Music;
                    if (string.IsNullOrEmpty(node.Speaker)) node.Speaker = lastResolved.Speaker;
                    // Characters: 如果当前没有指定任何角色，则继承上一节点的角色（浅复制）
                    if (!node.Characters.Any())
                    {
                        foreach (var c in lastResolved.Characters)
                        {
                            node.Characters.Add(new CharacterState
                            {
                                Name = c.Name,
                                Image = c.Image,
                                Side = c.Side,
                                Expression = c.Expression
                            });
                        }
                    }
                }

                story.Nodes.Add(node);
                lastResolved = node;
            }

            return story;
        }
    }
}