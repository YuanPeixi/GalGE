using System.Collections.Generic;

namespace GalEngineSample
{
    // Story root
    public class Story
    {
        public List<StoryNode> Nodes { get; set; } = new List<StoryNode>();
    }

    // 单句节点
    public class StoryNode
    {
        public string Id { get; set; } = "";

        // 为了支持“显式清除”与“继承”，我们增加两个标志，表示该字段在 XML 中是否被显式设置过
        public string? Background { get; set; }
        public bool BackgroundExplicitlySet { get; set; } = false;

        public string? Music { get; set; }
        public bool MusicExplicitlySet { get; set; } = false;

        public string? Speaker { get; set; }
        public string? Text { get; set; }

        public List<CharacterState> Characters { get; set; } = new List<CharacterState>();

        public List<StoryOption> Options { get; set; } = new List<StoryOption>();

        public List<CounterChange> Changes { get; set; } = new List<CounterChange>();

        public List<CounterTrigger> CounterTriggers { get; set; } = new List<CounterTrigger>();
    }

    public class CharacterState
    {
        public string Name { get; set; } = "";
        public string? Image { get; set; }
        public string Side { get; set; } = "left"; // left / right
        public string? Expression { get; set; }
    }

    public class StoryOption
    {
        public string Text { get; set; } = "";
        public string? Goto { get; set; }
        public List<CounterChange> Changes { get; set; } = new List<CounterChange>();
    }

    public class CounterChange
    {
        public string Counter { get; set; } = "";
        public int Delta { get; set; }
    }

    public class CounterTrigger
    {
        public string Counter { get; set; } = "";

        // 支持操作符（>=, <=, ==, >, <），默认 >=
        public string Operator { get; set; } = ">=";

        public int Value { get; set; }
        public string Goto { get; set; } = "";
    }
}