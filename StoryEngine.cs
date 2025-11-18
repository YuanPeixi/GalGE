using System;
using System.Collections.Generic;
using System.Linq;

namespace GalEngineSample
{
    public class StoryEngine
    {
        private Story? _story;
        private int _position = -1;
        public Dictionary<string, int> Counters { get; } = new Dictionary<string, int>();

        public event Action<StoryNode>? NodeChanged;
        public event Action? StoryEnded;

        public StoryNode? CurrentNode => (_story != null && _position >= 0 && _position < _story.Nodes.Count) ? _story.Nodes[_position] : null;

        public void LoadFromFile(string path)
        {
            _story = StoryLoader.LoadFromFile(path);
            _position = -1;
            Counters.Clear();
        }

        public void Start()
        {
            if (_story == null || !_story.Nodes.Any()) return;
            _position = 0;
            RaiseNodeChanged();
        }

        public void Next()
        {
            if (_story == null) return;
            if (_position < 0) { Start(); return; }

            _position++;
            if (_position >= _story.Nodes.Count)
            {
                StoryEnded?.Invoke();
                return;
            }
            RaiseNodeChanged();
        }

        public void ApplyOption(StoryOption opt)
        {
            // 应用 option 的 changes
            ApplyChanges(opt.Changes);

            if (!string.IsNullOrEmpty(opt.Goto) && _story != null)
            {
                var idx = _story.Nodes.FindIndex(n => n.Id == opt.Goto);
                if (idx >= 0)
                {
                    _position = idx;
                    RaiseNodeChanged();
                    return;
                }
            }

            // 若没有 goto 或找不到，走到下一个顺序节点
            Next();
        }

        private void ApplyChanges(IEnumerable<CounterChange>? changes)
        {
            if (changes == null) return;
            foreach (var ch in changes)
            {
                if (!Counters.ContainsKey(ch.Counter)) Counters[ch.Counter] = 0;
                Counters[ch.Counter] += ch.Delta;
            }
        }

        private void CheckTriggersAndMaybeJump()
        {
            var node = CurrentNode;
            if (node == null) return;
            foreach (var trig in node.CounterTriggers)
            {
                Counters.TryGetValue(trig.Counter, out var val);
                if (EvaluateTrigger(val, trig.Operator, trig.Value))
                {
                    // 找到目标节点
                    if (!string.IsNullOrEmpty(trig.Goto) && _story != null)
                    {
                        var idx = _story.Nodes.FindIndex(n => n.Id == trig.Goto);
                        if (idx >= 0)
                        {
                            _position = idx;
                            // 进入目标节点（注意避免递归无限循环：我们直接触发一次节点变更并返回）
                            RaiseNodeChanged();
                            return;
                        }
                    }
                }
            }
        }

        private bool EvaluateTrigger(int currentValue, string op, int target)
        {
            return op switch
            {
                ">=" => currentValue >= target,
                "<=" => currentValue <= target,
                ">" => currentValue > target,
                "<" => currentValue < target,
                "==" or "=" => currentValue == target,
                _ => currentValue >= target, // default
            };
        }

        private void RaiseNodeChanged()
        {
            var node = CurrentNode;
            if (node == null) return;

            // 触发 UI 更新
            NodeChanged?.Invoke(node);

            // 应用 node 内的变更（例如好感 +1）
            ApplyChanges(node.Changes);

            // 检查计数器触发（若触发则跳转）
            CheckTriggersAndMaybeJump();
        }
    }
}